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

# Possible BuildOptions:
#    None                          Perform the specified build without any special settings or extra tasks.
#    Development                   Build a development version of the player.
#    AutoRunPlayer                 Run the built player.
#    ShowBuiltPlayer               Show the built player.
#    BuildAdditionalStreamedScenes Build a compressed asset bundle that contains streamed scenes loadable with the WWW class.
#    AcceptExternalModificationsToPlayer Used when building Xcode (iPhone) or Eclipse (Android) projects.
#    WebPlayerOfflineDeployment    Copy UnityObject.js alongside Web Player so it wouldn't have to be downloaded from internet.
#    ConnectWithProfiler           Start the player with a connection to the profiler in the editor.
#    AllowDebugging                Allow script debuggers to attach to the player remotely.
#    SymlinkLibraries              Symlink runtime libraries when generating iOS Xcode project. (Faster iteration time).
#    UncompressedAssetBundle       Don't compress the data when creating the asset bundle.
#    DeployOnline                  Generate online version of webplayer.
#    EnableHeadlessMode            Build headless Linux standalone.
#    BuildScriptsOnly              Build only the scripts of a project.
#    ForceEnableAssertions         Include assertions in the build. By default, the assertions are only included in development builds.

# Possible BuildTargets:
#    StandaloneOSXUniversal        Build a universal OSX standalone.
#    StandaloneOSXIntel            Build an OS X standalone (Intel only).
#    StandaloneWindows             Build a Windows standalone.
#    WebPlayer                     Build a web player.
#    WebPlayerStreamed             Build a streamed web player.
#    iOS                           Build an iOS player.
#    Android                       Build an Android .apk standalone app.
#    StandaloneLinux               Build a Linux standalone.
#    StandaloneWindows64           Build a Windows 64-bit standalone.
#    WebGL                         WebGL.
#    StandaloneLinux64             Build a Linux 64-bit standalone.
#    StandaloneLinuxUniversal      Build a Linux universal standalone.
#    StandaloneOSXIntel64          Build an OSX Intel 64-bit standalone.

import itertools
import os
import re
import sys
import time
import glob
import shutil
import threading
import subprocess

import unitybuild.utils
import unitybuild.push
from unitybuild.constants import *

BUILD_OUT = 'TiltBrush'
EXE_BASE_NAME = 'TiltBrush'

# ----------------------------------------------------------------------
# Build logic
# ----------------------------------------------------------------------

class LogTailer(threading.Thread):
  """Copy interesting lines from Unity's logfile to stdout.
  Necessary because Unity's batchmode is completely silent on Windows.

  When used in a "with" block, *logfile* is guaranteed to be closed
  after the block exits."""
  POLL_TIME = 0.5
  def __init__(self, logfile, disabled=False):
    super(LogTailer, self).__init__()
    self.daemon = True
    self.logfile = logfile
    # It's not very easy to have optional context managers in Python,
    # so allow caller to pass an arg that makes this essentially a no-op
    self.should_exit = disabled

  def __enter__(self):
    self.start()

  def __exit__(self, *args):
    # Joining the thread is the easiest and safest way to close the logfile
    self.should_exit = True
    try:
      self.join(self.POLL_TIME + 0.5)
    except RuntimeError:
      # This exception is expected if the thread hasn't been started yet.
      pass
    sys.stdout.write("%-79s\r" % '')        # clear line
    return False

  def run(self):
    # Wait for file to be created
    while not os.access(self.logfile, os.R_OK):
      if self.should_exit: return
      time.sleep(self.POLL_TIME)

    # All of BuildTiltBrush.CommandLine()'s output is prefixed with _btb_
    munge_pat = re.compile('Updating (Assets/.*) - GUID')
    progress_pat = re.compile('(_btb_ |DisplayProgressbar: )(.*)')
    with open(self.logfile) as inf:
      while True:
        where = inf.tell()
        line = inf.readline()
        try:
          if not line:
            if self.should_exit: return
            time.sleep(self.POLL_TIME)
            inf.seek(where)
          elif progress_pat.match(line):
            print 'Unity> %-70s\r' % progress_pat.match(line).group(2)[-70:],
          elif munge_pat.match(line):
            print 'Munge> %-70s\r' % munge_pat.match(line).group(1)[-70:],
        except IOError:
          # The "print" can raise IOError
          pass

