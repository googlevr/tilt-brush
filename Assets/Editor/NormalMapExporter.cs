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
using UnityEngine;

namespace TiltBrush {

static class NormalMapExporter {
  [MenuItem("Tilt/Export Normal Map...")]
  private static void ExportNormalMap() {
    var tex = Selection.activeObject as Texture2D;
    if (tex == null) {
      EditorUtility.DisplayDialog("No texture selected", "Please select a texture.",
                                  "Cancel");
      return;
    }

    // Force the texture to be readable and uncompressed so that we can access its pixels.
    var texPath = AssetDatabase.GetAssetPath(tex);
    TextureImporter texImport = (TextureImporter) AssetImporter.GetAtPath(texPath);
    if (!texImport.isReadable) {
      texImport.isReadable = true;
    }
    if (texImport.textureCompression != TextureImporterCompression.Uncompressed) {
      texImport.textureCompression = TextureImporterCompression.Uncompressed;
    }
    AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);

    // Bytes seem to come in as BGGR, so reorder them.
    Debug.Log($"formatC: {tex.format.ToString()}");
    Color32[] colors = tex.GetPixels32();
    for (int i = 0; i < colors.Length; i++) {
      byte r = colors[i].r;
      byte g = colors[i].g;
      // byte b = colors[i].b;
      byte a = colors[i].a;
      colors[i] = new Color32(a, g, r, 0);
    }
    tex.SetPixels32(colors);
    var bytes = tex.EncodeToPNG();

    var path = EditorUtility.SaveFilePanel("Save Texture", "", tex.name + "_normal.png", "png");
    if (path != "") {
      System.IO.File.WriteAllBytes(path, bytes);
      AssetDatabase.Refresh(); // In case it was saved to the Assets folder
    }
  }
}
}
