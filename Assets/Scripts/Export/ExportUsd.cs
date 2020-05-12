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

#if USD_SUPPORTED
using System;
using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;
using UnityEngine;
using USD.NET.Unity;

namespace TiltBrush {
static class ExportUsd {

  // -------------------------------------------------------------------------------------------- //
  // Serialization Classes
  // -------------------------------------------------------------------------------------------- //

  #region "Geometry Classes for Serialization"

  /// The root / sketch metadata for the file.
  [Serializable]
  [UsedImplicitly(ImplicitUseTargetFlags.Members)]
  public class SketchRootSample : USD.NET.Unity.XformSample {

    [USD.NET.UsdNamespace("tiltBrush")]
    public string release;

    [USD.NET.UsdNamespace("tiltBrush")]
    public string toolkitVersion;

    [USD.NET.UsdNamespace("tiltBrush")]
    public string sketchName;

    [USD.NET.UsdNamespace("tiltBrush")]
    public string assetId;

    [USD.NET.UsdNamespace("tiltBrush")]
    public string sourceAssetId;

    [USD.NET.UsdNamespace("tiltBrush")]
    public string sketchFilePath;

  }

  /// StrokeInfo is an optional bundle of nested data that can be attached to a mesh. The mesh is a
  /// a batch of strokes and the StrokeInfo describes the individual strokes nested within.
  [Serializable]
  public class StrokeBatchInfo : USD.NET.SampleBase {
    public int[] triOffsets;
    public int[] triCounts;
    public int[] vertOffsets;
    public int[] vertCounts;
    public float[] startTimes;
    public float[] endTimes;
  }

  /// BrushSample extends the default USD MeshSample by adding a GUID for the brush (used when
  /// rebinding materials) and StrokeInfo.
  [Serializable]
  [UsedImplicitly(ImplicitUseTargetFlags.Members)]
  public class BrushSample : USD.NET.Unity.MeshSample {
    [USD.NET.CustomData]
    public Guid brush;

    [USD.NET.UsdNamespace("stroke")]
    public StrokeBatchInfo stroke;
  }

  /// Used to create a Mesh prim in USD but only write the transform.
  /// Intended to be used when writing refrences to existing meshes.
  [Serializable]
  [USD.NET.UsdSchema("Mesh")]
  public class MeshXformSample : USD.NET.Unity.XformSample {
  }

  /// Used to create a USD BasisCurves primitive.
  [Serializable]
  [UsedImplicitly(ImplicitUseTargetFlags.Members)]
  public class BrushCurvesSample : USD.NET.Unity.BasisCurvesSample {
    // TODO: this should be part of BasisCurvesSample, not defined here.
    public Bounds extent;

    /// The time at which the knot was authored.
    [USD.NET.UsdNamespace("knot")]
    public float[] times;

    /// The trigger pressure
    [USD.NET.UsdNamespace("knot")]
    public float[] pressures;
  }

  #endregion

  #region "Material Classes for Serialization"

  [Serializable]
  [UsedImplicitly(ImplicitUseTargetFlags.Members)]
  class ExportVertexLayout : USD.NET.SampleBase {
    public int uv0Size;
    public GeometryPool.Semantic uv0Semantic;
    public int uv1Size;
    public GeometryPool.Semantic uv1Semantic;
    public bool bUseNormals;
    public GeometryPool.Semantic normalSemantic;
    public bool bUseColors;
    public bool bUseTangents;
    public bool bUseVertexIds;
  }

  [Serializable]
  [UsedImplicitly(ImplicitUseTargetFlags.Members)]
  [USD.NET.UsdSchema("Material")]
  class ExportMaterialSample : USD.NET.Unity.MaterialSample {
  }

  [Serializable]
  [UsedImplicitly(ImplicitUseTargetFlags.Members)]
  class ExportShaderSample : PreviewSurfaceSample {
    [USD.NET.UsdNamespace("info")]
    public ExportableMaterialBlendMode blendMode;
    [USD.NET.UsdNamespace("info")]
    public float emissiveFactor;
    [USD.NET.UsdNamespace("info")]
    public bool enableCull;