def get_unity_exe(version, lenient=True):
  """Returns a Unity executable of the same major version.
  version - a (major, minor, point) tuple. Strings.
  lenient - if True, allow the micro version to be higher.
  """
  exes = sorted(iter_editors_and_versions(), reverse=True)
  if len(exes) == 0:
    raise BuildFailed("Cannot find any Unity versions (want %s)" % (version,))
  for (found_exe, found_version) in exes:
    if found_version == version:
      return found_exe

  if lenient:
    # Compatible is defined as same major and minor version
    compatible = [(exe, ver) for (exe, ver) in exes if ver[0:2] == version[0:2]]
    if len(compatible) > 0:
      def int_version(version):
        (major, minor, micro) = version
        return (int(major), int(minor), int(micro))
      def by_int_version((exe, ver)):
        return (int_version(ver), exe)
      found_exe, found_version = max(compatible, key=by_int_version)
      if int_version(found_version) >= int_version(version):
        return found_exe

  raise BuildFailed("Cannot find desired Unity version (want %s)" % (version,))


def iter_possible_windows_editor_locations():
  """Yields possible locations for Unity.exe"""
  # New-style Unity Hub install locations
  for editor_dir in glob.glob(r'c:\Program Files*\Unity*\Hub\Editor\*\Editor'):
    yield editor_dir
  # Old-school install locations
  for editor_dir in glob.glob(r'c:\Program Files*\Unity*\Editor'):
    yield editor_dir
  # Check to see if UnityHub has a secondary install path defined.
  install_config_file_path = os.path.join(
    os.getenv('APPDATA'),
    r'UnityHub\secondaryInstallPath.json')
  if os.path.exists(install_config_file_path):
    with open(install_config_file_path, 'r') as install_config_file:
      import json
      install_dir = json.load(install_config_file)
      for editor_dir in glob.glob(install_dir + r'\*\Editor'):
        yield editor_dir


def iter_editors_and_versions():
  """Yields (exe_path, (major, minor, micro)) tuples.
  All elements are strings."""
  hub_exe = None
  if sys.platform == 'win32':
    hub_exe = r'c:\Program Files\Unity Hub\Unity Hub.exe'
  elif sys.platform == 'darwin':
    hub_exe = r'/Applications/Unity Hub.app/Contents/MacOS/Unity Hub'
  else:
    hub_exe = None
  # Headless hub isn't totally headless; it forces the hub to pop up, which is irritating.
  # Disabling for now.
  if False and hub_exe and os.path.exists(hub_exe):
    proc = subprocess.Popen([hub_exe, '--', '--headless', 'editors', '--installed'],
                            stdout=subprocess.PIPE)
    for line in proc.stdout:
      m = re.search(r'(\d+)\.(\d+)\.(\d+).* at (.*)', line)
      if m:
        yield (m.group(4), (m.group(1), m.group(2), m.group(3).strip()))
    return

  if sys.platform == 'win32':
    for editor_dir in iter_possible_windows_editor_locations():
      editor_data_dir = os.path.join(editor_dir, 'Data')
      if os.path.exists(editor_data_dir):
        try:
          exe = os.path.join(editor_dir, 'Unity.exe')
          if os.path.exists(exe):
            yield (exe, get_editor_unity_version(exe, editor_data_dir))
          else:
            print 'WARN: Missing executable %s' % exe
        except LookupError as e:
          print e
        except Exception as e:
          print 'WARN: Cannot find version of %s: %s' % (editor_dir, e)
  elif sys.platform == 'darwin':
    # Kind of a hacky way of detecting the Daydream build machine
    is_build_machine = os.path.exists('/Users/jenkins/JenkinsCommon/Unity')
    if is_build_machine:
      app_list = glob.glob('/Users/jenkins/JenkinsCommon/Unity/Unity_*/Unity.app')
    else:
      # TODO: make it work with Unity hub?
      app_list = ['/Applications/Unity/Unity.app']
    for editor_dir in app_list:
      exe = os.path.join(editor_dir, 'Contents/MacOS/Unity')
      editor_data_dir = os.path.join(editor_dir, 'Contents')
      if os.path.exists(editor_dir):
        yield (exe, get_editor_unity_version(editor_dir, editor_data_dir))


def parse_version(txt):
  txt = txt.strip()
  major, minor, point = re.match(r'(\d+)\.(\d+)\.?(\d+)?', txt).groups()
  if point is None: point = 0
  return (major, minor, point)


