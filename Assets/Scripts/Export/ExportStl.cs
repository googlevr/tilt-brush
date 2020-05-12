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

using System.Text;
using UnityEngine;

namespace TiltBrush {
  public static class ExportStl {

    public static void Export(string outputFile) {

      float scale = 100f;
      ExportUtils.SceneStatePayload payload = ExportCollector.GetExportPayload(AxisConvention.kStl);

      var buffer = new StringBuilder();
      foreach (var group in payload.groups) {
        foreach (var brushMeshPayload in group.brushMeshes) {
          var pool = brushMeshPayload.geometry;
          var xf = brushMeshPayload.xform;
          var name = brushMeshPayload.legacyUniqueName;

          buffer.AppendFormat("solid {0}\n", name);
          for (int i = 0; i < pool.NumTriIndices; i += 3) {
            Vector3 normal = (pool.m_Normals[pool.m_Tris[i]] +
                              pool.m_Normals[pool.m_Tris[i + 1]] +
                              pool.m_Normals[pool.m_Tris[i + 2]]) / 3f;
            normal.Normalize();
            normal = xf.MultiplyVector(normal);
            Vector3 v1 = xf * pool.m_Vertices[pool.m_Tris[i]] * scale;
            Vector3 v2 = xf * pool.m_Vertices[pool.m_Tris[i + 1]] * scale;
            Vector3 v3 = xf * pool.m_Vertices[pool.m_Tris[i + 2]] * scale;
            buffer.AppendFormat("  facet normal {0} {1} {2}\n", normal.x, normal.y, normal.z);
            buffer.Append("    outer loop\n");
            buffer.AppendFormat("      vertex {0} {1} {2}\n", v1.x, v1.y, v1.z);
            buffer.AppendFormat("      vertex {0} {1} {2}\n", v2.x, v2.y, v2.z);
            buffer.AppendFormat("      vertex {0} {1} {2}\n", v3.x, v3.y, v3.z);
            buffer.Append("    endloop\n");
            buffer.Append("  endfacet\n");
          }
          buffer.AppendFormat("endsolid {0}\n", name);
        }
      }

      System.IO.File.WriteAllText(outputFile, buffer.ToString());
    }
  }
} // namespace TiltBrush