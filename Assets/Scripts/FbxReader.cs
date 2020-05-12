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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Autodesk.Fbx;
using UnityEngine;
using UObject = UnityEngine.Object;
using FbxLayerElementArray_int = Autodesk.Fbx.FbxLayerElementArrayTemplateInt;
using FbxLayerElementArray_FbxVector4 = Autodesk.Fbx.FbxLayerElementArrayTemplateFbxVector4;
using FbxLayerElementArray_FbxVector2 = Autodesk.Fbx.FbxLayerElementArrayTemplateFbxVector2;
namespace TiltBrush {

/**
 * Read models into Unity meshes using the FBX SDK.
 *
 * TODO:
 * - Separate out loading from building unity objects so that it can be multi-threaded.
 */
public class FbxReader {
  public const int MAX_VERTS_PER_MESH = 65534;

  private readonly Material m_standardMaterial;
  private readonly Material m_transparentMaterial;
  private readonly string m_path;  // Full path to file
  private readonly string m_dir;  // directory of file
  private readonly List<string> m_warnings = new List<string>();
  private readonly ImportMaterialCollector m_collector;

  private List<string> warnings => m_warnings;

  public FbxReader(string path) {
    m_standardMaterial = ModelCatalog.m_Instance.m_ObjLoaderStandardMaterial;
    m_transparentMaterial = ModelCatalog.m_Instance.m_ObjLoaderTransparentMaterial;
    m_path = path;
    m_dir = Path.GetDirectoryName(path);
    m_collector = new ImportMaterialCollector(m_dir, m_path);
  }

  public (GameObject, List<string> warnings, ImportMaterialCollector) Import() {
    FbxManager fbxManager = FbxManager.Create();
    FbxIOSettings ioSettings = FbxIOSettings.Create(fbxManager, Globals.IOSROOT);
    fbxManager.SetIOSettings(ioSettings);
    FbxImporter fbxImporter = FbxImporter.Create(fbxManager, "");
    if (!fbxImporter.Initialize(m_path, -1, ioSettings)) {
      warnings.Add("Failed to initialize FBX importer");
      return (null, warnings, null);
    }
    FbxScene scene = FbxScene.Create(fbxManager, "scene");
    fbxImporter.Import(scene);

    FbxNode root = scene.GetRootNode();
    SetPivots(root);
    root.ConvertPivotAnimationRecursive(null, FbxNode.EPivotSet.eDestinationPivot, 30);
    long totalVerts = GetTotalVerts(root);
    long completedVerts = 0;

    float fbxUnitToTiltUnit; {
      var unit = scene.GetGlobalSettings().GetSystemUnit();
      if (Path.GetExtension(m_path).ToLower() == ".obj") {
        // Obj doesn't specify units. We'd rather assume m, but fbx assumes cm.
        unit = FbxSystemUnit.m;
      }
      fbxUnitToTiltUnit = (float)unit.GetConversionFactorTo(FbxSystemUnit.m)
          * App.METERS_TO_UNITS;
    }

    GameObject go = ImportNodes(
        root, fbxUnitToTiltUnit, ref completedVerts, totalVerts);
    Debug.Assert(completedVerts == totalVerts);
    fbxImporter.Destroy();
    ioSettings.Destroy();
    fbxManager.Destroy();

    return (go, warnings.Distinct().ToList(), m_collector);
  }

  private static long GetTotalVerts(FbxNode node) {
    FbxNodeAttribute a = node.GetNodeAttribute();
    int nChildren = node.GetChildCount();
    long vertCount = 0;
    if (a != null) {
      var attrType = a.GetAttributeType();
      if (attrType == FbxNodeAttribute.EType.eMesh) {
        FbxMesh fbxMesh = node.GetMesh();
        fbxMesh.SplitPoints();
        vertCount = fbxMesh.GetControlPointsCount();
      }
    }
    for (int i = 0; i < nChildren; i++) {
      vertCount += GetTotalVerts(node.GetChild(i));
    }
    return vertCount;
  }