def get_editor_unity_version(editor_app, editor_data_dir):
  """Pass the app and its Editor/Data directory.
  The app should end with '.app' (OSX) or '.exe' (Windows)
  Returns a version 3-tuple like ("5", "6", "1") or ("2017", "1", "1").
  Does not return any suffixes like "p4" or "f3".
  Raises LookupError on failure."""

  # This works for 5.x as well as 2017.x and 2018.x, but not 2019.x
  packagemanager_dir = os.path.join(editor_data_dir, 'PackageManager/Unity/PackageManager')
  if os.path.exists(packagemanager_dir):
    # The package manager has names like "5.6.3".
    _, dirs, _ = os.walk(packagemanager_dir).next()
    if len(dirs) > 0:
      return parse_version(dirs[0])

  # This works for 5.x releases, but not 2017.x
  analytics_version = os.path.join(editor_data_dir, 'UnityExtensions/Unity/UnityAnalytics/version')
  if os.path.exists(analytics_version):
    with open(analytics_version) as inf:
      return parse_version(inf.read())

  # TODO(pld): For 2019, maybe search the modules.json file for strings
  # like "UnitySetup-Android-Support-for-Editor-<version>"? But that
  # file doesn't live in the editor data directory and it might not
  # exist at all for MacOS.

  try:
    (major, minor, micro) = unitybuild.utils.get_file_version(editor_app)
  except LookupError:
    # Keep trying; we have one last fallback
    pass
  else:
    return (str(major), str(minor), str(micro))

  # I can't find a way to get the version out of 2019.x.
  # This is pretty janky so only use for Jenkins and 2019.
  for m in re.finditer(r'/Users/jenkins/JenkinsCommon/Unity/Unity_(2019)\.(\d+)\.(\d+)',
                       editor_data_dir):
    major, minor, point = m.groups()
    ret = (major, minor, point)
    print "WARNING: %s using fallback to determine Unity version %s" % (editor_data_dir, ret)
    return ret

  raise LookupError('%s: Cannot determine Unity version' % editor_data_dir)


def get_project_unity_version(project_dir):
  """Returns a (major, minor, point) tuple."""
  fn = os.path.join(project_dir, 'ProjectSettings/ProjectVersion.txt')
  with open(fn) as inf:
    m = re.search(r'^m_EditorVersion: (.*)', inf.read(), flags=re.M)
    return parse_version(m.group(1))


def indent(prefix, text):
  return '\n'.join(prefix + line for line in text.split('\n'))


def iter_compiler_output(log):
  """Yields dicts containing the keys:
  exitcode, compilationhadfailure, outfile, stdout, stderr"""
  # Compile output looks like this:
  # -----CompilerOutput:-stdout--exitcode: 1--compilationhadfailure: True--outfile: Temp/Assembly-CSharp-Editor.dll
  # Compilation failed: 1 error(s), 0 warnings
  # -----CompilerOutput:-stderr----------
  # Assets/Editor/BuildTiltBrush.cs(33,7): error CS1519: <etc etc>
  # -----EndCompilerOutput---------------
  pat = re.compile(r'''
  ^-----CompilerOutput:-stdout(?P<metadata>.*?) \n
  (?P<body>.*?) \n
   -----EndCompilerOutput''',
                   re.DOTALL | re.MULTILINE | re.VERBOSE)
  for m in pat.finditer(log):
    dct = {}
    for chunk in m.group('metadata').split('--'):
      if chunk:
        key, value = chunk.split(': ', 1)
        dct[key] = value
    dct['exitcode'] = int(dct['exitcode'])
    dct['compilationhadfailure'] = (dct['compilationhadfailure'] != 'False')
    body = m.group('body').strip().split('-----CompilerOutput:-stderr----------\n')
    dct['stdout'] = body[0].strip()
    dct['stderr'] = body[1].strip() if len(body) > 1 else ''
    yield dct


def check_compile_output(log):
  """Raises BuildFailed if compile errors are found.
  Spews to stderr if compile warnings are found."""
  dcts = list(iter_compiler_output(log))
  compiler_output = '\n'.join(stuff.strip()
                              for dct in dcts
                              for stuff in [dct['stderr'], dct['stdout']])
  if any(dct['compilationhadfailure'] for dct in dcts):
    # Mono puts it in stderr; Roslyn puts it in stdout.
    # But! Unity 2018 also gives us a good build report, so we might be able to
    # get the compiler failures from the build report instead of this ugly parsing
    # through Unity's log file.
    raise BuildFailed('Compile\n%s' % indent('| ', compiler_output))
  elif compiler_output != '':
    print >>sys.stderr, 'Compile warnings:\n%s' % indent('| ', compiler_output)


