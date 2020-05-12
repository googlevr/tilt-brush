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

#if FBX_SUPPORTED
using Autodesk.Fbx;
#endif

namespace TiltBrush {


// Extension methods for Fbx math classes
// Also contains some random things that the USD exporter wants to have access to
public static class FbxUtils {
  public const string kRequiredToolkitVersion = "16.0";

  // Move normals into texcoord1, clearing normals and overwriting texcoord1
  // (which may have data, but it's assumed not to be useful for the export)
  public static void ApplyFbxTexcoordHack(GeometryPool pool) {
    var layout = pool.Layout;
    if (! layout.bFbxExportNormalAsTexcoord1) { return; }

    // Fix up the layout
    layout.bFbxExportNormalAsTexcoord1 = false;
    // Should uv1Semantic be "Vector"? Or "Unspecified"? This case currently
    // does not come up, but guard for it when/if it does.
    Debug.Assert(layout.normalSemantic != GeometryPool.Semantic.Unspecified,
                 "Ambiguous normalSemantic");
    layout.texcoord1.semantic = layout.normalSemantic;
    layout.texcoord1.size = 3;
    layout.bUseNormals = false;
    layout.normalSemantic = GeometryPool.Semantic.Unspecified;
    pool.Layout = layout;

    // Swap m_Normals <-> m_UvSet1.v3
    var tmp = pool.m_Normals;
    pool.m_Normals = pool.m_Texcoord1.v3;
    pool.m_Texcoord1.v3 = tmp;
    pool.m_Normals.Clear();
  }

#if FBX_SUPPORTED
  // FbxDouble3

  public static Vector3 ToUVector3(this FbxDouble3 d) {
    return new Vector3((float)d.X, (float)d.Y, (float)d.Z);
  }

  // FbxVector4

  public static Vector3 ToUVector3(this FbxVector4 d) {
    return new Vector3((float)d.X, (float)d.Y, (float)d.Z);
  }

  public static Vector4 ToUVector4(this FbxVector4 d) {
    return new Vector4((float)d.X, (float)d.Y, (float)d.Z, (float)d.W);
  }

  // FbxDouble4

  public static Vector3 ToUVector3(this FbxDouble4 d) {
    return new Vector3((float)d.X, (float)d.Y, (float)d.Z);
  }

  public static Vector4 ToUVector4(this FbxDouble4 d) {
    return new Vector4((float)d.X, (float)d.Y, (float)d.Z, (float)d.W);
  }

  // FbxQuaternion

  public static Quaternion ToUQuaternion(this FbxQuaternion fbx) {
    var q = new Quaternion();
    // the indexing order of FbxQuaternion is undocumented; w=3
    // was determined empirically.
    q.x = (float)fbx.GetAt(0);
    q.y = (float)fbx.GetAt(1);
    q.z = (float)fbx.GetAt(2);
    q.w = (float)fbx.GetAt(3);
    return q;
  }

  public static FbxQuaternion ToFbxQuaternion(this Quaternion q) {
    return new FbxQuaternion(q.x, q.y, q.z, q.w);
  }

  // FbxAMatrix

  /// Does not do any coordinate-convention switching.
  public static void ToTRS(
      this FbxAMatrix input,
      out Vector3 translation,
      out Quaternion rotation,
      out Vector3 scale) {
    translation = input.GetT().ToUVector3();
    rotation = input.GetQ().ToUQuaternion();
    scale = input.GetS().ToUVector3();
  }
#endif
}

} // namespace TiltBrush
