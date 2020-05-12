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

import os
from PIL import Image

class InvalidFile(Exception):
  pass


def get_alt_file(infile):
  f, ext = os.path.splitext(infile)
  ext = ext.lower()
  if ext in ('.jpg', '.jpeg'):
    return f + '.png'
  elif ext == '.png':
    return f + '.jpg'
  else:
    raise InvalidFile("Can't do anything with %s" % infile)


def convert(infile):
  outfile = get_alt_file(infile)
  if not os.path.exists(outfile):
    Image.open(infile).save(outfile)
    print "Saved ", outfile
  else:
    print "%s already exists" % outfile

def main():
  import argparse
  parser = argparse.ArgumentParser(description="Convert files between jpg and png")
  parser.add_argument('--all-jpg', help="Recursively convert all jpg files to png",
                      action='store_true')
  parser.add_argument('files', type=str, nargs='*',
                      help="Files to convert to the other format")
  args = parser.parse_args()

  for arg in args.files:
    convert(arg)

  if args.all_jpg:
    for (r, ds, fs) in os.walk('.'):
      for f in fs:
        if f.endswith('.jpg'):
          fullf = os.path.join(r, f)
          if not os.path.exists(get_alt_file(fullf)):
            convert(fullf)

main()
