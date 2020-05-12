using UnityEngine;
using System.Collections;

public class GlTF_MaterialColor : GlTF_ColorOrTexture {
  public GlTF_ColorRGBA color = null;

  // XXX: remove?
  // public GlTF_MaterialColor(GlTF_Globals globals) : base(globals) {
  //   color = new GlTF_ColorRGBA(globals, "diffuse");
  // }

  public GlTF_MaterialColor(GlTF_Globals globals, string n, Color c) : base(globals) {
    name = n;
    color = new GlTF_ColorRGBA(globals, name, c);
  }


  public override void WriteColorOrTexture() {
    //		Indent();		jsonWriter.Write ("\"" + name + "\": ");
    color.Write();
  }
}
