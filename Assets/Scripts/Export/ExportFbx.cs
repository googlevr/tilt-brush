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

#if FBX_SUPPORTED
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using UnityEngine;

using Autodesk.Fbx;

using static TiltBrush.ExportUtils;

namespace TiltBrush {

// Our old wrappers used an explicit FbxString; Unity's uses System.String
// instead of exposing FbxString. This papers over the difference.
class FbxString {
  private string m_s;
  public FbxString(string s) { m_s = s; }

  public static implicit operator string(FbxString fs) {
    return fs.m_s;
  }
}

// Analagous to GlTF_Globals -- this is global export state needed by most functions.
class FbxExportGlobals : IDisposable {
  public readonly string m_outputFile;
  public readonly string m_outputDir;
  public readonly string m_sanitizedFileName;
  public FbxManager m_manager;
  public FbxIOSettings m_ioSettings;
  public FbxExporter m_exporter;
  public FbxScene m_scene;

  private Dictionary<GeometryPool, FbxMesh> m_createdMeshes =
      new Dictionary<GeometryPool, FbxMesh>();
  private Dictionary<IExportableMaterial, FbxSurfaceMaterial> m_createdMaterials =
      new Dictionary<IExportableMaterial, FbxSurfaceMaterial>();
  private HashSet<string> m_createdMaterialNames = new HashSet<string>();
  public ExportFileReference.DisambiguationContext m_disambiguationContext =
      new ExportFileReference.DisambiguationContext();

  public FbxExportGlobals(string outputFile) {
    m_outputFile = outputFile;
    m_sanitizedFileName = System.Text.RegularExpressions.Regex.Replace(
        Path.GetFileNameWithoutExtension(m_outputFile),
        @"[^a-zA-Z0-9_]", "_");
    m_outputDir = Path.GetDirectoryName(outputFile);
    m_manager = FbxManager.Create();
    m_ioSettings = FbxIOSettings.Create(m_manager, Globals.IOSROOT);
    m_manager.SetIOSettings(m_ioSettings);
    m_exporter = FbxExporter.Create(m_manager, "");
  }

  public void Dispose() {
    if (m_exporter != null) { m_exporter.Destroy(); m_exporter = null; }
    if (m_ioSettings != null) { m_ioSettings.Destroy(); m_ioSettings = null; }
    if (m_manager != null) { m_manager.Destroy(); m_manager = null; }
  }

  /// Memoized version of CreateFbxMesh
  public FbxMesh GetOrCreateFbxMesh(ExportUtils.BaseMeshPayload payload) {
    if (m_createdMeshes.TryGetValue(payload.geometry, out FbxMesh mesh)) {
      return mesh;
    } else {
      // nb: this name is ignored by Unity, which names meshes after one of the nodes
      // which uses that mesh.
      FbxMesh newMesh = ExportFbx.CreateFbxMesh(this, payload.geometry, payload.geometryName);
      m_createdMeshes[payload.geometry] = newMesh;
      return newMesh;
    }
  }

  /// Memoized version of CreateFbxMaterial
  /// Guarantees 1:1 correspondence between IEM, FbxMaterial, and FbxMaterial.name
  public FbxSurfaceMaterial GetOrCreateFbxMaterial(
      string meshNamespace,
      IExportableMaterial exportableMaterial) {
    // Unity's able to ensure a 1:1 correspondence between FBX materials and generated Unity
    // materials. However, users like TBT who go through the OnAssignMaterialModel interface cannot
    // distinguish between "two unique materials with the same name" and "one material being
    // used multiple times".
    //
    // Since TBT can't detect reference-equality of FbxMaterial, we have to help it by
    // making name-equality the same as reference-equality. IOW distinct materials need
    // distinct names.
    if (m_createdMaterials.TryGetValue(exportableMaterial, out FbxSurfaceMaterial mtl)) {
      return mtl;
    } else {
      FbxSurfaceMaterial newMtl = ExportFbx.CreateFbxMaterial(
          this, meshNamespace, exportableMaterial, m_createdMaterialNames);
      m_createdMaterials[exportableMaterial] = newMtl;
      return newMtl;
    }
  }
}

public static class ExportFbx {
  private const string kRelativeTextureDir = "";

