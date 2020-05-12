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

using UnityEditor;

namespace TiltBrush {
  public static class ForceReserializeAssets {

    // Ensures all assets in the project are read in, and reserialized in the most recent format.
    [MenuItem("Tilt/Force reserialization of all assets")]
    public static void ReserializeAll() {
      EditorUtility.DisplayProgressBar(
          "Reserializing Assets", "Please Wait while assets are reserialized.", 0.5f);
      try {
        AssetDatabase.ForceReserializeAssets();
      } finally {
        EditorUtility.ClearProgressBar();
      }
    }
  }
} // namespace TiltBrush