    [USD.NET.UsdNamespace("info")]
    public Guid uniqueName;
    [USD.NET.UsdNamespace("info")]
    public string durableName;
    [USD.NET.UsdNamespace("info")]
    public string vertShaderUri;
    [USD.NET.UsdNamespace("info")]
    public string fragShaderUri;
    [USD.NET.UsdNamespace("info")]
    public string uriBase;

    [USD.NET.UsdNamespace("info:vertexLayout")]
    public ExportVertexLayout vertexLayout;
  }

  [Serializable]
  class ExportTextureSample : TextureReaderSample {
  }

  [Serializable]
  class PrimvarReader1fSample : PrimvarReaderSample<float> {
    public PrimvarReader1fSample()
      : base() {
    }

    public PrimvarReader1fSample(string primvarName)
      : base() {
      this.varname.defaultValue = new pxr.TfToken(primvarName);
    }
  }

  [Serializable]
  class PrimvarReader2fSample : PrimvarReaderSample<Vector2> {
    public PrimvarReader2fSample() : base() {
    }

    public PrimvarReader2fSample(string primvarName) : base() {
      this.varname.defaultValue = new pxr.TfToken(primvarName);
    }
  }

  [Serializable]
  class PrimvarReader3fSample : PrimvarReaderSample<Vector3> {
    public PrimvarReader3fSample()
      : base() {
    }

    public PrimvarReader3fSample(string primvarName)
      : base() {
      this.varname.defaultValue = new pxr.TfToken(primvarName);
    }
  }

  [Serializable]
  class PrimvarReader4fSample : PrimvarReaderSample<Vector4> {
    public PrimvarReader4fSample()
      : base() {
    }

    public PrimvarReader4fSample(string primvarName)
      : base() {
      this.varname.defaultValue = new pxr.TfToken(primvarName);
    }
  }

  #endregion

  // -------------------------------------------------------------------------------------------- //
  // Conversion Helpers
  // -------------------------------------------------------------------------------------------- //

  /// Fetch the appropriate UV set from the mesh, given the index and element size.
  static object GetUv(int uvSet, int uvSize, GeometryPool geomPool) {
    if (uvSet > 1) {
      return null;
    }
    switch (uvSize) {
    case 0:
      return null;
    case 2:
      return geomPool.GetTexcoordData(uvSet).v2;
    case 3:
      return geomPool.GetTexcoordData(uvSet).v3;
    case 4:
      return geomPool.GetTexcoordData(uvSet).v4;
    default:
      throw new ArgumentException(string.Format("Unexpected uv size: {0}", uvSize));
    }
  }

  /// Converts a GeometryPool into a BrushSample.
  static BrushSample GetBrushSample(GeometryPool geomPool,
                                    List<Stroke> strokes,
                                    Matrix4x4 mat44) {
    var sample = new BrushSample();
    GetMeshSample(geomPool, mat44, sample);

    var vertexLayout = geomPool.Layout;

    // Used for shader binding.
    sample.brush = strokes[0].m_BrushGuid;

    // Optional StrokeInfo, this can be disabled to improve performance.
    sample.stroke = GetStrokeBatchInfo(strokes);

    return sample;
  }

  /// Converts a GeometryPool into a MeshSample.
  static void GetMeshSample(GeometryPool geomPool,
                            Matrix4x4 mat44,
                            MeshSample sample) {
    var vertexLayout = geomPool.Layout;

    // Used for shader binding.
    sample.doubleSided = true;
    sample.orientation = USD.NET.Orientation.LeftHanded;
    sample.points = geomPool.m_Vertices.ToArray();
    sample.faceVertexIndices = geomPool.m_Tris.ToArray();
    sample.transform = mat44;

    sample.extent = new Bounds(sample.points[0], Vector3.zero);
    for (int i = 0; i < sample.points.Length; i++) {
      sample.extent.Encapsulate(sample.points[i]);
    }

    // Yuck. Perhaps push this down to a lower layer.
    sample.faceVertexCounts = new int[sample.faceVertexIndices.Length / 3];
    for (int i = 0; i < sample.faceVertexCounts.Length; i++) {
      sample.faceVertexCounts[i] = 3;
    }

    if (vertexLayout.bUseNormals) {
      sample.normals = geomPool.m_Normals.ToArray();
    }

    if (vertexLayout.bUseTangents) {
      sample.tangents = geomPool.m_Tangents.ToArray();
    }

    if (vertexLayout.bUseColors) {
      sample.colors = geomPool.m_Colors.Select(
          c => (new Color(c.r, c.g, c.b, c.a) / 255.0f).linear).ToArray();
    }

    sample.uv = GetUv(0, vertexLayout.texcoord0.size, geomPool);
    sample.uv2 = GetUv(1, vertexLayout.texcoord1.size, geomPool);
  }

