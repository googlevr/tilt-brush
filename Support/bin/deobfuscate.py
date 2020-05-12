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

import itertools
import os
import re
import sys
import argparse

from collections import defaultdict
from subprocess import Popen, PIPE

# Common 11-letter words that shouldn't be interpreted as obfuscated symbols
COMMON_WORDS_11 = set(['initializer'])

class ObfuscationSection(object):
  def __init__(self, name):
    self.name = name
    self.sym_to_ob = {}
    self.ob_to_syms = {}  # obfuscation -> set(symbols)

  def add_entry(self, sym, ob):
    """Pass a symbol, and its obfuscation"""
    # f(symbol) -> obfuscation is a one-to-one function
    # f(obfuscation) -> symbol might be a one-to-many function

    if self.name == 'Parameters':
      # Symbol is something like
      # Anchor/Side Anchor::opposite(Anchor/Side) side
      # ArtworkMetadata StellaMetadataFetcher/<LoadExhibitMetadata>c__Iterator3C::<>m__81(Vr2dExhibitItem) o
      # Strip off the type to avoid confusing shorten()
      assert ' ' in sym
      sym = sym.rsplit(' ')[-1]

    # Returns True if new entry was added
    if sym in self.sym_to_ob:
      if ob == self.sym_to_ob[sym]:
        return False
      # The same symbol mapped to two different obfuscations? Should be impossible,
      # since the mapping is a hash.
      raise Exception("Unexpected: %s -> %s and %s" % (sym, ob, self.sym_to_ob[sym]))

    self.sym_to_ob[sym] = ob
    syms = self.ob_to_syms.setdefault(ob, set())
    if sym in syms:
      return False
    else:
      syms.add(sym)
      return True

  @staticmethod
  def shorten(symbol):
    """Extract the piece of |symbol| that the obfuscator uses for hashing."""
    # Symbol can be:
    #   Namespace.Namespace.SHORTSYM
    #   Some.Class.Name::SHORTSYM(blah, blah)
    #   TiltBrush.Future`1/SHORTSYM     (enum defined in a class?)
    # If the symbol is a parameter, the extraneous type information will already
    # have been stripped off by add_entry()
    short = symbol.rsplit('::', 1)[-1]
    short = short.split('(', 1)[0]
    short = short.rsplit('/', 1)[-1]
    short = short.rsplit('.', 1)[-1]  # Not QUITE sure about this, but let's see how it goes
    return short


class ObfuscationMap(object):
  def __init__(self):
    self.sections_by_name = {}
    # A dict that maps obfuscated symbol to a user-friendly, short symbol
    self.ob_to_syms = None

  def is_empty(self):
    return len(self.sections_by_name) == 0

  def load_from_file(self, filename):
    """Additively loads entries from the given file.
    Returns True on success."""
    if os.path.exists(filename):
      self._load_from_text(file(filename).read())
      return True
    return False

  def load_from_git_rev(self, git_object):
    """Additively load entries from the given git object (eg HEAD:Assets/obfuscation_map.txt)"""
    proc = Popen(['git', 'cat-file', '-p', git_object], stdout=PIPE, stderr=PIPE)
    stdout, stderr = proc.communicate()
    if proc.returncode != 0:
      print >>sys.stderr, "WARN: Couldn't load deobfuscation from '%s'\n%s" % (git_object, stderr)
      return
    n = self._load_from_text(stdout)
    if n > 0 and os.isatty(sys.stdout.fileno()):
      print "Added %d symbols from '%s'" % (n, git_object)

  def _load_from_text(self, text):
    """Returns number of symbols added to the map."""
    SEP_CHAR = u'\u21e8'
    num_added = 0
    if not isinstance(text, unicode):
      text = text.decode('utf-8')
    for line in text.split('\n'):
      if not line:
        pass
      elif line.startswith('#'):
        section = self._get_section(line[1:])
      else:
        sym, ob = line.split(SEP_CHAR)
        if section.add_entry(sym, ob):
          num_added += 1
    # Reset cache
    self.ob_to_syms = None
    return num_added

  def _get_section(self, name):
    try:
      return self.sections_by_name[name]
    except KeyError:
      ret = self.sections_by_name[name] = ObfuscationSection(name)
      return ret

  def _create_ob_to_syms(self):
    # Create a simple aggregation of the lookup table
    ob_to_syms = defaultdict(set)
    for (name, section) in sorted(self.sections_by_name.items()):
      for (ob, syms) in section.ob_to_syms.iteritems():
        ob_to_syms[ob] |= syms
    self.ob_to_syms = dict(ob_to_syms)

  def deobfuscate(self, text):
    import re
    if self.ob_to_syms is None:
      self._create_ob_to_syms()
    def lookup(match):
      ob = match.group(0)
      try:
        syms = self.ob_to_syms[ob]
      except KeyError:
        if ob in COMMON_WORDS_11:
          return ob
        return '<? %s ?>' % ob
      else:
        short_syms = set(map(ObfuscationSection.shorten, syms))
        if len(short_syms) == 1:
          return short_syms.pop()
        return '< ' + ' or '.join(sorted(syms)) + ' >'
    pat = re.compile(r'\b[a-z]{11}\b')
    return pat.sub(lookup, text)


