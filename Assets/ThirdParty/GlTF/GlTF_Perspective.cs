using UnityEngine;
using System.Collections;

public class GlTF_Perspective : GlTF_Camera {
  public float aspect_ratio;
  public float yfov;//": 37.8492,
  public float zfar;//": 100,
  public float znear;//": 0.01
  public GlTF_Perspective(GlTF_Globals globals) : base(globals) {
    type = "perspective";
  }
  public override void WriteCamera() {
    /*
	        "camera_0": {
            "perspective": {
                "yfov": 45,
                "zfar": 3162.76,
                "znear": 12.651
            },
            "type": "perspective"
        }
	*/
    BeginGltfObject();

    Indent(); jsonWriter.Write("\"perspective\": {\n");
    IndentIn();
    Indent(); jsonWriter.Write("\"aspect_ratio\": " + aspect_ratio.ToString() + ",\n");
    Indent(); jsonWriter.Write("\"yfov\": " + yfov.ToString() + ",\n");
    Indent(); jsonWriter.Write("\"zfar\": " + zfar.ToString() + ",\n");
    Indent(); jsonWriter.Write("\"znear\": " + znear.ToString() + "\n");
    IndentOut();
    Indent(); jsonWriter.Write("},\n");
    Indent(); jsonWriter.Write("\"type\": \"perspective\"");

    EndGltfObject();
  }
}
