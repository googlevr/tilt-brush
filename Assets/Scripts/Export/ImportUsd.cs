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

#if USD_SUPPORTED && (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using pxr;
using UnityEngine;

namespace TiltBrush {
  public class ImportUsd : MonoBehaviour {

    [SerializeField] private double m_usdTime;

    private USD.NET.Scene m_scene;
    private Dictionary<SdfPath, GameObject> m_objectMap = new Dictionary<SdfPath, GameObject>();
    private double m_startTime;
    private double m_endTime;
    private double m_lastTime;
    private Dictionary<string, double[]> m_keyFrames;
    private Dictionary<double, List<string>> m_updateOrder;

    [Flags]
    [PublicAPI]
    public enum UpdateMask {
      None      = 0x0000,
      Topology  = 1 << 1,
      Bounds    = 1 << 2,
      Points    = 1 << 3,
      Colors    = 1 << 4,
      UVs       = 1 << 5,
      Normals   = 1 << 6,
      Tangents  = 1 << 7,
      Materials = 1 << 8,
      All       = 0xFFFF,
    }

    /// Sets the UV map on the unity mesh based on the held type.
    /// Currently, only List<> and Array are supported.
    static void SetUv(Mesh unityMesh, int uvIndex, object uv) {
      if (uv == null) {
        return;
      }

      if (uv.GetType() == typeof(List<Vector2>)) {
        unityMesh.SetUVs(uvIndex, (List<Vector2>)uv);
      } else if (uv.GetType() == typeof(List<Vector3>)) {
        unityMesh.SetUVs(uvIndex, (List<Vector3>)uv);
      } else if (uv.GetType() == typeof(List<Vector4>)) {
        unityMesh.SetUVs(uvIndex, (List<Vector4>)uv);
      } else if (uv.GetType() == typeof(Vector2[])) {
        unityMesh.SetUVs(uvIndex, new List<Vector2>((Vector2[])uv));
      } else if (uv.GetType() == typeof(Vector3[])) {
        unityMesh.SetUVs(uvIndex, new List<Vector3>((Vector3[])uv));
      } else if (uv.GetType() == typeof(Vector4[])) {
        unityMesh.SetUVs(uvIndex, new List<Vector4>((Vector4[])uv));
      } else {
        throw new NotImplementedException();
      }
    }

    /// Given a set of precomputed keyframes, the method schedules updates based on a max value to
    /// avoid overloading the CPU and/or GPU.
    private void BakeUpdateOrder() {
      // 40 seems to be the max on my machine, 30 is preferred to avoid going over budget on low
      // end systems.
      const int kMaxUpdates = 40;

      double start = m_scene.StartTime;
      double end = m_scene.EndTime + 1;
      List<string> overflow = new List<string>();
      var done = new Dictionary<double, List<string>>();

      List<string> pathsToUpdate;

      for (double i = start; i < end || overflow.Count > 0; done.Add(i, pathsToUpdate), i++) {
        pathsToUpdate = new List<string>();

        for (int j = 0; j < overflow.Count; j++) {
          if (pathsToUpdate.Count >= kMaxUpdates) {
            break;
          }
          pathsToUpdate.Add(overflow[0]);
          overflow.RemoveAt(0);
        }

        if (i >= end) {
          continue;
        }

        foreach (var kvp in m_keyFrames) {
          foreach (double t in kvp.Value) {
            if (t == i) {
              if (pathsToUpdate.Count >= kMaxUpdates) {
                overflow.Add(kvp.Key);
              } else {
                pathsToUpdate.Add(kvp.Key);
              }
              break;
            }
          }
        }

      }

      m_updateOrder = done;
    }

