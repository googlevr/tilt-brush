using System;
using UnityEngine;

using GeometryPool = TiltBrush.GeometryPool;
using ComponentType = GlTF_Accessor.ComponentType;

// A descriptive structure that allows Unity mesh vertex attributes to be inspected.
// Unfortunately, Unity provides no such interface.
public struct GlTF_VertexLayout : IEquatable<GlTF_VertexLayout> {
  // Contains types to be used for a particular attribute
  // accessor{Type,ComponentType} are used for the mesh data on disk / in gpu
  // techniqueType is used in gltf1 only, for the types used by the shader
  public struct AttributeInfo : IEquatable<AttributeInfo> {
    public readonly GlTF_Technique.Type techniqueType;
    public readonly GlTF_Accessor.Type accessorType;
    public readonly GlTF_Accessor.ComponentType accessorComponentType;

    public AttributeInfo(GlTF_Accessor.Type accessorType,
                         GlTF_Accessor.ComponentType accessorComponentType) {
      this.accessorType = accessorType;
      this.accessorComponentType = accessorComponentType;
      switch (accessorType) {
        case GlTF_Accessor.Type.SCALAR: techniqueType = GlTF_Technique.Type.FLOAT; break;
        case GlTF_Accessor.Type.VEC2:   techniqueType = GlTF_Technique.Type.FLOAT_VEC2; break;
        case GlTF_Accessor.Type.VEC3:   techniqueType = GlTF_Technique.Type.FLOAT_VEC3; break;
        case GlTF_Accessor.Type.VEC4:   techniqueType = GlTF_Technique.Type.FLOAT_VEC4; break;
        default:
          throw new ArgumentException("accessorType");
      }
    }

    #region Auto-generated equality
    public bool Equals(AttributeInfo other) {
      return techniqueType == other.techniqueType &&
             accessorType == other.accessorType &&
             accessorComponentType == other.accessorComponentType;
    }

    public override bool Equals(object obj) {
      return obj is AttributeInfo other && Equals(other);
    }

    public override int GetHashCode() {
      unchecked {
        var hashCode = (int) techniqueType;
        hashCode = (hashCode * 397) ^ (int) accessorType;
        hashCode = (hashCode * 397) ^ (int) accessorComponentType;
        return hashCode;
      }
    }

    public static bool operator ==(AttributeInfo left, AttributeInfo right) {
      return left.Equals(right);
    }

    public static bool operator !=(AttributeInfo left, AttributeInfo right) {
      return !left.Equals(right);
    }
    #endregion
  }

  public readonly GeometryPool.VertexLayout m_tbLayout;
  private readonly int m_texcoord0Size;
  private readonly int m_texcoord1Size;  // This might vary from tbLayout's because of vertexId :-P
  private readonly int m_texcoord2Size;
  private readonly int m_texcoord3Size;

  public readonly AttributeInfo PositionInfo;
  public readonly AttributeInfo? NormalInfo;
  public readonly AttributeInfo? ColorInfo;
  public readonly AttributeInfo? TangentInfo;

  /// If this is set, these things are guaranteed:
  ///   m_tbLayout.GetTexcoordInfo(1).size == 3   // the geometry from TB does not use .w ...
  ///   this.GetTexcoordSize(1) == 4              // ... so gltf puts the vertex id there
  public bool PackVertexIdIntoTexcoord1W;

