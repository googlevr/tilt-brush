using UnityEngine;
using System.Collections;

public sealed class GlTF_Channel : GlTF_Writer {
  public GlTF_AnimSampler sampler;
  public GlTF_Target target;

  public GlTF_Channel(GlTF_Globals globals, string ch, GlTF_AnimSampler s) : base(globals) {
    sampler = s;
    switch (ch) {
      case "translation":
        break;
      case "rotation":
        break;
      case "scale":
        break;
    }
  }

  public void Write() {
    IndentIn();
    Indent(); jsonWriter.Write("{\n");
    IndentIn();
    Indent(); jsonWriter.Write("\"sampler\": \"" + sampler.name + "\",\n");
    target.Write();
    jsonWriter.WriteLine();
    IndentOut();
    Indent(); jsonWriter.Write("}");
    IndentOut();
  }
}
