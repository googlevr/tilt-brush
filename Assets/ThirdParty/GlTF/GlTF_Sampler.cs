using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public sealed class GlTF_Sampler : GlTF_ReferencedObject {
  public enum MagFilter {
    NEAREST = 9728,
    LINEAR = 9729
  }

  public enum MinFilter {
    NEAREST = 9728,
    LINEAR = 9729,
    NEAREST_MIPMAP_NEAREST = 9984,
    LINEAR_MIPMAP_NEAREST = 9985,
    NEAREST_MIPMAP_LINEAR = 9986,
    LINEAR_MIPMAP_LINEAR = 9987
  }

  public enum Wrap {
    CLAMP_TO_EDGE = 33071,
    MIRRORED_REPEAT = 33648,
    REPEAT = 10497
  }

  private readonly MagFilter magFilter;
  private readonly MinFilter minFilter;
  private readonly Wrap wrap;

  /// Use this instead of the constructor, in order to share samplers.
  public static GlTF_Sampler LookupOrCreate(
      GlTF_Globals G, MagFilter magFilter, MinFilter minFilter, Wrap wrap = Wrap.REPEAT) {
    // Samplers are only distinguished by their filter settings, so no need for
    // unique naming beyond that.
    string name = "sampler_" + magFilter + "_" + minFilter + "_" + wrap;
    if (!G.samplers.ContainsKey(name)) {
      G.samplers[name] = new GlTF_Sampler(G, name, magFilter, minFilter, wrap);
    }
    return G.samplers[name];
  }

  // Use LookupOrCreate() instead
  private GlTF_Sampler(GlTF_Globals globals,
                       string name, MagFilter magFilter, MinFilter minFilter, Wrap wrap)
      : base(globals) {
    this.name = name;
    this.magFilter = magFilter;
    this.minFilter = minFilter;
    this.wrap = wrap;
  }

  public override IEnumerable<GlTF_ReferencedObject> IterReferences() {
    yield break;
  }

  public override void WriteTopLevel() {
    BeginGltfObject();
    Indent(); jsonWriter.Write("\"magFilter\": " + (int) magFilter + ",\n");
    Indent(); jsonWriter.Write("\"minFilter\": " + (int) minFilter + ",\n");
    Indent(); jsonWriter.Write("\"wrapS\": " + (int) wrap + ",\n");
    Indent(); jsonWriter.Write("\"wrapT\": " + (int) wrap);
    EndGltfObject();
  }
}