  /// Converts TiltBrush brush strokes into linear USD BasisCurves.
  static BrushCurvesSample GetCurvesSample(ExportUtils.SceneStatePayload payload,
                                           List<Stroke> strokes,
                                           Matrix4x4 mat44) {
    var sample = new BrushCurvesSample();
    var times = new List<float>();
    var pressures = new List<float>();

    sample.doubleSided = true;
    sample.orientation = USD.NET.Orientation.LeftHanded;
    sample.basis = BasisCurvesSample.Basis.Bezier;
    sample.type = BasisCurvesSample.CurveType.Linear;
    sample.wrap = BasisCurvesSample.WrapMode.Nonperiodic;
    sample.curveVertexCounts = strokes.Select(x => x.m_ControlPoints.Length).ToArray();
    sample.transform = mat44;

    int numCPs = sample.curveVertexCounts.Sum();
    sample.points = new Vector3[numCPs];
    sample.normals = new Vector3[numCPs];
    sample.colors = new Color[numCPs];
    int iKnot = 0;

    foreach (Stroke stroke in strokes) {
      foreach (var cp in stroke.m_ControlPoints) {
        times.Add(cp.m_TimestampMs / 1000.0f);
        pressures.Add(cp.m_Pressure);
      }
      foreach (var cp in stroke.m_ControlPoints) {
        // Normals in USD are stored in object/local space, just as points are.
        sample.normals[iKnot] = cp.m_Orient * Vector3.up;
        sample.points[iKnot] = cp.m_Pos;
        sample.colors[iKnot] = stroke.m_Color.linear;
        iKnot++;
      }
    }

    {
      Matrix4x4 basis = ExportUtils.GetFromUnity_Axes(payload);
      Matrix4x4 units = Matrix4x4.Scale(Vector3.one * payload.exportUnitsFromAppUnits);
      MathUtils.TransformVector3AsPoint(units * basis, 0, sample.points.Length, sample.points);
      MathUtils.TransformVector3AsVector(basis, 0, sample.normals.Length, sample.normals);
    }

    sample.extent = new Bounds(sample.points[0], Vector3.zero);
    for (int i = 0; i < sample.points.Length; i++) {
      sample.extent.Encapsulate(sample.points[i]);
    }

    sample.times = times.ToArray();
    sample.pressures = pressures.ToArray();

    return sample;
  }

  /// Collects the triangle offsets, vertex offsets and timing for the stroke mesh.
  static StrokeBatchInfo GetStrokeBatchInfo(List<Stroke> strokes) {
    var batchInfo = new StrokeBatchInfo();

    int numStrokes = strokes.Count;
    batchInfo.triOffsets = new int[numStrokes];
    batchInfo.triCounts = new int[numStrokes];
    batchInfo.vertOffsets = new int[numStrokes];
    batchInfo.vertCounts = new int[numStrokes];
    batchInfo.startTimes = new float[numStrokes];
    batchInfo.endTimes = new float[numStrokes];

    int iStroke = 0;
    int vertCount = 0;
    int faceCount = 0;
    foreach (var stroke in strokes) {
      batchInfo.triOffsets[iStroke] = faceCount;
      batchInfo.triCounts[iStroke] = stroke.m_BatchSubset.m_nTriIndex;
      faceCount += stroke.m_BatchSubset.m_nTriIndex;

      batchInfo.vertOffsets[iStroke] = vertCount;
      batchInfo.vertCounts[iStroke] = stroke.m_BatchSubset.m_VertLength;
      vertCount += stroke.m_BatchSubset.m_VertLength;

      batchInfo.startTimes[iStroke] = stroke.m_BatchSubset.m_Stroke.HeadTimestampMs / 1000.0f;
      batchInfo.endTimes[iStroke] = stroke.m_BatchSubset.m_Stroke.TailTimestampMs / 1000.0f;

      iStroke++;
    }

    return batchInfo;
  }

