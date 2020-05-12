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

"""Non Unity-specific utility functions and classes."""

import os
import contextlib

from unitybuild.constants import InternalError


@contextlib.contextmanager
def ensure_terminate(proc):
  """Ensure that *proc* is dead upon exiting the block."""
  try:
    yield
  finally:
    try:
      # Windows raises WindowsError if the process is already dead.
      if proc.poll() is None:
        proc.terminate()
    except Exception as e:
      print "WARN: Could not kill process: %s" % (e,)


def destroy(file_or_dir):
  """Ensure that *file_or_dir* does not exist in the filesystem,
  deleting it if necessary."""
  import stat
  if os.path.isfile(file_or_dir):
    os.chmod(file_or_dir, stat.S_IWRITE)
    os.unlink(file_or_dir)
  elif os.path.isdir(file_or_dir):
    import shutil, stat
    for r,ds,fs in os.walk(file_or_dir, topdown=False):
      for f in fs:
        os.chmod(os.path.join(r, f), stat.S_IWRITE)
        os.unlink(os.path.join(r, f))
      for d in ds:
        os.rmdir(os.path.join(r, d))
    os.rmdir(file_or_dir)
  if os.path.exists(file_or_dir):
    raise InternalError("Temp build location '%s' is not empty" % file_or_dir)


def msys_control_c_workaround():
  """Turn off console Ctrl-c support and implement it ourselves."""
  # Used to work around a bug in msys where control-c kills the process
  # abruptly ~100ms after the process receives SIGINT. This prevents us
  # from running cleanup handlers, like the one that kills the Unity.exe
  # subprocess.
  if not os.getenv('MSYSTEM'):
    return

  import ctypes
  from ctypes.wintypes import HANDLE, DWORD, BOOL
  kernel32 = ctypes.windll.kernel32
  kernel32.GetStdHandle.restype = HANDLE
  kernel32.GetStdHandle.argtypes = (DWORD,)
  #kernel32.GetConsoleMode.restype = BOOL
  kernel32.GetConsoleMode.argtypes = (HANDLE, ctypes.POINTER(DWORD))
  #kernel32.SetConsoleMode.restype = BOOL
  kernel32.SetConsoleMode.argtypes = (HANDLE, DWORD)
  STD_INPUT_HANDLE = DWORD(-10)
  ENABLE_PROCESSED_INPUT = DWORD(1)

  stdin = kernel32.GetStdHandle(STD_INPUT_HANDLE)
  mode = DWORD()
  kernel32.GetConsoleMode(stdin, ctypes.byref(mode))
  mode.value = mode.value & ~(ENABLE_PROCESSED_INPUT.value)
  kernel32.SetConsoleMode(stdin, mode)

  # interrupt_main won't interrupt WaitForSingleObject, so monkey-patch
  import subprocess
  def polling_wait(self):
    from _subprocess import WaitForSingleObject, WAIT_OBJECT_0
    while WaitForSingleObject(self._handle, 3000) != WAIT_OBJECT_0:
      continue
    return self.poll()
  subprocess.Popen.wait = polling_wait

  import thread
  import threading
  def look_for_control_c():
    import msvcrt, thread
    while msvcrt.getch() != '\x03':
      continue
    thread.interrupt_main()
  t = threading.Thread(target=look_for_control_c)
  t.daemon = True
  t.start()


def get_file_version(filename):
  """Raises LookupError if file has no version.
  Returns (major, minor, micro)"""
  import platform
  if platform.system() == 'Windows':
    import win32api
    ffi = win32api.GetFileVersionInfo(filename, "\\")
    # I don't know the difference between ProductVersion and FileVersion
    def extract_16s(i32): return ((i32 >> 16) & 0xffff), i32 & 0xffff
    file_version = extract_16s(ffi['FileVersionMS']) + extract_16s(ffi['FileVersionLS'])
    return file_version[0:3]
  else:
    raise LookupError("Not supported yet on macOS")
    # Untested -- get it from the property list
    import json
    from subprocess import check_output
    plist_file = os.path.join(filename, 'Contents', 'Info.plist')
    plist_json = check_output(['plutil', '-convert', 'json', '-o', '-', '-s', '--', plist_file])
    plist = json.loads(plist_json)
    # XXX: need to parse this out but I don't know the format
    return plist['CFBundleShortVersionString']
