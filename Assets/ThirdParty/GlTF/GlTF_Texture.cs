using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public sealed class GlTF_Texture : GlTF_ReferencedObject {
  /*
        "texture_O21_jpg": {
            "format": 6408,
            "internalFormat": 6408,
            "sampler": "sampler_0",
            "source": "O21_jpg",
            "target": 3553,
            "type": 5121
        },
*/
  private readonly int format = 6408;
  private readonly int internalFormat = 6408;
  private readonly GlTF_Sampler sampler;
  private readonly GlTF_Image source;
  private readonly int target = 3553;
  private readonly int tType = 5121;

  public static GlTF_Texture LookupOrCreate(
      GlTF_Globals G, GlTF_Image img, GlTF_Sampler sampler, string proposedName=null) {
    string name = "texture_" + (proposedName ?? $"{img.name}_{sampler.name}");
    if (! G.textures.ContainsKey(name)) {
      G.textures.Add(name, new GlTF_Texture(G, name, img, sampler));
    }
    return G.textures[name];
  }

  private GlTF_Texture(
      GlTF_Globals globals,
      string name, GlTF_Image source, GlTF_Sampler sampler)
      : base(globals) {
    this.name = name;
    this.source = source;
    this.sampler = sampler;
  }

  public override IEnumerable<GlTF_ReferencedObject> IterReferences() {
    yield return G.Lookup(sampler);
    yield return G.Lookup(source);
  }

  public override void WriteTopLevel() {
    BeginGltfObject();
    if (! G.Gltf2) {
      G.CNI.WriteNamedInt("format", format);
      G.CNI.WriteNamedInt("internalFormat", internalFormat);
    }
    G.CNI.WriteNamedReference("sampler", sampler);
    G.CNI.WriteNamedReference("source", source);
    if (! G.Gltf2) {
      G.CNI.WriteNamedInt("target", target);
      G.CNI.WriteNamedInt("type", tType);
    }
    EndGltfObject();
  }
}
