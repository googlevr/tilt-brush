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

from __future__ import print_function

import itertools
import json
import os
import struct


SIZES = {
  # accessor.type
  'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4,
  # accessor.componentType
  5120: 1, 5121: 1,  # BYTE, UBYTE
  5122: 2, 5123: 2,  # SHORT, USHORT
  5124: 4, 5125: 4,  # INT, UINT
  5126: 4            # FLOAT
}

# struct format characters, for accessor.componentType
STRUCT_FORMAT = {
  5120: 'b', 5121: 'B',  # BYTE, UBYTE
  5122: 'h', 5123: 'H',  # SHORT, USHORT
  5124: 'i', 5125: 'I',  # INT, UINT
  5126: 'f'              # FLOAT
}


# From itertools docs
def grouper(n, iterable, fillvalue=None):
  "grouper(3, 'ABCDEFG', 'x') --> ABC DEF Gxx"
  args = [iter(iterable)] * n
  return itertools.izip_longest(fillvalue=fillvalue, *args)


class binfile(object):
  # Helper for parsing
  def __init__(self, inf):
    self.inf = inf

  def read(self, n):
    data = self.inf.read(n)
    if len(data) < n:
      raise Exception("Short read %s < %s" % (len(data), n))
    return data

  def write(self, data):
    return self.inf.write(data)

  def read_length_prefixed(self):
    n, = self.unpack("<I")
    return self.read(n)

  def write_length_prefixed(self, data):
    self.pack("<I", len(data))
    self.inf.write(data)

  def unpack(self, fmt):
    n = struct.calcsize(fmt)
    data = self.read(n)
    return struct.unpack(fmt, data)

  def pack(self, fmt, *args):
    data = struct.pack(fmt, *args)
    return self.inf.write(data)


class BaseGltf(object):
  """Abstract subclass for classes that parse:
  - gltf+bin
  - glb version 1
  - glb version 2"""
  # Jeez
  PLURAL_SUFFIX = { 'mesh': 'es' }

  @staticmethod
  def create(filename):
    """Returns a Gltf, Glb1, or Glb2 instance."""
    bf = binfile(open(filename, 'rb'))
    first_bytes = bf.read(4)
    if first_bytes == 'glTF':
      version, = bf.unpack("I")
      if version == 1: return Glb1(filename)
      elif version == 2: return Glb2(filename)
      raise Exception("Bad version %d" % version)
    elif filename.lower().endswith('.gltf') or first_bytes.startswith("{"):
      return Gltf(filename)
    else:
      raise Exception("Unknown format")

  def __init__(self, filename):
    self.filename = filename
    # subclass will init version, json_chunk, json, and bin_chunk

  def dereference(self):
    """Converts (some) inter-object references from ints/strings to
    actual Python references. The Python reference will have a '_' appended.
    For example, accessor['bufferView_']."""
    def deref_property(obj, prop, dest_type=None):
      # Deref obj[prop]
      dest_type = dest_type or prop  # prop name is usually the obj type
      lookup_table_name = dest_type + 's'
      try: idx_or_name = obj[prop]
      except KeyError: pass
      else: obj[prop+'_'] = self.json[lookup_table_name][idx_or_name]

    def deref_all(source_type, prop, dest_type=None):
      # Deref obj[prop] for all objs of type source_type
      for name_or_idx, obj in self.iter_objs(source_type):
        deref_property(obj, prop, dest_type)

    deref_all('accessor', 'bufferView')
    deref_all('bufferView', 'buffer')
    for _, mesh in self.iter_objs('mesh'):
      for prim in mesh['primitives']:
        attrs = prim['attributes']
        for attr_name in attrs.keys():
          deref_property(attrs, attr_name, 'accessor')
        deref_property(prim, 'indices', 'accessor')
        deref_property(prim, 'material')

  def iter_objs(self, obj_type):
    """Yields (key, value) tuples.
    In gltf1 the keys are names; in gltf2 the keys are indices."""
    if self.version == 1:
      plural = self.PLURAL_SUFFIX.get(obj_type, 's')
      return self.json[obj_type + plural].items()
    elif self.version == 2:
      plural = self.PLURAL_SUFFIX.get(obj_type, 's')
      return enumerate(self.json[obj_type + plural])
    else:
      raise Exception("Unknown gltf version; cannot iterate objects")


  # backwards-compat
  def get_json(self): return self.json_chunk

  def get_mesh_by_name(self, name):
    if self.version == 1:
      return self.json['meshes'][name]
    else:
      for m in self.json['meshes']:
        if m['name'] == name: return m
      raise LookupError(name)

  def get_bufferView_data(self, buffer_view):
    """Returns a hunk of bytes."""
    start = buffer_view['byteOffset']
    end = start + buffer_view['byteLength']
    return self.bin_chunk[start:end]

  def get_accessor_data(self, accessor):
    """Returns accessor data, decoded according to accessor.componentType,
    and grouped according accessor.type."""
    componentType = accessor['componentType']
    start = accessor['byteOffset']
    count_per_element = SIZES[accessor['type']]  # eg 2 for VEC2
    # Parse a flat array of (int/float/whatever) and group it after, maybe
    flat_count = accessor['count'] * count_per_element
    byte_length = flat_count * SIZES[componentType]
    bufferview_data = self.get_bufferView_data(accessor['bufferView_'])
    attr_data = bufferview_data[start : start + byte_length]
    struct_format = '<' + str(flat_count) + STRUCT_FORMAT[componentType]
    flat = struct.unpack(struct_format, attr_data)
    if count_per_element == 1: return flat
    else: return list(grouper(count_per_element, flat))