  public const string kFbxBinary = "FBX binary (*.fbx)";
  public const string kFbxAscii = "FBX ascii (*.fbx)";
  public const string kObj = "Alias OBJ (*.obj)";

  public static Matrix4x4 FbxFromUnity { get; private set; }
  public static Matrix4x4 UnityFromFbx { get; private set; }

  class ExportMesh {
    public GeometryPool m_pool;
    public List<Color> m_linearColor;

    // If geometry does not contain normals, colors, and/or uvs, dummy values
    // will be added.
    public ExportMesh(GeometryPool pool) {
      m_pool = pool;
      FbxUtils.ApplyFbxTexcoordHack(m_pool);
      m_linearColor = ExportUtils.ConvertToLinearColorspace(m_pool.m_Colors);

      var layout = m_pool.Layout;
      var numVerts = m_pool.m_Vertices.Count;

      // TODO: all this padding code seems super bogus; try to remove.
      if (!layout.bUseNormals) {
        var lst = m_pool.m_Normals;
        lst.SetCount(numVerts);
        for (int i = 0; i < numVerts; ++i) { lst[i] = Vector3.up; }
      }

      if (!layout.bUseColors) {
        var lst = m_linearColor;
        lst.SetCount(numVerts);
        for (int i = 0; i < numVerts; ++i) { lst[i] = Color.white; }
      }
    }
  }

  static ExportFbx() {
    Matrix4x4 fbxFromUnity = Matrix4x4.identity;
    fbxFromUnity.m00 = -1; // Unity assumes .fbx files have a flipped x axis
    FbxFromUnity = fbxFromUnity;
    UnityFromFbx = FbxFromUnity.inverse;
  }

  /// Main entry point
  public static bool Export(string outputFile, string format, string fbxVersion = null) {
    using (var G = new FbxExportGlobals(outputFile)) {
      int fmt = G.m_manager.GetIOPluginRegistry().FindWriterIDByDescription(format);
      if (!G.m_exporter.Initialize(outputFile, fmt, G.m_ioSettings)) {
        OutputWindowScript.Error("FBX export failed", "Could not initialize exporter");
        return false;
      }
      if (!String.IsNullOrEmpty(fbxVersion)) {
        G.m_exporter.SetFileExportVersion(new FbxString(fbxVersion));
      }

      G.m_scene = FbxScene.Create(G.m_manager, "scene");
      if (G.m_scene == null) {
        OutputWindowScript.Error("FBX export failed", "Could not initialize scene");
        return false;
      }

      String version = string.Format("{0}.{1}", App.Config.m_VersionNumber,
                                     App.Config.m_BuildStamp);
      FbxDocumentInfo info = FbxDocumentInfo.Create(G.m_manager, "DocInfo");
      info.Original_ApplicationVendor.Set(new FbxString(App.kDisplayVendorName));
      info.Original_ApplicationName.Set(new FbxString(App.kAppDisplayName));
      info.Original_ApplicationVersion.Set(new FbxString(version));
      info.LastSaved_ApplicationVendor.Set(new FbxString(App.kDisplayVendorName));
      info.LastSaved_ApplicationName.Set(new FbxString(App.kAppDisplayName));
      info.LastSaved_ApplicationVersion.Set(new FbxString(version));
      // The toolkit's FBX parser is too simple to be able to read anything but
      // the UserData/Properties70 node, so add the extra info as a custom property
      var stringType = info.Original_ApplicationVersion.GetPropertyDataType();
      var prop = FbxProperty.Create(info.Original, stringType, "RequiredToolkitVersion");
      prop.SetString(FbxUtils.kRequiredToolkitVersion);

      G.m_scene.SetDocumentInfo(info);
      G.m_scene.GetGlobalSettings().SetSystemUnit(FbxSystemUnit.m);

      try {
        WriteObjectsAndConnections2(G);
        G.m_exporter.Export(G.m_scene);
      } catch (InvalidOperationException e) {
        OutputWindowScript.Error("FBX export failed", e.Message);
        return false;
      } catch (IOException e) {
        OutputWindowScript.Error("FBX export failed", e.Message);
        return false;
      }
      return true;
    }
  }

