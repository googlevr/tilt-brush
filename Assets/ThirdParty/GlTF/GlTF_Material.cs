using System.Collections.Generic;
using System.Linq;

using TiltBrush;
using UnityEngine;

public sealed class GlTF_Material : GlTF_ReferencedObject {
  // These names come from ImportMaterialCollector.GetExportableMaterial
  const string kBaseColorFactor = "BaseColorFactor";
  const string kBaseColorTex = "BaseColorTex";
  // These names come from a rigid(?) naming convention used by our brush materials
  const string kMainTex = "MainTex";
  const string kBumpMap = "BumpMap";  // this actually refers to a normal map, not a bump map

  public abstract class KeyValue {
    public string key;

    public abstract void Write(GlTF_Globals G);

    // Like WriteKeyValue(), but writes out the key alternateKey.
    public void Write(GlTF_Globals G, string writtenKey) {
      // super yucky
      string prev = this.key;
      this.key = writtenKey;
      try {
        Write(G);
      } finally {
        this.key = prev;
      }
    }
  }

  public class ColorKV : KeyValue {
    public Color color;

    public override void Write(GlTF_Globals G) {
      G.jsonWriter.Write("\"" + key + "\": [");
      G.jsonWriter.Write(color.r.ToString() + ", " + color.g.ToString() + ", " + color.b.ToString() + ", " + color.a.ToString());
      G.jsonWriter.Write("]");
    }
  }

  public class VectorKV : KeyValue {
    public Vector4 vector;

    public override void Write(GlTF_Globals G) {
      G.jsonWriter.Write("\"" + key + "\": [");
      G.jsonWriter.Write(vector.x.ToString() + ", " + vector.y.ToString() + ", " + vector.z.ToString() + ", " + vector.w.ToString());
      G.jsonWriter.Write("]");
    }
  }

  public class FloatKV : KeyValue {
    public float value;

    public override void Write(GlTF_Globals G) {
      G.jsonWriter.Write("\"" + key + "\": " + value + "");
    }
  }

  public class TextureKV : KeyValue {
    public readonly GlTF_Texture m_texture;
    public TextureKV(string key, GlTF_Texture texture) {
      this.key = key;
      m_texture = texture;
    }
    public override void Write(GlTF_Globals G) {
      G.WriteNamedReference(key, m_texture);
    }
  }

  public string instanceTechniqueName = "technique1";
  public GlTF_ColorOrTexture ambient;// = new GlTF_ColorRGBA ("ambient");
  public GlTF_ColorOrTexture diffuse;
  public float shininess;
  public GlTF_ColorOrTexture specular;// = new GlTF_ColorRGBA ("specular");
  public List<KeyValue> values = new List<KeyValue>();

  public readonly IExportableMaterial ExportableMaterial;

  // Returns the name used by the actual GlTF_Material.
  // This is what is referred to throughout the code as "matName" or "material.name".
  public static string GetNameFromObject(IExportableMaterial exportableMaterial) {
    return $"material_{exportableMaterial.UniqueName:D}";
  }

  // Public only for use by GlTF_Globals
  public GlTF_Material(GlTF_Globals globals, IExportableMaterial exportableMaterial)
      : base(globals) {
    this.ExportableMaterial = exportableMaterial;
    this.name = GlTF_Material.GetNameFromObject(exportableMaterial);
    // PresentationNameOverride is set by GlTF_Globals in order to make it unique-ish
  }

  public override IEnumerable<GlTF_ReferencedObject> IterReferences() {
    return IterReferencesWithNulls().Where(objref => objref != null);
  }

