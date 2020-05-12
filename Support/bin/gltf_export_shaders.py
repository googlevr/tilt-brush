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


"""Per-brush shader generator from template shaders.

This script generates per-brush shaders based on exportManifest.json and a few
template shaders with simple parameter substitution. Includes conditional code
inclusion via #if and #else.

Inputs:
  Support/GlTFShaders/Generators/*.glsl
  Support/exportManifest.json.
Outputs:
  Support/TiltBrush.com/shaders/brushes/*.glsl
"""

import argparse
import json
import os
import platform
import re
import shutil
import stat
import sys


# Fill this out to help copy shaders from previous versions of brushes
UPDATED_GUIDS_BY_NAME = {
  # 'OilPaint': ('c515dad7-4393-4681-81ad-162ef052241b', 'f72ec0e7-a844-4e38-82e3-140c44772699'),
}

# ---------------------------------------------------------------------------
# Utilities
# ---------------------------------------------------------------------------

def destroy(file_or_dir):
  """Ensure that *file_or_dir* does not exist in the filesystem,
  deleting it if necessary."""
  import stat
  if os.path.isfile(file_or_dir):
    os.chmod(file_or_dir, stat.S_IWRITE)
    os.unlink(file_or_dir)
  elif os.path.isdir(file_or_dir):
    for r,ds,fs in os.walk(file_or_dir, topdown=False):
      for f in fs:
        os.chmod(os.path.join(r, f), stat.S_IWRITE)
        os.unlink(os.path.join(r, f))
      for d in ds:
        os.rmdir(os.path.join(r, d))
    os.rmdir(file_or_dir)
  if os.path.exists(file_or_dir):
    raise Exception("Temp build location '%s' is not empty" % file_or_dir)


class PreprocessException(Exception):
  """Exception raised by preprocess_lite() and preprocess()"""
  pass


def preprocess_lite(input_file, defines, include_dirs):
  """Returns contents of input_file with #includes expanded.
defines is a dict of #defines.
include_dirs is a list of directories.
Raises PreprocessException on error."""
  include_pat = re.compile(r'^[ \t]*#[ \t]*include[ \t]+([<"])(.*)[">].*$\n?', re.MULTILINE)
  def expand_include(include, current_file, is_quote):
    """Given the body of an #include, returns replacement text."""
    # https://gcc.gnu.org/onlinedocs/cpp/Include-Syntax.html
    if is_quote:
      search_path = [os.path.dirname(current_file)] + include_dirs
    else:
      search_path = include_dirs

    for include_dir in search_path:
      candidate = os.path.join(include_dir, include)
      if os.path.exists(candidate):
        with open(candidate, 'r') as inf:
          candidate_text = inf.read()
          if not candidate_text.endswith('\n'):
            candidate_text += '\n'
          # uncomment for debugging
          # candidate_text = '// %s\n%s' % (candidate, candidate_text)
        def expand_include_match(match):
          char, body = match.groups()
          return expand_include(body, candidate, char == '"')
        return include_pat.sub(expand_include_match, candidate_text)
    else:
      raise PreprocessException("%s : fatal error: Cannot open include file: '%s'" % (
        current_file, include))

  contents = expand_include(input_file, input_file, True)
  # inject defines
  defines = ["#define %s %s\n" % (k, v)
             for (k, v) in sorted(defines.items())
             if k in contents
            ]
  return ''.join(defines) + contents


# Currently unused
def preprocess(input_file, defines, include_dirs):
  """Returns C preprocessed contents of input_file.
defines is a dict of #defines.
include_dirs is a list of directories.
Raises PreprocessException on error."""
  assert not isinstance(include_dirs, basestring)
  if platform.system() == 'Windows':
    stdout = preprocess_msvc(input_file, defines, include_dirs)
  else:
    assert False, "Platform %s not (yet?) supported" % platform.system()
  return stdout


