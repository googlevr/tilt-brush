# Copyright 2020 The Tilt Brush Authors
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#      http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

import os
import re
import cPickle as pickle
import cStringIO as StringIO
import sys

import networkx as nx

ROOT_GUID = '00001111222233334444555566667777'

META_GUID_PAT = re.compile('^guid: ([a-f0-9]{32})\s*$', re.M)
CONTAINS_NO_GUIDS = """
  fbx obj dae wav txt pdf
  tga png psd tif jpg jpeg
  shader cginc cs cpp c h
  dll so jar""".split()


def _get_guid_from_meta(meta_filename):
  with file(meta_filename) as inf:
    contents = inf.read()
  m = META_GUID_PAT.search(contents)
  if m is None:
    raise LookupError("No guid in %s" % (meta_filename,))
  return m.group(1)


# 0000000000000000e000000000000000 and 0000000000000000f000000000000000
# are some sort of hardcoded guid?
def _iter_guid_names(project):
  """Find all file guids and their corresponding filename (without the ".meta")
Yields (guid, filename)"""
  chop = len(project)+1
  for r, ds, fs in os.walk(os.path.join(project, 'Assets')):
    for f in fs:
      if f.endswith('.meta'):
        fullf = os.path.join(r, f)
        guid = _get_guid_from_meta(fullf)
        name = fullf[chop:-5].replace('\\', '/')
        yield guid, name
  yield ('0000000000000000e000000000000000', '?Unity hardcoded 0e?')
  yield ('0000000000000000f000000000000000', '?Unity hardcoded 0f?')


def _iter_refs(project):
  """Yields (src_guid, dst_guid)"""
  ignore_pat = '|'.join(CONTAINS_NO_GUIDS)
  ignore_pat = re.compile(r'(%s)$' % ignore_pat, re.I)
  guid_pat = re.compile(r'(?<!Hash: )\b([a-f0-9]{32})\b')
  chop = len(project)+1

  for r, ds, fs in os.walk(os.path.join(project, 'Assets')):
    for f in fs:
      if not f.endswith('.meta'):
        continue
      meta_fullf = os.path.join(r, f)
      data_fullf = os.path.join(r, f[:-5])
      src_guid = _get_guid_from_meta(meta_fullf)
      if not os.path.isfile(data_fullf):
        continue
      
      # Look in .meta file. It will contain its own guid, but sometimes
      # it contains others (eg, MonoBehaviour may have default asset references)
      for m in guid_pat.finditer(file(meta_fullf).read()):
        dst_guid = m.group(1)
        if dst_guid != src_guid:
          yield (src_guid, dst_guid)

      if not ignore_pat.search(data_fullf):
        for m in guid_pat.finditer(file(data_fullf).read()):
          dst_guid = m.group(1)
          yield (src_guid, dst_guid)


class ReferenceGraph(object):
  # .g             networkx.DiGraph
  # .guid_to_name  dict
  # .name_to_guid  dict (keys are lowercased)
  PICKLE = 'Support/refgraph.pickle'
  def __init__(self, project_dir, recreate=False):
    self.project_dir = os.path.abspath(project_dir)
    loaded = False
    if not recreate:
      try:
        self._load()
        loaded = True
      except IOError:
        pass
    if not loaded:
      self._recreate()
      self._save()

    self._finish()

  def _load(self):
    inf_name = os.path.join(self.project_dir, self.PICKLE)
    with file(inf_name, 'rb') as inf:
      self.g = nx.read_gpickle(inf)
      self.guid_to_name = pickle.load(inf)

  def _recreate(self):
    print "Recreating refgraph. Please wait..."
    sys.stdout.flush()
    self.g = nx.DiGraph()

    # This part of the graph is all guid -> guid
    self.guid_to_name = dict(_iter_guid_names(self.project_dir))
    self.g.add_nodes_from(self.guid_to_name.iterkeys())
    self.g.add_edges_from(_iter_refs(self.project_dir))

    self._recreate_tb_stuff()

  def _recreate_tb_stuff(self):
    """Tilt Brush specific refgraph stuff
    Adds references from .unity and .cs files to GlobalCommands enum entries.
    Also creates a dummy .cs file that can be used to find references to GlobalCommands
    enums from within Visual Studio and Rider"""
    import unitybuild.tb_refgraph as tb
    name_to_guid = dict((n, g) for (g, n) in self.guid_to_name.items())

    for command in tb.iter_command_nodes(self.project_dir):
      self.g.add_node(command)
      self.guid_to_name[command] = command

    command_edges = list(tb.iter_command_edges(self.project_dir))
    for (file_name, command) in command_edges:
      try: file_guid = name_to_guid[file_name]
      except KeyError: print "Couldn't find %s" % file_name
      else: self.g.add_edge(file_guid, command)

    tb.create_dummy_cs(self.project_dir, command_edges)

  def _save(self):
    tmpf = StringIO.StringIO()
    nx.write_gpickle(self.g, tmpf, -1)
    pickle.dump(self.guid_to_name, tmpf, -1)
    outf_name = os.path.join(self.project_dir, self.PICKLE)
    with file(outf_name, 'wb') as outf:
      outf.write(tmpf.getvalue())
    
  def _finish(self):
    """Perform post-load initialization"""
    # Add synthetic guid to use as a root node
    self.guid_to_name[ROOT_GUID] = 'ROOT'

    self.name_to_guid = {}
    # For convenience, also add lowercased-versions
    # (but this is incorrect on case-sensitive filesystems)
    for (g, n) in self.guid_to_name.iteritems():
      self.name_to_guid[n.lower()] = g
    # True capitalization takes precedence
    for (g, n) in self.guid_to_name.iteritems():
      self.name_to_guid[n] = g

    # TILT BRUSH SPECIFIC:
    # The one dynamic choice is "what .unity do you load at startup?"
    # Also link environments to the root, because not all builds include all the envs,
    # but we want to mark all of them as roots.
    n2g = self.name_to_guid
    for node_name in [
        'Assets/Scenes/Main.unity', # Tilt Brush
        'Assets/TiltBrush/Resources/TiltBrushToolkitSettings.asset',  # Tilt Brush Toolkit
        ]:
      if node_name in n2g:
        self.g.add_edge(n2g['ROOT'], n2g[node_name])

    prefab_pat = re.compile(r'^Assets/Resources/EnvironmentPrefabs/.*prefab|^Assets/Scenes', re.I)
    for n in n2g.iterkeys():
      if prefab_pat.search(n):
        self.g.add_edge(n2g['ROOT'], n2g[n])


if __name__ == '__main__':
  rg = ReferenceGraph('c:/src/tb')
