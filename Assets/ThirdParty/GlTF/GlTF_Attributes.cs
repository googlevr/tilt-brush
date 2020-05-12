using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using AttributeInfo = GlTF_VertexLayout.AttributeInfo;

// TODO: merge GLTF_VertexLayout and TiltBrush.GeometryPool.VertexLayout
// (which to keep is up in the air) and figure out what to do about UVSemantic
using Semantic = TiltBrush.GeometryPool.Semantic;

public sealed class GlTF_Attributes {
  public readonly GlTF_VertexLayout m_layout;
  public readonly GlTF_Accessor normalAccessor;
  public readonly GlTF_Accessor colorAccessor;
  public readonly GlTF_Accessor tangentAccessor;
  public readonly GlTF_Accessor positionAccessor;
  public readonly GlTF_Accessor texCoord0Accessor;
  public readonly GlTF_Accessor texCoord1Accessor;
  public readonly GlTF_Accessor texCoord2Accessor;
  public readonly GlTF_Accessor texCoord3Accessor;

  private readonly Dictionary<string, GlTF_Accessor> m_accessors =
      new Dictionary<string, GlTF_Accessor>();

  public GlTF_Attributes(
      GlTF_Globals G, ObjectName meshName, GlTF_VertexLayout layout) {
    // This prefix should be used with possibly-nonconforming gltf data:
    // - texcoords that are not 2-element or that don't contain texture coordinates
    // - made-up / mythical semantics like VERTEXID
    string nonconformingPrefix = (G.GltfCompatibilityMode) ? "_TB_UNITY_" : "";

    m_layout = layout;

    {
      AttributeInfo positionInfo = layout.PositionInfo;
      positionAccessor = G.CreateAccessor(
          GlTF_Accessor.GetNameFromObject(meshName, "position"),
          positionInfo.accessorType, positionInfo.accessorComponentType);
      m_accessors.Add("POSITION", positionAccessor);
    }

    {if (layout.NormalInfo is AttributeInfo normalInfo) {
      normalAccessor = G.CreateAccessor(
          GlTF_Accessor.GetNameFromObject(meshName, "normal"),
          normalInfo.accessorType, normalInfo.accessorComponentType);
      // Genius particles put things that don't look like normals into the normal attribute.
      bool isNonconforming = (layout.m_tbLayout.normalSemantic == Semantic.Position);
      string prefix = isNonconforming ? nonconformingPrefix : "";
      m_accessors.Add(prefix+"NORMAL", normalAccessor);
    }}

    {if (layout.ColorInfo is AttributeInfo cInfo) {
      colorAccessor = G.CreateAccessor(
          GlTF_Accessor.GetNameFromObject(meshName, "color"),
          cInfo.accessorType, cInfo.accessorComponentType,
          normalized: true);
      m_accessors.Add(G.Gltf2 ? "COLOR_0" : "COLOR", colorAccessor);
    }}

    {if (layout.TangentInfo is AttributeInfo tangentInfo) {
      tangentAccessor = G.CreateAccessor(
          GlTF_Accessor.GetNameFromObject(meshName, "tangent"),
          tangentInfo.accessorType, tangentInfo.accessorComponentType);
      m_accessors.Add("TANGENT", tangentAccessor);
    }}

    if (layout.PackVertexIdIntoTexcoord1W) {
      // The vertexid hack modifies the gl layout to extend texcoord1 so the vertexid
      // can be stuffed into it
      Debug.Assert(layout.m_tbLayout.GetTexcoordInfo(1).size == 3);
      Debug.Assert(layout.GetTexcoordSize(1) == 4);
    }

    GlTF_Accessor MakeAccessorFor(int texcoord) {
      var txcInfo = layout.GetTexcoordInfo(texcoord);
      if (txcInfo == null) { return null; }
      Semantic tbSemantic = layout.m_tbLayout.GetTexcoordInfo(texcoord).semantic;
      string attrName = $"{nonconformingPrefix}TEXCOORD_{texcoord}";
      // Timestamps are tunneled into us via a texcoord because there's not really a better way
      // due to GeometryPool limitations. But that's an internal implementation detail. I'd like
      // them to have a better attribute name in the gltf.
      if (tbSemantic == Semantic.Timestamp) {
        // For b/141876882; Poly doesn't like _TB_TIMESTAMP
        if (! G.Gltf2) { return null; }
        attrName = "_TB_TIMESTAMP";
      }
      var ret = G.CreateAccessor(
          GlTF_Accessor.GetNameFromObject(meshName, $"uv{texcoord}"),
          txcInfo.Value.accessorType, txcInfo.Value.accessorComponentType);
      m_accessors.Add(attrName, ret);
      return ret;
    }

    texCoord0Accessor = MakeAccessorFor(0);
    texCoord1Accessor = MakeAccessorFor(1);
    texCoord2Accessor = MakeAccessorFor(2);
    texCoord3Accessor = MakeAccessorFor(3);

    if (G.GltfCompatibilityMode) {
      TiltBrush.GeometryPool.VertexLayout tbLayout = layout.m_tbLayout;
      switch (tbLayout.texcoord0.semantic) {
        case Semantic.Unspecified when tbLayout.texcoord0.size == 2:
        case Semantic.XyIsUv:
        case Semantic.XyIsUvZIsDistance: {
          GlTF_Accessor accessor = GlTF_Accessor.CloneWithDifferentType(
              G, texCoord0Accessor, GlTF_Accessor.Type.VEC2);
          m_accessors.Add("TEXCOORD_0", accessor);
          break;
        }
      }
      // No need to check the other texcoords because TB only ever puts texture coordinates
      // in texcoord0
    }
  }

