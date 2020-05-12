using System;
using System.Collections.Generic;
using System.Linq;
using TiltBrush;
using UnityEngine;

public sealed class GlTF_Accessor : GlTF_ReferencedObject {
  public enum Type {
    SCALAR,
    VEC2,
    VEC3,
    VEC4
  }

  public enum ComponentType {
    BYTE = 5120,
    UNSIGNED_BYTE = 5121,
    SHORT = 5122,
    USHORT = 5123,
    UNSIGNED_INT = 5125,
    FLOAT = 5126
  }

  static int GetSize(ComponentType t) {
    switch (t) {
      case ComponentType.UNSIGNED_BYTE:
      case ComponentType.BYTE: return 1;
      case ComponentType.SHORT:
      case ComponentType.USHORT: return 2;
      case ComponentType.UNSIGNED_INT: return 4;
      case ComponentType.FLOAT: return 4;
      default: throw new InvalidOperationException($"Unknown {t}");
    }
  }

  // The inverse of GetTypeForNumComponents
  static int GetNumComponents(Type t) {
    switch (t) {
      case Type.SCALAR: return 1;
      case Type.VEC2: return 2;
      case Type.VEC3: return 3;
      case Type.VEC4: return 4;
      default: throw new InvalidOperationException($"Unknown {t}");
    }
  }

  // The inverse of GetNumComponents
  public static Type GetTypeForNumComponents(int size) {
    switch (size) {
      case 1: return Type.SCALAR;
      case 2: return Type.VEC2;
      case 3: return Type.VEC3;
      case 4: return Type.VEC4;
      default: throw new InvalidOperationException($"Cannot convert size {size}");
    }
  }

  /// Returns an accessor that uses a differenttype.
  /// This is useful if you want to create (for example) a VEC2 view of a buffer that holds VEC4s.
  /// If the type is smaller, you get a new accessor that uses the same bufferview
  /// If the type is the same, you get the same accessor back.
  /// If the type is bigger, you get an exception
  public static GlTF_Accessor CloneWithDifferentType(
      GlTF_Globals G, GlTF_Accessor fromAccessor, Type newType) {
    if (newType == fromAccessor.type) { return fromAccessor; }
    var ret = new GlTF_Accessor(G, fromAccessor, newType);
    G.accessors.Add(ret);
    return ret;
  }

  // Instance API

  public readonly GlTF_BufferView bufferView;//	"bufferView": "bufferView_30",
  public int byteStride;// ": 12,
  // GL enum vals ": BYTE (5120), UNSIGNED_BYTE (5121), SHORT (5122), UNSIGNED_SHORT (5123), FLOAT
  // (5126)
  public readonly Type type = Type.SCALAR;
  public readonly ComponentType componentType;
  private readonly bool m_normalized;

  // These are only assigned in Populate(), and only the most-ancestral gets populated.
  // All cloned accessors inherit these from their ancestor.
  private int? count;
  private long? byteOffset;

  // The 2.0 spec has this to say about min/max:
  // "While these properties are not required for all accessor usages, there are cases when minimum
  //  and maximum must be defined. Refer to other sections of this specification for details."
  //
  // "POSITION accessor must have min and max properties defined."
  //
  // "Animation Sampler's input accessor must have min and max properties defined."
  //
  // There are no other references to other cases where they're required)

  // Not changing these to nullables because it would be a total pain.
  private bool m_haveMinMax = false;
  private Vector4 maxFloat;
  private Vector4 minFloat;
  private int minInt;
  private int maxInt;

  private GlTF_Accessor m_clonedFrom;

  private GlTF_Accessor Ancestor {
    get {
      if (m_clonedFrom != null) {
        Debug.Assert(count == null && byteOffset == null, "Clone must inherit from ancestor");
        return m_clonedFrom.Ancestor;
      }
      return this;
    }
  }

  public int Count => Ancestor.count.Value;
  public long ByteOffset => Ancestor.byteOffset.Value;
  public Vector4 MaxFloat => Ancestor.maxFloat;
  public Vector4 MinFloat => Ancestor.minFloat;
  public int MinInt => Ancestor.minInt;
  public int MaxInt => Ancestor.maxInt;

  // Private to force people to use the better-named CloneWithDifferentType() method.
  private GlTF_Accessor(GlTF_Globals G, GlTF_Accessor fromAccessor, Type newType)
      : base(G) {
    m_clonedFrom = fromAccessor;
    if (newType >= fromAccessor.type) {
      throw new ArgumentException("newType must be smaller than fromAccessor.type");
    }
    this.name          = $"{fromAccessor.name}_{newType}";
    this.bufferView    = fromAccessor.bufferView;
    this.byteStride    = fromAccessor.byteStride;
    this.type          = newType;
    this.componentType = fromAccessor.componentType;
    // Leave these null; at serialization time, we "inherit" a value from m_clonedFrom
    // this.count      = fromAccessor.count;
    // this.byteOffset    = fromAccessor.byteOffset;
    // These aren't nullables because the purity isn't worth the pain, but at least poison them
    this.maxFloat      = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
    this.minFloat      = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
    this.minInt        = 0x0D00B015;
    this.maxInt        = 0x0D00B015;

    SanityCheckBufferViewStride();
  }

