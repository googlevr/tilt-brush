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
using System.IO;
using UnityEngine;
using UnityEditor;

namespace TiltBrush {

// Stripped-down descriptor, for export to the toolkit
public class ToolkitBrushDescriptor : ScriptableObject {
  // This part is the same as the Toolkit class

  public SerializableGuid m_Guid;
  [Tooltip("A human readable name that cannot change, but is not guaranteed to be unique.")]
  public string m_DurableName;
  public Material m_Material;
  public bool m_IsParticle;

  public int m_uv0Size;
  public GeometryPool.Semantic m_uv0Semantic;
  public int m_uv1Size;
  public GeometryPool.Semantic m_uv1Semantic;
  public bool m_bUseNormals;
  public GeometryPool.Semantic m_normalSemantic;
  public bool m_bFbxExportNormalAsTexcoord1;

  // This part is custom to Tilt Brush

  // These are text strings found in TB's ToolkitBrushDescriptor.cs.meta file,
  // and in TBT's BrushDescriptor.cs.meta.  The conversion from System.Guid is
  // ambiguous, and we don't actually ever need the Guid form, so leave as text.
  private static readonly string kMetaGuid = "45721540c3fcb89478be84e996474828";
  private static readonly string kToolkitMetaGuid = "103cd3480fe93f646be28a7da5812871";

  /// Returns a serialized ToolkitBrushDescriptor suitable for inclusion in the Toolkit
  public static string CreateAndSerialize(
      BrushDescriptor desc, Guid materialGuid, string assetName, out string meta) {
    ToolkitBrushDescriptor tkdesc = CreateFrom(desc);
    string yaml = SerializeToUnityString(tkdesc, assetName, out meta);
    yaml = ChangeToToolkitScript(yaml);
    yaml = SetMaterialGuid(yaml, materialGuid);
    return yaml;
  }

  private static ToolkitBrushDescriptor CreateFrom(BrushDescriptor desc) {
    var result = CreateInstance<ToolkitBrushDescriptor>();
    result.name = desc.name;
    result.m_Guid = desc.m_Guid;
    result.m_DurableName = desc.m_DurableName;
    result.m_Material = null;  // will be filled in by hand after serialization
    result.m_IsParticle = (desc.m_BrushPrefab.GetComponent<GeniusParticlesBrush>() != null);

    var layout = desc.VertexLayout;
    result.m_uv0Size = layout.texcoord0.size;
    result.m_uv0Semantic = layout.texcoord0.semantic;
    result.m_uv1Size = layout.texcoord1.size;
    result.m_uv1Semantic = layout.texcoord1.semantic;
    result.m_bUseNormals = layout.bUseNormals;
    result.m_normalSemantic = layout.normalSemantic;
    result.m_bFbxExportNormalAsTexcoord1 = layout.bFbxExportNormalAsTexcoord1;

    return result;
  }

  // Returns the YAML and .meta for the given object
  private static string SerializeToUnityString(
      ScriptableObject obj, string assetName, out string meta) {
    var tempAssetPath = string.Format("Assets/__Serialize__{0}.asset", assetName);
    AssetDatabase.CreateAsset(obj, tempAssetPath);
    try {
      // Fix up the object name
      meta = File.ReadAllText(tempAssetPath+".meta");
      return File.ReadAllText(tempAssetPath).Replace("__Serialize__", "");
    } finally {
      AssetDatabase.DeleteAsset(tempAssetPath);
    }
  }

  // Returns yaml that refers to Assets/TiltBrush/Scripts/BrushDescriptor.cs (a toolkit file)
  // rather than Assets/Editor/ToolkitBrushDescriptor.cs (a TB file)
  private static string ChangeToToolkitScript(string yaml) {
    Debug.Assert(yaml.Contains(kMetaGuid));
    return yaml.Replace(kMetaGuid, kToolkitMetaGuid);
  }

  // Returns yaml with m_Material set to the passed material guid.
  // We cant serialize it directly in, because that guid doesn't exist in TB
  private static string SetMaterialGuid(string yaml, Guid guid) {
    // m_Material: .*
    // m_Material: {fileID: 2100000, guid: 4391aaaadf7343969e3331e4e4930b27, type: 2}
    string src = "m_Material: {fileID: 0}";
    string dst = string.Format(
        "m_Material: {{fileID: 2100000, guid: {0}, type: 2}}",
        ToolkitUtils.GuidToUnityString_Incorrect(guid));
    Debug.Assert(yaml.Contains(src));
    return yaml.Replace(src, dst);
  }
}

}
