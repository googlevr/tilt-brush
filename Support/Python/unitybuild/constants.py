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

class Error(Exception):
  pass

class UserError(Error):
  pass

class BuildFailed(Error):
  pass

class BadVersionCode(BuildFailed):
  """The Oculus store had a build with a code >= the one we uploaded.
  self.desired_version_code is the lowest new version code that the store will accept."""
  def __init__(self, message, desired_version_code):
    super(BadVersionCode, self).__init__(message)
    self.desired_version_code = desired_version_code

class InternalError(Error):
  pass