def get_client_root():
  proc = Popen(['git', 'rev-parse', '--show-toplevel'], stdout=PIPE, stderr=PIPE)
  stdout, stderr = proc.communicate()
  assert proc.returncode == 0, "Couldn't determine git client root"
  return stdout.strip()


def format_nicely(txt, verbose):
  lines = txt.split('\n')
  del txt

  frame_pat = re.compile(r'( (?P<name>[A-Za-z0-9_:.+`<>\[\]]+) ?(?P<args>\([^)]*\)))$')

  def remove_instruction_pointer(line):
    ignore_pat = re.compile(r' \(at <[a-f0-9]+>:\d+\)$')
    ignore_pat2 = re.compile(r' \[0x[0-9a-f]+\] in <[a-f0-9]+>:\d+ *$')
    # Gets rid of non-useful stuff like "(at <hexhexhex>:0)" and
    # "[0x00016] in <d2957de1c3fd4781a43d89572183136c>:0"
    line = ignore_pat.sub('', line)
    line = ignore_pat2.sub('', line)
    return line
  lines = map(remove_instruction_pointer, lines)

  # Exception lines have a stack frame stuck onto them; move those frames to the next line.
  # Also clean up tabs and other junk that comes in when you copy/paste from the analytics table.
  def move_trailing_stack_frame(line):
    exception_pat = re.compile('^\t?(\d+\.\t)?(?P<exc>[A-Za-z0-9]+Exception:)')
    if line.startswith(' Rethrow as'):
      # Insert a newline before the stack frame
      line = line.replace(' Rethrow as', '\nRethrow as')
      return frame_pat.sub(r'\n\1', line)
    elif exception_pat.match(line):
      m = exception_pat.match(line)
      line = line[m.start('exc'):]
      # Insert a newline before the stack frame
      return frame_pat.sub(r'\n\1', line)
    else:
      return line
  lines = map(move_trailing_stack_frame, lines)

  if not verbose:
    def remove_arglist(line):
      m = frame_pat.match(line)
      if m is None:
        return line
      else:
        name = m.group('name')
        args = m.group('args')
        if len(name) + len(args) < 70:
          return ' %s%s' % (name, args)
        else:
          return ' %s(...)' % (name,)
    lines = [line for line in lines if line != " (wrapper remoting-invoke-with-check)"]
    lines = map(remove_arglist, lines)

    def demangle_coroutine(line):
      # TiltBrush.<RunInCompositor>d__38:MoveNext()
      # Google.Apis.Requests.ClientServiceRequest`1+<ExecuteUnparsedAsync>d__30[TResponse].MoveNext ()
      def repl(m): return '[co] %(class)s.%(coroutine)s' % m.groupdict()
      ret, n = re.subn(
        r'(?P<class>[a-zA-Z0-9_.`]+)[+.]<(?P<coroutine>[^>]+)>d_+\d+(?:\[[^\]]+\])?[:.]MoveNext', repl, line)
      if n == 0:
        # TiltBrush.DriveAccess+<<InitializeDriveLinkAsync>g__InitializeAsync|30_0>d.MoveNext ()
        ret, n = re.subn(
          r'(?P<class>[a-zA-Z0-9_.`]+)[+.]<(?P<coroutine><[^>]+>[^>]+)>d_*\d*[:.]MoveNext',
          repl, line)
      return ret
    lines = map(demangle_coroutine, lines)

    lines = elide_async_frames(lines)

    def demangle_lambda(line):
      # TiltBrush.SketchControlsScript+<>c.<ExportCoroutine>b__307_0()
      def repl(m): return '%(prefix)s.%(owner)s.[lambda %(id)s]' % m.groupdict()
      return re.sub(r'(?P<prefix>[a-zA-Z0-9_.]+)\+<>c\.<(?P<owner>[a-zA-Z0-9_]+)>b__(?P<id>[0-9_]+)', repl, line)
    lines = map(demangle_lambda, lines)

  return '\n'.join(lines)


