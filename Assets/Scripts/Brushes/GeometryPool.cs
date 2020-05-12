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
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;

namespace TiltBrush {

/// GeometryPool stores mesh data in convenient and C#-accessible data structures.
///
/// Quite often, GeometryPool data is associated with a particular UnityEngine.Mesh to which
/// data changes are pushed with CopyToMesh(mesh). This trades off increased memory usage for
/// efficient updates, and is analagous to a CPU cache. For control over memory usage,
/// GeometryPool provides:
///
///   CopyToMesh(mesh)               Copy GeometryPool -> Mesh
///   MakeGeometryNotResident(mesh)  Replace memory buffers with a pointer to Mesh
///   MakeGeometryNotResident(file)  Dump memory buffers to a file
///   MakeGeometryPermanentlyNotResident()   Like it sounds
///   EnsureGeometryResident()       Reload memory buffers from the Mesh or file
///
/// GeometryPool currently tries to make the distinction between "not resident" and "resident"
/// as transparent as possible to callers. All public methods and properties will call
/// EnsureGeometryResident() if necessary. This is convenient, but means that callers might
/// inadvertently reload the buffers when they don't mean to.
///
/// public fields like m_Triangles will not (cannot) be auto-reloaded; they will be null
/// if geometry is not resident, and users must explicitly call EnsureGeometryResident().
public class GeometryPool {
  const UInt32 kMagic = 0x6f6f7047;  // 'Gpoo'
  public const int kNumTexcoords = 3;

  // Static API

  /// Tells us what sort of data is getting put into the channel.
  /// These only make sense when applied to channels with 3 or 4 elements.
  /// The 4th element is always left alone.
  [Serializable]
  public enum Semantic {
    // DO NOT CHANGE THIS ORDER.
    // This enum must match the one in the Toolkit's
    // UnitySDK/Assets/TiltBrush/Scripts/BrushDescriptor.cs

    // Unknown; you'll have to guess. Reasonable guesses, based on where the data is from:
    //   texcoord 0: Semantic.XyIsUv (especially if it's 2-element)
    //   normals:    Semantic.UnitlessVector
    Unspecified,

    // Used for positions with units of distance
    // Transform(xf, v) = xf * float4(v.xyz, 1)
    Position,

    // Used for vectors with units of distance
    // Transform(xf, v) = xf * float4(v.xyz, 0)
    Vector,

    // Used when a distance is stored in the Z coordinate.
    // NOTE: Additionally implies that .xy is a uv coordinate, which has export implications
    // Transform(xf, v) = v * float4(1, 1, xf.scale, 1)
    XyIsUvZIsDistance,

    // Used for vectors that are unitless
    // They will change-of-basis, but not change-of-distance-unit
    // This is not currently used in any VertexLayout, only in import code
    UnitlessVector,

    // .xy is a uv, used for texture fetch
    // This impacts import and export, if the file format has different uv axis conventions
    // from Unity.
    XyIsUv,

    // .xy is stroke start/end timestamps, z is interpolated
    Timestamp,
  }

  /// Metadata about a texcoord channel used in VertexLayout.
  /// The data itself is stored in a corresponding struct TexcoordData in the GeometryPool.
  public struct TexcoordInfo : IEquatable<TexcoordInfo> {
    // The number of elements. Valid values are 0, 2, 3, and 4.
    public int size;
    public Semantic semantic;

    // Appease C#
    public static bool operator == (TexcoordInfo lhs, TexcoordInfo rhs) => lhs.Equals(rhs);
    public static bool operator != (TexcoordInfo lhs, TexcoordInfo rhs) => !lhs.Equals(rhs);
    public bool Equals(TexcoordInfo rhs) => size == rhs.size && semantic == rhs.semantic;
    public override bool Equals(object rhso) => (rhso is TexcoordInfo rhs) ? Equals(rhs) : false;
    public override int GetHashCode() => size << 3 | (int)semantic;
  }

  /// The contents of a texcoord channel.
  /// Metadata about the channel (number of components, semantic) is stored in
  /// a corresponding struct TexcoordInfo in the VertexLayout.
  public struct TexcoordData {
    // At most one of these will be non-null.
    // All will be non-null if there if the associated TexcoordInfo.size is 0
    public List<Vector2> v2;
    public List<Vector3> v3;
    public List<Vector4> v4;

    public void SetSize(int size) {
      switch (size) {
      case 0:
        v2 = null;
        v3 = null;
        v4 = null;
        break;
      case 2:
        if (v2 == null) { v2 = new List<Vector2>(); }
        v3 = null;
        v4 = null;
        break;
      case 3:
        v2 = null;
        if (v3 == null) { v3 = new List<Vector3>(); }
        v4 = null;
        break;
      case 4:
        v2 = null;
        v3 = null;
        if (v4 == null) { v4 = new List<Vector4>(); }
        break;
      }
    }

    public void Clear() {
      if (v2 != null) { v2.Clear(); }
      if (v3 != null) { v3.Clear(); }
      if (v4 != null) { v4.Clear(); }
    }
  }

  public struct VertexLayout : IEquatable<VertexLayout> {
    public TexcoordInfo texcoord0;
    public TexcoordInfo texcoord1;
    public TexcoordInfo texcoord2;
    public bool bUseNormals;
    public Semantic normalSemantic;
    public bool bUseColors;
    public bool bUseTangents;

    // The motivating use of this was for export when gl_VertexID is not available and we need to
    // export the vertex IDs as a vertex attribute (e.g. WebGL 1.0).
    public bool bUseVertexIds;

    // HACK: Indicates to the fbx exporter that it should move data from .normals
    // to TEXCOORD1 before exporting.
    // This works around a Unity import issue where it insists on making .normal unit-length.
    public bool bFbxExportNormalAsTexcoord1;

