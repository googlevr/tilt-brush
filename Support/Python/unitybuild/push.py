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

# For full documentation, see
#   https://partner.steamgames.com/documentation/steampipe
#   https://dashboard.oculus.com/tools/cli

import os
import re
import sys
import subprocess
from subprocess import Popen, PIPE, STDOUT

from unitybuild.constants import *
from unitybuild.credentials import get_credential, TB_OCULUS_RIFT_APP_ID, TB_OCULUS_QUEST_APP_ID

class ExpansionError(LookupError): pass

def steamcmd(*args):
  args = list(args)
  if len(args) == 1:
    args = args[0].split()

  # SteamCmd has this behavior where it treats relative file paths
  # as relative to the executable itself, rather than to os.getcwd().
  for arg in args:
    if os.path.exists(arg) and not os.path.isabs(arg):
      assert False, "steamcmd needs absolute paths"

  args.insert(0, 'steamcmd')
  proc = Popen(args, stdin=sys.stdin, stdout=sys.stdout, stderr=sys.stderr)
  if proc.wait() != 0:
    raise BuildFailed("SteamCmd failed with code %s" % proc.wait())


def get_builds_dir():
  # Look upwards for the Builds folder. Can fail.
  cur = os.path.abspath(os.path.dirname(__file__))
  while True:
    if os.path.exists(os.path.join(cur, 'Builds')):
      return os.path.join(cur, 'Builds')
    parent = os.path.dirname(cur)
    if parent == cur:
      raise BuildFailed("Cannot find Builds folder; specify one explicitly with --source DIR")
    cur = parent


def get_support_dir():
  # Look upwards for the Support folder. Can fail.
  cur_dir = os.path.abspath(__file__)
  start_dir = cur_dir
  while True:
    parent, name = os.path.split(cur_dir)
    if parent == cur_dir:
      raise LookupError("Can't find Support/ directory from %s" % start_dir)
    elif name.lower() == 'support':
      return cur_dir
    else:
      cur_dir = parent


def get_tmp_steam_dir():
  # Return an existing directory that we can write temp files into.
  # The directory does not have to be persistent, but ideally it will be;
  # it speeds up the content upload process.
  ret = os.path.join(get_support_dir(), 'tmp_steam')
  if not os.path.isdir(ret):
    os.makedirs(ret)
  return ret


def get_build_stamp(directory):
  filename = os.path.join(directory, 'build_stamp.txt')
  try:
    with file(filename, 'rb') as inf:
      return inf.read().strip()
  except IOError:
    print "WARN: Build stamp not found with this build."
    print "Supply one manually, or leave empty to abort this push."
    stamp = raw_input("Stamp: ").strip()
    if not stamp:
      raise BuildFailed("Aborted: no build stamp")


VDF_ESCAPES = {
  '\r': '_',
  '\n': '\\n',
  '\t': '\\t',
  '\\': '\\\\',
  '"' : '\\"'
}
def vdf_quote(txt):
  # See https://developer.valvesoftware.com/wiki/KeyValues
  def quote_char(match):
    return VDF_ESCAPES[match.group(0)]
  txt = re.sub(r'[\r\n\t\\\"]', quote_char, txt)
  return '"%s"' % txt


def expand_vdf_template(input_text, variables):
  """Expand variable references in input_text, ensuring that the expansion
  is a single vdf token."""
  def expand_and_quote(match):
    try:
      expansion = variables[match.group(1)]
    except KeyError:
      raise ExpansionError("unknown variable %s" % (match.group(0),))
    return vdf_quote(expansion)

  return re.sub(r'\$\{(.*?)\}', expand_and_quote, input_text)


def create_from_template(input_file, variables, tmp_dir):
  # Returns the name of an output file
  with file(input_file, 'rb') as inf:
    data = inf.read()

  try:
    expanded = expand_vdf_template(data, variables)
  except ExpansionError as e:
    raise BuildFailed("%s: %s" % (input_file, e))

  output_file = os.path.join(tmp_dir, os.path.basename(input_file).replace('_template', ''))
  with file(output_file, 'wb') as outf:
    outf.write(expanded)
  return output_file