  // This writes out payload.xform
  static FbxNode ExportMeshPayload_Global(
      FbxExportGlobals G, BaseMeshPayload payload, FbxNode parentNode) {
    // If these aren't unique, either FBX or Unity will uniquify them for us -- not sure which.
    // So roll payload.id into the name.
    FbxNode fbxNode = FbxNode.Create(G.m_manager, payload.nodeName);
    fbxNode.SetLocalTransform(payload.xform);
    fbxNode.SetNodeAttribute(G.GetOrCreateFbxMesh(payload));
    fbxNode.AddMaterial(G.GetOrCreateFbxMaterial(
                            payload.MeshNamespace, payload.exportableMaterial));

    parentNode.AddChild(fbxNode);
    return fbxNode;
  }

  // This writes out the local xform, and requires that parentNode use payload.instanceXform
  static void ExportMeshPayload_Local(
      FbxExportGlobals G, ModelMeshPayload payload, FbxNode parentNode) {
    // If these aren't unique, either FBX or Unity will uniquify them for us -- not sure which.
    // So roll payload.id into the name.
    FbxNode fbxNode = FbxNode.Create(G.m_manager, payload.nodeName);
    fbxNode.SetLocalTransform(payload.localXform);
    fbxNode.SetNodeAttribute(G.GetOrCreateFbxMesh(payload));
    fbxNode.AddMaterial(G.GetOrCreateFbxMaterial(
                            payload.MeshNamespace, payload.exportableMaterial));

    parentNode.AddChild(fbxNode);
  }

  internal static FbxSurfaceMaterial CreateFbxMaterial(
      FbxExportGlobals G, string meshNamespace, IExportableMaterial exportableMaterial,
      HashSet<string> createdMaterialNames) {
    string materialName;
    if (exportableMaterial is BrushDescriptor) {
      // Toolkit uses this guid (in "N" format) to look up a BrushDescriptor.
      // See Toolkit's ModelImportSettings.GetDescriptorForStroke
      materialName = $"{exportableMaterial.UniqueName:N}_{meshNamespace}_{exportableMaterial.DurableName}";
    } else if (exportableMaterial is DynamicExportableMaterial dem) {
      // Comes from {fbx,obj,gltf,...} import from {Poly, Media Library}
      // This is a customized version of a BrushDescriptor -- almost certainly
      // Pbr{Blend,Opaque}{Double,Single}Sided with maybe an added texture and
      // some of its params customized.
      // TBT will merge the material created by Unity for this FbxMaterial with
      // the premade material it has for the parent guid.
      materialName = $"{dem.Parent.m_Guid:N}_{meshNamespace}_{dem.DurableName}";
    } else {
      Debug.LogWarning($"Unknown class {exportableMaterial.GetType().Name}");
      materialName = $"{meshNamespace}_{exportableMaterial.DurableName}";
    }
    // If only ExportFbx were a non-static class we could merge it with FbxExportGlobals
    materialName = ExportUtils.CreateUniqueName(materialName, createdMaterialNames);

    FbxSurfaceLambert material = FbxSurfaceLambert.Create(G.m_scene, materialName);

    material.Ambient.Set(new FbxDouble3(0, 0, 0));
    material.Diffuse.Set(new FbxDouble3(1.0, 1.0, 1.0));
    if (exportableMaterial.EmissiveFactor > 0) {
      material.EmissiveFactor.Set(exportableMaterial.EmissiveFactor);
      material.Emissive.Set(new FbxDouble3(1.0, 1.0, 1.0));
    }
    if (exportableMaterial.BlendMode != ExportableMaterialBlendMode.None) {
      var blendMode = FbxProperty.Create(material, Globals.FbxStringDT, "BlendMode");
      switch (exportableMaterial.BlendMode) {
      case ExportableMaterialBlendMode.AlphaMask:
        blendMode.SetString(new FbxString("AlphaMask"));
        material.TransparencyFactor.Set(0.2);
        break;
      case ExportableMaterialBlendMode.AdditiveBlend:
        blendMode.SetString(new FbxString("AdditiveBlend"));
        break;
      }
    }

    // Export the texture
    if (exportableMaterial.HasExportTexture()) {
      // This is not perfectly unique, but it is good enough for fbx export
      // better would be to use <durable>_<guid> but that's ugly, and nobody uses
      // the textures anyway, so... let's leave well enough alone for now.
      string albedoTextureName = exportableMaterial.DurableName;
      var fullTextureDir = Path.Combine(G.m_outputDir, kRelativeTextureDir);
      if (!Directory.Exists(fullTextureDir)) {
        if (!FileUtils.InitializeDirectoryWithUserError(fullTextureDir)) {
          throw new IOException("Cannot write textures");
        }
      }
      string src = exportableMaterial.GetExportTextureFilename();
      var textureFileName = albedoTextureName + ".png";
      var textureFilePath = Path.Combine(fullTextureDir, textureFileName);
      FileInfo srcInfo = new FileInfo(src);
      if (srcInfo.Exists && !new FileInfo(textureFilePath).Exists) {
        srcInfo.CopyTo(textureFilePath);
      }

      FbxFileTexture texture = FbxFileTexture.Create(G.m_scene, albedoTextureName + "_texture");
      texture.SetFileName(textureFilePath);
      texture.SetTextureUse(FbxTexture.ETextureUse.eStandard);
      texture.SetMappingType(FbxTexture.EMappingType.eUV);
      texture.SetMaterialUse(FbxFileTexture.EMaterialUse.eModelMaterial);
      texture.UVSet.Set(new FbxString("uv0"));
      material.Diffuse.ConnectSrcObject(texture);
      material.TransparentColor.ConnectSrcObject(texture);
    } else {
      foreach (var kvp in exportableMaterial.TextureUris) {
        string parameterName = kvp.Key;
        string textureUri = kvp.Value;
        if (ExportFileReference.IsHttp(textureUri)) {
          // fbx can't deal with http references to textures
          continue;
        }
        ExportFileReference fileRef = ExportFileReference.GetOrCreateSafeLocal(
            G.m_disambiguationContext, textureUri, exportableMaterial.UriBase,
            $"{meshNamespace}_{Path.GetFileName(textureUri)}");
        AddTextureToMaterial(G, fileRef, material, parameterName);
      }
    }
    return material;
  }

