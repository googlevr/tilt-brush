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

"""A quick-and-dirty, limited conversion from gltf1 to gltf2.

Not intended for production use.

This was written to help scope out the work required to convert
Tilt Brush gltf1 to gltf2."""

from __future__ import print_function

import collections
import json
import os
import re

# (Brush guid, gltf alphaMode)
PBR_BRUSH_DESCRIPTORS = [
  ('f86a096c-2f4f-4f9d-ae19-81b99f2944e0', 'OPAQUE'),
  ("19826f62-42ac-4a9e-8b77-4231fbd0cfbf", 'BLEND')
]

def convert_to_array_helper(dct, name_to_index, key):
  if key not in dct:
    return
  by_name = dct[key]
  by_index = []
  for name, value in by_name.items():
    assert name not in name_to_index, "Name %s already added as %s" % (name, name_to_index[name])

    if 'name' in value:
      assert value['name'] == name, "Mismatching names %s %s %s" % (key, name, value['name'])
    value['name'] = name

    index = len(by_index)
    by_index.append(value)
    name_to_index[name] = (index, key)
  dct[key] = by_index


def convert_to_index(container, key, name_to_index, required_object_type):
  name = container[key]
  try:
    index, object_type = name_to_index[name]
  except KeyError:
    raise LookupError("No %s named %s" % (required_object_type, name))
  assert object_type == required_object_type
  container[key] = index


def convert_to_indices(dct, key, name_to_index, required_object_type):
  lst = dct[key]
  assert type(lst) is list
  for i in range(len(lst)):
    convert_to_index(lst, i, name_to_index, required_object_type)


COMPONENT_SIZES = {
  5120: 1, # byte
  5121: 1, # unsigned byte
  5122: 2, # short
  5123: 2, # unsigned short
  5126: 4, # float
}
NUM_COMPONENTS = {
  'SCALAR': 1,
  'VEC2': 2,
  'VEC3': 3,
  'VEC4': 4
}
def pop_explicit_byte_stride(accessor):
  """Removes and returns a byteStride to move from the accessor to the bufferVies.
  Returns None to mean "bufferView should not define it".

  This is useful because gltf1 defines 0 to mean "tightly packed",
  but gltf2 says 0 is invalid."""
  stride = accessor.pop('byteStride', None)
  calculated_stride = COMPONENT_SIZES[accessor['componentType']] * NUM_COMPONENTS[accessor['type']]

  if (calculated_stride % 4) != 0:
    # The weirdo rules are:
    # - if bufferView is used by more than one accessor, it must set stride
    # - if not set, it means "tightly packed"
    # - Values must be multiple of 4, and > 0
    # Thus sometimes we have to use the implicit version
    return None
  elif stride is None:
    return None
  elif stride == 0:
    return calculated_stride
  else:
    # It would be surprising if the calculated stride differed from the tightly-packed stride,
    # at least for Tilt Brush files
    if calculated_stride != stride:
      print('WARN: strange stride %s vs %s for accessor %s' % (
        calculated_stride, stride, accessor['name']))
    return stride


def pop_non_gltf2_property(thing, property_name, gltf1_default):
  """Removes thing[property_name] to make *thing* gltf2-compliant.
  Asserts if the property value is anything other than the gltf1 default."""
  value = thing.pop(property_name, gltf1_default)
  assert value == gltf1_default, "Non-default value %s for property %s.%s" % (
    value, thing['name'], property_name)