  private GameObject ImportNodes(
      FbxNode node, float fbxUnitToTiltUnit,
      ref long completedVerts, long totalVerts) {
    FbxNodeAttribute a = node.GetNodeAttribute();
    int nChildren = node.GetChildCount();

    GameObject go = null;
    if (a != null) {
      var attrType = a.GetAttributeType();
      if (attrType == FbxNodeAttribute.EType.eMesh) {
        go = ImportMesh(
            node, fbxUnitToTiltUnit,
            ref completedVerts, totalVerts);
      } else if (attrType == FbxNodeAttribute.EType.eNurbs ||
        attrType == FbxNodeAttribute.EType.eSubDiv ||
        attrType == FbxNodeAttribute.EType.eNurbsSurface) {
        warnings.Add("Ignoring non-mesh geometry");
      }
    }

    for (int i = 0; i < nChildren; i++) {
      GameObject child = ImportNodes(
          node.GetChild(i), fbxUnitToTiltUnit,
          ref completedVerts, totalVerts);
      if (child != null) {
        if (go == null) {
          go = new GameObject("Node");
        }
        child.transform.parent = go.transform;
      }
    }

    if (go != null) {
      ApplyTransform(node, go.transform, fbxUnitToTiltUnit);
    }
    return go;
  }

  // Currently this always creates a new material, but
  // TODO: Cache materials for reuse across meshes
  (Material mat, float alpha) CreateMaterial(FbxSurfaceMaterial fbxMaterial) {
    FbxSurfaceLambert l = FbxSurfaceLambert.fromMaterial(fbxMaterial);
    // Watch out for corrupt or unknown materials
    if (l == null) {
      return (UObject.Instantiate(m_standardMaterial), 1f);
    }

    // We only use the red channel from the transparency color
    Vector3 tc = l.TransparentColor.Get().ToUVector3();
    float alpha = 1 - tc.x * (float)l.TransparencyFactor.Get();
    Vector3 diffuse = l.Diffuse.Get().ToUVector3();
    diffuse *= (float)l.DiffuseFactor.Get();
    Vector3 emission = l.Emissive.Get().ToUVector3();
    emission *= (float)l.EmissiveFactor.Get();
    bool transparent = (alpha < 1);

    Material mat = UObject.Instantiate(transparent ? m_transparentMaterial : m_standardMaterial);
    mat.SetColor("_Color", new Color(diffuse.x, diffuse.y, diffuse.z, Mathf.Min(1f, alpha)));
    mat.SetColor("_EmissionColor", new Color(emission.x, emission.y, emission.z));

    string baseColorUri = null;
    if (l.Diffuse.GetSrcObjectCount() > 0) {
      baseColorUri = LoadTexture(l.Diffuse, mat, "_MainTex", true);
    }
    if (l.NormalMap.GetSrcObjectCount() > 0) {
      LoadTexture(l.NormalMap, mat, "_BumpMap", false);
      mat.EnableKeyword("_NORMALMAP");
    }

    m_collector.Add(mat, transparent, baseColorUri, l);

    return (mat, alpha);
  }