def push_tilt_brush_to_steam(source_dir, description, steam_user, steam_branch=None):
  try:
    steamcmd('+exit')
  except subprocess.CalledProcessError:
    raise BuildFailed("You don't seem to have steamcmd installed")

  support_dir = get_support_dir()
  tmp_steam_dir = get_tmp_steam_dir()

  variables = {
    'DESC': description,
    'TMP_STEAM': tmp_steam_dir,
    'CONTENT_ROOT': os.path.abspath(source_dir).replace('\\', '/'),
    'STEAM_BRANCH': '' if steam_branch is None else steam_branch,
  }
  # This file has no variables that need expanding, but steamcmd.exe
  # mutates it to add a digital signature so we should copy it off to a temp file.
  variables['INSTALLSCRIPT_WIN'] = create_from_template(
    os.path.join(support_dir, 'steam/installscript_win.vdf'), {}, tmp_steam_dir)
  variables['MAIN_DEPOT_VDF'] = create_from_template(
    os.path.join(support_dir, 'steam/main_depot_template.vdf'), variables, tmp_steam_dir)
  app_vdf = create_from_template(
    os.path.join(support_dir, 'steam/app_template.vdf'), variables, tmp_steam_dir)

  print "Pushing %s to Steam" % (variables['CONTENT_ROOT'], )
  steamcmd('+login', steam_user,
           '+run_app_build', app_vdf,
           '+quit')


# ----------------------------------------------------------------------
# Oculus support
# ----------------------------------------------------------------------

OCULUS_RIFT_REDISTS = [
  '1675031999409058',           # Visual C++ 2013
  '1183534128364060',           # Visual C++ 2015
]

def quote_oculus_release_notes(txt):
  return txt.replace('\r', '').replace('\n', '\\n')


def get_oculus_tiltbrush_exe(directory):
  files = os.listdir(directory)
  # Might be named TiltBrush_oculus.exe?
  files = [f for f in files if f.endswith('.exe') and f.lower().startswith('tiltbrush')]
  if len(files) == 0:
    raise BuildFailed("Can't find launch executable")
  elif len(files) == 1:
    return files[0]
  else:
    raise BuildFailed("Ambiguous launch executable: %s" % (files,))


def unbuffered_reads(inf):
  """Yields unbuffered reads from file, until eof."""
  while True:
    data = inf.read(1)
    if data == '': break
    yield data


def group_into_lines(iterable):
  """Yields complete lines (lines terminated with \\r and/or \\n)."""
  current = []
  terminator = re.compile(r'^([^\r\n]*[\r\n]+)(.*)$', re.MULTILINE)
  for data in iterable:
    # Deal with any line terminators in the data
    #print "IN %r" % data
    while True:
      m = terminator.match(data)
      if m is None:
        break

      eol, data = m.groups()
      #print " match %r %r" % (eol, data)
      current.append(eol)
      yield ''.join(current)
      current = []

    current.append(data)

  yield ''.join(current)


def get_oculus_build_type(build_path):
  files = os.listdir(build_path)
  if any(f.endswith('.apk') for f in files):
    return 'quest'
  elif any(f.endswith('.exe') for f in files):
    return 'rift'
  else:
    raise BuildFailed("Don't know what kind of build is in %s" % build_path)


def get_secret(env_var_name, credential_name):
  """Look in environment and in credentials to fetch a secret."""
  # TODO(pld): env-var path currently unused, since we don't push from Jenkins
  if env_var_name in os.environ:
    return os.environ[env_var_name]
  return get_credential(credential_name).get_secret()


