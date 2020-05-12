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

# Add ../Python to sys.path
sys.path.append(
  os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'Python'))

def gen_used_assets(build_dir):
  log = os.path.join(build_dir, 'build_log.txt')
  with file(log) as inf:
    data = inf.read()
  asset_pat = re.compile(r'% (.*)')
  m = re.search(r'^Used Assets([\w ]+), sorted by[^\n]+\n(?P<assets>.*?)^DisplayProgressNotification', data,
                re.MULTILINE | re.DOTALL)
  for match in asset_pat.finditer(m.group('assets')):
    yield match.group(1)

def gen_existing_assets(project_dir):
  for r,ds,fs in os.walk(os.path.join(project_dir, 'Assets')):
    rr = os.path.relpath(r, start=project_dir).replace('\\', '/') + '/'
    ds[:] = [d for d in ds if d != 'Editor']
    for f in fs:
      if f.endswith('.meta'):
        continue
      yield rr + f

def get_filesize(filename):
  try:
    return os.stat(filename).st_size
  except IOError:
    return -1

def main():
  from unitybuild.main import find_project_dir

  os.chdir(find_project_dir())
  used = set(gen_used_assets(r'../Builds/Windows_SteamVR_Release/'))
  exist = set(gen_existing_assets('.'))
  if len(used) == 0:
    print 'WARN: no used assets; did Unity change their build.log format again?'
    return

  missing = used-exist
  extra = exist-used
  for m in sorted(missing):
    print 'miss', m
  print '---'
  extra_with_size = [(get_filesize(x), x) for x in extra]
  extra_with_size.sort(key=lambda x: -x[0])
  for size, filename in extra_with_size:
    if '/Resources/' in x: continue
    print 'xtra %8d %s' % (size, filename)

  both = set(map(str.lower, missing)) & set(map(str.lower, extra))
  assert len(both) == 0

main()
