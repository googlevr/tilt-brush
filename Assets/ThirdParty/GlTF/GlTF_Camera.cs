using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public abstract class GlTF_Camera : GlTF_ReferencedObject {
  public string type;  // should be enum ": "perspective"
  public GlTF_Camera(GlTF_Globals globals) : base(globals) {}
  public override void WriteTopLevel() { WriteCamera(); }
  public abstract void WriteCamera();
  public override IEnumerable<GlTF_ReferencedObject> IterReferences() {
    yield break;
  }
}