  // Warning: can return nulls
  private IEnumerable<GlTF_ReferencedObject> IterReferencesWithNulls() {
    if (G.Gltf2 && ExportableMaterial != null) {
      Dictionary<string, KeyValue> valuesByName = values.ToDictionary(v => v.key);
      GlTF_Texture GetTextureOrNull(string name) {
        if (valuesByName.TryGetValue(name, out KeyValue kv) &&
            kv is TextureKV textureKv) {
          return textureKv.m_texture;
        } else {
          return null;
        }
      }
      // Man... it's a real pain keeping the IterReferences() logic parallel to the Write() logic.
      // Maybe we should do the hacky thing and implement IterReferences by running Write() and
      // looking for calls to SerializeRef().
      if (HasDynamicExportableMaterialPbr(valuesByName)) {
        yield return GetTextureOrNull(kBaseColorTex);
      } else if (HasBrushDescriptorPbr(valuesByName)) {
        yield return GetTextureOrNull(kMainTex);
        yield return GetTextureOrNull(kBumpMap);
      }
    } else if (!G.Gltf2) {
      yield return G.Lookup<GlTF_Technique>(instanceTechniqueName);
      foreach (KeyValue kv in values) {
        if (kv is TextureKV tkv) {
          yield return G.Lookup(tkv.m_texture);
        }
      }
    }
  }

  // Returns true if this is a DynamicExportableMaterial with enough info in it
  // to create a reasonable gltf2 material
  private bool HasDynamicExportableMaterialPbr(Dictionary<string, KeyValue> valuesByName) {
    // I guess we could also look at the dynamic type of ExportableMaterial to see if it's
    // DynamicExportableMaterial.
    return valuesByName.ContainsKey(kBaseColorFactor);
  }

  // Converts material properties that came from a DynamicExportableMaterial
  // from TB- and Poly-specific gltf1 to generic gltf2 pbr.
  private void WriteDynamicExportableMaterialAsPbr(
      Dictionary<string, KeyValue> valuesByName) {
    // These names "BaseColorFactor", "BaseColorTex" come from
    // ImportMaterialCollector.GetExportableMaterial()
    G.CNI.WriteKeyAndIndentIn("pbrMetallicRoughness", "{");
    MaybeWrite(valuesByName, kBaseColorFactor, "baseColorFactor");
    if (valuesByName.ContainsKey(kBaseColorTex)) {
      G.CNI.WriteKeyAndIndentIn("baseColorTexture", "{");
      MaybeWrite(valuesByName, kBaseColorTex, "index");
      int kHackTexcoord = 0;  // Just a guess and will only work for single-texcoord PBRs
      G.CNI.WriteNamedInt("texCoord", kHackTexcoord);
      G.NewlineAndIndentOut("}");  // baseColorTexture {}
    }
    MaybeWrite(valuesByName, "MetallicFactor", "metallicFactor");
    MaybeWrite(valuesByName, "RoughnessFactor", "roughnessFactor");
    G.NewlineAndIndentOut("}");  // pbrMetallicRoughness {}
  }

  // Returns true if this is a BrushDescriptor with enough info in it
  // to create a reasonable gltf2 material.
  private bool HasBrushDescriptorPbr(Dictionary<string, KeyValue> valuesByName) {
    // Could probably see if ExportableMaterial is a BrushDescriptor, but there's not
    // much point generating pbrMetallicRoughness{} if the brush doesn't use any textures
    return valuesByName.ContainsKey(kMainTex) || valuesByName.ContainsKey(kBumpMap);
  }

