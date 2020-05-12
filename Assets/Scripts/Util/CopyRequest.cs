// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.IO;
using UnityEngine;

namespace TiltBrush {

// This can't be defined in an Editor/ directory because it needs to be accessible by
// BrushDescriptor. It's still only used at build time.
#if UNITY_EDITOR
public struct CopyRequest {
  public string source;
  public string dest;
  public bool omitForAndroid;

  public CopyRequest(string s) : this(s, s) {}

  public CopyRequest(string s, string d) {
    if (Path.IsPathRooted(d)) {
      Debug.LogWarning(
          $"CopyRequest {s} -> {d} is invalid. Destinations must be relative");
    }
    source = s;
    dest = d;
    omitForAndroid = false;
  }
}
#endif

} // namespace TiltBrush
