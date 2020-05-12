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

import argparse
try:
  from tiltbrush.tilt import Tilt
except ImportError:
  print "You need the Tilt Brush Toolkit (https://github.com/googlevr/tilt-brush-toolkit)"
  print "and then put its Python directory in your PYTHONPATH."
  sys.exit(1)


def main():
  parser = argparse.ArgumentParser()
  parser.add_argument('--set-min-y', dest='desired_min_y', type=float,
                      default=None,
                      help='Move sketch up/down to match the passed y value')
  parser.add_argument('files', nargs='+')
  args = parser.parse_args()

  for filename in args.files:
    tilt = Tilt(filename)
    sketch = tilt.sketch
    print '=== %s ===' % filename

    if args.desired_min_y is not None:
      min_y = min(cp.position[1]
                  for stroke in sketch.strokes
                  for cp in stroke.controlpoints)
      delta = args.desired_min_y - min_y
      for stroke in sketch.strokes:
        for cp in stroke.controlpoints:
          cp.position[1] += delta

      print filename
      print 'Moved by %.3f' % delta
      tilt.write_sketch()

if __name__ == '__main__':
  main()