  // Converts material properties that came from a BrushDescriptor as a gltf2 pbr.
  private void WriteBrushDescriptorAsPbr(
      Dictionary<string, KeyValue> valuesByName) {
    // The names BumpMap, MainTex, Shininess derive from the naming conventions of the Unity
    // materials we use for our brushes. If it seems fragile to try and generically handle
    // all our fairly arbitrarily-authored brush materials with one conversion function,
    // you'd be correct!  But this is just best-effort.  TBT will ignore the pbr for
    // brush materials since it finds a guid.

    int kHackTexcoord = 0;  // Just a guess and will only work for single-texcoord PBRs

    G.CNI.WriteKeyAndIndentIn("pbrMetallicRoughness", "{");
    MaybeWrite(valuesByName, "BaseColorFactor", "baseColorFactor");
    if (valuesByName.TryGetValue(kMainTex, out KeyValue mainTexGeneric) &&
        mainTexGeneric is TextureKV mainTex) {
      G.CNI.WriteKeyAndIndentIn("baseColorTexture", "{");
      CommaNL(); Indent(); mainTex.Write(G, "index");
      G.CNI.WriteNamedInt("texCoord", kHackTexcoord);
      G.NewlineAndIndentOut("}");  // baseColorTexture {}
    }
    G.CNI.WriteNamedFloat("metallicFactor", 0);
    if (valuesByName.TryGetValue("Shininess", out KeyValue shininessGeneric) &&
        shininessGeneric is FloatKV shininess) {
      G.CNI.WriteNamedFloat("roughnessFactor", 1-shininess.value);
    }
    G.NewlineAndIndentOut("}");  // pbrMetallicRoughness {}

    if (valuesByName.TryGetValue(kBumpMap, out KeyValue normalMapGeneric) &&
        normalMapGeneric is TextureKV normalMap) {
      G.CNI.WriteKeyAndIndentIn("normalTexture", "{");
      CommaNL(); Indent(); normalMap.Write(G, "index");
      G.CNI.WriteNamedInt("texCoord", kHackTexcoord);
      G.NewlineAndIndentOut("}");
    }
  }

  public override void WriteTopLevel() {
    BeginGltfObject();

    if (G.Gltf2 && ExportableMaterial != null) {
      G.CNI.WriteNamedString("alphaMode", ConvertAlphaMode(ExportableMaterial.BlendMode));

      if (ExportableMaterial.FloatParams.TryGetValue("Cutoff", out float alphaCutoff)) {
        G.CNI.WriteNamedFloat("alphaCutoff", alphaCutoff);
      }

      if (! ExportableMaterial.EnableCull) {
        G.CNI.WriteNamedBool("doubleSided", true);
      }

      Dictionary<string, KeyValue> valuesByName = values.ToDictionary(v => v.key);

      if (HasDynamicExportableMaterialPbr(valuesByName)) {
        WriteDynamicExportableMaterialAsPbr(valuesByName);
      } else if (HasBrushDescriptorPbr(valuesByName)) {
        WriteBrushDescriptorAsPbr(valuesByName);
      }
    } else if (!G.Gltf2) {
      G.CNI.WriteNamedReference<GlTF_Technique>("technique", instanceTechniqueName);
      G.CNI.WriteNamedJObject("values", values, v => v.Write(m_globals));
    }

    G.CNI.WriteNamedString("name", PresentationName);
    if (G.UseTiltBrushMaterialExtension && ExportableMaterial is BrushDescriptor desc) {
      G.CNI.WriteKeyAndIndentIn("extensions", "{");
      G.CNI.WriteKeyAndIndentIn(GlTF_Globals.kTiltBrushMaterialExtensionName, "{");
      G.CNI.WriteNamedString("guid", desc.m_Guid.ToString());
      G.NewlineAndIndentOut("}");
      G.NewlineAndIndentOut("}");
    }

    EndGltfObject();
  }

  // Converts ExportableMaterialBlendMode to glTF 2 alphaMode
  static string ConvertAlphaMode(ExportableMaterialBlendMode mode) {
    switch (mode) {
      case ExportableMaterialBlendMode.None: return "OPAQUE";
      case ExportableMaterialBlendMode.AdditiveBlend:  // glTF doesn't support additive, but BLEND
                                                       // looks less bad than OPAQUE, so fall
                                                       // through here.
      case ExportableMaterialBlendMode.AlphaBlend: return "BLEND";
      case ExportableMaterialBlendMode.AlphaMask: return "MASK";
      default:
        // Add warning?
        return "OPAQUE";
    }
  }

  // Calls values[key].WriteValue(writtenKey), if it exists.
  private void MaybeWrite(
      Dictionary<string, KeyValue> values, string key,
      string writtenKey) {
    if (! values.TryGetValue(key, out KeyValue value)) { return; }
    CommaNL(); Indent(); value.Write(G, writtenKey);
  }
}