  private void PopulateUv(
      int channel, TiltBrush.GeometryPool pool, GlTF_Accessor accessor,
      Semantic semantic) {
    bool packVertId = m_layout.PackVertexIdIntoTexcoord1W && channel == 1;
    if (packVertId) {
      // Guaranteed by GlTF_VertexLayout
      Debug.Assert(m_layout.m_tbLayout.GetTexcoordInfo(channel).size == 3);
      Debug.Assert(m_layout.GetTexcoordSize(channel) == 4);
    }
    if (accessor == null) {
      return;
    }
    if (channel < 0 || channel > 3) {
      throw new ArgumentException("Invalid channel");
    }
    TiltBrush.GeometryPool.TexcoordData texcoordData = pool.GetTexcoordData(channel);

    if (semantic == Semantic.XyIsUvZIsDistance && accessor.type != GlTF_Accessor.Type.VEC3) {
      throw new ArgumentException("XyIsUvZIsDistance semantic can only be applied to VEC3");
    }

    bool flipY;
    if (semantic == Semantic.Unspecified && channel == 0 &&
        accessor.type == GlTF_Accessor.Type.VEC2) {
      Debug.LogWarning("Assuming Semantic.XyIsUv");
      semantic = Semantic.XyIsUv;
    }
    switch (semantic) {
    case Semantic.Position:
    case Semantic.Vector:
    case Semantic.Timestamp:
      flipY = false;
      break;
    case Semantic.XyIsUvZIsDistance:
    case Semantic.XyIsUv:
      flipY = true;
      break;
    default:
      throw new ArgumentException("semantic");
    }

    switch (accessor.type) {
    case GlTF_Accessor.Type.SCALAR:
      throw new NotImplementedException();

    case GlTF_Accessor.Type.VEC2:
      accessor.Populate(texcoordData.v2, flipY: flipY, calculateMinMax: false);
      break;

    case GlTF_Accessor.Type.VEC3:
      accessor.Populate(texcoordData.v3, flipY: flipY, calculateMinMax: false);
      break;

    case GlTF_Accessor.Type.VEC4:
      if (packVertId) {
        // In the vertexId case, we actually have a vec3, which needs to be augmented to a vec4.
        // TODO: this should happen at some higher level.
        int i = 0;
        var v4 = texcoordData.v3.ConvertAll<Vector4>((v => new Vector4(v.x, v.y, v.z, i++)));
        accessor.Populate(v4, flipY: flipY, calculateMinMax: false);
      } else {
        accessor.Populate(texcoordData.v4, flipY: flipY, calculateMinMax: false);
      }
      break;

    default:
      throw new ArgumentException("Unexpected accessor.type");
    }
  }

  public void Populate(TiltBrush.GeometryPool pool) {
    positionAccessor.Populate(pool.m_Vertices, flipY: false, calculateMinMax: true);
    if (normalAccessor != null) {
      normalAccessor.Populate(pool.m_Normals, flipY: false, calculateMinMax: false);
    }
    if (colorAccessor != null) {
      colorAccessor.Populate(pool.m_Colors);
    }
    if (tangentAccessor != null) {
      tangentAccessor.Populate(pool.m_Tangents, flipY: false, calculateMinMax: false);
    }

    // UVs may be 1, 2, 3 or 4 element tuples, which the following helper method resolves.
    // In the case of zero UVs, the texCoord accessor will be null and will not be populated.
    Debug.Assert(TiltBrush.GeometryPool.kNumTexcoords == 3);
    Debug.Assert(texCoord3Accessor == null);
    var layout = pool.Layout;
    PopulateUv(0, pool, texCoord0Accessor, layout.texcoord0.semantic);
    PopulateUv(1, pool, texCoord1Accessor, layout.texcoord1.semantic);
    PopulateUv(2, pool, texCoord2Accessor, layout.texcoord2.semantic);
    PopulateUv(3, pool, texCoord3Accessor, Semantic.Unspecified);
  }

  public void WriteAsNamedJObject(GlTF_Globals G, string name) {
    // Recreates the ordering of the old code, for easier diffing.

    G.WriteKeyAndIndentIn(name, "{");
    // Recreate ordering of the old code for easier diffing.
    int OldOrder(string attribute) {
      switch (attribute) {
        case "POSITION": return 0;
        case "NORMAL": return 1;
        case "COLOR_0":
        case "COLOR": return 2;
        case "TANGENT": return 3;
        case "VERTEXID": return 4;
        default: return 5;
      }
    }
    // Sort dictionary to make output deterministic.
    foreach (var keyValue in m_accessors.OrderBy(kvp => (OldOrder(kvp.Key), kvp.Key))) {
      G.CNI.WriteNamedReference(keyValue.Key, keyValue.Value);
    }
    G.NewlineAndIndentOut("}");
  }

  public IEnumerable<GlTF_ReferencedObject> IterReferences(GlTF_Globals G) {
    return m_accessors.Select(keyValue => keyValue.Value);
  }
}
