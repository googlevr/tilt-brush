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

"""Helpers for 3d printing."""

import os
import re
import sys
import math
import pprint
import shutil
import itertools
import subprocess
from collections import Counter

import numpy

try:
  from tiltbrush.tilt import Tilt
except ImportError:
  print "You need the Tilt Brush Toolkit (https://github.com/googlevr/tilt-brush-toolkit)"
  print "and then put its Python directory in your PYTHONPATH."
  sys.exit(1)

from tbdata.brush_lookup import BrushLookup

# Convert strokes for 3d printing.
#   True     Don't touch these strokes
#   False    Remove these strokes from the sketch
#   <name>   Replace the brush for these strokes
#   names can also be guids, which is useful when the name is ambiguous
BRUSH_REPLACEMENTS = [
  # Good brushes
  ('SquarePaper',   True),
  ('ThickGeometry', True),
  ('Wire',          True),
  # Brushes that should be replaced
  ('TaperedMarker', 'ThickGeometry'),
  ('OilPaint',      'ThickGeometry'),
  ('Ink',           'ThickGeometry'),
  ('Marker',        'ThickGeometry'),
  ('Paper',         'ThickGeometry'),
  ('FlatDeprecated','ThickGeometry'),
  # Questionable
  ('Highlighter',   'ThickGeometry'),
  ('Light',         'Wire'),

  # Remove particles
  ('Smoke',         None),
  ('Snow',          None),
  ('Embers',        None),
  ('Stars',         None),
  # Remove animated
  ('Fire',          None),
  # Remove shader-based
  ('Plasma',        None),
  ('Rainbow',       None),
  ('Streamers',     None),
]


# ----------------------------------------------------------------------
# Little utilities
# ----------------------------------------------------------------------

def msg(text):
  sys.stdout.write("%-79s\r" % text[:79])
  sys.stdout.flush()


def msgln(text):
  sys.stdout.write("%-79s\n" % text[:79])
  sys.stdout.flush()


def rgb8_to_hsl(rgb):
  """Takes a rgb8 tuple, returns a hsl tuple."""
  HUE_MAX = 6

  r = rgb[0] / 255.0
  g = rgb[1] / 255.0
  b = rgb[2] / 255.0

  cmin = min(r, g, b)
  cmax = max(r, g, b)
  delta = cmax - cmin
  h = 0
  s = 0
  l = (cmax + cmin)

  if delta != 0:
    if l < 0.5:
      s = delta / l
    else:
      s = delta / (2 - l)

    if r == cmax:
      h = (g - b) / delta
    elif g == cmax:
      h = 2 + (b - r) / delta
    elif b == cmax:
      h = 4 + (r - g) / delta

  return h, s, l


# ----------------------------------------------------------------------
# Brush conversion
# ----------------------------------------------------------------------

def get_replacements_by_guid(replacements_by_name):
  """Returns a lookup table that is by-guid rather than by-name."""
  brush_lookup = BrushLookup.get()

  def guid_or_name_to_guid(guid_or_name):
    if guid_or_name in brush_lookup.guid_to_name:
      return guid_or_name
    elif guid_or_name in brush_lookup.name_to_guids:
      return brush_lookup.get_unique_guid(guid_or_name)
    else:
      raise LookupError("Not a known brush or brush guid: %r" % guid_or_name)

  dct = {}
  for before, after in replacements_by_name:
    before_guid = guid_or_name_to_guid(before)
    if after is True:
      after_guid = before_guid
    elif after is None:
      after_guid = None
    else:
      after_guid = guid_or_name_to_guid(after)
    dct[before_guid] = after_guid
  return dct
    

