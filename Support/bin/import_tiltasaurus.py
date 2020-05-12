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
import csv
import itertools
import json
import StringIO

def iter_words_and_categories(filename):
  with file(filename) as inf:
    reader = csv.reader(inf)
    it = iter(reader)
    it.next()   # Skip first row
    for row in it:
      if len(row) == 2 and row[0] != '' and row[1] != '':
        yield row

def main():
  parser = argparse.ArgumentParser("Converts google docs .csv to tiltasaurus.json")
  parser.add_argument('-i', dest='input', required=True, help='Name of input .csv file')
  args = parser.parse_args()
  data = list(iter_words_and_categories(args.input))
  data.sort(key=lambda (word, category): (category.lower(), word.lower()))

  categories = []
  for _, group in itertools.groupby(data, key=lambda (word, category): category.lower()):
    group = list(group)
    category = { "Name": group[0][1],
                 "Words": sorted(set(pair[0] for pair in group)) }
    categories.append(category)
  data = json.dumps({"Categories": categories}, indent=2)

  with file('tiltasaurus.json', 'w') as outf:
    outf.write(data)
  print "Wrote tiltasaurus.json"


main()
