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

  // Exports vrml file of geometry in sketch to "outputFile" specified.
  // Colors are set per chunk,
  // A new chunk is defined as when a change occurs in any of the following:
  //   - color
  //   - brush type
  //   - "batch GameObject" holding the mesh information
  //
  // General organization of wrl file:
  //
  //   chunk {
  //     Vertices [...]
  //     Triangles [...]
  //     Color
  //   }
  //   chunk {
  //     Vertices [...]
  //     Triangles [...]
  //     Color
  //   }
  //   chunk {
  //     ...
  //   }
  //   ...
  //
public static class ExportVrml {
  public static bool Export(string outputFile) {
    ExportUtils.SceneStatePayload payload = ExportCollector.GetExportPayload(AxisConvention.kVrml);

    var buffer = new StringBuilder("#VRML V2.0 utf8\n");
    AppendSceneHeader(ref buffer);

    foreach (var group in payload.groups) {
      foreach (var brushMeshPayload in group.brushMeshes) {
        var pool = brushMeshPayload.geometry;
        Color32 lastColor = pool.m_Colors[0];  // Set to color of first vert initially
        int j = 0;                           // An index marking current position in pool.m_Tris
        int numVerts = 0;           // Number of verts appended
        int firstVertInchunk = 0;  // So chunks starts triangle-indexing verts at 0 in the wrl

        AppendchunkHeader(ref buffer);
        AppendchunkVerticesHeader(ref buffer);

        // Iterate through the verts-array and append them to the wrl.
        // Upon hitting a vertex with a different color (a new chunk), backtrack and iterate
        // through the tris-array, appending all triangles that are in the current chunk.
        // Continue again with the new chunk, etc., until all vertices are appended.
        for (int i = 0; i < pool.NumVerts; i++) {

          Color32 curColor = pool.m_Colors[i];

          if (curColor.Equals(lastColor)) {  // In the same chunk

            AppendVertex(ref buffer, pool, i);
            numVerts += 1;

            if (i == pool.NumVerts - 1) {  // last vertex

              AppendchunkVerticesFooter(ref buffer);

              AppendchunkTrianglesHeader(ref buffer);

              while (j < pool.NumTriIndices) {  // Append all remaining triangles
                AppendTriangle(ref buffer, pool, j, firstVertInchunk);
                j += 3;
              }

              AppendchunkTrianglesFooter(ref buffer);

              AppendchunkAppearanceHeader(ref buffer);
              AppendColorRGB(ref buffer, lastColor);
              AppendColorA(ref buffer, lastColor);
              AppendchunkAppearanceFooter(ref buffer);

              AppendchunkFooter(ref buffer);
            }
          } else {  // New color, so new chunk

            AppendchunkVerticesFooter(ref buffer);

            AppendchunkTrianglesHeader(ref buffer);

            // Vertex "i" is part of a new chunk, therefore only append
            // triangles that contain vertices up to and not including "i"
            while (j < pool.NumTriIndices) {
              // Only check first index in triangle because triangles cannot span across chunks.
              // Either all vertices of the triangle are in chunk_n or all are in chunk_n+1.
              if (pool.m_Tris[j] < i) {
                AppendTriangle(ref buffer, pool, j, firstVertInchunk);
                j += 3;
              } else {
                break;
              }
            }

            AppendchunkTrianglesFooter(ref buffer);

            AppendchunkAppearanceHeader(ref buffer);
            AppendColorRGB(ref buffer, lastColor);
            AppendColorA(ref buffer, lastColor);
            AppendchunkAppearanceFooter(ref buffer);

            AppendchunkFooter(ref buffer);

            AppendchunkHeader(ref buffer);  // Starting the next chunk

            AppendchunkVerticesHeader(ref buffer);

            AppendVertex(ref buffer, pool, i);

            firstVertInchunk = numVerts;
            numVerts += 1;
            lastColor = curColor;
          }
        }
      }
    }

    AppendSceneFooter(ref buffer);

    System.IO.File.WriteAllText(outputFile, buffer.ToString());
    return true;
  }

  private static void AppendVertex(ref StringBuilder buffer, GeometryPool pool, int i) {
    buffer.Append("\t\t\t" + pool.m_Vertices[i].x + " "
                           + pool.m_Vertices[i].y + " "
                           + pool.m_Vertices[i].z + ",\n");
  }

  private static void AppendTriangle(ref StringBuilder buffer, GeometryPool pool,
                                     int j, int chunkStartingVertex) {
    buffer.Append("\t\t\t"  + (pool.m_Tris[j + 0] - chunkStartingVertex) + " "
                            + (pool.m_Tris[j + 1] - chunkStartingVertex) + " "
                            + (pool.m_Tris[j + 2] - chunkStartingVertex) + " "
                            + "-1,\n");
  }

  private static void AppendColorRGB(ref StringBuilder buffer, Color32 lastColor) {
    buffer.Append("\t\t\t" + "diffuseColor " + lastColor.r + " "
                                              + lastColor.g + " "
                                              + lastColor.b + "\n");
  }

  private static void AppendColorA(ref StringBuilder buffer, Color32 lastColor) {
    buffer.Append("\t\t\t" + "transparency " + lastColor.a + "\n");
  }

  private static void AppendSceneHeader(ref StringBuilder buffer) {
    buffer.Append("Transform { children [\n");
  }

  private static void AppendSceneFooter(ref StringBuilder buffer) {
    buffer.Append("]}");
  }

  private static void AppendchunkHeader(ref StringBuilder buffer) {
    buffer.Append("\t" + "Shape { geometry IndexedFaceSet {\n");
    buffer.Append("\t\t" + "coord DEF objMeshCoords Coordinate {\n");
  }

  private static void AppendchunkVerticesHeader(ref StringBuilder buffer) {
    buffer.Append("\t\t" + "point [\n");
  }

  private static void AppendchunkVerticesFooter(ref StringBuilder buffer) {
    buffer.Append("\t\t" + "]}\n");
  }

  private static void AppendchunkTrianglesHeader(ref StringBuilder buffer) {
    buffer.Append("\t\t" + "coordIndex [\n");
  }

  private static void AppendchunkTrianglesFooter(ref StringBuilder buffer) {
    buffer.Append("\t\t" + "]}\n");
  }

  private static void AppendchunkAppearanceHeader(ref StringBuilder buffer) {
    buffer.Append("\t\t" + "appearance Appearance { material Material {\n");
  }

  private static void AppendchunkAppearanceFooter(ref StringBuilder buffer) {
    buffer.Append("\t\t" + "}}\n");
  }

  private static void AppendchunkFooter(ref StringBuilder buffer) {
    buffer.Append("\t" + "}\n");
  }
}
} // namespace TiltBrush