  /// Collects data from the exportable material and converts it to a ShaderSample.
  static ExportShaderSample GetShaderSample(IExportableMaterial material) {
    var shaderSample = new ExportShaderSample();

    shaderSample.useSpecularWorkflow.defaultValue = 1;
    shaderSample.roughness.defaultValue = 0.5f;
    shaderSample.specularColor.defaultValue = new Vector3(.1f, .1f, .1f);

    shaderSample.durableName = material.DurableName;
    shaderSample.uniqueName = material.UniqueName;
    shaderSample.uriBase = material.UriBase;
    if (material.SupportsDetailedMaterialInfo) {
      shaderSample.vertShaderUri = material.VertShaderUri;
      shaderSample.fragShaderUri = material.FragShaderUri;
      shaderSample.enableCull = material.EnableCull;

      if (material.FloatParams.ContainsKey("SpecColor")) {
        var c = material.ColorParams["SpecColor"].linear;
        shaderSample.specularColor.defaultValue = new Vector3(c.r, c.g, c.b);
      }

      if (material.FloatParams.ContainsKey("Color")) {
        var c = material.ColorParams["Color"].linear;
        shaderSample.diffuseColor.defaultValue = new Vector3(c.r, c.g, c.b);
      }

      if (material.FloatParams.ContainsKey("Shininess")) {
        shaderSample.roughness.defaultValue = 1.0f - material.FloatParams["Shininess"];
      }
    }
    shaderSample.blendMode = material.BlendMode;
    shaderSample.emissiveFactor = material.EmissiveFactor;

    shaderSample.vertexLayout = new ExportVertexLayout();
    shaderSample.vertexLayout.bUseColors = material.VertexLayout.bUseColors;
    shaderSample.vertexLayout.bUseNormals = material.VertexLayout.bUseNormals;
    shaderSample.vertexLayout.bUseTangents = material.VertexLayout.bUseTangents;
    shaderSample.vertexLayout.bUseVertexIds = material.VertexLayout.bUseVertexIds;
    shaderSample.vertexLayout.normalSemantic = material.VertexLayout.normalSemantic;
    shaderSample.vertexLayout.uv0Semantic = material.VertexLayout.texcoord0.semantic;
    shaderSample.vertexLayout.uv0Size = material.VertexLayout.texcoord0.size;
    shaderSample.vertexLayout.uv1Semantic = material.VertexLayout.texcoord1.semantic;
    shaderSample.vertexLayout.uv1Size = material.VertexLayout.texcoord1.size;

    return shaderSample;
  }

  // -------------------------------------------------------------------------------------------- //
  // USD Authoring Helpers
  // -------------------------------------------------------------------------------------------- //

  /// USD requires names adhere to C++ naming conventions, but Unity allows for arbitrary
  /// identifiers. This function sanitizes the USD name.
  static string SanitizeIdentifier(string identifier) {
    return USD.NET.IntrinsicTypeConverter.MakeValidIdentifier(identifier);
  }

  static public SketchRootSample CreateSketchRoot() {
    var sample = new SketchRootSample();

    // Xform matrix.
    sample.transform = Matrix4x4.identity;

    // Sketch metadata.
    sample.release = App.Config.m_VersionNumber;
    sample.toolkitVersion = FbxUtils.kRequiredToolkitVersion;
    sample.sketchName = SaveLoadScript.m_Instance.GetLastFileHumanName();
    sample.assetId = SaveLoadScript.m_Instance.SceneFile.AssetId;
    sample.sourceAssetId =
        SaveLoadScript.m_Instance.TransferredSourceIdFrom(SaveLoadScript.m_Instance.SceneFile);
    sample.sketchFilePath = SaveLoadScript.m_Instance.SceneFile.FullPath;

    // Normalize string path.
    if (!string.IsNullOrEmpty(sample.sketchFilePath)) {
      sample.sketchFilePath = sample.sketchFilePath.Replace("\\", "/");
    }

    return sample;
  }

