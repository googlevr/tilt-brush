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

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TiltBrush {

[CreateAssetMenu(fileName="Manifest", menuName="Tilt Brush Manifest")]
public class TiltBrushManifest : ScriptableObject {
  public BrushDescriptor[] Brushes;
  public Environment[] Environments;
  public BrushDescriptor[] CompatibilityBrushes;

  // lhs = lhs + rhs, with duplicates removed.
  // The leading portion of the returned array will be == lhs.
  void AppendUnique<T>(ref T[] lhs, T[] rhs) where T : class {
    var refEquals = new ReferenceComparer<T>();
    lhs = lhs.Except(rhs, refEquals).Union(rhs, refEquals).ToArray();
  }

  /// Append the contents of rhs to this, eliminating duplicates
  public void AppendFrom(TiltBrushManifest rhs) {
    AppendUnique(ref Brushes, rhs.Brushes);
    AppendUnique(ref Environments, rhs.Environments);
    AppendUnique(ref CompatibilityBrushes, rhs.CompatibilityBrushes);
  }

  private IEnumerable<BrushDescriptor> AllBrushesAndAncestors() {
    foreach (var brush in Brushes.Concat(CompatibilityBrushes)) {
      for (var current = brush; current != null; current = current.m_Supersedes) {
        yield return current;
      }
    }
  }

  /// Returns a list of unique brushes in the given manifest,
  /// including any ancestor brushes implicitly present.
  public List<BrushDescriptor> UniqueBrushes() {
    return AllBrushesAndAncestors()
        .Distinct()
        .ToList();
  }

  [ContextMenu("Export Brush GUIDs")]
  public void ExportBrushGuids() {
#if UNITY_EDITOR
    string path =
        EditorUtility.SaveFilePanel("Save Brush Guids", Application.dataPath, "BrushGuids", ".txt");
    string[] guids =
        Brushes.Select(x => string.Format("{0},{1}",x.m_Guid.ToString(), x.DurableName)).ToArray();
    System.IO.File.WriteAllLines(path, guids);
#endif
  }
}  // TiltBrushManifest

}  // namespace TiltBrush