def search_backwards(text, start_point, limit, pattern):
  """Search the range [limit, start_point] for instances of |pattern|.
  Returns the one closest to |start_point|.
  Returns |limit| if none are found."""
  assert limit < start_point
  matches = list(pattern.finditer(text[limit : start_point]))
  if len(matches) == 0:
    return limit
  else:
    return limit + matches[-1].start(0)


def analyze_unity_failure(exitcode, log):
  """Raise BuildFailed with as much information about the failure as possible."""
  # Build exceptions look like this:
  # BuildFailedException: <<Build sanity checks failed:
  # This is a dummy error>>
  #   at BuildTiltBrush.DoBuild (BuildOptions options, BuildTarget target, System.String location, SdkMode vrSdk, Boolean isExperimental, System.String stamp) [0x0026a] in C:\src\tb\Assets\Editor\BuildTiltBrush.cs:430
  #   at BuildTiltBrush.CommandLine () [0x001de] in C:\src\tb\Assets\Editor\BuildTiltBrush.cs:259

  build_failed_pat = re.compile(
      r'''BuildFailedException:\ <<(?P<description>.*?)>>
      (?P<traceback> (\n\ \ at\ [^\n]+)* )''',
      re.DOTALL | re.MULTILINE | re.VERBOSE)
  m = build_failed_pat.search(log)
  if m is not None:
    raise BuildFailed("C# raised BuildFailedException\n%s\n| ---\n%s" % (
        indent('| ', m.group('traceback').strip()),
        indent('| ', m.group('description').strip())))

  internal_error_pat = re.compile(
    r'^executeMethod method (?P<methodname>.*) threw exception\.',
    re.MULTILINE)
  m = internal_error_pat.search(log)
  if m is not None:
    exception_pat = re.compile(r'^[A-Z][A-Za-z0-9]+(Exception|Error):', re.MULTILINE)
    start = search_backwards(log, m.start(0), m.start(0) - 1024, exception_pat)
    end = m.end(0)
    suspicious_portion = log[start:end]
    raise BuildFailed("""Build script '%s' had an internal error.
Suspect log portion:
%s""" % (m.group('methodname'), indent('| ', log[start:end])))

  # Check for BuildTiltBrush.Die()
  btb_die_pat = re.compile(
    r'_btb_ Abort <<(?P<description>.*?)>>', re.DOTALL | re.MULTILINE)
  m = btb_die_pat.search(log)
  if m is not None:
    raise BuildFailed("C# called Die %s '%s'" % (exitcode, m.group('description')))

  if exitcode is None:
    raise BuildFailed("Unity build seems to have been terminated prematurely")
  raise BuildFailed("""Unity build failed with exit code %s but no errors seen
This probably means the project is already open in Unity""" % exitcode)


def get_end_user_version(project_dir):
  fn = os.path.join(project_dir, 'Assets', 'Scenes', 'Main.unity')
  with open(fn) as inf:
    m = re.search('^  m_VersionNumber: (.*)', inf.read(), flags=re.M)
    if m:
      return m.group(1).strip()
  return '<noversion>'


def make_unused_directory_name(directory_name):
  dirname, filename = os.path.split(directory_name)
  for i in itertools.count(1):
    prospective_name = os.path.join(dirname, '%d_%s' % (i, filename))
    if not os.path.exists(prospective_name):
      return prospective_name