  /// Authors the root "Sketch" object to be exported, with sketch metadta.
  static void AddSketchRoot(USD.NET.Scene scene, string path) {
    var sample = CreateSketchRoot();

    // Setup time to author default values, at no time sample.
    var oldTime = scene.Time;
    scene.Time = null;

    // Write the data.
    scene.Write(path, sample);

    // Restore the desired time.
    scene.Time = oldTime;
  }

  /// Authors a USD Xform prim at the given path with the given matrix transform.
  static void CreateXform(USD.NET.Scene scene, string path, Matrix4x4? xform = null) {
    var sample = new XformSample();
    if (xform.HasValue) {
      sample.transform = xform.Value;
    } else {
      sample.transform = Matrix4x4.identity;
    }
    scene.Write(path, sample);
  }

  /// Authors USD Shader inputs onto the given shader and material and connects private shader
  /// inputs to the public material inputs (the intent being, inputs are authored on the material
  /// and flow into the shader, which is a detail of the network).
  static void CreateShaderInputs<T>(pxr.UsdShadeShader shader,
                                 pxr.UsdShadeMaterial material,
                                 Dictionary<string, T> paramDict) {
    USD.NET.UsdTypeBinding binding;
    if (!USD.NET.UsdIo.Bindings.GetBinding(typeof(T), out binding)) {
      // TODO: add "with exception" to GetBinding().
      throw new Exception("Type not found: " + typeof(T).Name);
    }

    foreach (var kvp in paramDict) {
      var inputName = new pxr.TfToken(kvp.Key);
      var matInput = material.CreateInput(inputName, binding.sdfTypeName);
      var shaderInput = shader.CreateInput(inputName, binding.sdfTypeName);
      matInput.Set(binding.toVtValue(kvp.Value));
      shaderInput.Set(binding.toVtValue(kvp.Value));
      shaderInput.ConnectToSource(matInput);
    }
  }

  /// Authors a USD Material, Shader, parameters and connections between the two.
  /// The USD shader structure consists of a Material, which is connected to a shader output. The
  /// Shader consists of input parameters which are either connected to other shaders or in the case
  /// of public parameters, back to the material which is the public interface for the shading
  /// network.
  ///

  /// This function creates a material, shader, inputs, outputs, zero or more textures, and for each
  /// texture, a single primvar reader node to read the UV data from the geometric primitive.
  static string CreateMaterialNetwork(USD.NET.Scene scene,
                                      IExportableMaterial material,
                                      string rootPath = null) {
    var matSample = new ExportMaterialSample();

    // Used scene object paths.
    string materialPath = GetMaterialPath(material, rootPath);
    string shaderPath = GetShaderPath(material, materialPath);
    string displayColorPrimvarReaderPath = GetPrimvarPath(material, "displayColor", shaderPath);
    string displayOpacityPrimvarReaderPath = GetPrimvarPath(material, "displayOpacity", shaderPath);

    // The material was already created.
    if (scene.GetPrimAtPath(materialPath) != null) {
      return materialPath;
    }

    // Ensure the root material path is defined in the scene.
    scene.Stage.DefinePrim(new pxr.SdfPath(rootPath));

    // Connect the materail surface to the output of the shader.
    matSample.surface.SetConnectedPath(shaderPath, "outputs:result");
    scene.Write(materialPath, matSample);

    // Create the shader and conditionally connect the diffuse color to the MainTex output.
    var shaderSample = GetShaderSample(material);
    var texturePath = CreateAlphaTexture(scene, shaderPath, material);

    if (texturePath != null) {
      // A texture was created, so connect the opacity input to the texture output.
      shaderSample.opacity.SetConnectedPath(texturePath, "outputs:a");
    } else {
      // TODO: currently primvars:displayOpacity is not multiplied when an alpha texture is
      //                present. However, this only affects the USD preview. The correct solution
      //                requires a multiply node in the shader graph, but this does not yet exist.
      scene.Write(displayOpacityPrimvarReaderPath, new PrimvarReader1fSample("displayOpacity"));
      shaderSample.opacity.SetConnectedPath(displayOpacityPrimvarReaderPath, "outputs:result");
    }

    // Create a primvar reader to read primvars:displayColor.
    scene.Write(displayColorPrimvarReaderPath, new PrimvarReader3fSample("displayColor"));

    // Connect the diffuse color to the primvar reader.
    shaderSample.diffuseColor.SetConnectedPath(displayColorPrimvarReaderPath, "outputs:result");

    scene.Write(shaderPath, shaderSample);

    //
    // Everything below is ad-hoc data, which is written using the low level USD API.
    // It consists of the Unity shader parameters and the non-exported texture URIs.
    // Also note that scene.GetPrimAtPath will return null when the prim is InValid,
    // so there is no need to call IsValid() on the resulting prim.
    //
    var shadeMaterial = new pxr.UsdShadeMaterial(scene.GetPrimAtPath(materialPath));
    var shadeShader = new pxr.UsdShadeShader(scene.GetPrimAtPath(shaderPath));

    if (material.SupportsDetailedMaterialInfo) {
      CreateShaderInputs(shadeShader, shadeMaterial, material.FloatParams);
      CreateShaderInputs(shadeShader, shadeMaterial, material.ColorParams);
      CreateShaderInputs(shadeShader, shadeMaterial, material.VectorParams);
    }

    CreateTextureUris(shadeShader.GetPrim(), material);

    return materialPath;
  }