def convert_brushes(tilt, replacements_by_name, show_removed=False):
  """Convert brushes to 3d-printable versions, or remove their strokes from the tilt."""
  replacements = get_replacements_by_guid(replacements_by_name)
  brush_lookup = BrushLookup.get()

  with tilt.mutable_metadata() as dct:
    index_to_guid = dct['BrushIndex']

    # First, show us what brushes the tilt file uses
    used_guids = Counter()
    for stroke in tilt.sketch.strokes:
      guid = index_to_guid[stroke.brush_idx]
      used_guids[guid] += 1
    print "Brushes used:"
    for guid, n in sorted(used_guids.items(), key=lambda p:-p[1]):
      print "  %5d %s" % (n, brush_lookup.guid_to_name.get(guid))
    sys.stdout.flush()
    del used_guids

    index_to_new_index = {}

    for i, guid in enumerate(index_to_guid):
      name = brush_lookup.guid_to_name.get(guid, guid)
      try:
        new_guid = replacements[guid]
      except KeyError:
        print "%d: Don't know what to do with brush %s" % (i, name)
        index_to_new_index[i] = i
      else:
        new_name = brush_lookup.guid_to_name.get(new_guid, new_guid)
        if new_guid is None:
          print "%d: Remove %s" % (i, name)
          index_to_new_index[i] = None
        else:
          if guid == new_guid:
            print "%d: Keep %s" % (i, name)
          elif name == new_name:
            print "%d: Replace %s/%s -> %s/%s" % (i, name, guid, new_name, new_guid)
          else:
            print "%d: Replace %s -> %s" % (i, name, new_name)
          try:
            new_idx = index_to_guid.index(new_guid)
          except ValueError:
            new_idx = len(index_to_guid)
            index_to_guid.append(new_guid)
          index_to_new_index[i] = new_idx

  brush_indices_to_remove = set(i for (i, new_i) in index_to_new_index.items() if new_i is None)

  if brush_indices_to_remove:
    old_len = len(tilt.sketch.strokes)
    if show_removed:
      # Render in magenta instead of removing
      for stroke in tilt.sketch.strokes:
        if stroke.brush_idx in brush_indices_to_remove:
          stroke.brush_color = (1, 0, 1, 1)
        else:
          stroke.brush_color = stroke.brush_color
    else:
      tilt.sketch.strokes[:] = filter(
        lambda s: s.brush_idx not in brush_indices_to_remove,
        tilt.sketch.strokes)
    new_len = len(tilt.sketch.strokes)
    print "Strokes %d -> %d" % (old_len, new_len)

  for stroke in tilt.sketch.strokes:
    new_idx = index_to_new_index[stroke.brush_idx]
    # Might be none if it's a removed brush
    if new_idx is not None:
      stroke.brush_idx = new_idx


# ----------------------------------------------------------------------
# Stroke simplification
# ----------------------------------------------------------------------

def calculate_pos_error(cp0, cp1, middle_cps):
  if len(middle_cps) == 0:
    return 0
  strip_length = cp1._dist - cp0._dist
  if strip_length <= 0:
    return 0

  max_pos_error = 0
  for i, cp in enumerate(middle_cps):
    t = (cp._dist - cp0._dist) / strip_length
    pos_interpolated = t * cp0._pos + (1-t) * cp1._pos
    pos_error = numpy.linalg.norm((pos_interpolated - cp._pos))
    if pos_error > max_pos_error:
      max_pos_error = pos_error
  
  return max_pos_error


def simplify_stroke(stroke, max_error):
  # Do greedy optimization of stroke.
  REQUIRED_END_CPS = 1  # or 2
  keep_cps = []
  toss_cps = []   # The current set of candidates to toss

  n = len(stroke.controlpoints)
  brush_size = stroke.brush_size
  for i, cp in enumerate(stroke.controlpoints):
    cp._pos = numpy.array(cp.position)
    if i == 0:
      cp._dist = 0
    else:
      prev_cp = stroke.controlpoints[i-1]
      cp._dist = prev_cp._dist + numpy.linalg.norm(prev_cp._pos - cp._pos)

    if REQUIRED_END_CPS <= i < n - REQUIRED_END_CPS:
      pos_error = calculate_pos_error(keep_cps[-1], cp, toss_cps)
      keep = (pos_error > max_error * stroke.brush_size)
      #print "  %3d: %s %f %f" % (i, keep, pos_error, stroke.brush_size * .2)
    else:
      keep = True
      #print "  %3d: True (End)" % i

    if keep:
      keep_cps.append(cp)
      toss_cps = []
    else:
      toss_cps.append(cp)

  stroke.controlpoints[:] = keep_cps