def preprocess_msvc(input_file, defines, include_dirs):
  # Windows-specific helper for preprocess()
  from subprocess import Popen, PIPE, CalledProcessError

  def find_cpp_exe():
    for release in ('12.0', '13.0', '14.0'):
      exe = r'C:\Program Files (x86)\Microsoft Visual Studio %s\VC\bin\cl.exe' % release
      if os.path.exists(exe):
        return exe
    raise LookupError("Cannot find %s: Install MSVC?" % exe)

  with_line_directives = False
  with_comments = True

  # See https://msdn.microsoft.com/en-us/library/19z1t1wy.aspx for
  # docs on command-line args
  cmd = [find_cpp_exe(), '/nologo']
  cmd.append('/X')  # Ignore standard include paths
  cmd.append('/we4668')  # Enable C4668, "'X' is not defined" warning
  for key, val in defines.iteritems():
    assert re.match(r'^[A-Z0-9_]+$', key)
    cmd.append('/D%s=%s' % (key, val))
  for directory_name in include_dirs:
    directory_name = directory_name.replace('/', '\\')
    assert os.path.exists(directory_name)
    cmd.append('/I')
    cmd.append(directory_name)
  if with_comments:
    cmd.append('/C')
  cmd.append(('/E' if with_line_directives else '/EP'))
  cmd.append(input_file)

  proc = Popen(cmd, stdout=PIPE, stderr=PIPE)
  stdout, stderr = proc.communicate()
  if proc.returncode != 0:
    raise PreprocessException("%s returned result code %d: %s" % (
      cmd, proc.returncode, stderr))
  return stdout.replace('\r\n', '\n')

# ---------------------------------------------------------------------------
# Generation
# ---------------------------------------------------------------------------

def get_defines(brush):
  """Returns a dict of cpp #defines for the specified brush."""
  float_params = brush['floatParams']
  defines = {}

  try: defines['TB_EMISSION_GAIN'] = str(float_params['EmissionGain'])
  except KeyError: pass

  try:
    defines['TB_ALPHA_CUTOFF'] = str(float_params['Cutoff'])
    defines['TB_HAS_ALPHA_CUTOFF'] = '1' if float_params['Cutoff'] < 1 else '0'
  except KeyError:
    defines['TB_HAS_ALPHA_CUTOFF'] = '0'
  return defines


class Generator(object):
  """Instantiate this class to run generate()."""
  def __init__(self, input_dir, include_dirs, brush_manifest_file):
    self.input_dir = input_dir
    self.include_dirs = include_dirs
    self.template_dir = include_dirs[0]
    assert os.path.exists(os.path.join(self.template_dir, "VertDefault.glsl"))
    self.output_shaders = set()
    self.brush_manifest_file = brush_manifest_file
    with open(self.brush_manifest_file) as inf:
      self.brush_manifest = json.load(inf)

  def get_handcrafted_shader(self, shader_name):
    """shader_name is the name of the destination file.
    Returns path to the handcrafted shader, which may not exist."""
    assert shader_name.endswith('.glsl')
    full_name = os.path.join(self.input_dir, os.path.basename(shader_name))
    return full_name

  def get_frag_template(self, brush):
    """Given a brush, returns the path to a fragment shader template."""
    # Figure out the template -- should probably be replaced with explicit #includes
    if int(brush["blendMode"]) == 2:
      # Additive blending.
      return "FragAdditive.glsl"
    elif "OutlineMax" in brush['floatParams']:
      # For now, this is the best available mapping.
      return "FragDiffuse.glsl"
    elif "Color" not in brush['colorParams']:
      # The absence of a Color field here indicates this should be an unlit
      # shader. Maybe there's a better test?
      return "FragUnlit.glsl"
    elif "Shininess" not in brush['floatParams']:
      return "FragDiffuse.glsl"
    else:
      # Unity Standard Diffuse + Specular.
      return "FragStandard.glsl"

  def generate(self, out_root):
    """Generate output for all brushes in the manifest."""
    brushes = self.brush_manifest["brushes"]
    for guid, brush in brushes.iteritems():
      self.generate_brush(brush, out_root)

  def copy_from_prev_brush(self, brush, out_dir):
    """Copies vert and frag shaders from brush's predecessor, if possible."""
    try:
      old_guid, new_guid = UPDATED_GUIDS_BY_NAME[brush["name"]]
    except KeyError:
      return
    if brush['guid'] == old_guid:
      return
    old_brush = self.brush_manifest['brushes'][old_guid]
    new_brush = self.brush_manifest['brushes'][new_guid]

    def maybe_copy(shader_type):
      old_hc = self.get_handcrafted_shader(os.path.join(out_dir, old_brush[shader_type]))
      new_hc = self.get_handcrafted_shader(os.path.join(out_dir, new_brush[shader_type]))
      if os.path.exists(old_hc) and not os.path.exists(new_hc):
        txt = file(old_hc).read()
        txt = "// Auto-copied from %s\n%s" % (os.path.basename(old_hc), txt)
        file(new_hc, 'w').write(txt)
        print 'copy %s -> %s' % (os.path.basename(old_hc), os.path.basename(new_hc))

    maybe_copy('vertexShader')
    maybe_copy('fragmentShader')

  def generate_brush(self, brush, out_root):
    """Generate output for a single brush in the manifest.
    Pass the manifest entry."""
    name = brush["name"]
    version = brush["shaderVersion"]
    guid = brush["guid"]
    out_dir = os.path.join(out_root, brush['folderName'])

    float_params = brush["floatParams"]
    color_params = brush["colorParams"]

    defines = get_defines(brush)

    # Vertex shader

    vert_output = os.path.join(out_dir, brush['vertexShader'])
    vert_input = self.get_handcrafted_shader(vert_output)
    if not os.path.exists(vert_input):
      self.copy_from_prev_brush(brush, out_dir)
    if not os.path.exists(vert_input):
      print "Auto-creating %s" % os.path.basename(vert_input)
      file(vert_input, 'w').write('#include "VertDefault.glsl"\n')
    self.preprocess(vert_input, vert_output, defines, self.include_dirs)

    # Fragment shader

    frag_output = os.path.join(out_dir, brush['fragmentShader'])
    frag_input = self.get_handcrafted_shader(frag_output)
    if not os.path.exists(frag_input):
      print "Auto-creating %s" % os.path.basename(frag_input)
      file(frag_input, 'w').write('#include "%s"\n' % self.get_frag_template(brush))
    self.preprocess(frag_input, frag_output, defines, self.include_dirs)

  def preprocess(self, input_file, output_file, defines, include_dirs):
    """Wrapper around global preprocess that does some massaging of
    the input and output."""
    output_data = preprocess_lite(input_file, defines, include_dirs)
    try:
      os.makedirs(os.path.dirname(output_file))
    except OSError:
      pass
    with file(output_file, 'w') as outf:
      outf.write(output_data)