    public void Update() {
      // For now, we only support reading a fixed element on time-change, we should inspect the
      // authored time ranges to see what's actually animated.
      UpdateMask mask = UpdateMask.Topology;

      m_usdTime += App.Config.m_IntroSketchSpeed;

      if (App.Config.m_IntroLooped && m_usdTime > m_endTime * 1.25) {
        m_usdTime = m_startTime;
        foreach (Transform child in gameObject.transform) {
          var mf = child.GetComponent<MeshFilter>();
          if (mf == null) { continue; }
          var tris = mf.sharedMesh.triangles;
          for (int i = 0; i < tris.Length; i++) { tris[i] *= 0; }
          mf.sharedMesh.triangles = tris;
        }
      }

      // If not first update and outside of valid time range, bail.
      // This is not required, but an optimization to avoid redundant updates.
      if (mask != UpdateMask.All && (m_usdTime < m_startTime || m_usdTime > m_endTime)) {
        return;
      }

      if (m_usdTime == m_lastTime) {
        return;
      }

      m_lastTime = m_usdTime;
      m_scene.Time = m_usdTime;

      List<string> pathsToUpdate = null;
      if (mask != UpdateMask.All) {
        if (!m_updateOrder.TryGetValue(m_usdTime, out pathsToUpdate)) {
          return;
        }
      }

      List<string> warnings;
      Import(m_scene, gameObject, m_objectMap, mask, out warnings, pathsToUpdate);
    }

    /// Imports a USD file, suitable for the model catalog.
    public static GameObject Import(string rootFilePath, out List<string> warnings) {
      GameObject go = new GameObject("ModelRoot");
      Dictionary<SdfPath, GameObject> objectMap = new Dictionary<SdfPath, GameObject>();

      var scene = USD.NET.Scene.Open(rootFilePath);

      // The time from which we will read values.
      // For the purpose of importing a single frame, any value will suffice.
      scene.Time = 1;

      var rootObj = Import(scene, go, objectMap, UpdateMask.All, out warnings);

      //
      // We cannot set any non-serialized attributes on the generated game object here
      // because the media library logic will creates clones which will not include those values.
      //

      return rootObj;
    }

    public static GameObject ImportWithAnim(string rootFilePath) {
      List<string> warnings;
      GameObject go = new GameObject("ModelRoot");
      Dictionary<SdfPath, GameObject> objectMap = new Dictionary<SdfPath, GameObject>();

      var scene = USD.NET.Scene.Open(rootFilePath);

      // The time from which we will read values.
      // For the purpose of importing a single frame, any value will suffice.
      scene.Time = 1;

      var rootObj = Import(scene, go, objectMap, UpdateMask.All, out warnings);
      var import = rootObj.AddComponent<ImportUsd>();

      scene.SetInterpolation(USD.NET.Scene.InterpolationMode.Linear);
      import.m_objectMap = objectMap;
      import.m_startTime = scene.StartTime;
      import.m_endTime = scene.EndTime;
      import.m_scene = scene;

      // PERFORMANCE: This may be slow.
      import.m_keyFrames = scene.ComputeKeyFrames("/", "faceVertexIndices");

      // PERFORMANCE: This may be slow.
      import.BakeUpdateOrder();

      return rootObj;
    }

