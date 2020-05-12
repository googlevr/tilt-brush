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
import subprocess
from subprocess import Popen, PIPE, STDOUT, CalledProcessError

from unitybuild.constants import UserError


def _plural(noun, num):
  if num == 1: return '%d %s' % (num, noun)
  return '%d %ss' % (num, noun)


def git(cmd, cwd=None):
  """Runs git, returns stdout.
Raises CalledProcessError if process cannot be started, or exits with an error."""
  if cwd is None:
    cwd = os.getcwd()
  if type(cmd) is str:
    cmd = ['git'] + cmd.split()
  else:
    cmd = ['git'] + list(cmd)

  try:
    proc = Popen(cmd, cwd=cwd, stdout=PIPE, stderr=PIPE)
  except OSError as e:
    raise CalledProcessError(1, cmd, str(e))

  (stdout, stderr) = proc.communicate()
  if proc.wait() != 0:
    raise CalledProcessError(proc.wait(), cmd, "In %s:\nstderr: %s\nstdout: %s" % (
      cwd, stderr, stdout))
  return stdout


def create():
  """Returns a VCS instance."""
  warnings = []
  in_git = True
  try:
    git('status')
  except CalledProcessError as e1:
    return NullVcs()
  else:
    return GitVcs();


class VcsBase(object):
  # Pretty much just here to define API
  def __init__(self):
    """Raises UserError if the appropriate VCS is not detected."""
  def get_build_stamp(self, input_directory):
    """Returns a build stamp representing the build inputs.
    Raises LookupError if this is not possible.
    Build stamp is currently a p4 changelist number, eg '@1234'"""
    raise NotImplementedError()


class NullVcs(VcsBase):
  """VCS implementation that does nothing"""
  def get_build_stamp(self, input_directory):
    raise LookupError("Not using version control")
  

class GitVcs(VcsBase):
  """VCS implementation that uses git (without p4)"""
  def __init__(self):
    try:
      s = git('status')
    except CalledProcessError:
      raise UserError("Not in a git client")

  def _get_local_branch(self):
    ref = git('rev-parse --symbolic-full-name HEAD')
    m = re.match('refs/heads/(.*)', ref)
    if m is None:
      raise LookupError("Not on a branch")
    return m.group(1)

  def _get_gob_branch(self):
    """Returns the name of the branch on GoB that the current branch is tracking,
    as well as the local name of the tracking branch.
    eg, ("master", "refs/remotes/origin/master")
    Raises LookupError on failure, eg if not on a branch, or remote is not GoB."""
    branch = self._get_local_branch()
    try: remote = git('config branch.%s.remote' % branch).strip()
    except CalledProcessError: remote = ''
    if remote == '':
      raise LookupError("Can't determine GoB branch: no remote")
    remote_url = git('config remote.%s.url' % remote).strip()
    # if ('Prod/TiltBrush' not in remote_url) and ('tiltbrush/launch_trailer' not in remote_url):
    #   raise LookupError("Can't determine GoB branch: remote is not GoB")
    remote_branch = git('config branch.%s.merge' % branch).strip()
    m = re.match('refs/heads/(.*)', remote_branch)
    if m is None:
      raise LookupError("Can't determine GoB name: %s looks funny" % remote_branch)

    tracking = git('rev-parse --symbolic-full-name @{u}').strip()
    assert tracking != ''
    return m.group(1), tracking

  def get_build_stamp(self, input_directory):
    """Stamp is of the form:
      <sha>
      <sha>+<local changes>
    <sha> is a sha of the lastest GoB commit included in the current build.
    <local changes> is a tiny description of any changes in the build that aren't on GoB."""
    try:
      status = git('status --porcelain', cwd=input_directory)
    except CalledProcessError as e:
      print 'UNEXPECTED: %s\n%s' % (e, e.output)
      print 'In:', os.getcwd()
      assert False
    for match in re.finditer(r'^(.[MADR]|[MADR].) (.*)', status, re.MULTILINE):
      # Ignore changes in build script files
      filename = match.group(2)
      if re.match(r'Support/(.*\.py|obfuscation_map\.txt)$', filename):
        continue
      # For practicality, ignore changes to ProjectSettings too; allows Jon to re-build
      # with a corrected AndroidBundleVersionCode without requiring him to commit+push+fetch
      # TODO(pld): We may want to remove this once our process settles down, or tighten this so
      # we only ignore changes to AndroidBundleVersionCode.
      if filename == 'ProjectSettings/ProjectSettings.asset':
        continue
      raise LookupError('repo has modified files (%s)' % filename)

    tracked_name, tracked_ref = self._get_gob_branch()
    base = git('merge-base %s HEAD' % tracked_ref).strip()
    if base == '':
      raise LookupError('No common ancestor with %s' % tracked_ref)
    base = git('rev-parse --short %s' % base).strip()
    # It's verbose and redundant (with our human-made version number) to put the
    # gob branch name in the stamp. The sha is all we really need.
    # gob_name = '%s-%s' % (tracked_name.replace('-', ''), base)
    gob_name = base

    ahead_commits = git(['log', '--pretty=tformat:%h %s', '%s..HEAD' % base]).split('\n')[:-1]
    behind_commits = git(['log', '--pretty=tformat:%h %s', 'HEAD..%s' % tracked_ref]).split('\n')[:-1]
    if len(ahead_commits) == 0:
      if len(behind_commits) > 0:
        # Still allow the build without a custom stamp, but warn that it's not head
        print "HEAD is %s behind of %s:" % (_plural('commit', len(behind_commits)), tracked_ref)
        for c in behind_commits[::-1][:10]:
          print ' ',c
      return gob_name
    else:
      if len(ahead_commits) > 0:
        print "HEAD is %s ahead of %s:" % (_plural('commit', len(ahead_commits)), tracked_ref)
        for c in ahead_commits[:10]:
          print ' ',c
      if len(behind_commits) > 0:
        print "HEAD is %s behind of %s:" % (_plural('commit', len(behind_commits)), tracked_ref)
        for c in behind_commits[::-1][:10]:
          print ' ',c
      print "\nEnter a suffix to uniquify the build stamp, or empty string to abort"
      suffix = raw_input("> ")
      if not suffix.strip():
        raise LookupError("Not at the official GoB commit")
      return gob_name + '+' + suffix

#
# Testing
#

def test():
  try:
    print create().get_build_stamp(os.getcwd())
  except LookupError as e:
    print 'No stamp (%s)' % e

if __name__=='__main__':
  test()
