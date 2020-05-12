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
import re
import sys
import pickle

import networkx as nx

# Add ../Python to sys.path
sys.path.append(
  os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'Python'))

import unitybuild.refgraph


def find_project_dir(start=None):
  cur = os.getcwd() if start is None else start
  while True:
    if os.path.isdir(os.path.join(cur, 'Assets')):
      return cur
    next = os.path.dirname(cur)
    if cur == next:
      raise LookupError("Cannot find project dir")
    cur = next


def filter_case_folded_duplicates(lst):
  def iter():
    seen = set()
    for elt in lst:
      lowered = elt.lower()
      if lowered not in seen:
        seen.add(lowered)
        yield elt
  return list(iter())


def main(args):
  import argparse
  parser = argparse.ArgumentParser()
  parser.add_argument('--recreate', action='store_true', default=False,
                      help="Recreate the cached graph and DummyCommandRefs.cs")
  grp = parser.add_argument_group("Graph queries")
  grp.add_argument('--shortest-path', action='store_true',
                   help="Show the shortest path from Main.unity to ASSET")
  grp.add_argument('--predecessors', action='store_true',
                   help="Show incoming references to ASSET")
  grp.add_argument('--successors', action='store_true',
                   help="Show outgoing references from ASSET")
  grp.add_argument('--all', action='store_true',
                   help="If asset argument is ambiguous, show all matches")
  grp.add_argument('asset', nargs='*',
                   help="Asset(s) to examine")
  args = parser.parse_args(args)
  if not (args.shortest_path or args.predecessors or args.successors):
    args.shortest_path = True

  rg = unitybuild.refgraph.ReferenceGraph(find_project_dir(), args.recreate)
  root = rg.name_to_guid['ROOT']

  def lookup_guids(asset):
    """Returns a list of guids"""
    asset = asset.lower().replace('\\', '/')
    # Asset name?
    if asset in rg.name_to_guid:
      return [rg.name_to_guid[asset]]
    # Looks like guid?
    if re.match(r'^[a-f0-9]{32}$', asset):
      return [asset]
    # Exhaustive search
    possibilities = [name for name in rg.name_to_guid.iterkeys() if asset in name]
    # name_to_guid contains duplicate lowercased names; don't consider that ambiguous
    possibilities = filter_case_folded_duplicates(possibilities)
    if len(possibilities) == 0:
      raise LookupError("Cannot find any asset matching %s" % asset)
    else:
      if len(possibilities) > 1 and not args.all:
        print "Ambiguous:\n  %s" % '\n  '.join(possibilities)
        possibilities = [possibilities[0]]
      return map(rg.name_to_guid.get, possibilities)

  def iter_desired_guids():
    if len(args.asset) == 0 and not args.recreate:
      parser.error("Too few arguments")
    for asset in args.asset:
      try:
        guids = lookup_guids(asset)
      except LookupError as e:
        print e
        continue
      for guid in guids:
        yield guid

  for guid in iter_desired_guids():
    name = rg.guid_to_name.get(guid, guid)

    if args.shortest_path:
      print "\n=== %s (shortest path)" % name
      try:
        path = nx.shortest_path(rg.g, source=root, target=guid)
        path.reverse()
        for elt in path[:-1]:
          print ' ', elt, rg.guid_to_name.get(elt, elt)
      except nx.exception.NetworkXNoPath:
        print '  (no path)'

    if args.predecessors or args.successors:
      if args.predecessors:
        for guid2 in rg.g.predecessors(guid):
          print '< ', rg.guid_to_name.get(guid2, guid2)
      print '*  ', name
      if args.successors:
        for guid2 in rg.g.successors(guid):
          print '>   ', rg.guid_to_name.get(guid2, guid2)


if __name__ == '__main__':
  main(sys.argv[1:])