  // Pass:
  //   normalized -
  //      true if integral values are intended to be values in [0, 1]
  //      For convenience, this parameter is ignored if the ComponentType is non-integral.
  public GlTF_Accessor(
      GlTF_Globals globals, string n, Type t, ComponentType c,
      GlTF_BufferView bufferView,
      bool isNonVertexAttributeAccessor,
      bool normalized)
      : base(globals) {
    name = n;
    type = t;
    componentType = c;
    bool isIntegral = (c != ComponentType.FLOAT);
    m_normalized = isIntegral && normalized;
    this.bufferView = bufferView;

    // I think (but am not sure) that we can infer whether this is a vertex attribute or some
    // other kind of accessor by what the "target" of the bufferview is.
    // All bufferviews except for ushortBufferView use kTarget.ARRAY_BUFFER.
    // The old code used to look at Type (SCALAR implies non-vertex-attribute), but I
    // think vertexId is a counterexample of a scalar vertex attribute.
    Debug.Assert(isNonVertexAttributeAccessor ==
                 (bufferView.target == GlTF_BufferView.kTarget_ELEMENT_ARRAY_BUFFER));

    int packedSize = GetSize(componentType) * GetNumComponents(type);
    if (isNonVertexAttributeAccessor) {
      // Gltf2 rule is that bufferView.byteStride may only be set if the accessor is for
      // a vertex attributes. I think gltf1 is the same way.
      byteStride = 0;
    } else if (type == Type.SCALAR && !G.Gltf2) {
      // Old gltf1 code used to use "packed" for anything of Type.SCALAR.
      // I'm going to replicate that odd behavior for ease of diffing old vs new .glb1.
      // As long as stride == packedSize then it's mostly safe to omit the stride, at least in gltf1
      Debug.Assert(byteStride == 0 || byteStride == packedSize);
      byteStride = 0;
    } else {
      byteStride = packedSize;
    }

    SanityCheckBufferViewStride();
  }

  private void SanityCheckBufferViewStride() {
    int packedSize = GetSize(componentType) * GetNumComponents(type);
    // Check that all Accessors that use this bufferview agree on the stride to use.
    // See docs on m_byteStride and m_packedSize.
    if (bufferView != null) {
      Debug.Assert(byteStride == (bufferView.m_byteStride ?? byteStride));
      bufferView.m_byteStride = byteStride;

      if (byteStride == 0) {
        // Also check for agreement on packed size -- I am not sure how gltf could
        // tell if a SCALAR+UNSIGNED_SHORT and a SCALAR+UNSIGNED_INT accessor both
        // tried to use the same bufferview.
        Debug.Assert(packedSize == (bufferView.m_packedSize ?? packedSize));
        bufferView.m_packedSize = packedSize;
      }
    }
  }

  public static string GetNameFromObject(ObjectName o, string name) {
    return "accessor_" + name + "_" + o.ToGltf1Name();
  }

  private void InitMinMaxInt() {
    m_haveMinMax = true;
    maxInt = int.MinValue;
    minInt = int.MaxValue;
  }

  private void InitMinMaxFloat() {
    m_haveMinMax = true;
    float min = float.MinValue;
    float max = float.MaxValue;
    maxFloat = new Vector4(min, min, min, min);
    minFloat = new Vector4(max, max, max, max);
  }

  // Raises exception if our type does not match
  private void RequireType(Type t, ComponentType c) {
    if (this.type != t || this.componentType != c) {
      throw new InvalidOperationException($"Cannot write {t} {c} to {type} {componentType}");
    }
  }

  public void Populate(List<Color32> colorList) {
    // gltf2 spec says that only position and animation inputs _require_ min/max
    // so I'm going to skip it.
    this.byteOffset = this.bufferView.currentOffset;
    this.count = colorList.Count;
    if (componentType == ComponentType.FLOAT) {
      RequireType(Type.VEC4, ComponentType.FLOAT);
      Color[] colorArray = colorList.Select(c32 => (Color)c32).ToArray();
      this.bufferView.FastPopulate(colorArray, colorArray.Length);
    } else {
      RequireType(Type.VEC4, ComponentType.UNSIGNED_BYTE);
      this.bufferView.FastPopulate(colorList);
    }
  }

  public void PopulateUshort(int[] vs) {
    RequireType(Type.SCALAR, ComponentType.USHORT);
    byteOffset = bufferView.currentOffset;
    bufferView.PopulateUshort(vs);
    count = vs.Length;
    // TODO: try to remove
    if (count > 0) {
      InitMinMaxInt();
      for (int i = 0; i < count; ++i) {
        maxInt = Mathf.Max(vs[i], maxInt);
        minInt = Mathf.Min(vs[i], minInt);
      }
    }
  }

