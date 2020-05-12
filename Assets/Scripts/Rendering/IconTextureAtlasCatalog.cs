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
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TiltBrush {
  /// Stores the details of a texture atlas so that it can be generated offline.
  [CreateAssetMenu(fileName = "AtlasCatalog", menuName = "Atlas Catalog", order = 1)]
  public class IconTextureAtlasCatalog : ScriptableObject {

    [System.Serializable]
    public class TextureEntry {
      public Texture2D m_texture;
      public Rect m_rect;
    }

    [SerializeField] private TiltBrushManifest[] m_brushManifests;
    [SerializeField] private Texture2D m_Atlas;
#pragma warning disable 414
    [SerializeField] private int m_AtlasSize = 2048;
    [Tooltip("Set <= 0 to disable.")]
    [SerializeField] private int m_VerifyTextureSize = 128;
#pragma warning restore 414
    [SerializeField] private string[] m_paths;
    [SerializeField] private TextureEntry[] m_entries;
    [SerializeField] private int m_padding = 8;

    public Texture2D Atlas { get { return m_Atlas; } }
    public int Padding { get { return m_padding; } }
    public int Length { get { return m_entries.Length; } }

    public TextureEntry this[int key] {
      get { return m_entries[key]; }
    }

#if UNITY_EDITOR
    static IEnumerable<T> IterAssets<T>(string query=null, string[] folders=null)
        where T : UnityEngine.Object {
      if (query == null) {
        query = "t:" + typeof(T).Name;
      }
      string[] guids;
      if (folders == null) {
        guids = AssetDatabase.FindAssets(query);
      } else {
        guids = AssetDatabase.FindAssets(query, folders);
      }

      return guids.Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)));
    }

    static IEnumerable<Texture2D> IterTextures(TiltBrushManifest[] manifests) {
      foreach (var manifest in manifests) {
        foreach (var brush in manifest.Brushes.Concat(manifest.CompatibilityBrushes)) {
          var tex = brush.m_ButtonTexture;
          if (tex == null) { continue; }
          yield return brush.m_ButtonTexture;
        }
      }
    }

    static bool TextureIsReadable(Texture2D texture) {
      if (texture == null) { return false; }
      try {
        texture.GetPixel(0, 0);
        return true;
      } catch (UnityException) {
        return false;
      }
    }

    // TODO: maybe call this from BuildTiltBrush?
    [MenuItem("Tilt/Pack all atlases")]
    public static void PackAll() {
      foreach (var catalog in IterAssets<IconTextureAtlasCatalog>()) {
        catalog.Pack();
      }
    }

    [ContextMenu("RePack")]
    void Pack() {
      try {
        Progress("Reading Textures", 0.5f);

        Texture2D[] textures = new Texture2D[0];
        foreach (var group in IterAssets<Texture2D>(folders: m_paths)
            .Concat(IterTextures(m_brushManifests))
            .Distinct()
            .GroupBy(TextureIsReadable)) {
          if (group.Key) {
            textures = group.OrderBy(AssetDatabase.GetAssetPath).ToArray();
          } else {
            var unreadableTextures = group
                .Select(AssetDatabase.GetAssetPath)
                .OrderBy(x => x).ToArray();
            Debug.LogWarningFormat(
                this,
                "{0} texture(s) are not CPU-readable and cannot be atlased:\n{1}",
                unreadableTextures.Length, string.Join("\n", unreadableTextures));
          }
        }

        string thisPath = AssetDatabase.GetAssetPath(this);
        string atlasPath = thisPath.Substring(
            0, thisPath.Length - System.IO.Path.GetExtension(thisPath).Length) + ".png";
        if (m_Atlas != null) {
          atlasPath = AssetDatabase.GetAssetPath(m_Atlas);
        }

        if (m_VerifyTextureSize > 0) {
          StringBuilder missizedTextures = new StringBuilder();
          for (int i = 0; i < textures.Length; ++i) {
            if (textures[i].width != m_VerifyTextureSize ||
                textures[i].height != m_VerifyTextureSize) {
              missizedTextures.AppendLine(textures[i].name);
            }
          }
          if (missizedTextures.Length != 0) {
            Debug.LogWarningFormat("The following textures are incorrectly sized:\n{0}",
                missizedTextures);
          }
        }

        Progress(string.Format("Packing {0} Textures", textures.Length), 0.75f);
        StringBuilder errors = new StringBuilder();
        Texture2D atlas = new Texture2D(m_AtlasSize, m_AtlasSize);
        Rect[] rects = atlas.PackTextures(textures.ToArray(), m_padding, m_AtlasSize);
        m_entries = new TextureEntry[textures.Length];
        int textureIndex = 0;
        for (int i = 0; i < textures.Length; i++) {
          if (rects[i].width != 0) {
            m_entries[textureIndex] = new TextureEntry();
            m_entries[textureIndex].m_texture = textures[i];
            m_entries[textureIndex].m_rect = rects[i];
            textureIndex++;
          } else {
            errors.AppendLine(textures[i].name);
          }
        }

        if (errors.Length != 0) {
          Debug.LogWarningFormat("The following textures could not be packed:\n{0}", errors);
        }

        Progress("Saving Atlas", 0.85f);
        System.IO.File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());
        AssetDatabase.ImportAsset(atlasPath);
        if (m_Atlas == null) {
          m_Atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
        }
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
      } finally {
        EditorUtility.ClearProgressBar();
      }

      Debug.LogFormat("{0}: Packed {1} textures.", name, m_entries.Length);
    }

    private void Progress(string action, float p) {
      EditorUtility.DisplayProgressBar("Icon Texture Atlas Catalog", action, p);
    }
#endif
  }
} // namespace TiltBrush
