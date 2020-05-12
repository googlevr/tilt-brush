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

using Newtonsoft.Json;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TiltBrush {

public static class ExportRaw {
  public static bool Export(string outputFile) {
    var tempName = outputFile + "_part";
    try {
      using (var textWriter = new StreamWriter(tempName))
      using (var json = new JsonTextWriter(textWriter)) {
        json.Formatting = Formatting.Indented;
        json.WriteStartObject();

        Dictionary<Guid, int> brushMap;
        WriteStrokes(json, out brushMap);
        WriteBrushes(json, brushMap);

        json.WriteEndObject();
      }
      DestroyFile(outputFile);
      Directory.Move(tempName, outputFile);
      return true;
    } catch (IOException e) {
      Debug.LogException(e);
      return false;
    }
  }

  static void DestroyFile(string path) {
    if (File.Exists(path)) {
      File.SetAttributes(path, FileAttributes.Normal);
      File.Delete(path);
    }
  }

  static void WriteBrushes(JsonWriter json, Dictionary<Guid, int> brushMap) {
    var brushList = new BrushDescriptor[brushMap.Count];
    foreach (var pair in brushMap) {
      brushList[pair.Value] = BrushCatalog.m_Instance.GetBrush(pair.Key);
    }

    json.WritePropertyName("brushes");

    json.WriteStartArray();
    for (int i = 0; i < brushList.Length; ++i) {
      WriteBrush(json, brushList[i]);
    }
    json.WriteEnd();
  }

  static void WriteBrush(JsonWriter json, BrushDescriptor brush) {
    json.WriteStartObject();
    json.WritePropertyName("name");
    json.WriteValue(brush.name);
    json.WritePropertyName("guid");
    json.WriteValue(brush.m_Guid.ToString("D"));
    json.WriteEndObject();
  }

  static void WriteStrokes(JsonWriter json, out Dictionary<Guid, int> brushMap) {
    brushMap = new Dictionary<Guid, int>();

    json.WritePropertyName("strokes");
    json.WriteStartArray();
    var strokes = SketchMemoryScript.AllStrokes().ToList();
    for (int i = 0; i < strokes.Count; ++i) {
      if (strokes[i].IsGeometryEnabled) {
        WriteStroke(json, strokes[i], brushMap);
      }
    }
    json.WriteEnd();
  }

  static void WriteStroke(JsonWriter json, Stroke stroke,
                          Dictionary<Guid, int> brushMap) {
    json.WriteStartObject();

    var brushGuid = stroke.m_BrushGuid;
    BrushDescriptor desc = BrushCatalog.m_Instance.GetBrush(brushGuid);
    int brushIndex;
    if (!brushMap.TryGetValue(brushGuid, out brushIndex)) {
      brushIndex = brushMap.Count;
      brushMap[brushGuid] = brushIndex;
    }

    json.WritePropertyName("brush");
    json.WriteValue(brushIndex);

    if (stroke.m_Type == Stroke.Type.BrushStroke) {
      // Some strokes (eg particles) don't have meshes. For now, assume that
      // if the stroke has a mesh, it should be written.
      var meshFilter = stroke.m_Object.GetComponent<MeshFilter>();
      if (meshFilter != null) {
        var mesh = meshFilter.sharedMesh;
        if (mesh != null) {
          WriteMesh(json, mesh, desc.VertexLayout);
        }
      }
    } else if (stroke.m_Type == Stroke.Type.BatchedBrushStroke) {
      BatchSubset subset = stroke.m_BatchSubset;
      GeometryPool geom = subset.m_ParentBatch.Geometry;

      Mesh tempMesh = new Mesh();
      geom.CopyToMesh(tempMesh,
                      subset.m_StartVertIndex, subset.m_VertLength,
                      subset.m_iTriIndex, subset.m_nTriIndex);
      WriteMesh(json, tempMesh, geom.Layout);
      tempMesh.Clear();
      UnityEngine.Object.Destroy(tempMesh);
    }

    json.WriteEndObject();
  }

  static void WriteMesh(JsonWriter json, Mesh mesh, GeometryPool.VertexLayout layout) {
    // Unity does not import .obj verbatim. It makes these changes:
    // - flip x axis
    // - reverse winding of triangles
    // We undo these changes when exporting to obj.
    // NOTE: It's currently unknown whether this also happens for fbx files.

    int nVert = mesh.vertexCount;
    WriteArray(json, "v", AsByte(mesh.vertices, nVert, flip: true));
    if (layout.bUseNormals) {
      WriteArray(json, "n", AsByte(mesh.normals, nVert, flip: true));
    }
    WriteUvChannel(json, 0, layout.texcoord0.size, mesh, nVert);
    WriteUvChannel(json, 1, layout.texcoord1.size, mesh, nVert);
    if (layout.bUseColors) {
      WriteArray(json, "c", AsByte(mesh.colors32, nVert));
    }
    // NOTE(b/30710462): Bubble wand lies about its tangents, so check they are really there
    if (layout.bUseTangents && mesh.tangents.Length > 0) {
      WriteArray(json, "t", AsByte(mesh.tangents, nVert, flip: true));
    }

    var tris = mesh.GetTriangles(0);
    // Reverse winding, per above
    for (int i = 0; i < tris.Length; i += 3) {
      var tmp = tris[i+1];
      tris[i+1] = tris[i+2];
      tris[i+2] = tmp;
    }
    WriteArray(json, "tri", AsByte(tris, tris.Length));
  }

  static void WriteUvChannel(JsonWriter json, int channel, int numComponents, Mesh mesh, int n) {
    if (numComponents == 0 || n == 0) {
      return;
    }
    var name = string.Format("uv{0}", channel);
    switch (numComponents) {
    case 2: {
      var uv = new List<Vector2>();
      mesh.GetUVs(channel, uv);
      WriteArray(json, name, AsByte(uv.GetBackingArray(), n));
      break;
    }
    case 3: {
      var uv = new List<Vector3>();
      mesh.GetUVs(channel, uv);
      WriteArray(json, name, AsByte(uv.GetBackingArray(), n));
      break;
    }
    case 4: {
      var uv = new List<Vector4>();
      mesh.GetUVs(channel, uv);
      WriteArray(json, name, AsByte(uv.GetBackingArray(), n));
      break;
    }
    default:
      throw new ArgumentException("numComponents");
    }
  }

  static void WriteArray(JsonWriter json, string name, byte[] buf) {
    if (buf == null || buf.Length == 0) { return; }
    json.WritePropertyName(name);
    json.WriteValue(buf);
  }

  // Unfortunately, C# generics won't work here -- the compiler and
  // the runtime conservatively assume that the generic type isn't POD.

  static unsafe byte[] AsByte(Color32[] a, int n) {
    if (n == 0) { return null; }
    fixed (Color32* p = a) {
      int eltSize = (int)((byte*)&p[1] - (byte*)&p[0]);
      byte[] buf = new byte[n * eltSize];
      System.Runtime.InteropServices.Marshal.Copy(
          (IntPtr)p, buf, 0, buf.Length);
      return buf;
    }
  }
  static unsafe byte[] AsByte(int[] a, int n) {
    if (n == 0) { return null; }
    fixed (int* p = a) {
      int eltSize = (int)((byte*)&p[1] - (byte*)&p[0]);
      byte[] buf = new byte[n * eltSize];
      System.Runtime.InteropServices.Marshal.Copy(
          (IntPtr)p, buf, 0, buf.Length);
      return buf;
    }
  }
  static unsafe byte[] AsByte(Vector2[] a, int n) {
    if (n == 0) { return null; }
    fixed (Vector2* p = a) {
      int eltSize = (int)((byte*)&p[1] - (byte*)&p[0]);
      byte[] buf = new byte[n * eltSize];
      System.Runtime.InteropServices.Marshal.Copy(
          (IntPtr)p, buf, 0, buf.Length);
      return buf;
    }
  }

  // "flip" changes the VectorN from left-handed to right-handed coordinates.
  // Note that flipping "x" is _not_ arbitrary -- it mirrors (no pun intended)
  // the conversion Unity applies when importing right-handed file formats
  // like .obj.

  static unsafe byte[] AsByte(Vector3[] a, int n, bool flip=false) {
    if (n == 0) { return null; }
    fixed (Vector3* p = a) {
      int eltSize = (int)((byte*)&p[1] - (byte*)&p[0]);
      byte[] buf = new byte[n * eltSize];
      System.Runtime.InteropServices.Marshal.Copy(
          (IntPtr)p, buf, 0, buf.Length);
      if (flip) {
        // Flip the sign bit of the first float (x) in each element.
        for (int i = 3; i < buf.Length; i += eltSize) {
          buf[i] ^= 0x80;
        }
      }
      return buf;
    }
  }

  static unsafe byte[] AsByte(Vector4[] a, int n, bool flip=false) {
    if (n == 0) { return null; }
    fixed (Vector4* p = a) {
      int eltSize = (int)((byte*)&p[1] - (byte*)&p[0]);
      byte[] buf = new byte[n * eltSize];
      System.Runtime.InteropServices.Marshal.Copy(
          (IntPtr)p, buf, 0, buf.Length);
      if (flip) {
        // Flip the sign bit of the first float (x) in each element.
        for (int i = 3; i < buf.Length; i += eltSize) {
          buf[i] ^= 0x80;
        }
      }
      return buf;
    }
  }
}

} // namespace TiltBrush