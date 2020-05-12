using UnityEngine;
using System.Collections;

public class GlTF_Rotation : GlTF_FloatArray4 {
  public GlTF_Rotation(GlTF_Globals globals, Quaternion q) : base(globals) {
    name = "rotation"; minItems = 4; maxItems = 4; items = new float[] { q.x, q.y, q.z, q.w };
  }
  public void WriteRotation() {
	  WriteValsAsNamedArray();
  }
  /*
	public override void Write()
	{
		Indent();		jsonWriter.Write ("\"rotation\": [ ");
		WriteVals();
		jsonWriter.Write ("]");
	}
*/
}
