using UnityEngine;
using System.Collections;

public class GlTF_MaterialTexture : GlTF_ColorOrTexture {
  public GlTF_Texture texture;

  public GlTF_MaterialTexture(GlTF_Globals globals, string n, GlTF_Texture t) : base(globals) {
    name = n; texture = t;
  }
  public override void WriteColorOrTexture() {
    Indent(); jsonWriter.Write("\"" + name + "\": \"" + texture.name + "\"");
  }
}