    public static GameObject Import(
          USD.NET.Scene scene,
          GameObject rootObj,
          Dictionary<SdfPath, GameObject> objectMap,
          UpdateMask mask,
          out List<string> warnings,
          List<string> pathsToUpdate = null) {

      // TODO: generalize this to avoid having to dig down into USD for sparse reads.
      TfToken brushToken = new pxr.TfToken("brush");
      TfToken faceVertexIndicesToken = new pxr.TfToken("faceVertexIndices");

      warnings = new List<string>();

      // Would be nice to find a way to kick this off automatically.
      // Redundant calls are ignored.
      if (!App.InitializeUsd()) {
        return null;
      }

      // PLAN: Process any UsdStage either constructing or updating GameObjects as needed.
      // This should include analysis of the time samples to see what attributes are
      // actually varying so they are updated minimally.
      UsdPrimVector prims = null;
      if (pathsToUpdate == null) {
        prims = scene.Stage.GetAllPrims();
      } else {
        prims = new UsdPrimVector();
        foreach (var path in pathsToUpdate) {
          prims.Add(scene.Stage.GetPrimAtPath(new pxr.SdfPath(path)));
        }
      }

      for (int p = 0; p < prims.Count; p++) {
        // TODO: prims[p] generates garbage.
        UsdPrim usdPrim = prims[p];
        UsdGeomMesh usdMesh = new UsdGeomMesh(usdPrim);

        if (!usdMesh) {
          continue;
        }

        ExportUsd.BrushSample sample = new ExportUsd.BrushSample();

        if (mask == UpdateMask.All) {
          scene.Read(usdPrim.GetPath(), sample);
        } else {
          // TODO: Generalize this as a reusable mechanism for sparse reads.
          if (mask == UpdateMask.Topology) {
            sample.brush = new Guid((string)usdPrim.GetCustomDataByKey(brushToken));
            var fv = usdPrim.GetAttribute(faceVertexIndicesToken).Get(scene.Time);
            sample.faceVertexIndices = USD.NET.IntrinsicTypeConverter.FromVtArray((VtIntArray)fv);
          } else {
            throw new NotImplementedException();
          }
        }

        GameObject strokeObj;
        Mesh unityMesh;

        //
        // Construct the GameObject if needed.
        //
        if (!objectMap.TryGetValue(usdPrim.GetPath(), out strokeObj)) {
          // On first import, we need to pull in all the data, regardless of what was requested.
          mask = UpdateMask.All;

          BrushDescriptor brush = BrushCatalog.m_Instance.GetBrush(sample.brush);
          if (brush == null) {
            Debug.LogWarningFormat("Invalid brush GUID at path: <{0}> guid: {1}",
              usdPrim.GetPath(), sample.brush);
            continue;
          }
          strokeObj = UnityEngine.Object.Instantiate(brush.m_BrushPrefab);

          // Register the Prim/Object mapping.
          objectMap.Add(usdPrim.GetPath(), strokeObj);

          // Init the game object.
          strokeObj.transform.parent = rootObj.transform;
          strokeObj.GetComponent<MeshRenderer>().material = brush.Material;
          strokeObj.GetComponent<MeshFilter>().sharedMesh = new Mesh();
          strokeObj.AddComponent<BoxCollider>();
          unityMesh = strokeObj.GetComponent<MeshFilter>().sharedMesh;
        } else {
          unityMesh = strokeObj.GetComponent<MeshFilter>().sharedMesh;
        }

        //
        // Points
        // Note that points must come first, before all other mesh data.
        //
        if ((mask & UpdateMask.Points) == UpdateMask.Points) {
          unityMesh.vertices = sample.points;
        }

        //
        // Bounds
        //
        if ((mask & UpdateMask.Bounds) == UpdateMask.Bounds) {
          var bc = strokeObj.GetComponent<BoxCollider>();

          bc.center = sample.extent.center;
          bc.size = sample.extent.size;

          unityMesh.bounds = bc.bounds;
        }

        //
        // Topology
        //
        if ((mask & UpdateMask.Topology) == UpdateMask.Topology) {
          unityMesh.triangles = sample.faceVertexIndices;
        }

        //
        // Normals
        //
        if ((mask & UpdateMask.Normals) == UpdateMask.Normals) {
          unityMesh.normals = sample.normals;
        }

        //
        // Color & Opacity
        //
        if ((mask & UpdateMask.Colors) == UpdateMask.Colors && sample.colors != null) {
          unityMesh.colors = sample.colors;
        }

        //
        // Tangents
        //
        if ((mask & UpdateMask.Tangents) == UpdateMask.Tangents && sample.tangents != null) {
          unityMesh.tangents = sample.tangents;
        }

        //
        // UVs
        //
        if ((mask & UpdateMask.UVs) == UpdateMask.UVs) {
          SetUv(unityMesh, 0, sample.uv);
          SetUv(unityMesh, 1, sample.uv2);
          SetUv(unityMesh, 2, sample.uv3);
          SetUv(unityMesh, 3, sample.uv4);
        }

      } // For each prim

      return rootObj;
    }
  }
} // namespace TiltBrush
#endif
