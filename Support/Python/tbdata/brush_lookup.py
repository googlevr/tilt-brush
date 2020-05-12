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
from collections import defaultdict

class BrushLookup(object):
  """Helper for doing name <-> guid conversions for brushes."""
  @staticmethod
  def iter_brush_guid_and_name(tilt_brush_dir):
    for brush_dir in ("Assets/Resources/Brushes", "Assets/Resources/X/Brushes"):
      for r, ds, fs in os.walk(os.path.join(tilt_brush_dir, brush_dir)):
        for f in fs:
          if f.lower().endswith('.asset'):
            fullf = os.path.join(r, f)
            data = file(fullf).read()
            guid = re.search('m_storage: (.*)$', data, re.M).group(1)
            name = re.search('m_Name: (.*)$', data, re.M).group(1)

            yield guid, f[:-6]

  _instances = {}

  @classmethod
  def get(cls, tilt_brush_dir=None):
    if tilt_brush_dir is None:
      tilt_brush_dir = os.path.normpath(os.path.join(os.path.abspath(__file__), "../../../.."))

    try:
      return cls._instances[tilt_brush_dir]
    except KeyError:
      val = cls._instances[tilt_brush_dir] = BrushLookup(tilt_brush_dir)
      return val

  def __init__(self, tilt_brush_dir):
    self.initialized = True
    self.guid_to_name = dict(self.iter_brush_guid_and_name(tilt_brush_dir))
    # Maps name -> list of guids
    self.name_to_guids = defaultdict(list)
    for guid, name in self.guid_to_name.iteritems():
      self.name_to_guids[name].append(guid)
    self.name_to_guids = dict(self.name_to_guids)

  def get_unique_guid(self, name):
    lst = self.name_to_guids[name]
    if len(lst) == 1:
      return lst[0]
    raise LookupError("%s refers to multiple brushes" % name)
