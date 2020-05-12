#!/usr/bin/env python

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

"""One-off hacky script that goes through brushes to find the shaders they use,
and does a bit of introspection to find brushes that generate double-sided
geometry and also use double-sided (ie, non-culling) shaders.

Also useful as sample code for working with the refgraph."""

import os
import re
import sys

# Add ../Python to sys.path
sys.path.append(
  os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'Python'))

import unitybuild.refgraph

BASE = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))

def dfs_iter(graph, guid):
  """graph: networkx.DiGraph
  guid: node name"""
  import collections
  seen = set()
  q = collections.deque()
  q.append(guid)
  while q:
    elt = q.pop()
    seen.add(elt)
    yield elt
    q.extend(succ for succ in graph.successors_iter(elt) if succ not in seen)


def shaders_for_brush(rg, g_brush):
  """rg: unitybuild.refgraph.ReferenceGraph
  g_brush: node (brush guid)
  yields nodes for shaders."""
  for g in dfs_iter(rg.g, g_brush):
    try:
      n = rg.guid_to_name[g]
    except KeyError:
      continue
    if n.lower().endswith('.shader'):
      yield g


def cullmodes_for_brush(rg, g_brush):
  """rg: unitybuild.refgraph.ReferenceGraph
  g_brush: node (brush guid)
  Returns list of culling modes used by shaders for that brush."""
  modes = set()
  for g_shader in shaders_for_brush(rg, g_brush):
    for mode in cullmodes_for_shader(rg.guid_to_name[g_shader]):
      modes.add(mode)
  return sorted(modes, key=lambda m: m.lower)


def cullmodes_for_shader(shader, memo={}):
  """shader: name of shader asset
  Returns list of culling modes used by the shader."""
  try: return memo[shader]
  except KeyError: pass
  txt = file(os.path.join(BASE, shader)).read()
  culls = [m.group(1) for m in
           re.finditer(r'cull\s+(\w+)', txt, re.I|re.M)]
  memo[shader] = culls
  return culls


def is_brush_doublesided(rg, g_brush, memo={}):
  """rg: unitybuild.refgraph.ReferenceGraph
  g_brush: node (brush guid)
  Returns True if brush generates doublesided geometry."""
  filename = rg.guid_to_name[g_brush]
  txt = file(os.path.join(BASE, filename)).read()
  return int(re.search(r'm_RenderBackfaces: (.)', txt).group(1))


def main():
  rg = unitybuild.refgraph.ReferenceGraph(BASE)
  g2n = rg.guid_to_name
  def is_brush(guid):
    try: name = g2n[guid]
    except KeyError: return False
    return re.search(r'Brush.*asset$', name) is not None
  brushes = filter(is_brush, rg.g.nodes_iter())
  for g_brush in sorted(brushes, key=g2n.get):
      culls = cullmodes_for_brush(rg, g_brush)
      if len(culls) > 0 and is_brush_doublesided(rg, g_brush):
        print "Brush %s\n is double-sided but has cull %s" % (g2n[g_brush], culls)

if __name__ == '__main__':
  main()