def elide_async_frames(lines):
  def list_to_pat(lst):
    """Returns a pattern that matches any of the items in lst"""
    return '(?:' + '|'.join(map(re.escape, lst)) + ')'

  # Gets rid of uninteresting stack frames that have to do with the C# async machinery,
  # to show more clearly the frames that are awaiting each other.
  # Sometimes the stack dump has "  at " in it, sometimes not. I think it has to do with
  # whether you're running in editor or not?
  task_execute_pat = re.compile(r'^(?:  at )?' + list_to_pat([
    'System.Threading.Tasks.Task`1[TResult].InnerInvoke',
    'System.Threading.Tasks.Task.Execute'
  ]))
  task_await_pat = re.compile(r'^(?:  at )?' + list_to_pat([
    'System.Runtime.CompilerServices.TaskAwaiter.GetResult',
    'System.Runtime.CompilerServices.TaskAwaiter`1[TResult].GetResult',
    'System.Threading.Tasks.Task.Wait',
  ]))
  task_throw_pat = re.compile(r'^(?:  at )?' + list_to_pat([
    'System.Threading.Tasks.Task.ThrowIfExceptional',
    'System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw',
    'System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess',
    'System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification',
    'System.Runtime.CompilerServices.TaskAwaiter.ValidateEnd',
    'System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1+ConfiguredTaskAwaiter[TResult].GetResult',
    'System.Runtime.CompilerServices.AsyncMethodBuilderCore+<>c.<ThrowAsync>',
    'System.Runtime.CompilerServices.AsyncMethodBuilderCore.ThrowAsync',
    #'--- End of stack trace from previous location where exception was thrown ---',
  ]))
  def elide_frame(line):
    if task_execute_pat.match(line):
      return '<task exec>'
    if task_throw_pat.match(line):
      return '<task throw>'
    if task_await_pat.match(line):
      return '<task await>'
    return line

  txt = '\n'.join(map(elide_frame, lines))
  txt = re.sub(r'(<task exec>\n)*--- End of stack trace from previous location where exception was thrown ---\n(<task (throw|await)>\n)* ?', '  [await]', txt)
  return txt.split('\n')


def main():
  parser = argparse.ArgumentParser()
  parser.add_argument('-r', dest='releases', action='append', default=[],
                      help="Add symbols from specified release branch (eg 1.4, 5). " +
                      '(Shortcut for Tilt Brush release-N naming format.)')
  parser.add_argument('-m', '--map_file', dest='map_file', action='store',
                      default='Support/obfuscation_map.txt',
                      help='Path of obfuscation map relative to client root')
  parser.add_argument('-b', '--branch', dest='branches', action='append',
                      default=[], help='Add symbols from specified release branch')
  parser.add_argument('-v', '--verbose', action='store_true',
                      help='Do not elide any information')
  args = parser.parse_args()

  os.chdir(os.path.dirname(os.path.realpath(__file__)))
  map_file = os.path.join(get_client_root(), args.map_file)
  omap = ObfuscationMap()
  omap.load_from_file(map_file)
  # Assumes that the remote is called "origin", but that's typically the case
  args.releases = map(lambda s: 'origin/release/' + s, args.releases)
  for branch in itertools.chain(args.releases, args.branches):
    omap.load_from_git_rev('%s:%s' % (branch, args.map_file))
  sys.stdout.flush()

  if omap.is_empty():
    parser.error("No symbols loaded. Do you need to pass '--release' or '--branch'?")

  if os.isatty(sys.stdout.fileno()):
    print 'Paste text and hit Control-Z or Control-D'
  txt = sys.stdin.read().decode('ascii', 'ignore')
  txt = omap.deobfuscate(txt)
  txt = format_nicely(txt, args.verbose)
  print txt


if __name__ == '__main__':
  main()