    [System.Diagnostics.Contracts.Pure]
    public TexcoordInfo GetTexcoordInfo(int channel) {
      switch (channel) {
      case 0: return texcoord0;
      case 1: return texcoord1;
      case 2: return texcoord2;
      default: throw new ArgumentException("channel");
      }
    }

    // These are only here for legacy reasons; do not use them in new code
    internal int uv0Size { set => texcoord0.size = value; }
    internal int uv1Size { set => texcoord1.size = value; }
    internal Semantic uv0Semantic { set => texcoord0.semantic = value; }
    internal Semantic uv1Semantic { set => texcoord1.semantic = value; }

    // Appease C#
    public static bool operator == (VertexLayout lhs, VertexLayout rhs) => lhs.Equals(rhs);
    public static bool operator != (VertexLayout lhs, VertexLayout rhs) => !lhs.Equals(rhs);
    public bool Equals(VertexLayout rhs) =>
           texcoord0 == rhs.texcoord0
        && texcoord1 == rhs.texcoord1
        && texcoord2 == rhs.texcoord2
        && bUseNormals   == rhs.bUseNormals
        && bUseColors    == rhs.bUseColors
        && bUseTangents  == rhs.bUseTangents;
    public override bool Equals(object rhso) => (rhso is VertexLayout rhs) ? Equals(rhs) : false;
    public override int GetHashCode() =>
          texcoord0.GetHashCode()
        ^ (texcoord1.GetHashCode() << 5)
        ^ (texcoord2.GetHashCode() << 10)
        ^ (bUseNormals  ? 0x10000 : 0)
        ^ (bUseColors   ? 0x20000 : 0)
        ^ (bUseTangents ? 0x40000 : 0);
  }

  // Plain-old-data storage for m_BackingFileInfo.
  private class BackingFileInfo {
    // If this is null, it means there's no way to read the data back again.
    public string filename;
    public int numVertsInFile;
    public int numTriIndicesInFile;
  }

  // A pool of GeometryPools
  static Stack<GeometryPool> sm_unused = new Stack<GeometryPool>();

  /// Thread-safe
  public static GeometryPool Allocate() {
    try {
      lock (sm_unused) {
        return sm_unused.Pop();
      }
    } catch (InvalidOperationException) {
      return new GeometryPool();
    }
  }

  /// Thread-safe
  public static void Free(GeometryPool g) {
    g.Reset(false);
    lock (sm_unused) {
      sm_unused.Push(g);
    }
  }

  /// Returns an array of pools, one per subset.
  /// Pass the layouts to use for each subset; if the layout is null, no pool will be created.
  /// Currently, the resulting pools do not share any underlying vertex data, although
  /// it will all be the same.
  public static GeometryPool[] FromMesh(
      Mesh mesh, VertexLayout?[] layouts,
      Color32? fallbackColor = null,
      bool useFallbackTexcoord = false) {
    var pools = new GeometryPool[mesh.subMeshCount];

    for (int i = 0; i < pools.Length; i++) {
      GeometryPool pool;
      if (layouts[i] is VertexLayout layout) {
        pool = new GeometryPool();
        pool.Layout = layout;
        pool.AppendVertexData(mesh, null, fallbackColor, useFallbackTexcoord);
        mesh.GetTriangles(pool.m_Tris, i);
      } else {
        Debug.LogWarning("Missing layout for submesh");
        pool = null;
      }

      pools[i] = pool;
    }

    return pools;
  }

  // Takes ownership of data and turns it into a List<>
  private static List<T> StealArrayForList<T>(T[] data) {
    List<T> ret = new List<T>();
    ret.SetBackingArray(data);
    return ret;
  }

  // Like Mesh.GetUVs(), but returns a TexcoordData.
  // Pass:
  //   mesh, channel -
  //     Same as Mesh.GetUVs
  //   uvSize -
  //     Number of channels in the texcoord.
  //     Necessary because Mesh is missing API to fetch this.
  private static TexcoordData GetTexcoordDataFromMesh(Mesh mesh, int channel, int uvSize) {
    TexcoordData ret = new TexcoordData();
    switch (uvSize) {
    case 2:
      ret.v2 = new List<Vector2>();
      mesh.GetUVs(channel, ret.v2);
      break;
    case 3:
      ret.v3 = new List<Vector3>();
      mesh.GetUVs(channel, ret.v3);
      break;
    case 4:
      ret.v4 = new List<Vector4>();
      mesh.GetUVs(channel, ret.v4);
      break;
    }
    return ret;
  }

  // Instance API

  public List<Vector3> m_Vertices;
  public List<int> m_Tris;
  public List<Vector3> m_Normals;
  public TexcoordData m_Texcoord0;
  public TexcoordData m_Texcoord1;
  public TexcoordData m_Texcoord2;

  public List<Color32> m_Colors;
  public List<Vector4> m_Tangents;

  // All writes must go through the Layout setter
  private VertexLayout m_Layout;

  // Whether the C# geometry buffers are filled-in and valid.
  // If this is true, all the "m_Backing___" fields will be null.
  // If this is false, exactly one of the "m_Backing___" fields will be non-null.
  private bool m_IsResident;

  // This Mesh contains data that can be used to re-fill our buffers.
  // It is not owned by us; and it's up to the owner of the Mesh to ensure
  // that the Mesh is neither modified nor deleted while we reference it.
  private Mesh m_BackingMesh;

  // This file contains data that can be used to re-fill our buffers.
  // It is owned by us, and we need to delete it when we're finished with it.
  // See also m_IsResident for invariants, and m_BackingMesh.
  private BackingFileInfo m_BackingFileInfo;

  /// Returns true if the Pool's geometry buffers are usable.
  /// See also EnsureGeometryResident().
  public bool IsGeometryResident {
    get { return m_IsResident; }
  }

