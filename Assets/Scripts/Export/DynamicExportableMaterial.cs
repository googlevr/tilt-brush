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

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush {

public class DynamicExportableMaterial : IExportableMaterial {
  // Constructor
  public DynamicExportableMaterial(
      BrushDescriptor parent,
      string durableName,
      Guid uniqueName,
      string uriBase) {
    this.Parent = parent;
    m_UniqueName = uniqueName;
    m_DurableName = durableName;
    m_UriBase = uriBase;
    m_TextureUris  = new Dictionary<string, string>();
    m_TextureSizes = new Dictionary<string, Vector2>();
    m_FloatParams  = new Dictionary<string, float>();
    m_VectorParams = new Dictionary<string, Vector3>();
    m_ColorParams  = new Dictionary<string, Color>();

    // Set defaults

    BaseColorFactor = Color.white;
    // Some of the Unity materials we use assume that there's an albedo texture,
    // so every material we export gets a texture. This is an old hack that we
    // should fix at some point.
    BaseColorTex = ExportUtils.kBuiltInPrefix + "whiteTextureMap.png";
    MetallicFactor = 0.02f;
    RoughnessFactor = 1f;
  }

  // These are the only properties honored by the exporter

  public Color BaseColorFactor {
    get => m_ColorParams["BaseColorFactor"];
    set => m_ColorParams["BaseColorFactor"] = value;
  }

  public string BaseColorTex {
    get => m_TextureUris["BaseColorTex"];
    set {
      // Only store non-null values for BaseColorTex.
      if (value != null) {
        m_TextureUris["BaseColorTex"] = value;
      } else {
        if (m_TextureUris.ContainsKey("BaseColorTex")) {
          m_TextureUris.Remove("BaseColorTex");
        }
      }
    }
  }

  public float MetallicFactor {
    get => m_FloatParams["MetallicFactor"];
    set => m_FloatParams["MetallicFactor"] = value;
  }

  public float RoughnessFactor {
    get => m_FloatParams["RoughnessFactor"];
    set => m_FloatParams["RoughnessFactor"] = value;
  }

#region IExportableMaterial interface
  public Guid UniqueName => m_UniqueName;
  public string DurableName => m_DurableName;
  public ExportableMaterialBlendMode BlendMode => Parent.BlendMode;
  public float EmissiveFactor => 0;

  // TODO(b/142396408): create a better layout?
  public GeometryPool.VertexLayout VertexLayout => Parent.VertexLayout;
  public bool HasExportTexture() { return false; }
  public string GetExportTextureFilename() { return null; }
  public bool SupportsDetailedMaterialInfo => true;
  public string VertShaderUri => Parent.VertShaderUri;
  public string FragShaderUri => Parent.FragShaderUri;
  public bool EnableCull => Parent.EnableCull;
  public string UriBase => m_UriBase;
  public Dictionary<string, string> TextureUris => m_TextureUris;
  public Dictionary<string, Vector2> TextureSizes => m_TextureSizes;
  public Dictionary<string, float> FloatParams => m_FloatParams;
  public Dictionary<string, Vector3> VectorParams => m_VectorParams;
  public Dictionary<string, Color> ColorParams => m_ColorParams;
#endregion

  /// The descriptor this material was based from.
  /// This can be useful when exporting to formats like fbx that don't have good material support
  public BrushDescriptor Parent { get; }

  private Guid m_UniqueName;
  private readonly string m_DurableName;
  private string m_UriBase;
  private Dictionary<string, string> m_TextureUris;
  private Dictionary<string, Vector2> m_TextureSizes;
  private Dictionary<string, float> m_FloatParams;
  private Dictionary<string, Vector3> m_VectorParams;
  private Dictionary<string, Color> m_ColorParams;
}

}  // namespace TiltBrush