def reduce_control_points(tilt, max_error):
  # If debug_simplify, the resulting .tilt file shows both the old and the new
  before_cp = 0
  after_cp = 0

  msg("Simplify strokes")
  pct = 0
  n = len(tilt.sketch.strokes)
  for i, stroke in enumerate(tilt.sketch.strokes):
    new_pct = (i+1) * 100 / n
    if new_pct != pct:
      pct = new_pct
      removed_pct = (before_cp - after_cp) * 100 / (before_cp+1)
      msg("Simplify strokes: %3d%% %5d/%5d  Removed %3d%%" % (pct, i, n, removed_pct))

    before_cp += len(stroke.controlpoints)
    simplify_stroke(stroke, max_error)
    after_cp += len(stroke.controlpoints)
  msg("Simplify strokes: done")

  msgln("Control points: %5d -> %5d (%2d%%)" % (
    before_cp, after_cp, after_cp * 100 / before_cp))


# ----------------------------------------------------------------------
# Stray strokes
# ----------------------------------------------------------------------

def remove_stray_strokes(tilt, max_dist=0, replacement_brush_guid=None):
  """Show histograms of control point positions, to help with resizing."""
  import numpy as np
  from math import sqrt

  def iter_pos(tilt):
    first_cp = 0
    for stroke in tilt.sketch.strokes:
      stroke._first_cp = first_cp
      first_cp += len(stroke.controlpoints)
      for cp in stroke.controlpoints:
        yield cp.position

  positions = np.array(list(iter_pos(tilt)))

  if False:
    # Print out x/y/z histograms
    histograms = [np.histogram(positions[... , i], bins=30) for i in range(3)]
    for irow in xrange(len(histograms[0][0])+1):
      for axis, histogram in enumerate(histograms):
        try:
          print "%s %3d %6d   " % ('xyz'[axis], histogram[1][irow], histogram[0][irow]),
        except IndexError:
          print "%s %3d %6s   " % ('xyz'[axis], histogram[1][irow], ''),
      print

  if max_dist > 0:
    # Convert replacement guid -> replacement index
    if replacement_brush_guid is None:
      replacement_brush_index = None
    else:
      with tilt.mutable_metadata() as dct:
        try:
          replacement_brush_index = dct['BrushIndex'].index(replacement_brush_guid)
        except ValueError:
          dct['BrushIndex'].append(replacement_brush_guid)
          replacement_brush_index = dct['BrushIndex'].index(replacement_brush_guid)

    # Compute Mahalanobis distance and remove strokes that fall outside
    # https://en.wikipedia.org/wiki/Mahalanobis_distance
    mean = np.mean(positions, axis=0)
    cov = np.cov(positions, rowvar=False)
    invcov = np.linalg.inv(cov)
    
    def mahalanobis_distance(v):
      """Return distance of row vector"""
      cv = (v - mean)[np.newaxis]
      return sqrt(cv.dot(invcov).dot(cv.T)[0, 0])

    def out_of_bounds(stroke):
      i0 = stroke._first_cp
      i1 = i0 + len(stroke.controlpoints)
      dists = np.array(map(mahalanobis_distance, positions[i0 : i1]))
      return np.any(dists > max_dist)

    msg("Finding OOB strokes")
    # TODO: figure out how to use np.einsum() and remove all the python-level loops
    oob_strokes = [
      pair for pair in enumerate(tilt.sketch.strokes)
      if out_of_bounds(pair[1])
    ]
    msg("")

    if len(oob_strokes):
      if replacement_brush_index is not None:
        for i, stroke in oob_strokes:
          print "Replacing out-of-bounds stroke", i
          stroke.brush_idx = replacement_brush_index
          stroke.brush_color = (1,0,1,1)
      else:
        print "Removing %d strokes" % len(oob_strokes)
        remove_indices = set(pair[0] for pair in oob_strokes)
        tilt.sketch.strokes[:] = [
          stroke for i, stroke in enumerate(tilt.sketch.stroke)
          if i not in remove_indices
        ]


# ----------------------------------------------------------------------
# Color reduction
# ----------------------------------------------------------------------

def get_most_similar_factors(n):
  """Factorize n into two numbers. 
  Returns the best pair, in the sense that the numbers are the closest to each other."""
  i = int(n**0.5 + 0.5)
  while n % i != 0:
    i -= 1
  return i, n/i