  /// All writes must go through the setter.
  ///
  /// The setter ensures that the correct member in m_Texcoord{0,1} is created
  /// (and that the other members are set to null); but that is all.
  /// If you modify the layout after adding data, the data will not be
  /// copied (eg, from m_Texcoord0.v2 to m_Texcoord0.v3).
  public VertexLayout Layout {
    get { return m_Layout; }
    set {
      m_Layout = value;
      m_Texcoord0.SetSize(value.texcoord0.size);
      m_Texcoord1.SetSize(value.texcoord1.size);
      m_Texcoord2.SetSize(value.texcoord2.size);
    }
  }

  /// Assigning to this also extends any other arrays that
  /// may be used by the current vertex layout.
  /// Assigning to this property currently forces data to be resident.
  public int NumVerts {
    get {
      if (IsGeometryResident) {
        return m_Vertices.Count;
      } else if (!ReferenceEquals(m_BackingMesh, null)) {
        return m_BackingMesh.vertexCount;
      } else if (m_BackingFileInfo != null) {
        return m_BackingFileInfo.numVertsInFile;
      } else {
        throw new InvalidOperationException("Invalid state");
      }
    }

    set {
      if (value == NumVerts) {
        return;
      }
      // TODO: it would be nice if we could reduce the # verts without forcing the
      // data resident -- when we reload the data we can always ignore part of it.
      EnsureGeometryResident();
      m_Vertices.SetCount(value);
      if (m_Layout.bUseNormals) {
        m_Normals.SetCount(value);
      }

      for (int channel = 0; channel < kNumTexcoords; ++channel) {
        var texcoordData = GetTexcoordData(channel);
        switch (m_Layout.GetTexcoordInfo(channel).size) {
        case 0: break;
        case 2: texcoordData.v2.SetCount(value); break;
        case 3: texcoordData.v3.SetCount(value); break;
        case 4: texcoordData.v4.SetCount(value); break;
        }
      }
      if (m_Layout.bUseColors) {
        m_Colors.SetCount(value);
      }
      if (m_Layout.bUseTangents) {
        m_Tangents.SetCount(value);
      }
    }
  }

  /// This property does not force data to be resident.
  public int NumTriIndices {
    get {
      if (IsGeometryResident) {
        return m_Tris.Count;
      } else if (!ReferenceEquals(m_BackingMesh, null)) {
        return (int)m_BackingMesh.GetIndexCount(0);
      } else if (m_BackingFileInfo != null) {
        return m_BackingFileInfo.numTriIndicesInFile;
      } else {
        throw new InvalidOperationException("Invalid state");
      }
    }

    set {
      if (!IsGeometryResident) {
        throw new InvalidOperationException("Not resident");
      }
      Debug.Assert(value % 3 == 0);
      m_Tris.SetCount(value);
    }
  }

  public void ShiftForward(int verts, int tris) {
    EnsureGeometryResident();
    if (verts > m_Vertices.Count) {
      throw new ArgumentOutOfRangeException("verts");
    }
    if (tris > m_Tris.Count) {
      throw new ArgumentOutOfRangeException("tris");
    }
    m_Vertices.RemoveRange(0, verts);
    if (m_Layout.bUseNormals) {
      m_Normals.RemoveRange(0, verts);
    }
    if (m_Layout.bUseColors) {
      m_Colors.RemoveRange(0, verts);
    }
    if (m_Layout.bUseTangents) {
      m_Tangents.RemoveRange(0, verts);
    }
    for (int channel = 0; channel < kNumTexcoords; ++channel) {
      var texcoordData = GetTexcoordData(channel);
      switch (m_Layout.GetTexcoordInfo(channel).size) {
      case 0: break;
      case 2: texcoordData.v2.RemoveRange(0, verts); break;
      case 3: texcoordData.v3.RemoveRange(0, verts); break;
      case 4: texcoordData.v4.RemoveRange(0, verts); break;
      }
    }
    m_Tris.RemoveRange(0, tris * 3);
    for (int t = 0; t < m_Tris.Count; t++) {
      m_Tris[t] -= verts;
    }
  }

  public GeometryPool() {
    m_Vertices = new List<Vector3>();
    m_Tris     = new List<int>();
    m_Normals  = new List<Vector3>();
    m_Colors   = new List<Color32>();
    m_Tangents = new List<Vector4>();
    Reset(false);
  }

  /// Similar to Reset(), but with the intent that this pool will never be used again,
  /// even on a freelist.
  /// It is safe to call this multiple times.
  public void Destroy() {
    ClearBackingFile();
  }

  public GeometryPool Clone() {
    var clone = new GeometryPool();
    clone.Layout = this.Layout;
    clone.Append(this, 0, this.NumVerts, 0, this.NumTriIndices);
    return clone;
  }

  /// Sense of keepVertexLayout is weird/awkward because it copies the
  /// meaning from UnityEngine.Mesh.Clear()
  public void Reset(bool keepVertexLayout=true) {
    if (IsGeometryResident) {
      // Explicitly _not_ calling TrimExcess(), to avoid allocations
      // when these instances are used later.
      m_Vertices.Clear();
      m_Tris.Clear();
      m_Normals.Clear();
      m_Texcoord0.Clear();
      m_Texcoord1.Clear();
      m_Texcoord2.Clear();
      m_Colors.Clear();
      m_Tangents.Clear();
    } else {
      m_IsResident = true;
      m_BackingMesh = null;
      ClearBackingFile();
      // The lists were nulled out to catch mistaken use; bring them back
      m_Vertices = new List<Vector3>();
      m_Tris     = new List<int>();
      m_Normals  = new List<Vector3>();
      // For completeness. The TexcoordDatas should already be clear, since their value is default.
      m_Texcoord0.Clear();
      m_Texcoord1.Clear();
      m_Texcoord2.Clear();
      m_Colors   = new List<Color32>();
      m_Tangents = new List<Vector4>();
    }

    if (!keepVertexLayout) {
      Layout = new VertexLayout {
        texcoord0 = new TexcoordInfo { size = 2 },
        bUseNormals = true,
        bUseColors = true,
        bUseTangents = true
      };
    }
  }