def finalize_dir(tmp_dir, out_dir):
  """Move files from tmp_dir to out_dir.
  Print output for changed, new, or removed files.
  Avoids touching timestamp if file not changed."""
  # Could handle this case, but it's unexpepcted
  assert not os.path.isfile(out_dir), "Unexpected: %s is a file" % out_dir
  try: os.makedirs(out_dir)
  except OSError: pass

  tmp_files = set(os.listdir(tmp_dir))
  out_files = set(os.listdir(out_dir))

  new_files = tmp_files - out_files
  orphan_files = out_files - tmp_files

  for filename in tmp_files:
    tmp_file = os.path.join(tmp_dir, filename)
    out_file = os.path.join(out_dir, filename)
    if os.path.isdir(tmp_file):
      finalize_dir(tmp_file, out_file)
    elif os.path.isdir(out_file):
      assert False, "Unexpected: %s is a dir" % out_file
    elif filename not in out_files:
      shutil.copyfile(tmp_file, out_file)
      print '+', out_file
    elif file(tmp_file,'rb').read() != file(out_file,'rb').read():
      shutil.copyfile(tmp_file, out_file)
      print '~', out_file
    else:
      # identical; ignore
      pass

  # Cannot remove unwanted files (yet); output directory contains input files also
  if False:
    for filename in out_files - tmp_files:
      out_file = os.path.join(out_dir, filename)
      if not os.path.isfile(out_file):
        continue
      print '-', out_file
      destroy(out_file)


def main():
  parser = argparse.ArgumentParser()
  parser.add_argument('brush_manifest', nargs='?', default=None,
                      help='Path to exportManifest.json (optional)')
  parser.add_argument('export_root', nargs='?', default=None,
                      help='Output root directory (optional)')
  args = parser.parse_args()

  project_root = os.path.normpath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), '../..'))
  if args.brush_manifest is None:
    args.brush_manifest = os.path.join(project_root, 'Support/exportManifest.json')
  if args.export_root is None:
    args.export_root = os.path.join(project_root, 'Support/TiltBrush.com/shaders/brushes')

  tmp_dir = os.path.join(project_root, 'Temp/tmp_gltf')
  input_dir = os.path.join(project_root, 'Support/GlTFShaders/Generators')
  include_dirs = [os.path.join(project_root, 'Support/GlTFShaders/include')]

  gen = Generator(input_dir, include_dirs, args.brush_manifest)
  destroy(tmp_dir)
  gen.generate(tmp_dir)
  print "Writing to %s" % os.path.normpath(args.export_root)
  finalize_dir(tmp_dir, args.export_root)
  destroy(tmp_dir)


if __name__ == '__main__':
  main()