  /// Authors USD Texture and PrimvarReader shader nodes for the given exportable material.
  ///

  /// Note that textureUris are stored as metadata and only the "Export Texture" is authored as a
  /// true texture in the shading network. This is due to the fact that the actual material textures
  /// are not currently exported with the USD file, but export textures are.
  /// 
  /// Returns the texture path if a texture node was created, otherwise null.
  static string CreateAlphaTexture(USD.NET.Scene scene,
                                   string shaderPath,
                                   IExportableMaterial material) {
    // Currently, only export texture is previewed in USD.
    // Create an input parameter to read the texture, e.g. inputs:_MainTex.
    if (!material.HasExportTexture()) {
      return null;
    }

    string texFile = SanitizeIdentifier(material.DurableName)
                    + System.IO.Path.GetExtension(material.GetExportTextureFilename());

    // Establish paths in the USD scene.
    string texturePath = GetTexturePath(material, "MainTex", shaderPath);
    string primvarPath = GetPrimvarPath(material, "uv", texturePath);

    // Create the texture Prim.
    var texture = new ExportTextureSample();

    // Connect the texture to the file on disk.
    texture.file.defaultValue = new pxr.SdfAssetPath(texFile);
    texture.st.SetConnectedPath(primvarPath, "outputs:result");
    scene.Write(texturePath, texture);

    if (scene.GetPrimAtPath(new pxr.SdfPath(primvarPath)) == null) {
      if (material.VertexLayout.texcoord0.size == 2) {
        var primvar = new PrimvarReader2fSample("uv");
        scene.Write(primvarPath, primvar);
      } else if (material.VertexLayout.texcoord0.size == 3) {
        var primvar = new PrimvarReader3fSample("uv");
        scene.Write(primvarPath, primvar);
      } else if (material.VertexLayout.texcoord0.size == 4) {
        var primvar = new PrimvarReader4fSample("uv");
        scene.Write(primvarPath, primvar);
      }
    }

    return texturePath;
  }

  static void CreateTextureUris(pxr.UsdPrim shaderPrim,
                                IExportableMaterial material) {
    if (material.SupportsDetailedMaterialInfo) {
      foreach (var kvp in material.TextureUris) {
        var attr = shaderPrim.CreateAttribute(new pxr.TfToken("info:textureUris:" + kvp.Key),
                                              SdfValueTypeNames.String);
        attr.Set(kvp.Value);
      }
    }
  }

  /// Set the material:binding relationship on the mesh/curve to target the given materialPath.
  static void BindMaterial(USD.NET.Scene scene, string primPath, string materialPath) {
    scene.Write(primPath, new MaterialBindingSample(materialPath));
  }

  // -------------------------------------------------------------------------------------------- //
  // Path Helpers
  // -------------------------------------------------------------------------------------------- //

  static string GetSketchPath() {
    return "/Sketch";
  }

  // Returns: /Sketch/Strokes
  static string GetStrokesPath() {
    return GetSketchPath() + "/Strokes";
  }

  // Returns: /Sketch/Strokes/Group_{groupId}
  static string GetGroupPath(UInt32 groupId) {
    return GetStrokesPath() + "/Group_" + groupId.ToString();
  }