def get_good_factors(n, max_aspect_ratio=None):
  """Factorize n into two integers that are closest to each other.
  If max_aspect_ratio is passed, search numbers >= n until
  a pair is found whose aspect ratio is <= max_aspect_ratio."""
  if max_aspect_ratio is None:
    return get_most_similar_factors(n)
  for i in itertools.count():
    a, b = get_most_similar_factors(n + i)
    if float(b)/a <= max_aspect_ratio:
      return a, b


def rgbaf_to_rgb8(rgbaf):
  """Convert [r, g, b, a] floats to (r, g, b) bytes."""
  return tuple(int(channel * 255) for channel in rgbaf[0:3])


def rgb8_to_rgbaf(rgb8):
  """Convert (r, g, b) bytes to [r, g, b, a] floats."""
  lst = [channel / 255.0 for channel in rgb8]
  lst.append(1.0)
  return lst


def tilt_colors_to_image(tilt, max_aspect_ratio=None, preserve_colors=()):
  """Returns a PIL.Image containing the colors used in the tilt.
  The image will have colors in roughly the same proportion as the
  control points in the tilt.

  preserve_colors is a list of rgb8 colors."""
  import numpy as np
  from PIL import Image
  assert max_aspect_ratio is None or max_aspect_ratio > 0

  preserve_colors = set(preserve_colors)

  def iter_rgb8_colors(tilt):
    for stroke in tilt.sketch.strokes:
      yield (rgbaf_to_rgb8(stroke.brush_color), len(stroke.controlpoints))

  def by_decreasing_usage(counter_pair):
    # Sort function for colors
    return -counter_pair[1]

  def by_color_similarity(counter_pair):
    # Sort function for colors
    rgb8, usage = counter_pair
    h, s, l = rgb8_to_hsl(rgb8)
    return (rgb8 in preserve_colors), l

  counter = Counter()
  for color, n in iter_rgb8_colors(tilt):
    counter[color] += n
  most_used_color, amt = max(counter.iteritems(), key=lambda pair: pair[1])

  for rgb8 in preserve_colors:
    if rgb8 not in counter:
      print "Ignoring: #%02x%02x%02x is not in the image" % rgb8
    else:
      counter[rgb8] += amt / 2

  # Find a "nice" width and height, possibly adjusting the number of texels
  num_texels = sum(counter.itervalues())
  width, height = get_good_factors(num_texels, max_aspect_ratio)
  if width * height != num_texels:
    counter[most_used_color] += width * height - num_texels
    assert counter[most_used_color] > 0
    num_texels = sum(counter.itervalues())
    assert width * height == num_texels
    
  # Expand the colors into a 1d array, then turn into an Image
  colors_array = np.zeros(shape=(num_texels, 3), dtype='uint8')
  i = 0
  # The sort used here only matters to humans when they look at the images
  colors_and_counts = sorted(counter.iteritems(), key=by_color_similarity)
  # colors_and_counts = sorted(counter.iteritems(), key=by_decreasing_usage)
  for (color, count) in colors_and_counts:
    colors_array[i:i+count] = color
    i += count
  colors_array.shape = (height, width, 3)
  return Image.fromarray(colors_array, mode='RGB')
  

def get_quantized_image_pillow(im, num_colors):
  MAXIMUM_COVERAGE = 1
  print "Falling back to old color quantization"
  return im.quantize(colors=num_colors, method=MAXIMUM_COVERAGE), 'pillow'

def get_quantized_image_pngquant(im, num_colors):
  from PIL import Image
  import subprocess
  # pngquant errors out if its best solution is below this "quality"
  QUALITY_MIN = 0               # never error out
  # pngquant stops using colors when "quality" goes above this.
  # I have no real feeling for what this number means in practice
  QUALITY_MAX = 40
  im.save('tmp_pngquant.png')
  try:
    subprocess.check_call([
      'pngquant',
      '--nofs',                   # no dithering
      '--force',
      '--quality', '%d-%d' % (QUALITY_MIN, QUALITY_MAX),
      '-o', 'tmp_pngquant_out.png',
      str(num_colors), '--',
      'tmp_pngquant.png'
    ])
    imq = Image.open('tmp_pngquant_out.png')
    imq.load()
  finally:
    if os.path.exists('tmp_pngquant.png'):
      os.unlink('tmp_pngquant.png')
    if os.path.exists('tmp_pngquant_out.png'):
      os.unlink('tmp_pngquant_out.png')
  return imq, 'pngquant'