  // Pass:
  //   parameterName -
  //      used for the FbxTexture name. Can be something arbitrary since as far as I know,
  //      the texture node's name is unused by importers.
  private static void AddTextureToMaterial(
      FbxExportGlobals G,
      ExportFileReference fileRef,
      FbxSurfaceLambert fbxMaterial,
      string parameterName) {
    Debug.Assert(File.Exists(fileRef.m_originalLocation));

    var destPath = Path.Combine(G.m_outputDir, fileRef.m_uri);
    if (!File.Exists(destPath)) {
      if (!FileUtils.InitializeDirectoryWithUserError(Path.GetDirectoryName(destPath))) {
        return;
      }
      File.Copy(fileRef.m_originalLocation, destPath);
    }

    // It's kind of weird that the parameter name is used for the texture node's name,
    // but as far as I can tell nobody cares about that name, so whatever.
    FbxFileTexture fbxTexture = FbxFileTexture.Create(G.m_scene, parameterName);
    fbxTexture.SetFileName(destPath);
    fbxTexture.SetTextureUse(FbxTexture.ETextureUse.eStandard);
    fbxTexture.SetMappingType(FbxTexture.EMappingType.eUV);
    fbxTexture.SetMaterialUse(FbxFileTexture.EMaterialUse.eModelMaterial);
    fbxTexture.UVSet.Set(new FbxString("uv0"));
    // It's also weird that we only ever assign to the Diffuse slot.
    // Shouldn't we be looking at the parameter name and assigning to Diffuse, Normal, etc
    // based on what we see?
    // TODO: check
    fbxMaterial.Diffuse.ConnectSrcObject(fbxTexture);
    fbxMaterial.TransparentColor.ConnectSrcObject(fbxTexture);
  }

  // Returns the root, or a node right under the root.
  // Either way, the transform stack is guaranteed to be identity.
  static FbxNode GetGroupNode(FbxManager manager, FbxScene scene, UInt32 group) {
    FbxNode root = scene.GetRootNode();
    if (group == 0) {
      return root;
    }
    string childName = $"group_{group}";
    FbxNode child = root.FindChild(childName, false);
    if (child == null) {
      child = FbxNode.Create(manager, childName);
      root.AddChild(child);
    }
    return child;
  }