  /// Writes internal buffers to the specified filename.
  /// Deletes internal buffers and arranges to reload them from that file when
  /// EnsureGeometryResident is called.
  ///
  /// If you take advantage of this API, make sure you call Destroy() when
  /// you no longer need the GeometryPool so it can clean up after itself.
  ///
  /// Pass:
  ///   backingFile - the name of a file; it will be overwritten.
  public void MakeGeometryNotResident(string backingFile) {
    Debug.Assert(m_IsResident);
    Debug.Assert(m_BackingMesh == null);
    Debug.Assert(m_BackingFileInfo == null);
    int oldVertexCount = NumVerts;
    int oldIndexCount = NumTriIndices;
    Directory.CreateDirectory(Path.GetDirectoryName(backingFile));
    using (FileStream fs = File.OpenWrite(backingFile)) {
      SerializeToStream(fs);
    }
    m_BackingFileInfo = new BackingFileInfo {
        filename = backingFile,
        numVertsInFile = NumVerts,
        numTriIndicesInFile = NumTriIndices
    };
    m_IsResident = false;
    ClearBuffers();
    if (oldVertexCount != NumVerts || oldIndexCount != NumTriIndices) {
      Debug.LogWarning("Self check fail");
    }
  }

  /// This is like the other MakeGeometryNotResident() calls, but there
  /// is no returning from this one.
  ///
  /// Use it if you still need valid metadata (NumVerts, NumTriIndices, Layout);
  /// but will never need the mesh data again.
  public void MakeGeometryPermanentlyNotResident() {
    Debug.Assert(m_IsResident);
    Debug.Assert(m_BackingMesh == null);
    Debug.Assert(m_BackingFileInfo == null);
    // Sort of a hack: abuse m_BackingFileInfo with filename=null
    m_BackingFileInfo = new BackingFileInfo {
        filename = null,
        numVertsInFile = NumVerts,
        numTriIndicesInFile = NumTriIndices
    };
    m_IsResident = false;
    ClearBuffers();
  }

  /// Deletes internal buffers and arranges to reload them from the passed mesh when
  /// EnsureGeometryResident() is called.
  ///
  /// CopyToMesh() is _not_ called. It would be wasteful if the GeometryPool
  /// and Mesh are already in sync. The caller should call it first, if necessary.
  ///
  /// Pass:
  ///   mesh - A mesh containing the same data as in the GeometryPool.
  ///     Caller retains (conceptual) ownership of the Mesh.
  public void MakeGeometryNotResident(Mesh mesh) {
    Debug.Assert(m_IsResident);
    Debug.Assert(m_BackingMesh == null);
    Debug.Assert(m_BackingFileInfo == null);
    int oldVertexCount = NumVerts;
    int oldIndexCount = NumTriIndices;
    m_IsResident = false;
    m_BackingMesh = mesh;
    ClearBuffers();
    if (oldVertexCount != NumVerts || oldIndexCount != NumTriIndices) {
      Debug.LogWarning("Self check fail");
    }
  }

  // Helper for MakeGeometryNotResident() functions
  private void ClearBuffers() {
    // These are null to catch accidental/mistaken use when the geometry is not resident.
    m_Vertices = null;
    m_Tris = null;
    m_Normals = null;
    for (int channel = 0; channel < kNumTexcoords; ++channel) {
      InternalGetTexcoordDataRef(channel) = default;
    }
    m_Colors = null;
    m_Tangents = null;
  }

  /// TODO: this is another hack that should be replaced with some way
  /// of temporarily making the Pool resident without destroying backing data
  /// so we can cheaply and easily make it not-resident again.
  public Mesh GetBackingMesh() {
    return m_BackingMesh;
  }

  /// Re-fills internal buffers if necessary; after this, IsGeometryResident == true.
  /// Throws exceptions on failure (eg, backing file or mesh no longer exists, backing file
  /// is corrupt, etc)
  public void EnsureGeometryResident() {
    if (IsGeometryResident) {
      return;
    }
    if (!ReferenceEquals(m_BackingMesh, null)) {
      ReloadFromMesh();
    } else if (m_BackingFileInfo != null) {
      if (m_BackingFileInfo.filename == null) {
        throw new InvalidOperationException("You can't undo MakeGeometryPermanentlyNotResident()");
      }
      ReloadFromFile();
    } else {
      throw new InvalidOperationException("Invalid state");
    }
    Debug.Assert(IsGeometryResident);
  }

  // Goes from state (m_BackingMesh != null) to state (m_IsResident = true).
  private void ReloadFromMesh() {
    Debug.Assert(!ReferenceEquals(m_BackingMesh, null));
    if (m_BackingMesh == null) {
      throw new InvalidOperationException("Backing mesh was destroyed");
    }

    Mesh mesh = m_BackingMesh;
    m_BackingMesh = null;
    m_IsResident = true;

    m_Tris       = StealArrayForList(mesh.triangles);

    m_Vertices   = StealArrayForList(mesh.vertices);
    if (m_Layout.bUseNormals) {
      m_Normals  = StealArrayForList(mesh.normals);
    }
    if (m_Layout.bUseColors) {
      m_Colors   = StealArrayForList(mesh.colors32);
    }
    if (m_Layout.bUseTangents) {
      m_Tangents = StealArrayForList(mesh.tangents);
    }
    for (int channel = 0; channel < kNumTexcoords; ++channel) {
      TexcoordInfo txcInfo = m_Layout.GetTexcoordInfo(channel);
      TexcoordData fromMesh = GetTexcoordDataFromMesh(mesh, channel, txcInfo.size);
      InternalGetTexcoordDataRef(channel) = fromMesh;
    }
  }

