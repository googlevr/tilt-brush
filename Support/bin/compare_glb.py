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

from __future__ import print_function
import glob
import json
import os
import re
import struct
import sys
from subprocess import Popen, PIPE, STDOUT

from tbdata.glb import binfile, BaseGlb, Glb1, Glb2

try:
  import jsondiff
except ImportError:
  print("Try 'pip install jsondiff'", file=sys.stderr)
  raise

DEFAULT_BASELINE_DIR = 'Baseline 22.0'
ROOT = os.path.expanduser('~/Documents/Tilt Brush/Exports')

def first_glob(glob_pat):
  """Returns the first match for the passed glob_pat, or raises error."""
  maybe = glob.glob(glob_pat)
  if len(maybe) == 0: raise LookupError("No %s" % glob_pat)
  if len(maybe) == 1: return maybe[0].replace('\\', '/')
  raise LookupError("Too many %s: %s" % (glob_pat, maybe))


def get_latest_glb(tilt_name, poly):
  """Gets the .glb file that was most-recently exported from <name>.tilt
  Pass:
    poly - True for Poly-style glb1, False for glb2 """
  assert type(poly) is bool
  def get_index(dirname):
    """Returns the small NN if dirname matches '<tilt_name> NN'"""
    length = len(tilt_name)
    prefix, suffix = dirname[:length], dirname[length:]
    # Careful; if tilt_name = 'ET_All', directories like 'ET_All_Huge 3' will pass this check
    if prefix != tilt_name: return None
    if suffix == '': return -1
    m = re.match(r' (\d+)$', suffix)
    if m is not None: return int(m.group(1))
    return None

  matches = [d for d in os.listdir(ROOT) if get_index(d) is not None]
  matches.sort(key=get_index)
  if len(matches) == 0:
    raise LookupError("No export %s" % tilt_name)
  directory = 'glb1' if poly else 'glb'
  return first_glob(os.path.join(os.path.join(ROOT, matches[-1]), directory, '*.glb*'))


def get_baseline_glb(name, baseline_dir_name, poly=True):
  """Gets a known-good .glb file that was exported from <name>.tilt.
  It's your responsibility to create them and save these off in the "Baseline" folder.
  Pass:
    poly - same as for get_latest_glb()"""
  assert type(poly) is bool
  name_no_digit = re.sub(r' \d+$', '', os.path.basename(name))
  directory = 'glb1' if poly else 'glb'
  parent = os.path.join(ROOT, baseline_dir_name, name_no_digit, directory)
  return first_glob(parent + '/*.glb*')


def redact(dct, keys):
  """Helper for the tweak_ functions"""
  if isinstance(keys, basestring): keys = [keys]
  for key in keys:
    if key in dct:
      dct[key] = 'redacted'


def tweak_fix_sampler(dcts):
  for label, dct in enumerate(dcts):
    # Older files have an incorrect name for this sampler
    # label==1 is the old file
    if label == 1:
      def rename(txt):
        return txt.replace('sampler_LINEAR_LINEAR_REPEAT',
                           'sampler_LINEAR_LINEAR_MIPMAP_LINEAR_REPEAT')
      dct['samplers'] = dict((rename(k), v) for (k,v) in dct['samplers'].items())
      for texture in dct.get('textures', {}).values():
        texture['sampler'] = rename(texture['sampler'])


def tweak_ignore_nondeterministic_geometry(dcts):
  for label, dct in enumerate(dcts):
    # Geometry is nondeterministic, so ignore min/max values
    for accessor in dct.get('accessors', {}).values():
      redact(accessor, ['min', 'max'])


def tweak_ignore_envlight(dcts):
  for label, dct in enumerate(dcts):
    # The exported light color is slightly nondeterminstic
    # and also I changed the environment in one of the .tilt files and don't
    # want to bother re-exporting it
    for name, node in items(dct.get('nodes', {})):
      if type(name) is int:
        name = node['name']
      if 'SceneLight' in name:
        redact(node, 'matrix')
    for mat in values(dct.get('materials', {})):
      redact(mat.get('values', {}),
             ['SceneLight_0_color', 'SceneLight_1_color', 'ambient_light_color'])