  // Returns: /Sketch/Models
  static string GetModelsPath() {
    return GetSketchPath() + "/Models";
  }

  // Example: /Sketch/Strokes/Materials/Material_Light
  static string GetMaterialPath(IExportableMaterial material, string rootPath) {
    return rootPath + "/Materials/Material_" + SanitizeIdentifier(material.DurableName);
  }

  // Example: /Sketch/Strokes/Materials/Material_Light/Shader_Light
  static string GetShaderPath(IExportableMaterial material, string parentMaterialPath) {
    return parentMaterialPath + "/Shader_" + SanitizeIdentifier(material.DurableName);
  }

  // Example: /Sketch/Strokes/Materials/Material_Light/Shader_Light/Texture_MainTex
  static string GetTexturePath(IExportableMaterial material, string textureName, string parentShaderPath) {
    return parentShaderPath + "/Texture_" + SanitizeIdentifier(textureName);
  }

  // Example: /Sketch/Strokes/Materials/Material_Light/Shader_Light/Texture_MainTex/Primvar_Uv
  static string GetPrimvarPath(IExportableMaterial material, string primvarName, string parentTexturePath) {
    return parentTexturePath + "/Primvar_" + SanitizeIdentifier(primvarName);
  }

  // -------------------------------------------------------------------------------------------- //
  // Export Logic
  // -------------------------------------------------------------------------------------------- //