  // Goes from state (m_BackingFileInfo != null) to state (m_IsResident = true).
  private void ReloadFromFile() {
    Debug.Assert(m_BackingFileInfo != null);
    if (!File.Exists(m_BackingFileInfo.filename)) {
      throw new InvalidOperationException("Backing file was destroyed");
    }
    try {
      using (FileStream stream = File.OpenRead(m_BackingFileInfo.filename)) {
        if (!DeserializeFromStream(stream)) {
          throw new InvalidOperationException("Backing file was corrupt");
        }
      }
    } finally {
      m_IsResident = true;
      ClearBackingFile();  // Safe because it won't throw exceptions
    }
  }

  // Helper for ReloadFromFile().
  // Always succeeds; never throws exceptions.
  // Clears m_BackingFileInfo; also deletes m_BackingFileInfo.filename if possible.
  private void ClearBackingFile() {
    if (m_BackingFileInfo == null) { return; }
    string backingFile = m_BackingFileInfo.filename;
    m_BackingFileInfo = null;
    if (backingFile != null && File.Exists(backingFile)) {
      try {
        File.Delete(backingFile);
      } catch (IOException e) {
        Debug.LogException(e);
      }
    }
  }

  public TexcoordData GetTexcoordData(int channel) {
    EnsureGeometryResident();
    return InternalGetTexcoordDataRef(channel);
  }

  // Does _not_ enforce geometry residency, and so is safe to use when reloading geometry
  private ref TexcoordData InternalGetTexcoordDataRef(int channel)  {
    switch (channel) {
    case 0: return ref m_Texcoord0;
    case 1: return ref m_Texcoord1;
    case 2: return ref m_Texcoord2;
    default: throw new ArgumentException("channel");
    }
  }

  static T[] SubArray<T>(List<T> list, int start, int length) {
    T[] subarray = new T[length];
    Array.Copy(list.GetBackingArray(), start, subarray, 0, length);
    return subarray;
  }

  static List<T> SubList<T>(List<T> list, int start, int length) {
    var sublist = new List<T>(length);
    sublist.AddRange(list.GetBackingArray(), start, length);
    return sublist;
  }

  /// Clear mesh and its vertex layout, then assigns only the necessary arrays.
  public void CopyToMesh(Mesh mesh) {
    EnsureGeometryResident();
    mesh.Clear(false);

    mesh.SetVertices (m_Vertices);
    mesh.SetTriangles(m_Tris, 0);

    if (m_Layout.bUseNormals) {
      mesh.SetNormals(m_Normals);
    }

    for (int channel = 0; channel < kNumTexcoords; ++channel) {
      var texcoordData = GetTexcoordData(channel);
      switch (m_Layout.GetTexcoordInfo(channel).size) {
      case 0: break;
      case 2: mesh.SetUVs(channel, texcoordData.v2); break;
      case 3: mesh.SetUVs(channel, texcoordData.v3); break;
      case 4: mesh.SetUVs(channel, texcoordData.v4); break;
      }
    }

    if (m_Layout.bUseColors) {
      mesh.SetColors(m_Colors);
    }

    if (m_Layout.bUseTangents) {
      mesh.SetTangents(m_Tangents);
    }
  }

  /// Like CopyToMesh(), except copies a sub-chunk of verts and triangles.
  /// It's assumed that the triangles do not reference any verts outside
  /// the given range.
  public void CopyToMesh(Mesh mesh, int iVert, int nVert, int iTriIndex, int nTriIndex) {
    EnsureGeometryResident();
    mesh.Clear(false);

    mesh.vertices = SubArray(m_Vertices, iVert, nVert);
    {
      int[] tris = SubArray(m_Tris, iTriIndex, nTriIndex);
      for (int i = 0; i < tris.Length; ++i) {
        tris[i] -= iVert;
      }
      mesh.triangles = tris;
    }

    if (m_Layout.bUseNormals) {
      mesh.normals  = SubArray(m_Normals,  iVert, nVert);
    }

    for (int channel = 0; channel < kNumTexcoords; ++channel) {
      TexcoordData texcoordData = GetTexcoordData(channel);
      switch (m_Layout.GetTexcoordInfo(channel).size) {
      case 0: break;
      case 2: mesh.SetUVs(channel, SubList(texcoordData.v2, iVert, nVert)); break;
      case 3: mesh.SetUVs(channel, SubList(texcoordData.v3, iVert, nVert)); break;
      case 4: mesh.SetUVs(channel, SubList(texcoordData.v4, iVert, nVert)); break;
      }
    }

    if (m_Layout.bUseColors) {
      mesh.colors32 = SubArray(m_Colors,   iVert, nVert);
    }
    if (m_Layout.bUseTangents) {
      mesh.tangents = SubArray(m_Tangents, iVert, nVert);
    }
  }

  static int GetVertexCount(Stroke stroke) {
    if (stroke.m_Type == Stroke.Type.BrushStroke) {
      return stroke.m_Object.GetComponent<MeshFilter>().sharedMesh.vertexCount;
    } else if (stroke.m_Type == Stroke.Type.BatchedBrushStroke) {
      return stroke.m_BatchSubset.m_VertLength;
    } else {
      throw new InvalidOperationException();
    }
  }

  /// Append all geometry from the specified stroke to this pool.
  /// Vertex layouts must be identical.
  /// Optional vertexLimit is the maximum number of vertices allowed in the pool.
  /// Returns false if the append would push the pool over the limit.
  public bool Append(Stroke stroke, int vertexLimit=0) {
    if (vertexLimit > 0) {
      int newCount = NumVerts + GetVertexCount(stroke);
      if (newCount > vertexLimit) {
        return false;
      }
    }
    EnsureGeometryResident();

    if (stroke.m_Type == Stroke.Type.BrushStroke) {
      Append(stroke.m_Object.GetComponent<MeshFilter>().sharedMesh,
             BrushCatalog.m_Instance.GetBrush(stroke.m_BrushGuid).VertexLayout);
    } else if (stroke.m_Type == Stroke.Type.BatchedBrushStroke) {
      Append(stroke.m_BatchSubset);
    } else {
      throw new InvalidOperationException();
    }

    return true;
  }

