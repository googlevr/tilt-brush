// TODO: I've removed editor dependencies by stripping out some functionality:
// * animations
using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;

// GlTF_Writer contains helper code for emitting json to a text stream.
// It is not meant to be directly instantiated; inherit from it instead.
public class GlTF_Writer {
  // Returns existing technique based on name of material.
  public static GlTF_Technique GetTechnique(
      GlTF_Globals G, TiltBrush.IExportableMaterial exportableMaterial) {
    var name = GlTF_Technique.GetNameFromObject(exportableMaterial);
    Debug.Assert(G.techniques.ContainsKey(name));
    return G.techniques[name];
  }

  // Creates new technique based on name of material.
  public static GlTF_Technique CreateTechnique(
      GlTF_Globals G, TiltBrush.IExportableMaterial exportableMaterial) {
    var name = GlTF_Technique.GetNameFromObject(exportableMaterial);
    Debug.Assert(!G.techniques.ContainsKey(name));
    var ret = new GlTF_Technique(G);
    ret.name = name;
    G.techniques.Add(name, ret);
    return ret;
  }

  // Instance API

  public GlTF_Globals m_globals;

  public string name; // name of this object

  public GlTF_Globals G { get { return m_globals; } }
  public StreamWriter jsonWriter { get { return G.jsonWriter; } }

  public GlTF_Writer(GlTF_Globals globals) {
    m_globals = globals;
  }

  public void Indent() { G.Indent(); }

  public void IndentIn() { G.IndentIn(); }

  public void IndentOut() { G.IndentOut(); }

  public void CommaNL() { G.CommaNL(); }
}