PLATFORM_TO_UNITYTARGET = {
  'Windows': 'StandaloneWindows64',
  'OSX':     'StandaloneOSXIntel',
  'Linux':   'StandaloneLinuxUniversal',
  'Android': 'Android',
  'iOS':     'iOS',
}
def build(stamp, output_dir, project_dir, exe_base_name,
          experimental, platform, il2cpp, vrsdk, config, for_distribution,
          is_jenkins):
  """Create a build of Tilt Brush.
  Pass:
    stamp - string describing the version+build; will be embedded into the build somehow.
    output_dir - desired output directory name
    project_dir - directory name
    project_name - name of the executable to create (sans extension)
    experimental - boolean
    platform - one of (Windows, OSX, Linux, Android, iOS)
    il2cpp - boolean
    vrsdk - Config.SdkMode; valid values are (Oculus, SteamVR, Monoscopic)
    config - one of (Debug, Release)
    for_distribution - boolean. Enables android signing, version code bump, removal of pdb files.
    is_jenkins - boolean; used to customize stdout logging
  Returns:
    the actual output directory used
  """
  def get_exe_suffix(platform):
    if 'Windows' in platform: return '.exe'
    if 'OSX' in platform: return '.app'
    if 'Linux' in platform: return ''
    if 'Android' in platform: return '.apk'
    if 'iOS' in platform: return ''
    raise InternalError("Don't know executable suffix for %s" % platform)

  try:
    unitybuild.utils.destroy(output_dir)
  except Exception as e:
    print 'WARN: could not use %s: %s' % (output_dir, e)
    output_dir = make_unused_directory_name(output_dir)
    print 'WARN: using %s intead' % output_dir
    unitybuild.utils.destroy(output_dir)
  os.makedirs(output_dir)
  logfile = os.path.join(output_dir, 'build_log.txt')

  exe_name = os.path.join(output_dir, exe_base_name + get_exe_suffix(platform))
  cmd_env = os.environ.copy()
  cmdline = [get_unity_exe(get_project_unity_version(project_dir),
                           lenient=is_jenkins),
             '-logFile', logfile,
             '-batchmode',
             # '-nographics',   Might be needed on OSX if running w/o window server?
             '-projectPath', project_dir,
             '-executeMethod', 'BuildTiltBrush.CommandLine',
               '-btb-target', PLATFORM_TO_UNITYTARGET[platform],
               '-btb-out', exe_name,
               '-btb-display', vrsdk]
  if experimental:
    cmdline.append('-btb-experimental')

  if il2cpp:
    cmdline.append('-btb-il2cpp')

  # list of tuples:
  # - the name of the credential in the environment (for Jenkins)
  # - the name of the credential in the keystore (for interactive use)
  required_credentials = []

  if for_distribution and platform == 'Android':
    if vrsdk != 'Oculus':
      raise BuildFailed('Signing is currently only implemented for Oculus Quest')
    keystore = os.path.abspath(os.path.join(project_dir, 'Support/Keystores/TiltBrush.keystore'))
    keystore = keystore.replace('/', '\\')
    if not os.path.exists(keystore):
      raise BuildFailed("To sign you need %s.\n" % keystore)

    cmdline.extend([
      '-btb-keystore-name', keystore,
      '-btb-keyalias-name', 'oculusquest',
    ])
    required_credentials.extend([
      ('BTB_KEYSTORE_PASS', 'Tilt Brush keystore password'),
      ('BTB_KEYALIAS_PASS', 'Tilt Brush Oculus Quest signing key password')])
  cmdline.extend(['-btb-stamp', stamp])

  if config == 'Debug':
    cmdline.extend([
      '-btb-bopt', 'Development',
      '-btb-bopt', 'AllowDebugging',
    ])

  cmdline.append('-quit')

  full_version = "%s-%s" % (get_end_user_version(project_dir), stamp)

  # Populate environment with secrets just before calling subprocess
  for (env_var, credential_name) in required_credentials:
    if env_var not in cmd_env:
      if is_jenkins:
        # TODO(pld): Look into Jenkins plugins to get at these credentials
        raise BuildFailed(
          'Credential "%s" is missing from Jenkins build environment' % env_var)
      else:
        from unitybuild.credentials import get_credential
        cmd_env[env_var] = get_credential(credential_name).get_secret().encode('ascii')
  proc = subprocess.Popen(cmdline, stdout=sys.stdout, stderr=sys.stderr, env=cmd_env)
  del cmd_env

  with unitybuild.utils.ensure_terminate(proc):
    with LogTailer(logfile, disabled=is_jenkins):
      with open(os.path.join(output_dir, 'build_stamp.txt'), 'w') as outf:
        outf.write(full_version)

      # Use wait() instead of communicate() because Windows can't
      # interrupt the thread joins that communicate() uses.
      proc.wait()

  with open(logfile) as inf:
    log = inf.read().replace('\r', '')

  check_compile_output(log)

  if proc.returncode != 0:
    analyze_unity_failure(proc.returncode, log)

  # sanity-checking since we've been seeing bad Oculus builds
  if platform == 'Windows':
    required_files = []
    for f in required_files:
      if not os.path.exists(os.path.join(output_dir, f)):
        raise BuildFailed("""Build is missing the file '%s'
This is a known Unity bug and the only thing to do is try the build
over and over again until it works""" % f)
  return output_dir


def finalize_build(project_dir, src_dir, dst_dir):
  """Attempts to move *src_dir* to *dst_dir*.
  Return *dst_dir* on success, or some other directory name if there was some problem.
  This should be as close to atomic as possible."""
  try:
    unitybuild.utils.destroy(dst_dir)
  except OSError as e:
    print 'WARN: Cannot remove %s; putting output in %s' % (dst_dir, src_dir)
    return src_dir

  try: os.makedirs(os.path.dirname(dst_dir))
  except OSError: pass

  try:
    os.rename(src_dir, dst_dir)
    return dst_dir
  except OSError as e:
    # TODO(pld): Try to do something better
    # On Jon's computer, Android builds always leave behind a Java.exe process that
    # holds onto the directory and prevents its rename.
    # raise InternalError("Can't rename %s to %s: %s" % (src_dir, dst_dir, e))
    print 'WARN: Cannot rename %s; leaving it as-is' % (src_dir,)
    return src_dir