  /// Appends vertices, and topology for all submeshes, to this pool.
  ///
  /// Pass:
  ///   layout -
  ///     optional. The layout of the mesh, if you know it and want it to be
  ///     checked against the pool's Layout. If you want to be lenient about layout
  ///     mismatches, use the fallback* parameters instead.
  ///   fallbackColor -
  ///     optional. Used if the pool requires colors and the mesh lacks them.
  ///   useFallbackTexcoord -
  ///     optional. Fills in a default value if the pool requires texcoords and the mesh lacks them.
  ///
  /// Raises:
  ///   InvalidOperation if the mesh lacks attributes that the pool's Layout requires,
  ///   and no fallback has been provided.
  public void Append(Mesh mesh,
                     VertexLayout? layout = null,
                     Color32? fallbackColor = null,
                     bool useFallbackTexcoord = false) {
    int indexOffset = m_Vertices.Count;
    AppendVertexData(mesh, layout, fallbackColor, useFallbackTexcoord);

    int[] aTriIndices = mesh.triangles;
    for (int i = 0; i < aTriIndices.Length; ++i) {
      m_Tris.Add(indexOffset + aTriIndices[i]);
    }
    VerifySizes();
  }

  /// Appends vertices to this pool.
  public void AppendVertexData(
      Mesh mesh,
      VertexLayout? layout = null,
      Color32? fallbackColor = null,
      bool useFallbackTexcoord = false) {
    Debug.Assert(layout == null || m_Layout == layout);
    EnsureGeometryResident();
    VerifySizes();

    int meshVertexCount = mesh.vertexCount;

    m_Vertices.AddRange(mesh.vertices);

    if (m_Layout.bUseNormals) {
      var meshNormals = mesh.normals;
      if (meshNormals.Length != meshVertexCount) {
        throw new InvalidOperationException("Missing normal");
      }
      m_Normals.AddRange(mesh.normals);
    }

    // Copy texcoord data from src to dst.
    // If there is no src data, raises an exception or fills in default data.
    void CopyTexcoordWithDefault<T>(List<T> dst, List<T> src, int channel) {
      Debug.Assert(src.Count == 0 || src.Count == meshVertexCount, "Unity acting weird");
      if (src.Count == meshVertexCount) {
        dst.AddRange(src);
      } else if (useFallbackTexcoord) {
        dst.AddRange(Enumerable.Repeat(default(T), meshVertexCount));
      } else {
        throw new InvalidOperationException($"Missing texcoord{channel} data");
      }
    }

    for (int channel = 0; channel < kNumTexcoords; ++channel) {
      var txcInfo = m_Layout.GetTexcoordInfo(channel);
      var txc = GetTexcoordData(channel);
      switch (txcInfo.size) {
        case 2: {
          var t = new List<Vector2>();
          mesh.GetUVs(channel, t);
          CopyTexcoordWithDefault(txc.v2, t, channel);
          break;
        }
        case 3: {
          var t = new List<Vector3>();
          mesh.GetUVs(channel, t);
          CopyTexcoordWithDefault(txc.v3, t, channel);
          break;
        }
        case 4: {
          var t = new List<Vector4>();
          mesh.GetUVs(channel, t);
          CopyTexcoordWithDefault(txc.v4, t, channel);
          break;
        }
      }
    }

    if (m_Layout.bUseColors) {
      var meshColors32 = mesh.colors32;
      if (meshColors32.Length != meshVertexCount) {
        if (fallbackColor != null) {
          var color32 = fallbackColor.Value;
          for (int i = 0; i < meshVertexCount; ++i) { m_Colors.Add(color32); }
        } else {
          throw new InvalidOperationException("Missing color");
        }
      } else {
        m_Colors.AddRange(meshColors32);
      }
    }

    if (m_Layout.bUseTangents) {
      var meshTangents = mesh.tangents;
      if (meshTangents.Length != meshVertexCount) {
        throw new InvalidOperationException("Missing tangent");
      }
      m_Tangents.AddRange(mesh.tangents);
    }

    VerifySizes();
  }

  /// Append all geometry from the subset to this pool.
  /// Vertex layouts must be identical
  public void Append(BatchSubset subset) {
    GeometryPool geom = subset.m_ParentBatch.Geometry;
    Append(geom,
           subset.m_StartVertIndex, subset.m_VertLength,
           subset.m_iTriIndex, subset.m_nTriIndex);
  }

  /// Does not copy vertex format -- caller can do that, if desired
  /// Bad things will happen if the rhs does not have all the vertex
  /// data needed by lhs.
  public void Append(
      GeometryPool rhs, int iVert, int nVert,
      int iTriIndex, int nTriIndex,
      TrTransform? leftTransform = null) {
    EnsureGeometryResident();
    rhs.EnsureGeometryResident();
    if (m_Layout != rhs.m_Layout) {
      throw new ArgumentException("rhs: must have same layout");
    }

    // base vert index is shifting from iVert to m_Vertices.Count
    int iVertDest = m_Vertices.Count;
    int indexOffset = iVertDest - iVert;

    m_Vertices.AddRange(rhs.m_Vertices, iVert, nVert);
    if (m_Layout.bUseNormals) {
      m_Normals.AddRange(rhs.m_Normals, iVert, nVert);
    }

    for (int channel = 0; channel < kNumTexcoords; ++channel) {
      var txcData = GetTexcoordData(channel);
      var rhsTxcData = rhs.GetTexcoordData(channel);
      switch (m_Layout.GetTexcoordInfo(channel).size) {
      default:
      case 0: break;
      case 2: txcData.v2.AddRange(rhsTxcData.v2, iVert, nVert); break;
      case 3: txcData.v3.AddRange(rhsTxcData.v3, iVert, nVert); break;
      case 4: txcData.v4.AddRange(rhsTxcData.v4, iVert, nVert); break;
      }
    }

    if (m_Layout.bUseColors) {
      m_Colors.AddRange(rhs.m_Colors, iVert, nVert);
    }

    if (m_Layout.bUseTangents) {
      m_Tangents.AddRange(rhs.m_Tangents, iVert, nVert);
    }

    if (leftTransform.HasValue) {
      ApplyTransform(leftTransform.Value, iVertDest, nVert);
    }

    // Triangles
    var aTriIndices = rhs.m_Tris.GetBackingArray();
    int iTriDest = m_Tris.Count;
    m_Tris.SetCount(iTriDest + nTriIndex);
    int[] destTris = m_Tris.GetBackingArray();
    for (int i = 0; i < nTriIndex; ++i) {
      destTris[iTriDest + i] = indexOffset + aTriIndices[iTriIndex + i];
    }
  }