  // flipY -
  //   true if value.xy is a UV, and if a UV axis convention swap is needed
  //   glTF defines uv axis conventions to be u right, v down -- origin top-left
  //   Ref: https://github.com/KhronosGroup/glTF/tree/master/specification/2.0, search for "uv"
  //   Unity defines uv axis conventions to be u right, v up -- origin bottom-left
  //   Ref: https://docs.unity3d.com/ScriptReference/Texture2D.GetPixelBilinear.html
  // calculateMinMax -
  //   gltf2 spec says that only position and animation inputs _require_ min/max.
  //   It's safe to pass false if it's neither of those things.
  //   It's always safe to pass true, but it wastes CPU
  public void Populate(List<Vector2> v2s, bool flipY, bool calculateMinMax) {
    if (flipY) {
      v2s = v2s.Select(v => new Vector2(v.x, 1f - v.y)).ToList();
    }
    RequireType(Type.VEC2, ComponentType.FLOAT);
    byteOffset = bufferView.currentOffset;
    count = v2s.Count;
    bufferView.FastPopulate(v2s);
    if (calculateMinMax && count > 0) {
      InitMinMaxFloat();
      for (int i = 0; i < v2s.Count; i++) {
        maxFloat = Vector2.Max(v2s[i], maxFloat);
        minFloat = Vector2.Min(v2s[i], minFloat);
      }
    }
  }

  public void Populate(List<Vector3> v3s, bool flipY, bool calculateMinMax) {
    if (flipY) {
      v3s = v3s.Select(v => new Vector3(v.x, 1f - v.y, v.z)).ToList();
    }
    RequireType(Type.VEC3, ComponentType.FLOAT);
    byteOffset = bufferView.currentOffset;
    count = v3s.Count;
    bufferView.FastPopulate(v3s);
    if (calculateMinMax && count > 0) {
      InitMinMaxFloat();
      for (int i = 0; i < v3s.Count; i++) {
        maxFloat = Vector3.Max(v3s[i], maxFloat);
        minFloat = Vector3.Min(v3s[i], minFloat);
      }
    }
  }

  public void Populate(List<Vector4> v4s, bool flipY, bool calculateMinMax) {
    if (flipY) {
      v4s = v4s.Select(v => new Vector4(v.x, 1f - v.y, v.z, v.w)).ToList();
    }
    RequireType(Type.VEC4, ComponentType.FLOAT);
    byteOffset = bufferView.currentOffset;
    count = v4s.Count;
    bufferView.FastPopulate(v4s);
    if (calculateMinMax && count > 0) {
      InitMinMaxFloat();
      for (int i = 0; i < v4s.Count; i++) {
        maxFloat = Vector4.Max(v4s[i], maxFloat);
        minFloat = Vector4.Min(v4s[i], minFloat);
      }
    }
  }

  // Writes either the integral or floating-point value(s), based on type and componentType
  private void WriteNamedTypedValue(string name, int i, Vector4 fs) {
    string val = null;
    if (componentType == ComponentType.FLOAT) {
      switch (type) {
        case Type.SCALAR: val = $"{fs.x:G9}"; break;
        case Type.VEC2:   val = $"{fs.x:G9}, {fs.y:G9}"; break;
        case Type.VEC3:   val = $"{fs.x:G9}, {fs.y:G9}, {fs.z:G9}"; break;
        case Type.VEC4:   val = $"{fs.x:G9}, {fs.y:G9}, {fs.z:G9}, {fs.w:G9}"; break;
      }
    } else if (componentType == ComponentType.USHORT) {
      if (type == Type.SCALAR) {
        val = i.ToString();
      }
    }
    if (val == null) {
      throw new InvalidOperationException($"Unhandled: {type} {componentType}");
    }
    jsonWriter.Write($"\"{name}\": [ {val} ]");
  }

  public override IEnumerable<GlTF_ReferencedObject> IterReferences() {
    yield return G.Lookup(bufferView);
  }

  public override void WriteTopLevel() {
    BeginGltfObject();
    G.CNI.WriteNamedReference("bufferView", bufferView);
    G.CNI.WriteNamedInt("byteOffset", ByteOffset);
    if (!G.Gltf2) {
      G.CNI.WriteNamedInt("byteStride", byteStride);
    }
    G.CNI.WriteNamedInt("componentType", (int)componentType);
    if (G.Gltf2 && m_normalized) {
      G.CNI.WriteNamedBool("normalized", m_normalized);
    }

    var inheritedCount = Count;
    G.CNI.WriteNamedInt("count", inheritedCount);
    // min and max are not well-defined if count == 0
    if (m_haveMinMax && inheritedCount > 0) {
      G.CommaNL(); G.Indent(); WriteNamedTypedValue("max", MaxInt, MaxFloat);
      G.CommaNL(); G.Indent(); WriteNamedTypedValue("min", MinInt, MinFloat);
    }
    G.CNI.WriteNamedString("type", type.ToString());

    EndGltfObject();
  }
}