def create_notice_file(project_dir):
  def iter_notice_files():
    """Yields (library_name, notice_file_name) tuples."""
    root = os.path.join(project_dir, 'Assets/ThirdParty')
    if not os.path.exists(root):
      raise BuildFailed("Cannot generate NOTICE: missing %s" % root)
    for r, ds, fs in os.walk(root):
      for f in fs:
        if f.lower() in ('notice', 'notice.txt', 'notice.tiltbrush', 'notice.md'):
          yield (os.path.basename(r), os.path.join(r, f))
    root = os.path.join(project_dir, 'Assets/ThirdParty/NuGet/Packages')
    if not os.path.exists(root):
      raise BuildFailed("Cannot generate NOTICE: missing %s" % root)
    for r, ds, fs in os.walk(root):
      for f in fs:
        if f.lower() in ('notice', 'notice.md', 'notice.txt'):
          m = re.match('\D+', os.path.basename(r))
          if m:
            name = m.group(0).rstrip('.')
            if (name[-2:] == '.v' or name[-2:] == '.V'):
              name = name[:-2]
            yield (name, os.path.join(r, f))

  import codecs
  import StringIO
  tmpf = StringIO.StringIO()
  tmpf.write('''This file is automatically generated.
This software makes use of third-party software with the following notices.
''')
  for (library_name, notice_file) in iter_notice_files():
    tmpf.write('\n\n=== %s ===\n' % library_name)
    with open(notice_file) as inf:
      contents = inf.read()
      if contents.startswith(codecs.BOM_UTF8):
        contents = contents[len(codecs.BOM_UTF8):]
    tmpf.write(contents)
    tmpf.write('\n')

  output_filename = os.path.join(project_dir,
                                 'Support/ThirdParty/GeneratedThirdPartyNotices.txt')
  with open(output_filename, 'w') as outf:
    outf.write(tmpf.getvalue())

# ----------------------------------------------------------------------
# Front-end
# ----------------------------------------------------------------------

def parse_args(args):
  import argparse
  parser = argparse.ArgumentParser(description="Make Tilt Brush builds")
  parser.add_argument('--vrsdk',
                      action='append', dest='vrsdks',
                      choices=['Monoscopic', 'Oculus', 'SteamVR'],
                      help='Can pass multiple times; defaults to SteamVR (or Oculus on Android))')
  parser.add_argument('--platform',
                      action='append', dest='platforms',
                      choices=['OSX', 'Windows', 'Android', 'iOS'],
                      help='Can pass multiple times; defaults to Windows')
  parser.add_argument('--config',
                      action='append', dest='configs',
                      choices=['Debug', 'Release'],
                      help='Can pass multiple times; defaults to Release. Controls the ability to profile, the ability to debug scripts, and generation of debug symbols.')
  parser.add_argument(
    '--experimental', action='store_true', default=False,
    help='Include experimental features in the build')
  parser.add_argument(
    '--il2cpp', action='store_true', default=False,
    help='Build using il2cpp as the runtime instead of Mono')
  parser.add_argument(
    '--for-distribution', dest='for_distribution', action='store_true', default=False,
    help='Implicitly set when the build is being pushed; use explicitly if you want a signed build but do not want to push it yet')

  # TODO(pld): update docs to talk about Oculus Home?
  grp = parser.add_argument_group('Pushing to Steam/Oculus')
  grp.add_argument('--push', action='store_true', help='Push to Steam/Oculus')
  grp.add_argument('--user', type=str, help='(optional) Steam user to authenticate as.')
  grp.add_argument('--branch', type=str, help='(optional) Steam branch or Oculus release channel.')

  grp = parser.add_argument_group('Continuous Integration')
  grp.add_argument('--jenkins', action='store_true', help='Build with continuous integration settings.')

  args = parser.parse_args(args)
  if not args.configs:
    args.configs = ['Release']

  if not args.platforms and not args.vrsdks:
    args.platforms = [os.getenv('TILT_BRUSH_BUILD_PLATFORM', 'Windows')]
    args.vrsdks = [os.getenv('TILT_BRUSH_BUILD_VRSDK', 'SteamVR')]
  elif not args.platforms:
    args.platforms = ['Windows']
  elif not args.vrsdks:
    if 'Android' in args.platforms:
      args.vrsdks = ['Oculus']
    else:
      args.vrsdks = ['SteamVR']

  if args.branch is not None:
    args.push = True

  if args.push:
    args.for_distribution = True

  return args


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