  /// Apply a transform to a subset of the geometry.
  ///
  /// Pass:
  ///   leftTransform -
  ///     The transform to apply.
  ///   i/nVert -
  ///     start/count of verts to transform
  public void ApplyTransform(TrTransform leftTransform, int iVert, int nVert) {
    ApplyTransform(leftTransform.ToMatrix4x4(),
                   TrTransform.R(leftTransform.rotation).ToMatrix4x4(),
                   leftTransform.scale,
                   iVert, nVert);
  }

  /// Apply a transform to a subset of the geometry.
  ///
  /// Pass:
  ///   xf -
  ///     Transform to apply to positions, vectors.
  ///
  ///   xfBivector -
  ///     Transform to apply to normals (unless they are Position or Vector semantic) and tangents.
  ///     There are two ways in which normals and tangents are not like positions, vectors:
  ///
  ///     1. They are bivectors -- cross products. They must honor this transform rule:
  ///          xf * (a x b) === (xf * a) x (xf * b)
  ///        Since each of (xf * a and (xf * b) get some scale, cross products get
  ///        the scale factored in _twice_.
  ///        See TrTransform.MultiplyBivector and its documentation for more info.
  ///
  ///     2. Unrelated to being bivectors, they are almost always constrained to be unit-length.
  ///        Typically this is implemented by constraining their scaling to 1 or -1.
  ///
  ///     If you put rules 1 and 2 together, you get (scale * scale) / abs(scale * scale)
  ///     which is always 1. Since normals don't get _translated_ either, typically just
  ///     pass xf.rotation.
  ///
  ///     EXCEPTION: you can add in some extra -1 scale if you need to flip your normals
  ///     in order to complete some winding change; see ExportUtils.cs.
  ///
  ///   xfDist -
  ///     Transform to apply to distance scalars. This should probably be Abs(xf.uniformScale),
  ///     but you have to pass it explicitly since nobody wants to extract scale from a mat4.
  public void ApplyTransform(
      Matrix4x4 xf,
      Matrix4x4 xfBivector,
      float xfDist,
      int iVert, int nVert) {

    // Transform vertex positions.
    int iVertEnd = iVert + nVert;
    ApplyTransformToVector3(xf, xfDist, iVert, iVertEnd, m_Vertices, Semantic.Position);

    // Transform vertex normals.
    if (m_Layout.bUseNormals) {
      if (m_Layout.normalSemantic == Semantic.Unspecified) {
        ApplyTransformToVector3(xfBivector, 1f, iVert, iVertEnd, m_Normals, Semantic.Vector);
      } else {
        ApplyTransformToVector3(xf, xfDist, iVert, iVertEnd, m_Normals, m_Layout.normalSemantic);
      }
    }

    // Transform uv sets
    for (int channel = 0; channel < kNumTexcoords; ++channel) {
      var txcData = GetTexcoordData(channel);
      var txcInfo = m_Layout.GetTexcoordInfo(channel);
      if (txcInfo.size == 3) {
        ApplyTransformToVector3(xf, xfDist, iVert, iVertEnd, txcData.v3, txcInfo.semantic);
      } else if (txcInfo.size == 4) {
        ApplyTransformToVector4(xf, xfDist, iVert, iVertEnd, txcData.v4, txcInfo.semantic);
      }
    }

    // Transform tangents.
    if (m_Layout.bUseTangents) {
      ApplyTransformToVector4(xfBivector, 1f, iVert, iVertEnd, m_Tangents, Semantic.Vector);
    }
  }

  /// Apply a transform to a subset of a list of Vector3 elements based on the semantic.
  /// Pass:
  ///   m        - the transform to apply to vectors
  ///   scale    - the transform to apply to distance scalars.
  ///   iVert    - start of verts to transform
  ///   iVertEnd - end (not inclusive) of verts to transform
  ///   v3List   - the list of vectors to transform
  ///   semantic - the semantic which tells us how to treat these vectors
  private static void ApplyTransformToVector3(Matrix4x4 mat, float scale, int iVert, int iVertEnd,
                                              List<Vector3> v3List, Semantic semantic) {
    switch (semantic) {
    default:
    case Semantic.Unspecified:
      // Logic error
      Debug.LogErrorFormat("Cannot transform Vector3 as {0}", semantic);
      break;
    case Semantic.Position:
      MathUtils.TransformVector3AsPoint(mat, iVert, iVertEnd, v3List.GetBackingArray());
      break;
    case Semantic.Vector:
      MathUtils.TransformVector3AsVector(mat, iVert, iVertEnd, v3List.GetBackingArray());
      break;
    case Semantic.XyIsUvZIsDistance:
      MathUtils.TransformVector3AsZDistance(scale, iVert, iVertEnd, v3List.GetBackingArray());
      break;
    case Semantic.Timestamp:
      break;
    }
  }