def get_quantized_image(im, num_colors):
  try:
    return get_quantized_image_pngquant(im, num_colors)
  except subprocess.CalledProcessError as e:
    print "Error running pngquant: %s" % e
  except OSError as e:
    print "Missing pngquant: %s" % e
    print "Download pngquant.exe it and put it in your PATH."
  return get_quantized_image_pillow(im, num_colors)


def simplify_colors(tilt, num_colors, preserve_colors):
  im = tilt_colors_to_image(tilt, max_aspect_ratio=4, preserve_colors=preserve_colors)
  if num_colors < 0:
    # Little hack to force use of pillow
    imq, method = get_quantized_image_pillow(im, -num_colors)
  else:
    imq, method = get_quantized_image(im, num_colors)

  def iter_rgb8(im):
    return itertools.izip(im.getdata(0), im.getdata(1), im.getdata(2))

  def get_imq_color(ipixel, data=imq.getdata(), palette=imq.getpalette()):
    # Look up color in imq, which is awkward because it's palettized
    palette_entry = data[ipixel]
    r, g, b = palette[palette_entry * 3 : (palette_entry + 1) * 3]
    return (r, g, b)
  
  # Create table mapping unquantized rgb8 to quantized rgbaf
  old_to_new = {}
  idx = 0
  for (old_color, group) in itertools.groupby(iter_rgb8(im)):
    assert old_color not in old_to_new
    old_to_new[old_color] = rgb8_to_rgbaf(get_imq_color(idx))
    idx += len(list(group))

  for stroke in tilt.sketch.strokes:
    stroke.brush_color = old_to_new[rgbaf_to_rgb8(stroke.brush_color)]

  if True:
    import numpy as np
    for old8, newf in old_to_new.iteritems():
      oldv = np.array(rgb8_to_rgbaf(old8)[0:3])
      newv = np.array(newf[0:3])
      err = oldv - newv
      err = math.sqrt(np.dot(err, err))
      if err > .2:
        print "High color error: #%02x%02x%02x" % old8

    num_colors = len(set(map(tuple, old_to_new.values())))
    base, _ = os.path.splitext(tilt.filename)
    im.save('%s_%s.png' % (base, 'orig'))
    imq.save('%s_%s_%d.png' % (base, method, num_colors))
  

# ----------------------------------------------------------------------
# Split export into multiple .obj files
# ----------------------------------------------------------------------

def iter_aggregated_by_color(json_filename):
  """Yields TiltBrushMesh instances, each of a uniform color."""
  from tiltbrush.export import iter_meshes, TiltBrushMesh
  def by_color(m): return m.c[0]
  meshes = iter_meshes(json_filename)
  for (color, group) in itertools.groupby(sorted(meshes, key=by_color), key=by_color):
    yield TiltBrushMesh.from_meshes(group)


def write_simple_obj(mesh, outf_name):
  from cStringIO import StringIO
  tmpf = StringIO()

  for v in mesh.v:
    tmpf.write("v %f %f %f\n" % v)

  for (t1, t2, t3) in mesh.tri:
    t1 += 1; t2 += 1; t3 += 1
    tmpf.write("f %d %d %d\n" % (t1, t2, t3))

  with file(outf_name, 'wb') as outf:
    outf.write(tmpf.getvalue())


def split_json_into_obj(json_filename):
  import struct

  output_base = os.path.splitext(json_filename)[0].replace('_out', '')

  meshes = list(iter_aggregated_by_color(json_filename))
  meshes.sort(key=lambda m: len(m.v), reverse=True)
  for i, mesh in enumerate(meshes):
    # It's the "ignore normals" that does the most collapsing here.
    mesh.collapse_verts(ignore=('uv0', 'uv1', 'c', 't', 'n'))
    mesh.remove_degenerate()

    (r, g, b, a) = struct.unpack('4B', struct.pack('I', mesh.c[0]))
    assert a == 255, (r, g, b, a)
    hex_color = '%02x%02x%02x' % (r, g, b)
    outf_name = '%s %02d %s.obj' % (output_base, i, hex_color)
    write_simple_obj(mesh, outf_name)
    msgln("Wrote %s" % outf_name)