def iter_builds(args):
  """Yields (platform, vrsdk, config) tuples."""
  for platform in args.platforms:
    for vrsdk in args.vrsdks:
      for config in args.configs:
        yield (platform, vrsdk, config)


def get_android_version_code(project_dir):
  """Returns the integer AndroidBundleVersionCode, or raises LookupError."""
  filename = os.path.join(project_dir, 'ProjectSettings/ProjectSettings.asset')
  contents = open(filename, 'rb').read()
  m = re.search(r'(?<=AndroidBundleVersionCode: )(?P<code>\d+)', contents)
  if m is not None:
    try: return int(m.group('code'))
    except: pass
  raise LookupError('code')


def set_android_version_code(project_dir, code):
  """If *code* is 'increment', increments the existing code."""
  filename = os.path.join(project_dir, 'ProjectSettings/ProjectSettings.asset')
  contents = open(filename, 'rb').read()
  def replacement(match):
    if code == 'increment':
      return str(int(match.group('code')) + 1)
    else:
      return str(code)
  new_contents, n = re.subn(r'(?<=AndroidBundleVersionCode: )(?P<code>\d+)',
                            replacement, contents)
  if n < 1:
    print("WARNING: Failed to set AndroidBundleVersionCode")
  else:
    open(filename, 'wb').write(new_contents)


def maybe_prompt_and_set_version_code(project_dir):
  import webbrowser
  from unitybuild.credentials import TB_OCULUS_QUEST_APP_ID
  existing_code = get_android_version_code(project_dir)
  uri = 'https://dashboard.oculus.com/application/%s/build/' % TB_OCULUS_QUEST_APP_ID
  webbrowser.open(uri)
  print 'Currently building version code %s' % existing_code
  print 'Please enter the highest version code you see on this web page,'
  print 'or hit enter to skip.'
  highest_seen = raw_input('Code > ')
  if highest_seen.strip() == '': return
  highest_seen = int(highest_seen)
  if existing_code <= highest_seen:
    set_android_version_code(project_dir, highest_seen+1)
    print 'Now building version code %s' % get_android_version_code(project_dir)


def sanity_check_build(build_dir):
  # We've had issues with Unity dying(?) or exiting(?) before emitting an exe
  exes = []
  for pat in ('*.app', '*.exe', '*.apk'):
    exes.extend(glob.glob(os.path.join(build_dir, pat)))
  if len(exes) == 0:
    raise BuildFailed("Cannot find any executables in %s" % build_dir)


