#!/usr/bin/python

"""fix_bom.py: Remove errant BOM markers.

It's easy to create invalid non-first BOM (byte order mark) characters in a
file, e.g. when inserting lines above the first line. This script removes such
errant BOMs, provided that each one is the first character of a non-first line.

  usage: python fix_bom.py <path_expression>

  e.g. python fix_bom.py 'Editor/*.cs'

This searches the given <path_expression>, with globbing, and applies fixes as
needed to the files found. If using wildcards, remember to enclose in quotes as in
the above example.
"""

import codecs
import glob
import sys

BOM = u"\ufeff"

if len(sys.argv) < 2:
  print __doc__
  exit(0)

if len(sys.argv) > 2:
  print "Only one argument is allowed. If using wildcards, enclose path in quotes."
  exit(-1)

search_paths = sys.argv[1]
hits = glob.glob(search_paths)
print "Searching %s [%d hit(s)]" % (search_paths, len(hits))
for filename in hits:
  with codecs.open(filename, "rb", "utf-8") as handle:
    lines = handle.read().splitlines()
    update_file = False
    for num, line in enumerate(lines):
      if num > 0 and line and line[0] == BOM:
        lines[num] = line[1:]
        update_file = True
    if not update_file:
      continue

    print "Updating " + filename
    with open(filename, "w") as out_handle:
      for line in lines:
        out_handle.write("%s\n" % line)
