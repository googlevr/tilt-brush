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

import re
import sys
import subprocess

TEST_STEAM_1 = 'testing_point_release	1327514	Sep 9, 2016 @ 12:26pm	Release 7.1-06cb99f'
TEST_STEAM_2 = '1327514	Sep 9, 2016 @ 12:26pm	Release 7.1-06cb99f'
# This is their old format
# TEST_OCULUS_1 = '''9.0-9197ad956Feb 17, 2017 (4:34pm)
#  Complete
# No release notes'''
TEST_OCULUS_1 = '''Sep 06, 2017 (4:27pm)
Version: 14.0-67933466c Code: 204
 Complete
Release 14.0-67933466c | machk@skillman0-w to testing_release'''

# This is actually more like storefront + platform
STOREFRONT_INFO = {
  'steam': {
    # Redacted: 'https://partner.steamgames.com/apps/builds/XXXXXX"
    'url': ''
  },
  'oculus-desktop': {
    # Redacted: 'https://dashboard.oculus.com/application/XXXXXXXXXXXXXXXX/channel/XXXXXXXXXXXXXXXX'
    ''
  },
  'oculus-quest': {
    # Redacted: 'https://dashboard.oculus.com/application/XXXXXXXXXXXXXXXX/channel/XXXXXXXXXXXXXXX'
    'url': ''
  },
}
STOREFRONTS = sorted(STOREFRONT_INFO.keys())

class Error(Exception):
  pass


def make_tag_name(full_version, store):
  """Converts a version number to a tag name, and does some sanity checking."""
  (major_version, rest) = full_version.split('.', 1)
  if full_version.endswith('b'):
    raise Error('Do you really want to tag a beta version %s?' % full_version)
  if major_version == '19' and store != 'oculus-quest':
    print 'WARNING: 19.x builds are only for Oculus Quest'
  return 'v%s/%s-%s' % (major_version, full_version, store)


def make_steam_cmd(line, store):
  """Returns a dictionary with the keys 'buildid', 'version', 'sha'"""
  #1327514	Sep 9, 2016 @ 12:26pm	Release 7.1-06cb99f
  pat = re.compile(r'''
    (?P<branch>[a-z_]+ \t )?
    (?P<buildid>\d+)   \t 
    (?P<date>[^\t]+)  \t
    Release \s (?P<version>\d+\.\d+)-(?P<sha>[0-9a-f]+)''', re.X)
  m = pat.match(line)
  if m is None:
    raise Error('Could not parse. Input should look something like\n%s' % TEST_STEAM_2)
  dct = m.groupdict()
  return ['git', 'tag',
          '-m', 'Steam build %s' % (dct['buildid']),
          make_tag_name(dct['version'], store), dct['sha']]


def make_oculus_cmd(txt, store):
  """store: either 'oculus-desktop' or 'oculus-quest'"""
  # From https://dashboard.oculus.com/application/1111640318951750/channel/1019939354782811
  # Apr 27, 2017 (9:44am)
  # Version: 10.0-b3edd1a Code: 107
  #  Complete
  # Release 10.0-b3edd1a | pld@PHACKETT2-W to testing_release
  assert store in ('oculus-desktop', 'oculus-quest')
  pat = re.compile(r'''
    (?P<date>[A-Za-z,0-9\ ]+) \s
    \( (?P<time>[0-9:]+[ap]m) \) \n
    Version: \s (?P<version>\d+\.\d+b?)-(?P<sha>[0-9a-f]{7,9}) \s
    Code: \s (?P<versioncode>[0-9]+)
    ''', re.X | re.M)
  m = pat.match(txt)
  if m is None:
    raise Error('Could not parse. Input should look something like\n%s' % TEST_OCULUS_1)
  dct = m.groupdict()
  return ['git', 'tag',
          '-m', 'Oculus Version Code %s' % (dct['versioncode']),
          make_tag_name(dct['version'], store), dct['sha']]


def do_gui():
  import Tkinter as tk
  root = tk.Tk()

  def close_window():
    text_var.set(text_widget.get(1.0, tk.END))
    root.destroy()

  store_var = tk.StringVar()
  store_var.set('steam')
  text_var = tk.StringVar()

  def open_url(event):
    name = str(event.widget.cget("text"))
    try: url = STOREFRONT_INFO[name]['url']
    except KeyError as e:
      print e
      return
    import webbrowser
    webbrowser.open_new(url)

  for storefront, info in sorted(STOREFRONT_INFO.items()):
    button = tk.Radiobutton(
      root, text=storefront, padx=20, variable=store_var, value=storefront)
    button.pack(anchor=tk.W)
    button.bind("<Button-1>", open_url)

  tk.Label(root, text="Paste build info here").pack()
  text_widget = tk.Text(root, height=6, width=80)
  text_widget.pack()
  button = tk.Button(root, text='OK', command=close_window)
  button.pack()
  root.mainloop()
  return store_var.get(), text_var.get()


def main():
  import argparse
  parser = argparse.ArgumentParser()
  parser.add_argument('--store', choices=sorted(STOREFRONT_INFO.keys()))
  parser.add_argument('--gui', action='store_true')
  args = parser.parse_args()

  if args.gui:
    args.store, text_input = do_gui()
  else:
    if args.store is None:
      parser.error("argument --store is required")
    print("Enter input, then hit Control-Z (cmd.exe) or Control-D (bash)")
    args.store, text_input = args.store, sys.stdin.read()

  try:
    if args.store == 'steam':
      lines = text_input.strip().split('\n')
      line = lines[0].strip()
      cmd = make_steam_cmd(line, args.store)
    elif args.store.startswith('oculus'):
      cmd = make_oculus_cmd(text_input.strip(), args.store)
  except Error as e:
    if args.gui:
      import tkMessageBox
      tkMessageBox.showerror('Error', str(e))
    else:
      print >>sys.stderr, e
    sys.exit(1)
  else:
    def quotify(txt):
      return txt if ' ' not in txt else '"%s"' % txt
    print("Running %s" % ' '.join(map(quotify, cmd)))
    subprocess.call(cmd)

if __name__ == '__main__':
  main()
