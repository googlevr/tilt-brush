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
using UnityEngine;

namespace TiltBrush {

public class IconTextureAtlas : MonoBehaviour {
  const int m_AtlasSize = 2048;
  const int m_TextureSize = 128;

  [SerializeField] private Material m_BaseMaterial;
  [SerializeField] private Material m_ActiveMaterial;
  [SerializeField] private float m_ActiveMaterialBorderPercent = 0.115f;
  [SerializeField] private string m_CatalogPath;
  [SerializeField] private string m_ExperimentalCatalogPath;
  private IconTextureAtlasCatalog m_Catalog;

  private Material m_AtlasMaterial_Base;
  private Material m_AtlasMaterial_Active;
  private Material m_AtlasMaterial_Base_NoFocus;
  private Material m_AtlasMaterial_Active_NoFocus;
  private Dictionary<int, Rect> m_TextureDictionary;

  public void Init() {
    AtlasIconTextures();
  }

  public Material GetAppropriateMaterial(bool activated, bool focus) {
    if (activated) {
      return focus ? m_AtlasMaterial_Active : m_AtlasMaterial_Active_NoFocus;
    } else {
      return focus ? m_AtlasMaterial_Base : m_AtlasMaterial_Base_NoFocus;
    }
  }

  void AtlasIconTextures() {
    // Load the appropriate catalog from Resources.
    string catalogPath = App.Config.m_IsExperimental ? m_ExperimentalCatalogPath : m_CatalogPath;
    m_Catalog = Resources.Load<IconTextureAtlasCatalog>(catalogPath);
    Debug.Assert(m_Catalog != null);

    // Create new material and assign base texture to material.
    m_AtlasMaterial_Base = new Material(m_BaseMaterial);
    m_AtlasMaterial_Base.mainTexture = m_Catalog.Atlas;

    // Create no focus base version.
    m_AtlasMaterial_Base_NoFocus = new Material(m_AtlasMaterial_Base);
    m_AtlasMaterial_Base_NoFocus.color = new Color(UIComponent.kUnavailableTintAmount,
        UIComponent.kUnavailableTintAmount, UIComponent.kUnavailableTintAmount);

    // Create active material.
    m_AtlasMaterial_Active = new Material(m_ActiveMaterial);
    m_AtlasMaterial_Active.mainTexture = m_Catalog.Atlas;
    Vector2 dims = new Vector2(m_Catalog.Atlas.width, m_Catalog.Atlas.height);
    Vector2 textureDim = m_Catalog[0].m_rect.size * m_AtlasSize;
    m_AtlasMaterial_Active.SetVector("_Dimensions", dims);
    m_AtlasMaterial_Active.SetVector("_TextureDim", textureDim);
    m_AtlasMaterial_Active.SetFloat("_Padding", m_Catalog.Padding);
    m_AtlasMaterial_Active.SetFloat("_BorderPercent", m_ActiveMaterialBorderPercent);

    // No focus active material.
    m_AtlasMaterial_Active_NoFocus = new Material(m_AtlasMaterial_Active);
    m_AtlasMaterial_Active_NoFocus.SetVector("_Dimensions", dims);
    m_AtlasMaterial_Active_NoFocus.SetVector("_TextureDim", textureDim);
    m_AtlasMaterial_Active_NoFocus.SetFloat("_Padding", m_Catalog.Padding);
    m_AtlasMaterial_Active_NoFocus.SetFloat("_BorderPercent", m_ActiveMaterialBorderPercent);
    m_AtlasMaterial_Active_NoFocus.color = new Color(UIComponent.kUnavailableTintAmount,
        UIComponent.kUnavailableTintAmount, UIComponent.kUnavailableTintAmount);

    // Populate our dictionary.
    m_TextureDictionary = new Dictionary<int, Rect>();
    for (int i = 0; i < m_Catalog.Length; ++i) {
      if (m_Catalog[i] != null && m_Catalog[i].m_texture != null) {
        m_TextureDictionary.Add(m_Catalog[i].m_texture.GetHashCode(), m_Catalog[i].m_rect);
      } else {
        Debug.LogWarningFormat(
            "A texture is missing from the catalog. Do you need to re-pack {0}?", catalogPath);
      }
    }
  }

  public bool GetTextureUVs(Texture2D texture, out Rect uvs) {
    if (texture != null &&
        m_TextureDictionary != null &&
        m_TextureDictionary.ContainsKey(texture.GetHashCode())) {
      uvs = m_TextureDictionary[texture.GetHashCode()];
      return true;
    }

    uvs = new Rect();
    return false;
  }

  static public List<Vector2> ScaleUvsWithAtlasRect(Rect r, List<Vector2> inUvs) {
    List<Vector2> outUvs = new List<Vector2>();
    Vector2 offset = new Vector2(r.xMin, r.yMin);
    Vector2 extent = new Vector2(r.xMax - r.xMin, r.yMax - r.yMin);
    for (int i = 0; i < inUvs.Count; ++i) {
      Vector2 scaledUv = new Vector2();
      scaledUv.x = (inUvs[i].x * extent.x) + offset.x;
      scaledUv.y = (inUvs[i].y * extent.y) + offset.y;
      outUvs.Add(scaledUv);
    }
    return outUvs;
  }
}

} // namespace TiltBrush