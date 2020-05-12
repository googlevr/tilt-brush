#!/usr/bin/python

"""Adds editor || experimental guard to all scripts in this directory."""

import glob

begin_guard = "#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)"
end_guard = "#endif"

for filename in glob.glob("*.cs"):
  with file(filename) as handle:
    lines = handle.read().splitlines()
    if not lines or lines[0] == begin_guard:
      print "skipped: %s" % filename
      continue
  with open(filename, "w") as out_handle:
    out_handle.write(begin_guard + "\n")
    for line in lines:
      out_handle.write("%s\n" % line)
    out_handle.write(end_guard + "\n")
  print "updated: %s" % filename
