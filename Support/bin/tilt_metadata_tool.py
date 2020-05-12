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

import sys
import pprint

try:
  from tiltbrush.tilt import Tilt
except ImportError:
  print "You need the Tilt Brush Toolkit (https://github.com/googlevr/tilt-brush-toolkit)"
  print "and then put its Python directory in your PYTHONPATH."
  sys.exit(1)

def as_unicode(txt):
  if type(txt) is not unicode:
    try:
      txt = txt.decode('utf-8')
    except UnicodeDecodeError:
      # probably latin-1 or some windows codepage
      print "Warning: argument is not utf-8. Trying latin-1."
      txt = txt.decode('latin-1')
  return txt

def main(args=None):
  import argparse
  parser = argparse.ArgumentParser(description="View and modify .tilt file metadata")
  parser.add_argument('--list', action='store_true', help='Print metadata')
  parser.add_argument('--author', action='append', type=as_unicode, help='Set author (may be passed multiple times)',
                      default=None)
  parser.add_argument('files', nargs='+', type=str, help="File to examine")
  args = parser.parse_args(args)

  for filename in args.files:
    print '-- %s -- ' % filename
    sketch = Tilt(filename)
    with sketch.mutable_metadata() as meta:
      if args.author is not None:
        meta['Authors'] = args.author
      if args.list:
        pprint.pprint(meta)

if __name__ == '__main__':
  main()
