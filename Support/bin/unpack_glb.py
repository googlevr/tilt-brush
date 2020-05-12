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

import argparse
import json
import os
import sys

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../Python')))
from tbdata.glb import BaseGlb


def unpack_glb(glb_file):
  no_ext = os.path.splitext(glb_file)[0]
  gltf_file = no_ext + ".gltf"
  bin_file = no_ext + ".bin"

  glb = BaseGlb.create(glb_file)
  glb.json["buffers"][0]["uri"] = os.path.basename(bin_file)
  with file(gltf_file, 'wb') as outf:
    json.dump(glb.json, outf, indent=2)
  with file(bin_file, 'wb') as outf:
    outf.write(glb.bin_chunk)
  return (gltf_file, bin_file)


def main():
  parser = argparse.ArgumentParser(
    description="Unpacks a .glb to a valid pair of .gltf and .bin files")
  parser.add_argument('files', metavar='FILE', action='append')
  args = parser.parse_args()
  for glb_file in args.files:
    output = unpack_glb(glb_file)
  print("Wrote %s" % (output,))


if __name__ == '__main__':
  main()