  static void SetLocalTransform(this FbxNode node, TrTransform xf) {
    node.LclTranslation.Set(new FbxDouble3(xf.translation.x, xf.translation.y, xf.translation.z));
    node.LclRotation.Set(XYZEulerFromQuaternion(xf.rotation));
    node.LclScaling.Set(new FbxDouble3(xf.scale, xf.scale, xf.scale));
  }

  static void SetLocalTransform(this FbxNode node, Matrix4x4 xf) {
    node.SetLocalTransform(TrTransform.FromMatrix4x4(xf));
  }

  // outputFile must be a full path.
  static void WriteObjectsAndConnections2(FbxExportGlobals G) {
    var payload = ExportCollector.GetExportPayload(
        AxisConvention.kFbxAccordingToUnity,
        includeLocalMediaContent: true);

    // Write out each brush entry's geometry.
    foreach (var brushMeshPayload in payload.groups.SelectMany(g => g.brushMeshes)) {
      // This code used to not set the transform; check that it didn't cause issues
      Debug.Assert(brushMeshPayload.xform.isIdentity);
      FbxNode parentNode = GetGroupNode(G.m_manager, G.m_scene, brushMeshPayload.group);
      ExportMeshPayload_Global(G, brushMeshPayload, parentNode);
    }

    // Models with exportable meshes.
    foreach (var sameInstance in payload.modelMeshes.GroupBy(m => (m.model, m.modelId))) {
      var modelMeshPayloads = sameInstance.ToList();
      if (modelMeshPayloads.Count == 0) { continue; }

      // All of these pieces will come from the same Widget and therefore will have
      // the same group id, root transform, etc
      var first = modelMeshPayloads[0];
      FbxNode parentParentNode = GetGroupNode(G.m_manager, G.m_scene, first.group);
      string rootNodeName = $"model_{first.model.GetExportName()}_{first.modelId}";
      if (modelMeshPayloads.Count == 1 && first.localXform.isIdentity) {
        // Condense the two nodes into one; give the top-level node the same name
        // it would have had had it been multi-level.
        FbxNode newNode = ExportMeshPayload_Global(G, first, parentParentNode);
        newNode.SetName(rootNodeName);
      } else {
        FbxNode parentNode = FbxNode.Create(G.m_manager, rootNodeName);
        parentNode.SetLocalTransform(first.parentXform);
        parentParentNode.AddChild(parentNode);
        foreach (var modelMeshPayload in modelMeshPayloads) {
          ExportMeshPayload_Local(G, modelMeshPayload, parentNode);
        }
      }
    }

    foreach (ImageQuadPayload meshPayload in payload.imageQuads) {
      FbxNode groupNode = GetGroupNode(G.m_manager, G.m_scene, meshPayload.group);
      ExportMeshPayload_Global(G, meshPayload, groupNode);
    }

    // Things that can only be exported as transforms (videos, images, models, ...)
    foreach (var referenceThing in payload.referenceThings) {
      FbxNode node = FbxNode.Create(G.m_manager, "reference_" + referenceThing.name);
      node.SetLocalTransform(referenceThing.xform);
      GetGroupNode(G.m_manager, G.m_scene, referenceThing.group).AddChild(node);
    }
  }

  public static FbxDouble3 XYZEulerFromQuaternion(Quaternion rotation) {
    // Unity wrappers don't have FbxVector4::SetXYZ(FbxQuaternion) to convert quat -> xyz euler.
    // We can abuse FbxAMatrix to do the same thing.

    FbxQuaternion fbxQuaternion = new FbxQuaternion(rotation.x, rotation.y, rotation.z, rotation.w);
    // We're using the FBX API to do the conversion to XYZ eulers.
    var mat = new FbxAMatrix();
    mat.SetIdentity();
    mat.SetQ(fbxQuaternion);
    // "The returned rotation vector is in Euler angle and the rotation order is XYZ."
    FbxVector4 v4 = mat.GetR();
    return new FbxDouble3(v4.X, v4.Y, v4.Z);
  }