  /// Apply a transform to a subset of a list of Vector4 elements based on the semantic.
  /// Pass:
  ///   m        - the transform to apply.
  ///   scale    - the scale of the transform.
  ///   iVert    - start of verts to transform
  ///   iVertEnd - end (not inclusive) of verts to transform
  ///   v4List   - the list of vectors to transform
  ///   semantic - the semantic which tells us how to treat these vectors
  private static void ApplyTransformToVector4(Matrix4x4 mat, float scale, int iVert, int iVertEnd,
                                              List<Vector4> v4List, Semantic semantic) {
    switch (semantic) {
    case Semantic.Unspecified:
      break;
    case Semantic.Position:
      MathUtils.TransformVector4AsPoint(mat, iVert, iVertEnd, v4List.GetBackingArray());
      break;
    case Semantic.Vector:
      MathUtils.TransformVector4AsVector(mat, iVert, iVertEnd, v4List.GetBackingArray());
      break;
    case Semantic.XyIsUvZIsDistance:
      MathUtils.TransformVector4AsZDistance(scale, iVert, iVertEnd, v4List.GetBackingArray());
      break;
    case Semantic.Timestamp:
      break;
    }
  }

  /// Spits out warning and returns false if any data is mis-sized
  public bool VerifySizes() {
    bool ok = true;
    int nVert = m_Vertices.Count;

    if (m_Layout.bUseNormals) {
      ok &= (m_Normals.Count == nVert);
    }

    for (int channel = 0; channel < kNumTexcoords; ++channel) {
      var texcoordData = GetTexcoordData(channel);
      switch (m_Layout.GetTexcoordInfo(channel).size) {
      default:
      case 0: break;
      case 2: ok &= (texcoordData.v2.Count == nVert); break;
      case 3: ok &= (texcoordData.v3.Count == nVert); break;
      case 4: ok &= (texcoordData.v4.Count == nVert); break;
      }
    }

    if (m_Layout.bUseColors) {
      ok &= (m_Colors.Count == nVert);
    }

    if (m_Layout.bUseTangents) {
      ok &= (m_Tangents.Count == nVert);
    }

    if (!ok) {
      Debug.LogError("Arrays not correctly sized");
    }
    return ok;
  }

  private static void WriteTexcoordData(
      SketchBinaryWriter writer, TexcoordData texcoordData, int texcoordSize) {
    switch (texcoordSize) {
    case 2: writer.WriteLengthPrefixed(texcoordData.v2); break;
    case 3: writer.WriteLengthPrefixed(texcoordData.v3); break;
    case 4: writer.WriteLengthPrefixed(texcoordData.v4); break;
    }
  }

  /// Public only for unit-testing purposes; not general-purpose.
  /// Only the minimum state necessary for EnsureGeometryResident gets serialized.
  /// In particular: Layout, etc are skipped.
  public void SerializeToStream(Stream stream) {
    using (var writer = new SketchBinaryWriter(stream)) {
      writer.UInt32(kMagic);
      writer.WriteLengthPrefixed(m_Tris);
      writer.WriteLengthPrefixed(m_Vertices);
      if (m_Layout.bUseNormals) {
        writer.WriteLengthPrefixed(m_Normals);
      }
      if (m_Layout.bUseColors) {
        writer.WriteLengthPrefixed(m_Colors);
      }
      if (m_Layout.bUseTangents) {
        writer.WriteLengthPrefixed(m_Tangents);
      }
      for (int channel = 0; channel < kNumTexcoords; ++channel) {
        WriteTexcoordData(writer, GetTexcoordData(channel), m_Layout.GetTexcoordInfo(channel).size);
      }
    }
  }

  private static bool ReadTexcoordData(
      SketchBinaryReader reader, out TexcoordData texcoordData, int texcoordSize, int numVerts) {
    texcoordData = new TexcoordData();
    switch (texcoordSize) {
    case 0:
      return true;
    case 2:
      texcoordData.v2 = new List<Vector2>();
      return reader.ReadIntoExact(texcoordData.v2, numVerts);
    case 3:
      texcoordData.v3 = new List<Vector3>();
      return reader.ReadIntoExact(texcoordData.v3, numVerts);
    case 4:
      texcoordData.v4 = new List<Vector4>();
      return reader.ReadIntoExact(texcoordData.v4, numVerts);
    }
    throw new ArgumentException("texcoordSize");
  }

  /// Public only for unit-testing purposes; not general-purpose.
  /// this.Layout is used to validate the stream's contents.
  /// It is not read from (or even stored in) the stream.
  public bool DeserializeFromStream(Stream stream) {
    using (var reader = new SketchBinaryReader(stream)) {
      int numVerts = NumVerts;
      int numTriIndices = NumTriIndices;
      if (reader.UInt32() != kMagic) { return false; }

      if (m_Tris == null) { m_Tris = new List<int>(); }
      if (!reader.ReadIntoExact(m_Tris, numTriIndices)) { return false; }

      if (m_Vertices == null) { m_Vertices = new List<Vector3>(); }
      if (!reader.ReadIntoExact(m_Vertices, numVerts)) { return false; }

      if (m_Layout.bUseNormals) {
        if (m_Normals == null) { m_Normals = new List<Vector3>(); }
        if (!reader.ReadIntoExact(m_Normals, numVerts)) { return false; }
      }
      if (m_Layout.bUseColors) {
        if (m_Colors == null) { m_Colors = new List<Color32>(); }
        if (!reader.ReadIntoExact(m_Colors, numVerts)) { return false; }
      }
      if (m_Layout.bUseTangents) {
        if (m_Tangents == null) { m_Tangents = new List<Vector4>(); }
        if (!reader.ReadIntoExact(m_Tangents, numVerts)) { return false; }
      }
      for (int channel = 0; channel < kNumTexcoords; ++channel) {
        TexcoordInfo txcInfo = m_Layout.GetTexcoordInfo(channel);
        if (!ReadTexcoordData(reader, out TexcoordData txcData, txcInfo.size, numVerts)) {
          return false;
        }
        InternalGetTexcoordDataRef(channel) = txcData;
      }
    }
    return true;
  }
}

} // namespace TiltBrush