  private GameObject ImportMesh(
      FbxNode node, float fbxUnitToTiltUnit,
      ref long completedVerts, long totalVerts) {
    FbxMesh fbxMesh = node.GetMesh();

    Debug.Assert(totalVerts != 0);
    // fbxMesh.SplitPoints();

    // fbxMesh must call SplitPoints before this. A non-zero total vert count is a proxy for
    // fbxMesh.SplitPoints() having previously been called through GetTotalVerts()
    int nVerts = fbxMesh.GetControlPointsCount();

    fbxMesh.ComputeBBox();

    // Note that converting coordinate systems may cause some of the "max" components
    // to actually be min, and vice versa. Thus we simply encapsulate the 2 points.
    Bounds b; {
      Vector3 va = ToUnityPosition(fbxMesh.BBoxMin.Get()) * fbxUnitToTiltUnit;
      Vector3 vb = ToUnityPosition(fbxMesh.BBoxMax.Get()) * fbxUnitToTiltUnit;
      b = new Bounds(va, Vector3.zero);
      b.Encapsulate(vb);
    }

    Vector3[] verts = new Vector3[nVerts];
    unsafe {
      fixed (Vector3* f = verts) {
        Globals.GetControlPoints(fbxMesh, (IntPtr)f);
      }
    }
    for (int i = 0; i < nVerts; i++) {
      verts[i] = ExportFbx.UnityFromFbx.MultiplyVector(verts[i]) * fbxUnitToTiltUnit;
    }

    Vector3[] normals = null;
    bool degenerateNormals = false;
    Vector2[] uvs = null;
    FbxLayerElementArray_int materialIndices = null;

    if (fbxMesh.GetLayerCount() > 0) {
      FbxLayer layer = fbxMesh.GetLayer(0);

      // Normals
      FbxLayerElementNormal layerElementNormal = layer.GetNormals();
      if (layerElementNormal != null) {
        if (layerElementNormal.GetMappingMode() == FbxLayerElement.EMappingMode.eByControlPoint) {
          FbxLayerElementArray_FbxVector4 fbxNormals = layerElementNormal.GetDirectArray();
          int n = fbxNormals.GetCount();
          normals = new Vector3[n];
          unsafe {
            fixed(Vector3* f = normals) {
              Globals.CopyFbxVector4ToVector3(fbxNormals, (IntPtr)f);
            }
          }
          for (int i = 0; i < n; i++) {
            // Models from Blocks can have zero'd normals; in this case we ignore them and have
            // Unity compute them.
            if (normals[i] == Vector3.zero) {
              degenerateNormals = true;
              break;
            }
            normals[i] = ExportFbx.UnityFromFbx.MultiplyVector(normals[i]);
          }
          if (!degenerateNormals &&
              layerElementNormal.GetReferenceMode()
              == FbxLayerElement.EReferenceMode.eIndexToDirect) {
            Vector3[] vNormals = new Vector3[nVerts];
            FbxLayerElementArray_int indices = layerElementNormal.GetIndexArray();
            for (int j = 0; j < nVerts; j++) {
              int i = indices.GetAt(j);
              if (i < 0 || i > normals.Length) {
                Debug.Log("Bad FBX normals index");
                continue;
              }
              vNormals[j] = normals[i];
            }
            normals = vNormals;
          }
        } else {
          warnings.Add("Can only import per-vertex normals");
        }
      }

      // UVs
      FbxLayerElementUV layerElementUV = layer.GetUVs();
      if (layerElementUV != null) {
        if (layerElementUV.GetMappingMode() == FbxLayerElement.EMappingMode.eByControlPoint) {
          FbxLayerElementArray_FbxVector2 uv = layerElementUV.GetDirectArray();
          int nUVs = uv.GetCount();
          uvs = new Vector2[nUVs];
          unsafe {
            fixed(Vector2 *f = uvs) {
              Globals.CopyFbxVector2ToVector2(uv, (IntPtr)f);
            }
          }
          if (layerElementUV.GetReferenceMode() == FbxLayerElement.EReferenceMode.eIndexToDirect) {
            Vector2[] vUvs = new Vector2[nVerts];
            FbxLayerElementArray_int indices = layerElementUV.GetIndexArray();
            for (int j = 0; j < nVerts; j++) {
              int i = indices.GetAt(j);
              if (i < 0 || i > uvs.Length) {
                Debug.Log("Bad FBX UVs index");
              }
              vUvs[j] = uvs[i];
            }
            uvs = vUvs;
          }
        }
      }

      FbxLayerElementMaterial layerElementMaterial = layer.GetMaterials();
      if (layerElementMaterial != null
        && layerElementMaterial.GetMappingMode() == FbxLayerElement.EMappingMode.eByPolygon) {
        materialIndices = layerElementMaterial.GetIndexArray();
      }

      // Warn about layers we don't support -- requires sdk support for holes
      // if (layer.GetHole() != null) {
      //   warnings.Add("Ignoring unsupported holes");
      // }
    }

    // Materials
    List<Material> mats = new List<Material>();
    bool allTransparent = true;
    for (int i = 0; i < node.GetMaterialCount(); i++) {
      var (mat, alpha) = CreateMaterial(node.GetMaterial(i));
      mats.Add(mat);
      allTransparent &= (alpha == 0);
    }


    if (allTransparent) {
      warnings.Add("All textures are transparent\nAll materials set to full opacity");
      foreach (Material m in mats) {
        var matColor = m.GetColor("_Color");
        matColor.a = 1f;
        m.SetColor("_Color", matColor);
      }
    }

    if (mats.Count == 0) {
      // Default material
      mats.Add(UnityEngine.Object.Instantiate(m_standardMaterial));
    }

    // Polygons
    List<int>[] triangles = new List<int>[mats.Count];
    for (int i = 0; i < triangles.Length; i++) {
      triangles[i] = new List<int>();
    }
    int nPolys = fbxMesh.GetPolygonCount();
    for (int i = 0; i < nPolys; i++) {
      int submesh = 0;
      if (materialIndices != null) {
        submesh = materialIndices.GetAt(i);
        if (submesh > mats.Count) {
          Debug.Log("Bad FBX material index");
          continue;
        }
      }
      int polySize = fbxMesh.GetPolygonSize(i);
      int i0 = fbxMesh.GetPolygonVertex(i, 0);
      // Convert to triangle fans
      for (int j = 2; j < polySize; j++) {
        triangles[submesh].Add(i0);
        triangles[submesh].Add(fbxMesh.GetPolygonVertex(i, j));
        triangles[submesh].Add(fbxMesh.GetPolygonVertex(i, j - 1));
      }
    }

    // Create meshes
    GameObject parent = new GameObject("Mesh");

    if (nVerts <= MAX_VERTS_PER_MESH) {
      Mesh mesh = new Mesh();
      mesh.vertices = verts;
      if (uvs != null) {
        mesh.uv = uvs;
      }
      mesh.subMeshCount = triangles.Length;
      for (int i = 0; i < triangles.Length; i++) {
        mesh.SetTriangles(triangles[i], i);
      }
      if (!degenerateNormals && normals != null) {
        mesh.normals = normals;
      } else {
        mesh.RecalculateNormals();
      }
      mesh.bounds = b;
      parent.AddComponent<MeshFilter>().mesh = mesh;
      parent.AddComponent<MeshRenderer>().materials = mats.ToArray();
      if (mats.All(m => m.GetColor("_Color").a < 1)) {
        parent.GetComponent<MeshRenderer>().shadowCastingMode =
          UnityEngine.Rendering.ShadowCastingMode.Off;
      }
    } else {
      // Split into subobjects by material, and then split those into multiple meshes if required.
      List<Vector3> v = new List<Vector3>();
      List<Vector3> n = new List<Vector3>();
      List<Vector2> uv = new List<Vector2>();
      List<int> t = new List<int>();
      Dictionary<int, int> map = new Dictionary<int,int>();

      int iTri = 0;
      int nTris = triangles.Aggregate(0, (sum, elt) => sum + elt.Count);

      for (int i = 0; i < mats.Count; i++) {
        for (int j = 0; j < triangles[i].Count; j += 3) {
          if (v.Count > MAX_VERTS_PER_MESH - 3) {
            CreateSubmesh(v, n, uv, t, mats[i]).transform.parent = parent.transform;
            v.Clear();
            n.Clear();
            uv.Clear();
            t.Clear();
            map.Clear();

            float delta = ((float)(j + iTri) / nTris) * ((float)nVerts / totalVerts);
            OverlayManager.m_Instance.UpdateProgress((float)completedVerts / totalVerts + delta);
          }
          // map it
          for (int k = 0; k < 3; k++) {
            int p = triangles[i][j + k];
            int index;
            if (map.ContainsKey(p)) {
              index = map[p];
            } else {
              if (p > verts.Length) {
                Debug.Log("Bad triangle index building FBX submesh");
                continue;
              }
              v.Add(verts[p]);
              if (normals != null) {
                n.Add(normals[p]);
              }
              if (uvs != null) {
                uv.Add(uvs[p]);
              }
              index = v.Count - 1;
              map.Add(p, index);
            }
            t.Add(index);
          }
        }
        // Make object
        CreateSubmesh(v, n, uv, t, mats[i]).transform.parent = parent.transform;
        v.Clear();
        n.Clear();
        uv.Clear();
        t.Clear();
        map.Clear();

        iTri += triangles[i].Count;
        float progressDelta = (float)iTri / nTris * (float)nVerts / totalVerts;
        OverlayManager.m_Instance.UpdateProgress((float)completedVerts / totalVerts + progressDelta);
      }
    }
    completedVerts += nVerts;
    OverlayManager.m_Instance.UpdateProgress((float)completedVerts / totalVerts);

    var bc = parent.gameObject.AddComponent<BoxCollider>();
    bc.center = b.center;
    bc.size = b.size;
    return parent;
  }

