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

using UnityEngine;

// This class is used to bake an inverted outline mesh around a base mesh. Practically,
// this means putting an outline around a mesh so that it is visible on light and dark
// environments.
// To do this, we duplicate the mesh verts, create an "exploded" set, flip their normals and
// winding order, then bake the two meshes in to one via mesh.CombineMeshes().  The outline is
// set with vert colors, requiring the material to support vert colors.
public class BakedMeshOutline : MonoBehaviour {
  // For picky meshes.
  [SerializeField] public float m_OffsetOverride = -1.0f;

  public void Bake(Color baseMeshColor, Color outlineColor, float offset) {
    // Get the mesh filter, run through all the verts and explode them.
    // Take the exploded mesh and fuse it with the original.  Bake white
    // vert colors in to the original mesh and black in to the exploded mesh.
    MeshFilter meshFilter = GetComponent<MeshFilter>();
    Mesh baseMesh = meshFilter.mesh;

    int[] tris = baseMesh.triangles;
    Vector3[] verts = baseMesh.vertices;
    Vector3[] normals = baseMesh.normals;

    float vertOffset = (m_OffsetOverride != -1.0f) ? m_OffsetOverride : offset;
    Vector3 [] explodeVerts = new Vector3[verts.Length];
    for (int i = 0; i < verts.Length; ++i) {
      Vector3 newVert;
      newVert.x = verts[i].x + normals[i].x * vertOffset;
      newVert.y = verts[i].y + normals[i].y * vertOffset;
      newVert.z = verts[i].z + normals[i].z * vertOffset;
      explodeVerts[i] = newVert;
    }

    // Reverse the winding order for the exploded mesh.  To achieve the outline effect,
    // the exploded mesh needs to be inverted so the backside is visible behind the base mesh.
    int[] explodeTris = new int[tris.Length];
    for (int i = 0; i < tris.Length; i += 3) {
      explodeTris[i + 0] = tris[i + 0];
      explodeTris[i + 1] = tris[i + 2];
      explodeTris[i + 2] = tris[i + 1];
    }

    Vector3[] explodeNorms = new Vector3[normals.Length];
    for (int i = 0; i < normals.Length; ++i) {
      explodeNorms[i] = -normals[i];
    }

    Color[] explodeColors = new Color[verts.Length];
    Color[] baseColors = new Color[verts.Length];
    for (int i = 0; i < verts.Length; ++i) {
      baseColors[i] = baseMeshColor;
      explodeColors[i] = outlineColor;
    }
    baseMesh.colors = baseColors;

    Mesh explodeMesh = new Mesh();
    explodeMesh.vertices = explodeVerts;
    explodeMesh.colors = explodeColors;
    explodeMesh.normals = explodeNorms;
    explodeMesh.triangles = explodeTris;

    CombineInstance[] combine = new CombineInstance[2];
    combine[0].mesh = baseMesh;
    combine[0].transform = Matrix4x4.identity;
    combine[1].mesh = explodeMesh;
    combine[1].transform = Matrix4x4.identity;
    Mesh combineMesh = new Mesh();
    combineMesh.CombineMeshes(combine);
    meshFilter.mesh = combineMesh;
  }
}
