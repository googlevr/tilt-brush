import contextlib
import os
import shutil
import sys

from subprocess import *

DEFAULT_REPO_PATH = r'c:\src\github\AsyncAwaitUtil'


class UserError(Exception): pass


def git(*args):
  """Run git, returning first line of output."""
  args = list(args)
  args.insert(0, 'git')
  proc = Popen(args, stdout=PIPE, stderr=PIPE)
  stdout, stderr = proc.communicate()
  ret = proc.wait()
  if ret != 0:
    raise CalledProcessError(ret, stderr)
  return stdout.split('\n', 1)[0].strip()


def copy_file_if_different(s, d, no_overwrite=False):
  try:
    if file(s).read() == file(d).read():
      return
  except IOError as e:
    pass

  if os.path.exists(d) and no_overwrite:
    print 'not overwriting', os.path.normpath(d)
    return
  print 'copy', os.path.normpath(d)
  try: os.makedirs(os.path.dirname(d))
  except OSError: pass
  shutil.copyfile(s, d)


def import_asset(source_dir, dest_dir, relpath):
  """relpath may be a file or a directory
  The .meta file is copied
  If a directory, the import is recursive"""
  s = os.path.join(source_dir, relpath)
  d = os.path.join(dest_dir, relpath)
  assert os.path.exists(s)
  if not os.path.exists(d):
    copy_file_if_different(s+'.meta', d+'.meta', no_overwrite=True)
  if os.path.isdir(s):
    try: os.makedirs(d)
    except OSError: pass
    for fn in os.listdir(s):
      if not fn.endswith('.meta'):
        import_asset(source_dir, dest_dir, os.path.join(relpath, fn))
  else:
    copy_file_if_different(s, d)


@contextlib.contextmanager
def in_cwd(cwd):
  prev = os.getcwd()
  try:
    os.chdir(cwd)
    yield
  finally:
    os.chdir(prev)


def copy_AsyncAwaitUtil(async_await_repo, tb_repo):
  src = os.path.join(async_await_repo, 'UnityProject/Assets/Plugins/AsyncAwaitUtil')
  dst = os.path.join(tb_repo, 'Assets/ThirdParty/AsyncAwaitUtil')

  if not os.path.exists(src):
    raise UserError("%s does not exist", src)
  if not os.path.exists(dst):
    os.makedirs(dst)

  # We don't need 'Tests'
  # 'UniRx' is a separate project https://github.com/neuecc/UniRx.
  # It's not required by AsyncAwaitUtil, and we can import it separately should we want it.
  to_copy = [x for x in os.listdir(src)
             if not x.endswith('.meta') and x not in ('Tests', 'UniRx')]
  for asset in to_copy:
    import_asset(src, dst, asset)

  # Additional stuff that isn't in Assets and therefore has no .meta files
  for fn in ('ReadMe.md', 'License.md'):
    copy_file_if_different(os.path.join(async_await_repo, fn),
                           os.path.join(dst, fn))


def main():
  import argparse
  parser = argparse.ArgumentParser()

  parser.add_argument(
    '--repo',
    help='Path to a clone of https://github.com/modesttree/Unity3dAsyncAwaitUtil.git')
  if os.path.exists(DEFAULT_REPO_PATH):
    parser.set_defaults(repo=DEFAULT_REPO_PATH)
  args = parser.parse_args()

  tb_repo = os.path.normpath(os.path.join(git('rev-parse', '--git-dir'), '..'))
  copy_AsyncAwaitUtil(args.repo, tb_repo)


if __name__ == '__main__':
  main()
