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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush {
public class ShaderWarmup : MonoBehaviour {
  [SerializeField] private int m_FramesBeforeWarmup;
  [SerializeField] private int m_FramesAfterWarmup;
  [SerializeField] private int m_ShadersPerFrame = 10;

  [SerializeField] private GameObject m_RootObject;

  public static ShaderWarmup Instance { get; private set; }

  public float Progress { get; private set; }

  private IEnumerator Start() {
    Instance = this;
    Progress = 0;
    for (int i = 0; i < m_FramesBeforeWarmup; ++i) {
      yield return null;
    }
    Progress = 0.05f;
    yield return StartCoroutine(WarmupShaders());
    Progress = 0.95f;
    for (int i = 0; i < m_FramesAfterWarmup; ++i) {
      yield return null;
    }
    m_RootObject.SetActive(false);
  }

  // Enumerates the materials we need and creates a quad with each one.
  private IEnumerator WarmupShaders() {
    List<Material> materials = BrushCatalog.m_Instance.AllBrushes.Select(x => x.Material).ToList();
    // Add SELECTION_ON to the materials
    List<Material> selectionMaterials = new List<Material>();
    foreach (var material in materials) {
      Material newMaterial = new Material(material);
      newMaterial.EnableKeyword("SELECTION_ON");
      selectionMaterials.Add(newMaterial);
    }
    materials.AddRange(selectionMaterials);
    Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
    materials.AddRange(renderers.SelectMany(x => x.sharedMaterials));

    var distinctShaders = materials.Distinct(new MaterialComparer()).ToArray();

    int size = Mathf.CeilToInt(Mathf.Sqrt(distinctShaders.Length));
    Vector3 offset = new Vector3(-size / 2f, -size / 2f, 0);
    int index = 0;
    foreach (Material material in distinctShaders) {
      if (material == null) {
        continue;
      }
      GameObject gobj = GameObject.CreatePrimitive(PrimitiveType.Quad);
      gobj.name = material.name;
      gobj.GetComponent<Renderer>().material = material;
      gobj.transform.parent = transform;
      gobj.transform.localPosition = new Vector3(index % size, index / size, 0) + offset;
      index++;
      Progress = 0.05f + (index / (float)distinctShaders.Length) * 0.9f;
      if (index % m_ShadersPerFrame == 0) {
        yield return null;
      }
    }
  }

  // Comparator for materials compares them on shader, shader keywords and global illumination flags
  // This is used to help work out which shaders are distinct.
  private class MaterialComparer : IEqualityComparer<Material> {
    public bool Equals(Material x, Material y) {
      if (ReferenceEquals(x, y)) {
        return true;
      }

      if (ReferenceEquals(x, null) || ReferenceEquals(y, null)) {
        return false;
      }

      return x.shader == y.shader &&
             x.shaderKeywords.SequenceEqual(y.shaderKeywords) &&
             x.globalIlluminationFlags == y.globalIlluminationFlags;
    }

    public int GetHashCode(Material material) {
      if (ReferenceEquals(material, null)) {
        return 0;
      }

      int hashCode = (material.shader == null ? 0 :  material.shader.GetHashCode()) ^
                     material.globalIlluminationFlags.GetHashCode();
      foreach (string keyword in material.shaderKeywords) {
        hashCode ^= keyword.GetHashCode();
      }

      return hashCode;
    }
  }
}
} // namespace TiltBrush