  static private void ApplyTransform(FbxNode node, Transform transform, float fbxUnitToTiltUnit) {
    FbxAMatrix am =
      node.EvaluateLocalTransform(new FbxTime(), FbxNode.EPivotSet.eDestinationPivot);

    Vector3 unityTranslation, unityScale;
    Quaternion unityRotation;
    {
      Vector3 fbxTranslation, fbxScale;
      Quaternion fbxRotation;
      am.ToTRS(out fbxTranslation, out fbxRotation, out fbxScale);
      ExportUtils.ChangeBasis(fbxTranslation, fbxRotation, fbxScale,
                              out unityTranslation, out unityRotation, out unityScale,
                              ExportFbx.UnityFromFbx, ExportFbx.FbxFromUnity);
      unityTranslation *= fbxUnitToTiltUnit;
    }
    transform.localPosition = unityTranslation;
    transform.localScale    = unityScale;
    transform.localRotation = unityRotation;
  }

  static private GameObject CreateSubmesh(List<Vector3> v, List<Vector3> n, List<Vector2> uv,
    List<int> t, Material m) {
    GameObject go = new GameObject("Submesh", typeof(MeshFilter), typeof(MeshRenderer));
    Mesh mesh = new Mesh();
    mesh.vertices = v.ToArray();
    mesh.triangles = t.ToArray();
    if (uv.Count > 0) {
      mesh.uv = uv.ToArray();
    }
    if (n.Count > 0) {
      mesh.normals = n.ToArray();
    } else {
      mesh.RecalculateNormals();
    }
    go.GetComponent<MeshFilter>().mesh = mesh;
    go.GetComponent<MeshRenderer>().material = m;
    if (m.GetColor("_Color").a < 1) {
      go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    return go;
  }

  static private void SetPivots(FbxNode node) {
    node.SetPivotState(FbxNode.EPivotSet.eSourcePivot, FbxNode.EPivotState.ePivotActive);
    node.SetPivotState(FbxNode.EPivotSet.eDestinationPivot, FbxNode.EPivotState.ePivotActive);

    FbxVector4 zero = new FbxVector4(0, 0, 0);
    FbxVector4 one = new FbxVector4(1, 1, 1);
    node.SetPostRotation(FbxNode.EPivotSet.eDestinationPivot, zero);
    node.SetPreRotation(FbxNode.EPivotSet.eDestinationPivot, zero);
    node.SetRotationOffset(FbxNode.EPivotSet.eDestinationPivot, zero);
    node.SetScalingOffset(FbxNode.EPivotSet.eDestinationPivot, zero);
    node.SetRotationPivot(FbxNode.EPivotSet.eDestinationPivot, zero);
    node.SetScalingPivot(FbxNode.EPivotSet.eDestinationPivot, zero);

    node.SetRotationOrder(FbxNode.EPivotSet.eDestinationPivot, FbxEuler.EOrder.eOrderXYZ);

    node.SetGeometricTranslation(FbxNode.EPivotSet.eDestinationPivot, zero);
    node.SetGeometricRotation(FbxNode.EPivotSet.eDestinationPivot, zero);
    node.SetGeometricScaling(FbxNode.EPivotSet.eDestinationPivot, one);

    node.SetQuaternionInterpolation(FbxNode.EPivotSet.eDestinationPivot,
      node.GetQuaternionInterpolation(FbxNode.EPivotSet.eSourcePivot));
    for (int i = 0; i < node.GetChildCount(); i++) {
      SetPivots(node.GetChild(i));
    }
  }

  // Makes path relative, if possible.
  // If not possible or if path is already relative, returns it unchanged.
  // Propagates nulls.
  private static string MakePathRelative(string path, string relativeTo) {
    if (!Path.IsPathRooted(path)) { return path; }
    relativeTo = relativeTo.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    // It's actually not an easy thing to do to make a path relative, but this works
    if (path.ToLowerInvariant().StartsWith(relativeTo.ToLowerInvariant())) {
      return path.Substring(relativeTo.Length + 1);
    } else {
      return path;
    }
  }

  // Returns a path to the referenced texture, or null
  // The path will be relative to the fbx, if possible
  private string GetValidTextureFile(FbxFileTexture fbxTexture) {
    string relative = fbxTexture.GetRelativeFileName();
    string rooted = fbxTexture.GetFileName();

    if (!string.IsNullOrEmpty(relative)) {
      string absolute = Path.Combine(m_dir, relative);
      if (File.Exists(absolute)) {
        return relative;
      }
    }

    if (string.IsNullOrEmpty(rooted)) {
      return null;
    }
    if (File.Exists(rooted)) {
      return rooted;
    }

    // Look in the same directory as the model.
    // I think this code is largely superseded by using GetRelativeFileName().
    {
      string madeUpRelative = Path.GetFileName(rooted);
      string absolute = Path.Combine(m_dir, madeUpRelative);
      if (File.Exists(absolute)) {
        return madeUpRelative;
      }
    }

    warnings.Add($"Texture not found: {relative ?? rooted}");
    return null;
  }

  // Returns path to the loaded texture, or null on failure.
  // The path will be relative to the fbx, if possible
  private string LoadTexture(
      FbxPropertyDouble3 property, Material mat, string target,
      bool compress = true) {
    FbxFileTexture fbxTexture = property.GetSrcObject_FileTexture();
    if (fbxTexture == null) { return null; }
    string maybeRelative = MakePathRelative(GetValidTextureFile(fbxTexture), m_dir);
    if (maybeRelative == null) { return null; }
    string ext = Path.GetExtension(maybeRelative).ToLower();
    if (! (ext == ".jpg" || ext == ".jpeg" || ext == ".png")) {
      warnings.Add($"Unsupported texture type: {ext}");
      return null;
    }
    if (!mat.HasProperty(target)) {
      warnings.Add($"mat has no property {target}, not assigning {maybeRelative}");
    }

    Texture2D texture = new Texture2D(2, 2);
    texture.LoadImage(File.ReadAllBytes(Path.Combine(m_dir, maybeRelative)));
    texture.Apply();
    if (compress) {
      texture.Compress(false);
    }
    mat.SetTexture(target, texture);
    return maybeRelative;
  }

  // Converts to Vector3 and applies coordinate system conversion
  static private Vector3 ToUnityPosition(FbxDouble3 d) {
    Vector3 fbxStyle = d.ToUVector3();
    // MultiplyPoint and MultiplyVector both work, since coordinate
    // conversion matrices are 3x3
    return ExportFbx.UnityFromFbx.MultiplyVector(fbxStyle);
  }
}
}  // namespace TiltBrush
#endif