def convert(filename):
  txt = open(filename).read()
  txt = re.sub('// [^\"\n]*\n', '\n', txt)

  gltf = json.loads(txt, object_pairs_hook=collections.OrderedDict)
  name_to_index = {}

  # Store the vertex shader URI for convenient access; it'll be removed again later down
  for mat in gltf['materials'].values():
    if 'technique' in mat:
      technique = gltf['techniques'][mat['technique']]
      program = gltf['programs'][technique['program']]
      shader = gltf['shaders'][program['vertexShader']]
      mat['_vs_uri'] = shader['uri']

  # Convert by-name lookups to by-index lookups
  for key in ('accessors', 'bufferViews', 'buffers', 'cameras', 'images', 'materials', 'meshes',
              'nodes', 'samplers', 'scenes', 'textures', 'shaders', 'programs', 'techniques'):
    convert_to_array_helper(gltf, name_to_index, key)

  # If there was a buffers['binary_glTF'], make sure it is now at buffers[0]
  # This is required by the binary gltf spec.
  try: assert name_to_index['binary_glTF'] == (0, 'buffers')
  except KeyError: pass

  # Don't need these things in gltf 2
  for key in ('shaders', 'programs', 'techniques'):
    if key in gltf:
      del gltf[key]

  gltf['asset']['version'] = '2.0'

  if 'scene' in gltf:
    convert_to_index(gltf, 'scene', name_to_index, 'scenes')

  if 'extensionsUsed' in gltf:
    lst = gltf['extensionsUsed']
    # This extension is obsolete
    lst[:] = [elt for elt in lst if elt != "KHR_binary_glTF"]
    if len(lst) == 0:
      del gltf['extensionsUsed']

  for accessor in gltf['accessors']:
    convert_to_index(accessor, 'bufferView', name_to_index, 'bufferViews')
    # Move byteStride from accessor to bufferView.
    buffer_view = gltf['bufferViews'][accessor['bufferView']]
    byte_stride = pop_explicit_byte_stride(accessor)
    assert buffer_view.get('byteStride', byte_stride) == byte_stride, \
      "byteStride conflict: %s vs %s" % (buffer_view.get('byteStride'), byte_stride)
    if byte_stride is not None:
      buffer_view['byteStride'] = byte_stride

  for thing in gltf['buffers']:
    pop_non_gltf2_property(thing, 'type', 'arraybuffer')

  for thing in gltf['bufferViews']:
    convert_to_index(thing, 'buffer', name_to_index, 'buffers')

  for material in gltf['materials']:
    material.pop('technique', None)
    vertex_shader_uri = material.pop('_vs_uri', '')

    # Convert to pbr material
    values = material.pop('values', {})
    if 'BaseColorFactor' in values:
      for (guid, alpha_mode) in PBR_BRUSH_DESCRIPTORS:
        if guid in vertex_shader_uri:
          material['alphaMode'] = alpha_mode

      material['pbrMetallicRoughness'] = {
        'baseColorFactor': values['BaseColorFactor'],
        'baseColorTexture': {
          'index': values['BaseColorTex'],
          'texCoord' : 0    # ???
        },
        'metallicFactor': values['MetallicFactor'],
        'roughnessFactor': values['RoughnessFactor']
      }
      convert_to_index(material['pbrMetallicRoughness']['baseColorTexture'], 'index',
                       name_to_index, 'textures')

  for mesh in gltf['meshes']:
    for primitive in mesh.get('primitives', []):
      attributes = primitive.get('attributes', {})
      for semantic, accessor in attributes.items():
        convert_to_index(attributes, semantic, name_to_index, 'accessors')
      convert_to_index(primitive, 'indices', name_to_index, 'accessors')
      convert_to_index(primitive, 'material', name_to_index, 'materials')
      # COLOR is not a valid semantic; COLOR_0 is
      if 'COLOR' in attributes:
        assert 'COLOR_0' not in attributes
        attributes['COLOR_0'] = attributes.pop('COLOR')

  for node in gltf['nodes']:
    # neither gltf 1 nor gltf 2 define a 'light' property on nodes.
    node.pop('light', None)

    # gltf1 allows multiple meshes per node; gltf2 does not
    meshes = node.pop('meshes', [])
    if len(meshes) == 0:
      pass
    elif len(meshes) == 1:
      node['mesh'] = meshes[0]
    else:
      assert False, "Unsupported: convert node with multiple meshes"

    if 'mesh' in node:
      convert_to_index(node, 'mesh', name_to_index, 'meshes')
    if 'children' in node:
      convert_to_indices(node, 'children', name_to_index, 'nodes')

  for scene in gltf['scenes']:
    convert_to_indices(scene, 'nodes', name_to_index, 'nodes')

  for texture in gltf['textures']:
    pop_non_gltf2_property(texture, 'format', 6408)
    pop_non_gltf2_property(texture, 'internalFormat', 6408)
    pop_non_gltf2_property(texture, 'target', 3553)
    pop_non_gltf2_property(texture, 'type', 5121)
    convert_to_index(texture, 'sampler', name_to_index, 'samplers')
    convert_to_index(texture, 'source', name_to_index, 'images')

  check_for_forbidden_values(gltf, set(name_to_index.keys()))

  return json.dumps(gltf, indent=2)


def check_for_forbidden_values(value, forbidden, primitives=set([int, long, float, str, unicode])):
  """Recursively check that value does not contain any values in forbidden."""
  if type(value) in (dict, collections.OrderedDict):
    for (k, v) in value.iteritems():
      # It's okay for the name to be in the forbidden list
      if k != 'name':
        check_for_forbidden_values(v, forbidden)
  elif type(value) is list:
    if len(value) > 0 and type(value[0]) in (int, float, long):
      # Don't bother
      return
    for elt in value:
      check_for_forbidden_values(elt, forbidden)
  elif type(value) in primitives:
    if value in forbidden:
      print('Found forbidden %s' % (value,), file=sys.stderr)
  else:
    assert False, "Cannot handle type %s" % (type(value), )


def write_if_different(filename, contents):
  try:
    old_contents = open(filename).read()
  except IOError:
    old_contents = None
  if old_contents != contents:
    open(filename, 'w').write(contents)
    print("Updated", filename)


def main(args):
  import argparse
  parser = argparse.ArgumentParser()
  parser.add_argument('files', nargs=1, help="File to convert")
  parser.add_argument('--stdout', action='store_true')
  args = parser.parse_args(args)

  for src in args.files:
    gltf2 = convert(src)
    if args.stdout:
      print(gltf2)
    else:
      dst = os.path.splitext(src)[0] + '.2.gltf'
      assert dst != src
      write_if_different(dst, gltf2)




if __name__ == '__main__':
  import sys
  main(sys.argv[1:])
