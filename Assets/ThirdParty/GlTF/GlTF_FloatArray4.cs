using UnityEngine;
using System.Collections;

public class GlTF_FloatArray4 : GlTF_FloatArray {
  public GlTF_FloatArray4(GlTF_Globals globals) : base(globals) {
    minItems = 4;
    maxItems = 4;
    items = new float[] { 1.0f, 0.0f, 0.0f, 0.0f };
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