  // For use only by FbxExportGlobals.
  internal static FbxMesh CreateFbxMesh(FbxExportGlobals G, GeometryPool pool, string poolName) {
    FbxMesh fbxMesh = FbxMesh.Create(G.m_manager, poolName);

    ExportMesh mesh = new ExportMesh(pool);
    int nVerts = mesh.m_pool.m_Vertices.Count;
    fbxMesh.InitControlPoints(nVerts);

    unsafe {
      fixed (Vector3* f = mesh.m_pool.m_Vertices.GetBackingArray()) {
        Globals.SetControlPoints(fbxMesh, (IntPtr)f);
      }
    }

    List<int> triangles = mesh.m_pool.m_Tris;
    // Not available in Unity's wrappers
    // fbxMesh.ReservePolygonCount(triangles.Count / 3);
    // fbxMesh.ReservePolygonVertexCount(triangles.Count);
    for (int i = 0; i < triangles.Count; i += 3) {
      fbxMesh.BeginPolygon(-1 /* Material */, -1 /* Texture */, -1 /* Group */, false /* Legacy */);
      fbxMesh.AddPolygon(triangles[i]);
      fbxMesh.AddPolygon(triangles[i+1]);
      fbxMesh.AddPolygon(triangles[i+2]);
      fbxMesh.EndPolygon();
    }

    FbxLayer layer0 = fbxMesh.GetLayer(0);
    if (layer0 == null) {
      fbxMesh.CreateLayer();
      layer0 = fbxMesh.GetLayer(0);
    }

    var layerElementNormal = FbxLayerElementNormal.Create(fbxMesh, "normals");
    layerElementNormal.SetMappingMode(FbxLayerElement.EMappingMode.eByControlPoint);
    layerElementNormal.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);
    CopyToFbx(layerElementNormal.GetDirectArray(), mesh.m_pool.m_Normals);
    layer0.SetNormals(layerElementNormal);

    var layerElementColor = FbxLayerElementVertexColor.Create(fbxMesh, "color");
    layerElementColor.SetMappingMode(FbxLayerElement.EMappingMode.eByControlPoint);
    layerElementColor.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);
    CopyToFbx(layerElementColor.GetDirectArray(), mesh.m_linearColor);
    layer0.SetVertexColors(layerElementColor);