# ----------------------------------------------------------------------
# Main
# ----------------------------------------------------------------------

def process_tilt(filename, args):
  msg("Load tilt")
  tilt = Tilt(filename)
  msg("Load strokes")
  tilt.sketch.strokes
  msg("")

  if args.debug:
    msg("Clone strokes")
    before_strokes = [s.clone() for s in tilt.sketch.strokes]

  # Do this before color quantization, because it removes strokes (and their colors)
  if args.convert_brushes:
    convert_brushes(tilt, BRUSH_REPLACEMENTS)

  if args.remove_stray_strokes is not None:
    remove_stray_strokes(tilt, args.remove_stray_strokes,
                         BrushLookup.get().get_unique_guid('Wire'))

  if args.pos_error_tolerance > 0:
    reduce_control_points(tilt, args.pos_error_tolerance)

  if args.simplify_colors is not None:
    simplify_colors(tilt, num_colors=args.simplify_colors, preserve_colors=args.preserve_colors)

  if args.debug:
    final_strokes = []
    # interleave them so it renders semi-nicely...
    for before, after in itertools.izip_longest(before_strokes, tilt.sketch.strokes):
      if before is not None:
        for cp in before.controlpoints:
          cp.position[1] += 10
        final_strokes.append(before)
      if after is not None:
        final_strokes.append(after)
    tilt.sketch.strokes[:] = final_strokes

  tilt.write_sketch()
  msgln("Wrote %s" % os.path.basename(tilt.filename))


def main():
  import argparse
  parser = argparse.ArgumentParser(usage='''%(prog)s [ files ]

Process .tilt files to get them ready for 3D printing.

You should generally do the steps in this order:

1. Use --remove-stray-strokes (which actually just colors them magenta).
   Manually delete the strokes you don't want to keep.
2. Experiment with different values for --simplify-colors. Use
   --preserve-color option to force a color to remain present.
3. Use --convert-brushes and --pos-error-tolerance.
4. Load .tilt files in Tilt Brush, and export to .json
5. Convert from .json -> multiple .obj files
''')

  def hex_color(arg):
    arg = arg.lower()
    m = re.match(r'^#?([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$', arg)
    if m is not None:
      return tuple(int(m.group(i), 16) for i in (1, 2, 3))
    else:
      raise argparse.ArgumentTypeError("Must be exactly hex 6 digits: %r" % arg)

  parser.add_argument(
    '--debug', action='store_true',
    help='For debugging: put both the original and modified strokes in the resulting .tilt file')

  parser.add_argument(
    '--remove-stray-strokes', metavar='float', type=float, default=None,
    help="Replace strokes that are far away from the sketch with magenta wire. Argument is the number of standard deviations; 5.0 is a reasonable starting point.")

  parser.add_argument(
    '--simplify-colors', type=int, metavar='N',
    help='Simplify down to N colors. Use a negative number to try the alternate algorithm.')
  parser.add_argument(
    '--preserve-color', dest='preserve_colors', type=hex_color, action='append',
    default=[],
    help='Color to preserve, as a hex string like #ff00ff')

  parser.add_argument(
    '--convert-brushes', action='store_true',
    help='Convert brushes to 3d-printable ones')
  parser.add_argument(
    '--pos-error-tolerance', type=float, default=0,
    help='Allowable positional error when simplifying strokes, as a fraction of stroke width. If 0, do not simplify. .1 to .3 are good values. (default %(default)s)')

  parser.add_argument('-o', dest='output_file', help='Name of output file (optional)')
  parser.add_argument('files', type=str, nargs='+', help='File(s) to hack')

  args = parser.parse_args()

  for i, orig_filename in enumerate(args.files):
    if orig_filename.endswith('.tilt'):
      base, ext = os.path.splitext(orig_filename)
      if i == 0 and args.output_file is not None:
        working_filename = args.output_file
      else:
        working_filename = base + '_out' + ext
      shutil.copyfile(orig_filename, working_filename)
      process_tilt(working_filename, args)
    elif orig_filename.endswith('.json'):
      split_json_into_obj(orig_filename)
  

if __name__=='__main__':
  main()
