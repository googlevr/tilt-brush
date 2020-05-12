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

import os
import re

# Stolen from unitybuild/main.py
def find_project_dir():
  def search_upwards_from(dir):
    # Search upwards for root of unity project
    from os.path import exists, join
    dir = os.path.abspath(dir)
    while True:
      if exists(join(dir, 'Assets')) and exists(join(dir, 'ProjectSettings')):
        return dir
      parent = os.path.dirname(dir)
      if parent == dir:
        return None
      dir = parent
  return search_upwards_from('.') or search_upwards_from(__file__)


def do_codegen(filename):
  contents = open(filename).read()
  pat = re.compile(r'''^ \#if\ USING_CODEGEN_PY \n
  (?P<fullbody>
      \s* // \s+ EXPAND\( (?P<varlist> .*?) \) \n
      (?P<body> .*? )
  ) \n
  \#else \n
  .*?            # not captured because this part is ignored
  \#endif \n''', re.MULTILINE | re.VERBOSE | re.DOTALL)

  def expand_one(varname, substitution, body):
    body = body.replace(varname, substitution)
    return '  // %s = %s\n%s' % (varname, substitution, body)

  def expand_all(match):
    # First line of the body should look like
    #  // EXPAND(VAR, value1, value2, value3)
    var_values = [x.strip() for x in match.group('varlist').split(',')]
    var_name = var_values.pop(0)
    body = match.group('body')
    # Strip comments, because they'll just duplicate the comments you can read
    # in the #if'd out code.
    body = re.sub(r'^\s*//.*\n', '', body, flags=re.M)
    expansion = '\n\n'.join(expand_one(var_name, var_value, body)
                            for var_value in var_values)
    return '''#if USING_CODEGEN_PY
%s
#else
# region codegen
%s
# endregion
#endif
''' % (match.group('fullbody'), expansion)

  new_contents, n = pat.subn(expand_all, contents)
  assert n == 1
  if new_contents != contents:
    with open(filename, 'w') as outf:
      outf.write(new_contents)
    print('Updated', filename)
  else:
    print('Not updated', filename)


def main():
  project_dir = find_project_dir()
  files = ['Assets/Scripts/SketchBinaryWriter.cs',
           'Assets/Scripts/SketchBinaryReader.cs']
  for filename in files:
    do_codegen(os.path.join(project_dir, filename))


if __name__ == '__main__':
  main()