class Gltf(BaseGltf):
  def __init__(self, filename):
    super(Gltf, self).__init__(filename)
    # Not fully general; just good enough to work for TB .gltf/bin pairs
    bin_name = os.path.splitext(filename)[0] + '.bin'
    if not os.path.exists(bin_name):
      raise Exception('No %s to go with %s' % (bin_name, filename))
    self.total_len = None  # Only meaningful for glb files
    self.json_chunk = open(filename, 'rb').read()
    self.bin_chunk = open(bin_name, 'rb').read()
    self.json = json.loads(self.json_chunk)
    version_str = self.json['asset'].get('version', "0")
    self.version = int(float(version_str))


class Glb1(BaseGltf):
  def __init__(self, filename):
    super(Glb1, self).__init__(filename)
    bf = binfile(open(self.filename, 'rb'))
    assert bf.read(4) == 'glTF'
    self.version, self.total_len, json_len, json_fmt = bf.unpack("<4I")
    assert self.version == 1 and json_len % 4 == 0 and json_fmt == 0
    self.json_chunk = bf.read(json_len)
    self.bin_chunk = bf.inf.read()
    self.json = json.loads(self.json_chunk)


class Glb2(BaseGltf):
  def __init__(self, filename):
    self.filename = filename
    bf = binfile(open(self.filename, 'rb'))
    assert bf.read(4) == 'glTF'
    self.version, self.total_len = bf.unpack("II")
    assert self.version == 2
    assert self.total_len == os.stat(self.filename).st_size
    self.json_chunk = self._read_chunk(bf, 'JSON')
    self.bin_chunk = self._read_chunk(bf, 'BIN\0')
    self.json = json.loads(self.json_chunk)

  def _read_chunk(self, bf, expect_tag):
    length, = bf.unpack("I")
    tag = bf.read(4)
    assert tag == expect_tag, tag
    data = bf.read(length)
    return data


#
# Testing
#

def load(version, name):
  ROOT = os.path.expanduser('~/Documents/Tilt Brush/Exports/Baseline 22.0b4')
  formatname = 'glb1' if (version == 1) else 'glb'
  return BaseGltf.create(os.path.join(ROOT, name, formatname, name+'.glb'))

def test(version):
  # It's CelVinyl texcoord 0 that has the NaNs
  glb = load(version, 'ET_All')
  glb.dereference()
  mesh = glb.get_mesh_by_name("mesh_CelVinyl_700f3aa8-9a7c-2384-8b8a-ea028905dd8c_0_i0")
  bad_accessor = mesh['primitives'][0]['attributes']['TEXCOORD_0_']
  print(glb.get_accessor_data(bad_accessor)[0:3])

if __name__ == '__main__':
  test(2)