def main(args=None):
  unitybuild.utils.msys_control_c_workaround()

  if sys.platform == 'cygwin':
    raise UserError("Running under cygwin python is not supported.")
  args = parse_args(args)

  if args.push:
    num = len(args.platforms) * len(args.vrsdks) * len(args.configs)
    if num != 1:
      raise UserError('Must specify exactly one build to push')

  import unitybuild.vcs as vcs
  vcs = vcs.create()
  project_dir = find_project_dir()
  print "Project dir:", os.path.normpath(project_dir)

  if args.jenkins:
    # Jenkins does not allow building outside of the source tree.
    build_dir = os.path.normpath(os.path.join(project_dir, 'Builds'))
  else:
    # Local build setup.
    build_dir = os.path.normpath(os.path.join(project_dir, '..', 'Builds'))

  # TODO(pld): maybe faster to call CommandLine() multiple times in the same
  # Unity rather than to start up Unity multiple times. OTOH it requires faith
  # in Unity's stability.
  try:
    tmp_dir = None
    try:
      revision = vcs.get_build_stamp(project_dir)
    except LookupError as e:
      print 'WARN: no build stamp (%s). Continue?' % (e,)
      if not raw_input('(y/n) > ').strip().lower().startswith('y'):
        raise UserError('Aborting: no stamp')
      revision = 'nostamp'

    create_notice_file(project_dir)

    for (platform, vrsdk, config) in iter_builds(args):
      stamp = revision + ('-exp' if args.experimental else '')
      print "Building %s %s %s exp:%d signed:%d il2cpp:%d" % (
        platform, vrsdk, config, args.experimental, args.for_distribution, args.il2cpp)

      tags = [platform, vrsdk, config]
      if args.experimental:     tags.append('Exp')
      if args.for_distribution and platform != 'Windows': tags.append('Signed')
      if args.il2cpp:           tags.append('Il2cpp')
      dirname = '_'.join(tags)

      tmp_dir = os.path.join(build_dir, 'tmp_' + dirname)
      output_dir = os.path.join(build_dir, dirname)

      if args.for_distribution and platform == 'Android' and sys.stdin.isatty():
        try: maybe_prompt_and_set_version_code(project_dir)
        except Exception as e:
          print 'Error prompting for version code: %s' % e

      tmp_dir = build(stamp, tmp_dir, project_dir, EXE_BASE_NAME,
            experimental=args.experimental,
            platform=platform,
            il2cpp=args.il2cpp, vrsdk=vrsdk, config=config,
            for_distribution=args.for_distribution,
            is_jenkins=args.jenkins)
      output_dir = finalize_build(project_dir, tmp_dir, output_dir)
      sanity_check_build(output_dir)

      if args.for_distribution and platform == 'Android':
        set_android_version_code(project_dir, 'increment')

      if args.for_distribution and vrsdk == 'Oculus':
        # .pdb files violate VRC.PC.Security.3 and ovr-platform-utils rejects the submission
        to_remove = []
        for (r, ds, fs) in os.walk(output_dir):
          for f in fs:
            if f.endswith('.pdb'):
              to_remove.append(os.path.join(r, f))
        if to_remove:
          print 'Removing from submission:\n%s' % ('\n'.join(
            os.path.relpath(f, output_dir) for f in to_remove))
          map(os.unlink, to_remove)

      if platform == 'iOS':
        # TODO: for iOS, invoke xcode to create ipa.  E.g.:
        # $ cd tmp_dir/TiltBrush
        # $ xcodebuild -scheme Unity-iPhone archive -archivePath ARCHIVE_DIR
        # $ xcodebuild -exportArchive -exportFormat ipa -archivePath ARCHIVE_DIR -exportPath IPA
        print 'iOS build must be completed from Xcode (%s)' % (
          os.path.join(output_dir, EXE_BASE_NAME, 'Unity-iPhone.xcodeproj'))
        continue

      if args.push:
        with open(output_dir+'/build_stamp.txt') as inf:
          embedded_stamp = inf.read().strip()
        import getpass
        from platform import node as platform_node  # Don't overwrite 'platform' local var!
        description = '%s %s | %s@%s' % (
          config, embedded_stamp, getpass.getuser(), platform_node())
        if args.branch is not None:
          description += ' to %s' % args.branch

        if vrsdk == 'SteamVR':
          if platform not in ('Windows',):
            raise BuildFailed("Unsupported platform for push to Steam: %s" % platform)
          unitybuild.push.push_tilt_brush_to_steam(
            output_dir, description, args.user or 'tiltbrush_build', args.branch)
        elif vrsdk == 'Oculus':
          if platform not in ('Windows', 'Android'):
            raise BuildFailed("Unsupported platform for push to Oculus: %s" % platform)
          release_channel = args.branch
          if release_channel is None:
            release_channel = 'ALPHA'
            print("No release channel specified for Oculus: using %s" % release_channel)
          unitybuild.push.push_tilt_brush_to_oculus(output_dir, release_channel, description)
  except Error as e:
    print "\n%s: %s" % ('ERROR', e)
    if isinstance(e, BadVersionCode):
      set_android_version_code(project_dir, e.desired_version_code)
      print("\n\nVersion code has been auto-updated to %s.\nPlease retry your build." %
            e.desired_version_code)
    if tmp_dir:
      print "\nSee %s" % os.path.join(tmp_dir, 'build_log.txt')
    sys.exit(1)
  except KeyboardInterrupt:
    print "Aborted."
    sys.exit(2)


# Tests

def test_get_unity_exe():
  global iter_editors_and_versions
  def iter_editors_and_versions():
    return map(lambda s: ("Unity_%s.exe" % s, tuple(s.split('.'))), [
      "2017.1.2", "2017.1.3", "2017.4.3", "2017.4.10", "2017.4.9"])
  assert get_unity_exe(('2017', '4', '8'), True) == 'Unity_2017.4.10.exe'
  try:   get_unity_exe(('2017', '4', '8'), False)
  except BuildFailed as e: pass
  else: assert False  # must raise

def test_iter_editors():
  for tup in iter_editors_and_versions():
    print tup

if __name__ == '__main__':
  maybe_prompt_and_set_version_code(os.getcwd())