def tweak_remove_vertexid(dcts):
  removed = []     # nodes that were deleted; may contain Nones
  for label, dct in enumerate(dcts):
    accs = dct['accessors']
    for k in accs.keys():
      if 'vertexId' in k:
        removed.append(accs.pop(k))
    for m in dct['meshes'].values():
      for prim in m['primitives']:
        removed.append(prim['attributes'].pop('VERTEXID', None))

  # Only do this if we detected any vertexid; otherwise I want to verify the offsets, lengths, etc
  if any(filter(None, removed)):
    for label, dct in enumerate(dcts):
      dct['bufferViews'].pop('floatBufferView', None)
      for bv in dct['bufferViews'].values():
        redact(bv, 'byteOffset')
      redact(dct['buffers']['binary_glTF'], 'byteLength')


def tweak_remove_color_minmax(dcts):
  # It's ok if the newer glb doesn't have min/max on color. I intentionally removed it.
  for dct in dcts:
    for name, acc in dct['accessors'].items():
      if 'color' in name:
        acc.pop('min', None)
        acc.pop('max', None)


def items(dct_or_lst):
  """Returns list items, or dictionary items"""
  if type(dct_or_lst) is dict:
    return dct_or_lst.items()
  else:
    return list(enumerate(dct_or_lst))


def values(dct_or_lst):
  """Returns list values, or dictionary values"""
  if type(dct_or_lst) is dict:
    return dct_or_lst.values()
  else:
    return list(dct_or_lst)


def tweak_rename_refimage(dcts):
  # I renamed reference image uris from "refimageN_" -> "media_"; change the baseline to suit
  for name, image in items(dcts[1]['images']):
    if 'uri' in image:
      image['uri'] = re.sub(r'^refimage[0-9]*', 'media', image['uri'])


def tweak_ignore_generator(dcts):
  for dct in dcts:
    redact(dct['asset'], 'generator')


def binary_diff(bina, binb):
  # Returns (success, details)
  # No need to get fancy yet since the binary is identical :-D
  bin_same = (bina == binb)
  return bin_same, '' if bin_same else '\nBINARY DIFFERENCE'


def compare_glb(a, b, binary,
                tweaks=(
                        #tweak_fix_sampler,
                        #tweak_remove_vertexid,
                        #tweak_ignore_nondeterministic_geometry,
                        #tweak_remove_color_minmax,
                        tweak_ignore_generator,
                        tweak_rename_refimage,
                        tweak_ignore_envlight,
                )):
  if open(a).read() == open(b).read():
    return (True, 'IDENTICAL')

  glbs = map(BaseGlb.create, [a, b])
  objs = [json.loads(g.get_json()) for g in glbs]
  for tweak in tweaks: tweak(objs)
  details = jsondiff.diff(objs[0], objs[1], syntax='symmetric',
                          dump=True,
                          dumper=jsondiff.JsonDumper(indent=2))
  if binary:
    bin_same, bin_details = binary_diff(glbs[0].bin_chunk, glbs[1].bin_chunk)
  else:
    bin_same, bin_details = True, 'n/a'
  return details=='{}' and bin_same, details + bin_details


def compare_to_baseline(name, binary=True, poly=True, baseline_dir_name=DEFAULT_BASELINE_DIR):
  """Compare the Poly .glb file to its baseline and report differences"""
  try:
    latest = get_latest_glb(name, poly=poly)
  except LookupError:
    print("%s: Not found" % name)
    return
  baseline = get_baseline_glb(name, baseline_dir_name, poly=poly)
  result, details = compare_glb(latest, baseline, binary)
  short = os.path.basename(os.path.dirname(os.path.dirname(latest)))
  summary = ('ok' if result else ('FAIL: %s' % (details, )))
  print("%s ver %d: %s" % (short, 1 if poly else 2, summary))


def compare_two(name1, name2, binary=True):
  def get_glb_named(name):
    return first_glob(os.path.join(ROOT, name, 'glb1', '*.glb*'))
  result, details = compare_glb(get_glb_named(name1), get_glb_named(name2), binary)
  summary = ('ok' if result else ('FAIL: %s' % (details, )))
  print(summary)

# -----

def test():
  for dirname in glob.glob(os.path.join(ROOT, DEFAULT_BASELINE_DIR, 'ET_All*')):
    compare_to_baseline(os.path.basename(dirname), poly=True)
    compare_to_baseline(os.path.basename(dirname), poly=False)


def main():
  import argparse
  parser = argparse.ArgumentParser()
  parser.add_argument('exports', nargs='*', help='Names of tilt exports to check')
  args = parser.parse_args()
  for arg in args.exports:
    compare_to_baseline(arg)

if __name__ == '__main__':
  test()