  /// Convert a Tilt Brush VertexLayout to a GlTF VertexLayout.
  public GlTF_VertexLayout(GlTF_Globals G, GeometryPool.VertexLayout tbLayout) {
    bool suppressVertexId = G.Gltf2 || G.GltfCompatibilityMode;
    m_tbLayout = tbLayout;

    this.PositionInfo = new AttributeInfo(
        GlTF_Accessor.Type.VEC3, GlTF_Accessor.ComponentType.FLOAT);
    this.NormalInfo = tbLayout.bUseNormals
        ? new AttributeInfo(GlTF_Accessor.Type.VEC3, GlTF_Accessor.ComponentType.FLOAT)
        : default(AttributeInfo?);
    // Keep using float colors for gltf1 because it breaks Poly (uint8 values are not normalized)
    // and Poly Toolkit. It's possible gltf1 just doesn't support uint8 color.
    var colorType = (G.Gltf2 ? ComponentType.UNSIGNED_BYTE : ComponentType.FLOAT);
    this.ColorInfo = tbLayout.bUseColors
        ? new AttributeInfo(GlTF_Accessor.Type.VEC4, colorType)
        : default(AttributeInfo?);
    this.TangentInfo = tbLayout.bUseTangents
        ? new AttributeInfo(GlTF_Accessor.Type.VEC4, GlTF_Accessor.ComponentType.FLOAT)
        : default(AttributeInfo?);

    Debug.Assert(GeometryPool.kNumTexcoords == 3);
    this.m_texcoord0Size = (tbLayout.texcoord0.size);
    this.m_texcoord1Size = (tbLayout.texcoord1.size);
    this.m_texcoord2Size = (tbLayout.texcoord2.size);
    this.m_texcoord3Size = 0;

    // Poly can't currently load custom attributes via the GLTFLoader (though glTF 1.0 does
    // support this), so we pack them into uv1.w, assuming uv1 starts out as a three element UV
    // channel.
    if (!tbLayout.bUseVertexIds || suppressVertexId) {
      PackVertexIdIntoTexcoord1W = false;
    } else if (m_texcoord1Size == 3) {
      PackVertexIdIntoTexcoord1W = true;
      m_texcoord1Size = 4;
    } else {
      throw new InvalidOperationException("Can't have vertex ids unless texcoord1 size == 3'");
    }
  }

  // Returns the number of components in that texcoord: 0, 2, 3, or 4.
  // 0 means there is no data.
  [System.Diagnostics.Contracts.Pure]
  public int GetTexcoordSize(int texcoord) {
    switch (texcoord) {
      case 0: return m_texcoord0Size;
      case 1: return m_texcoord1Size;
      case 2: return m_texcoord2Size;
      case 3: return m_texcoord3Size;
      default: throw new ArgumentException("texcoord");
    }
  }

  // Returns null if the layout says there's no data in the specified texcoord
  public AttributeInfo? GetTexcoordInfo(int texcoord) {
    var numComponents = GetTexcoordSize(texcoord);
    if (numComponents == 0) { return null; }
    return new AttributeInfo(
        GlTF_Accessor.GetTypeForNumComponents(numComponents),
        GlTF_Accessor.ComponentType.FLOAT);
  }

  #region Auto-generated equality
  public bool Equals(GlTF_VertexLayout other) {
    return m_texcoord0Size == other.m_texcoord0Size &&
           m_texcoord1Size == other.m_texcoord1Size &&
           m_texcoord2Size == other.m_texcoord2Size &&
           m_texcoord3Size == other.m_texcoord3Size &&
           PositionInfo.Equals(other.PositionInfo) &&
           Nullable.Equals(NormalInfo, other.NormalInfo) &&
           Nullable.Equals(ColorInfo, other.ColorInfo) &&
           Nullable.Equals(TangentInfo, other.TangentInfo) &&
           PackVertexIdIntoTexcoord1W == other.PackVertexIdIntoTexcoord1W;
  }

  public override bool Equals(object obj) {
    return obj is GlTF_VertexLayout other && Equals(other);
  }

  public override int GetHashCode() {
    unchecked {
      int hashCode = m_texcoord0Size;
      hashCode = (hashCode * 397) ^ m_texcoord1Size;
      hashCode = (hashCode * 397) ^ m_texcoord2Size;
      hashCode = (hashCode * 397) ^ m_texcoord3Size;
      hashCode = (hashCode * 397) ^ PositionInfo.GetHashCode();
      hashCode = (hashCode * 397) ^ NormalInfo.GetHashCode();
      hashCode = (hashCode * 397) ^ ColorInfo.GetHashCode();
      hashCode = (hashCode * 397) ^ TangentInfo.GetHashCode();
      hashCode = (hashCode * 397) ^ PackVertexIdIntoTexcoord1W.GetHashCode();
      return hashCode;
    }
  }

  public static bool operator ==(GlTF_VertexLayout left, GlTF_VertexLayout right) {
    return left.Equals(right);
  }

  public static bool operator !=(GlTF_VertexLayout left, GlTF_VertexLayout right) {
    return !left.Equals(right);
  }
  #endregion
}