    var layerElementTangent = FbxLayerElementTangent.Create(fbxMesh, "tangents");
    layerElementTangent.SetMappingMode(FbxLayerElement.EMappingMode.eByControlPoint);
    layerElementTangent.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);
    CopyToFbx(layerElementTangent.GetDirectArray(), mesh.m_pool.m_Tangents);
    layer0.SetTangents(layerElementTangent);

    // Compute and export binormals since Unity's FBX importer won't import the tangents without
    // them, even though they're not used.
    var layerElementBinormal = FbxLayerElementBinormal.Create(fbxMesh, "binormals");
    layerElementBinormal.SetMappingMode(FbxLayerElement.EMappingMode.eByControlPoint);
    layerElementBinormal.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);
    var binormals = mesh.m_pool.m_Tangents
        .Select( (tan, idx) => {
          var b3 = Vector3.Cross(tan, mesh.m_pool.m_Normals[idx]) * tan.w;
          return new Vector4(b3.x, b3.y, b3.z, 1);
        } )
        .ToList();
    CopyToFbx(layerElementBinormal.GetDirectArray(), binormals);
    layer0.SetBinormals(layerElementBinormal);

    var layerElementMaterial = FbxLayerElementMaterial.Create(fbxMesh, "materials");
    layerElementMaterial.SetMappingMode(FbxLayerElement.EMappingMode.eAllSame);
    layer0.SetMaterials(layerElementMaterial);

    // Export everything up to the last uvset containing data
    // even if some intermediate uvsets have no data.
    // Otherwise Unity will get the uvset numbering wrong on import

    List<List<Vector2>> uvSets = DemuxTexcoords(mesh.m_pool);
    for (int i = 0; i < uvSets.Count; i++) {
      FbxLayer layerN = fbxMesh.GetLayer(i);
      while (layerN == null) {
        fbxMesh.CreateLayer();
        layerN = fbxMesh.GetLayer(i);
      }
      var layerElementUV = FbxLayerElementUV.Create(fbxMesh, String.Format("uv{0}", i));
      layerElementUV.SetMappingMode(FbxLayerElement.EMappingMode.eByControlPoint);
      layerElementUV.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);

      List<Vector2> uvSet = uvSets[i];
      if (uvSet == null) {
        // Do nothing
        // Replicates what the old fbx export code did; seems to work fine
      } else {
        Debug.Assert(uvSet.Count == nVerts);
        CopyToFbx(layerElementUV.GetDirectArray(), uvSet);
      }

      layerN.SetUVs(layerElementUV, FbxLayerElement.EType.eTextureDiffuse);
    }
    return fbxMesh;
  }

  static void CopyToFbx(FbxLayerElementArrayTemplateFbxColor fbx, List<Color> unity) {
    fbx.SetCount(unity.Count);
    unsafe {
      fixed (void* ptr = unity.GetBackingArray()) {
        Globals.CopyColorToFbxColor(fbx, (IntPtr)ptr);
      }
    }
  }

  static void CopyToFbx(FbxLayerElementArrayTemplateFbxVector2 fbx, List<Vector2> unity) {
    fbx.SetCount(unity.Count);
    unsafe {
      fixed (void* ptr = unity.GetBackingArray()) {
        Globals.CopyVector2ToFbxVector2(fbx, (IntPtr)ptr);
      }
    }
  }

  static void CopyToFbx(FbxLayerElementArrayTemplateFbxVector4 fbx, List<Vector3> unity) {
    fbx.SetCount(unity.Count);
    unsafe {
      fixed (void* ptr = unity.GetBackingArray()) {
        Globals.CopyVector3ToFbxVector4(fbx, (IntPtr)ptr);
      }
    }
  }

  static void CopyToFbx(FbxLayerElementArrayTemplateFbxVector4 fbx, List<Vector4> unity) {
    fbx.SetCount(unity.Count);
    unsafe {
      fixed (void* ptr = unity.GetBackingArray()) {
        Globals.CopyVector4ToFbxVector4(fbx, (IntPtr)ptr);
      }
    }
  }

  // Fbx only supports 2-channel texcoord data; turn (up to) 2 float4s
  // into (up to) 4 float2s.
  //
  // The resulting List will have no nulls at the end, but may have some
  // gaps with missing data.
  static List<List<Vector2>> DemuxTexcoords(GeometryPool pool) {
    var allSets = new List<List<Vector2>>();
    {
      List<Vector2> tmpXy, tmpZw;
      DemuxTexcoord(pool, pool.Layout.texcoord0.size, pool.m_Texcoord0, out tmpXy, out tmpZw);
      allSets.Add(tmpXy);
      allSets.Add(tmpZw);
      DemuxTexcoord(pool, pool.Layout.texcoord1.size, pool.m_Texcoord1, out tmpXy, out tmpZw);
      allSets.Add(tmpXy);
      allSets.Add(tmpZw);
    }
    // Remove unused sets from the end
    while (allSets.Count > 0 && allSets[allSets.Count-1] == null) {
      allSets.RemoveAt(allSets.Count-1);
    }
    return allSets;
  }

  // Copy a 4-channel texcoord into 2 2-channel texcoords.
  // If the source isn't 4-channel, one or both lists may be left empty.
  static void DemuxTexcoord(GeometryPool pool, int uvSize, GeometryPool.TexcoordData texcoordData,
                            out List<Vector2> destXy, out List<Vector2> destZw) {
    destXy = null;
    destZw = null;
    switch (uvSize) {
    case 0:
      break;
    case 2:
      destXy = texcoordData.v2;
      break;
    case 3:
      destXy = new List<Vector2>(texcoordData.v3.Select(v3 => new Vector2(v3.x, v3.y)));
      destZw = new List<Vector2>(texcoordData.v3.Select(v3 => new Vector2(v3.z, 0)));
      break;
    case 4:
      destXy = new List<Vector2>(texcoordData.v4.Select(v4 => new Vector2(v4.x, v4.y)));
      destZw = new List<Vector2>(texcoordData.v4.Select(v4 => new Vector2(v4.z, v4.w)));
      break;
    default:
      Debug.Assert(false);
      break;
    }
  }

}
}  // namespace TiltBrush
#endif