  /// Exports either all brush strokes or the given selection to the specified file.
  static public void ExportPayload(string outputFile) {
    // Would be nice to find a way to kick this off automatically.
    // Redundant calls are ignored.
    if (!App.InitializeUsd()) {
      return;
    }

    // Unity is left handed (DX), USD is right handed (GL)
    var payload = ExportCollector.GetExportPayload(AxisConvention.kUsd);
    var brushCatalog = BrushCatalog.m_Instance;

    // The Scene object provids serialization methods arbitrary C# objects to USD.
    USD.NET.Scene scene = USD.NET.Scene.Create(outputFile);

    // The target time at which samples will be written.
    //
    // In this case, all data is being written to the "default" time, which means it can be
    // overridden by animated values later.
    scene.Time = null;

    // Bracketing times to specify the valid animation range.
    scene.StartTime = 1.0;
    scene.EndTime = 1.0;

    const string kGeomName = "/Geom";
    const string kCurvesName = "/Curves";

    string path = "";

    AddSketchRoot(scene, GetSketchPath());   // Create: </Sketch>

    CreateXform(scene, GetStrokesPath());  // Create: </Sketch/Strokes>
    CreateXform(scene, GetModelsPath());   // Create: </Sketch/Models>

    // Main export loop.
    try {
      foreach (ExportUtils.GroupPayload group in payload.groups) {
        // Example: </Sketch/Strokes/Group_0>
        path = GetGroupPath(group.id);
        CreateXform(scene, path);

        // Example: </Sketch/Strokes/Group_0/Geom>
        CreateXform(scene, path + kGeomName);

        // Example: </Sketch/Strokes/Group_0/Curves>
        CreateXform(scene, path + kCurvesName);

        int iBrushMeshPayload = -1;
        foreach (var brushMeshPayload in group.brushMeshes) {
          ++iBrushMeshPayload;
          // Conditionally moves Normal into Texcoord1 so that the normal semantic is respected.
          // This only has an effect when layout.bFbxExportNormalAsTexcoord1 == true.
          // Note that this modifies the GeometryPool in place.
          FbxUtils.ApplyFbxTexcoordHack(brushMeshPayload.geometry);

          // Brushes are expected to be batched by type/GUID.
          Guid brushGuid = brushMeshPayload.strokes[0].m_BrushGuid;
          string brushName = "/" +
              SanitizeIdentifier(brushCatalog.GetBrush(brushGuid).DurableName) + "_";

          // Example: </Sketch/Strokes/Group_0/Geom/Marker_0>
          string meshPath = path + kGeomName + brushName;

          // Example: </Sketch/Strokes/Group_0/Curves/Marker_0>
          string curvePath = path + kCurvesName + brushName;

          var geomPool = brushMeshPayload.geometry;
          var strokes = brushMeshPayload.strokes;
          var mat44 = Matrix4x4.identity;
          var meshPrimPath = new pxr.SdfPath(meshPath + iBrushMeshPayload.ToString());
          var curvesPrimPath = new pxr.SdfPath(curvePath + iBrushMeshPayload.ToString());

          //
          // Geometry
          //
          BrushSample brushSample = GetBrushSample(geomPool, strokes, mat44);

          // Write the BrushSample to the same point in the scenegraph at which it exists in Tilt
          // Brush. Notice this method is Async, it is queued to a background thread to perform I/O
          // which means it is not safe to read from the scene until WaitForWrites() is called.
          scene.Write(meshPrimPath, brushSample);

          //
          // Stroke Curves
          //
          var curvesSample = GetCurvesSample(payload, strokes, Matrix4x4.identity);
          scene.Write(curvesPrimPath, curvesSample);

          //
          // Materials
          //
          double? oldTime = scene.Time;
          scene.Time = null;

          string materialPath = CreateMaterialNetwork(
              scene,
              brushMeshPayload.exportableMaterial,
              GetStrokesPath());

          BindMaterial(scene, meshPrimPath.ToString(), materialPath);
          BindMaterial(scene, curvesPrimPath.ToString(), materialPath);

          scene.Time = oldTime;
        }
      }

      //
      // Models
      //

      var knownModels = new Dictionary<Model, string>();

      int iModelMeshPayload = -1;
      foreach (var modelMeshPayload in payload.modelMeshes) {
        ++iModelMeshPayload;
        var modelId = modelMeshPayload.modelId;
        var modelNamePrefix = "/Model_"
                      + SanitizeIdentifier(modelMeshPayload.model.GetExportName())
                      + "_";
        var modelName = modelNamePrefix + modelId;

        var xf = modelMeshPayload.xform;
        // Geometry pools may be repeated and should be turned into references.
        var geomPool = modelMeshPayload.geometry;

        var modelRootPath = new pxr.SdfPath(GetModelsPath() + modelName);

        // Example: </Sketch/Models/Model_Andy_0>
        CreateXform(scene, modelRootPath, xf);

        // Example: </Sketch/Models/Model_Andy_0/Geom>
        CreateXform(scene, modelRootPath + kGeomName);

        string modelPathToReference;
        if (knownModels.TryGetValue(modelMeshPayload.model, out modelPathToReference)
            && modelPathToReference != modelRootPath) {
          // Create an Xform, note that the world transform here will override the referenced model.
          var meshXf = new MeshXformSample();
          meshXf.transform = xf;
          scene.Write(modelRootPath, meshXf);
          // Add a USD reference to previously created model.
          var prim = scene.Stage.GetPrimAtPath(modelRootPath);
          prim.GetReferences().AddReference("", new pxr.SdfPath(modelPathToReference));
          continue;
        }

        // Example: </Sketch/Models/Geom/Model_Andy_0/Mesh_0>
        path = modelRootPath + kGeomName + "/Mesh_" + iModelMeshPayload.ToString();

        var meshPrimPath = new pxr.SdfPath(path);
        var meshSample = new MeshSample();

        GetMeshSample(geomPool, Matrix4x4.identity, meshSample);
        scene.Write(path, meshSample);
        scene.Stage.GetPrimAtPath(new pxr.SdfPath(path)).SetInstanceable(true);

        //
        // Materials
        //

        // Author at default time.
        double? oldTime = scene.Time;
        scene.Time = null;

        // Model materials must live under the model root, since we will reference the model.
        string materialPath = CreateMaterialNetwork(
            scene,
            modelMeshPayload.exportableMaterial,
            modelRootPath);
        BindMaterial(scene, meshPrimPath.ToString(), materialPath);

        // Continue authoring at the desired time index.
        scene.Time = oldTime;

        //
        // Setup to be referenced.
        //
        if (!knownModels.ContainsKey(modelMeshPayload.model)) {
          knownModels.Add(modelMeshPayload.model, modelRootPath);
        }
      }
    } catch {
      scene.Save();
      scene.Close();
      throw;
    }

    // Save will force a sync with all async reads and writes.
    scene.Save();
    scene.Close();
  }
}
} // namespace TiltBrush
#endif