def push_tilt_brush_to_oculus(
    build_path,
    release_channel,
    release_notes):
  assert os.path.isabs(build_path)
  assert os.path.exists(build_path)
  assert release_channel is not None, "You must pass a release channel to push to Oculus Home"

  # TEMP: yucky code to figure out if rift or quest
  build_type = get_oculus_build_type(build_path)
  if build_type == 'rift':
    app_id = TB_OCULUS_RIFT_APP_ID
    args = [
      'ovr-platform-util',
      'upload-rift-build',
      '--app_id', app_id,
      '--build_dir', build_path,
      '--app_secret', get_secret('APP_SECRET_FOR_' + app_id, app_id),
      '--channel', release_channel,
      '--version', get_build_stamp(build_path),
      '--notes', quote_oculus_release_notes(release_notes),
      '--launch_file', get_oculus_tiltbrush_exe(build_path),
      '--redistributables', ','.join(OCULUS_RIFT_REDISTS),
      '--firewall_exceptions', 'true'
    ]
  elif build_type == 'quest':
    import glob
    apks = glob.glob(build_path + '/*.apk')
    if len(apks) != 1:
      raise BuildFailed("No or too many APKs in %s: %s" % (build_path, apks))
    apk = apks[0]
    # This requires a recent build of ovr-platform-util
    app_id = TB_OCULUS_QUEST_APP_ID
    args = [
      'ovr-platform-util',
      'upload-quest-build',
      '--app_id', app_id,
      '--app_secret', get_secret('APP_SECRET_FOR_' + app_id, app_id),
      '--apk', apk,
      # --assets-dir
      # --assets-file-iap-configs-file
      # --obb
      '--channel', release_channel,
      '--notes', quote_oculus_release_notes(release_notes),
    ]
  else:
    raise BuildFailed("Internal error: %s" % build_type)

  try:
    proc = Popen(args, stdin=PIPE, stdout=PIPE, stderr=STDOUT)
    proc.stdin.close()
  except OSError as e:
    # Probably "cannot find the file specified"
    if 'cannot find the file' in str(e):
      raise BuildFailed("You don't seem to have ovr-platform-util installed.\nDownload it at https://dashboard.oculus.com/tools/cli")
    else:
      raise
  except subprocess.CalledProcessError as e:
    raise BuildFailed("ovr-platform-util failed: %s" % e)

  saw_output = False
  desired_version_code = None
  for line in group_into_lines(unbuffered_reads(proc.stdout)):
    if line.strip():
      saw_output = True
    sys.stdout.write(line)
    # The request will be retried indefinitely, so stall it out
    if 'error occurred. The request will be retried.' in line:
      print
      get_credential(app_id).delete_secret()
      # Maybe the secret changed; ask user to re-enter it
      raise BuildFailed("Your App Secret might be incorrect. Try again.")
    m = re.search(r'higher version code has previously been uploaded .code: (?P<code>\d+)', line)
    if m is not None:
      desired_version_code = int(m.group('code')) + 1

    # Example error text:
    # * An APK has already been uploaded with version code 59. Please update the application manifest's version code to 64 or higher and try again.
    m = re.search(r'version code to (?P<code>\d+) or higher and try again', line)
    if m is not None:
      desired_version_code = int(m.group('code'))

  if proc.wait() != 0:
    message = 'ovr-platform-util failed with code %s' % proc.wait()
    if desired_version_code is not None:
      raise BadVersionCode(message, desired_version_code)
    else:
      raise BuildFailed(message)
  if not saw_output:
    raise BuildFailed('ovr-platform-util seemed to do nothing.\nYou probably need a newer version.\nDownload it at https://dashboard.oculus.com/tools/cli')


# ----------------------------------------------------------------------
# Command-line use. Deprecated and mostly for testing
# ----------------------------------------------------------------------

def main(args=None):
  import argparse

  parser = argparse.ArgumentParser(
    description="(deprecated) Upload a build directory to Steam or Oculus.")

  parser.add_argument('--user', type=str, default='tiltbrush_build',
                      help='(Steam only) User to authenticate as. (default: %(default)s)')
  parser.add_argument('--desc',
                      help="Optional description of this build.")
  parser.add_argument('--branch',
                      help='Steam Branch or Oculus Release Channel to set live.')
  parser.add_argument('--what', required=True, metavar='DIR',
                      help='Path to a Tilt Brush build folder')
  parser.add_argument('--where', metavar='SERVICE', required=True, choices=['Oculus', 'SteamVR'],
                      help='Oculus or SteamVR')

  args = parser.parse_args(args)
  if args.branch == 'none':
    args.branch = ''

  if not os.path.exists(args.what):
    raise UserError("%s does not exist" % args.what)
  args.what = os.path.abspath(args.what)

  if args.where == 'Oculus':
    if args.branch == '' or args.branch is None:
      raise UserError("For Oculus, you must specify a --branch")
    push_tilt_brush_to_oculus(args.what, args.branch, "No release notes")
  elif args.where == 'SteamVR':
    description = 'Manual: %s | %s' % (get_build_stamp(args.what), args.desc)
    push_tilt_brush_to_steam(args.what, description, args.user, steam_branch=None)
  else:
    raise BuildFailed("Don't know how to push %s" % args.display)


if __name__ == '__main__':
  try:
    main()
  except BuildFailed as e:
    print "ERROR: %s" % e
