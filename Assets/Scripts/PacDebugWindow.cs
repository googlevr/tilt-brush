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

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEditor;

namespace TiltBrush {

public partial class PolyAssetCatalog {

  private class DebugWindow : EditorWindow {
    [MenuItem("Tilt/Poly Asset Catalog Debug Window")]
    private static void OpenDebugWindow() => GetWindow<DebugWindow>();

    private static string ToString(AssetGetter ag) {
      return $"{ag.Asset.Id} {(ag.IsCanceled ? 'c' : '-')} {(ag.IsReady ? 'r' : '-')}";
    }

    private static string ToString(ModelLoadRequest m) {
      return $"{m.AssetId} {m.Reason}";
    }

    private bool m_showActiveRequests;
    private bool m_showRequestQueue;
    private bool m_showLoadQueue;

    private void Update() {
      if (Application.isPlaying) {
        Repaint();
      }
    }

    private void OnGUI() {
      if (!Application.isPlaying) {
        EditorGUILayout.HelpBox("Only works in Play Mode.", MessageType.Info);
        return;
      }
      var pac = App.PolyAssetCatalog;

      DrawCollection(ref m_showActiveRequests, "Downloads", pac.m_ActiveRequests, ToString);
      DrawCollection(ref m_showRequestQueue, "RequestLoadQueue", pac.m_RequestLoadQueue, ToString);
      DrawCollection(ref m_showLoadQueue, "LoadQueue", pac.m_LoadQueue, ToString);
    }

    private void DrawCollection<T>(
        ref bool state, string label, ICollection<T> collection, Func<T, string> toString,
        bool sort=false) {
      string withCount = $"{label} : {collection.Count}";
      state = EditorGUILayout.ToggleLeft(withCount, state);
      if (!state || collection.Count == 0) { return; }
      int i = 0;
      if (sort) {
        List<string> strings = collection.Select(toString).ToList();
        strings.Sort();
        foreach (string elt in strings) {
          EditorGUILayout.LabelField(i.ToString(), elt);
          i += 1;
        }
      } else {
        foreach (T elt in collection) {
          EditorGUILayout.LabelField(i.ToString(), toString(elt));
          i += 1;
        }
      }
    }
  }  // class DebugWindow
}  // class PolyAssetCatalog

}  // namespace TiltBrush

#endif
