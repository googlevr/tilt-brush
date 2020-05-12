using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public sealed class GlTF_Technique : GlTF_ReferencedObject {
  public enum Type {
    FLOAT = 5126,
    FLOAT_VEC2 = 35664,
    FLOAT_VEC3 = 35665,
    FLOAT_VEC4 = 35666,
    FLOAT_MAT3 = 35675,
    FLOAT_MAT4 = 35676,
    SAMPLER_2D = 35678
  }

  public enum Enable {
    BLEND = 3042,
    CULL_FACE = 2884,
    DEPTH_TEST = 2929,
    POLYGON_OFFSET_FILL = 32823,
    SAMPLE_ALPHA_TO_COVERAGE = 32926,
    SCISSOR_TEST = 3089
  }

  [System.Serializable]
  public enum Semantic {
    UNKNOWN,
    POSITION,
    NORMAL,
    TANGENT,
    COLOR,
    TEXCOORD_0,  // Texcoords 0-4 must be contiguous in this enum!
    TEXCOORD_1,
    TEXCOORD_2,
    TEXCOORD_3,
    MODELVIEW,
    PROJECTION,
    MODELVIEWINVERSETRANSPOSE,
    CESIUM_RTC_MODELVIEW
  }

  public class Parameter {
    public string name;
    public Type type;
    public Semantic semantic = Semantic.UNKNOWN;
    public GlTF_Node node;
  }

  public class Attribute {
    public string name;
    public string param;
  }

  public class Uniform {
    public string name;
    public string param;
  }

  public class States {
    public List<Enable> enable;
    public Dictionary<string, Value> functions = new Dictionary<string, Value>();
  }

  public sealed class Value : GlTF_Writer {
    public enum Type {
      Unknown,
      Bool,
      Int,
      Float,
      Color,
      Vector2,
      Vector4,
      IntArr,
      BoolArr
    }

    private bool boolValue;
    private int intValue;
    private float floatValue;
    private Color colorValue;
    private Vector2 vector2Value;
    private Vector4 vector4Value;
    private int[] intArrValue;
    private bool[] boolArrvalue;
    private Type type = Type.Unknown;

    public Value(GlTF_Globals globals, bool value) : base(globals) {
      boolValue = value;
      type = Type.Bool;
    }

    public Value(GlTF_Globals globals, int value) : base(globals) {
      intValue = value;
      type = Type.Int;
    }

    public Value(GlTF_Globals globals, float value) : base(globals) {
      floatValue = value;
      type = Type.Float;
    }

    public Value(GlTF_Globals globals, Color value) : base(globals) {
      colorValue = value;
      type = Type.Color;
    }

    public Value(GlTF_Globals globals, Vector2 value) : base(globals) {
      vector2Value = value;
      type = Type.Vector2;
    }

    public Value(GlTF_Globals globals, Vector4 value) : base(globals) {
      vector4Value = value;
      type = Type.Vector4;
    }

    public Value(GlTF_Globals globals, int[] value) : base(globals) {
      intArrValue = value;
      type = Type.IntArr;
    }

    public Value(GlTF_Globals globals, bool[] value) : base(globals) {
      boolArrvalue = value;
      type = Type.BoolArr;
    }

    private void WriteArr<T>(T arr) where T : ArrayList {
      jsonWriter.Write("[");
      for (var i = 0; i < arr.Count; ++i) {
        jsonWriter.Write(arr[i].ToString().ToLower());
        if (i != arr.Count - 1) {
          jsonWriter.Write(", ");
        }
      }
      jsonWriter.Write("]");
    }

    public void Write() {
      switch (type) {
        case Type.Bool:
          jsonWriter.Write("[" + boolValue.ToString().ToLower() + "]");
          break;

        case Type.Int:
          jsonWriter.Write("[" + intValue + "]");
          break;

        case Type.Float:
          jsonWriter.Write("[" + floatValue + "]");
          break;

        case Type.Color:
          jsonWriter.Write("[" + colorValue.r + ", " + colorValue.g + ", " + colorValue.b + ", " + colorValue.a + "]");
          break;

        case Type.Vector2:
          jsonWriter.Write("[" + vector2Value.x + ", " + vector2Value.y + "]");
          break;

        case Type.Vector4:
          jsonWriter.Write("[" + vector4Value.x + ", " + vector4Value.y + ", " + vector4Value.z + ", " + vector4Value.w + "]");
          break;

        case Type.IntArr:
          WriteArr(new ArrayList(intArrValue));
          break;

        case Type.BoolArr:
          WriteArr(new ArrayList(boolArrvalue));
          break;

      }
    }
  }

  public string program;
  public List<Attribute> attributes = new List<Attribute>();
  public List<Parameter> parameters = new List<Parameter>();
  public List<Uniform> uniforms = new List<Uniform>();
  public States states = new States();

  public static string GetNameFromObject(TiltBrush.IExportableMaterial iem) {
    return $"technique_{iem.UniqueName:D}";
  }

  public GlTF_Technique(GlTF_Globals globals) : base(globals) {}
  public void AddDefaultUniforms(bool rtc) {
    var tParam = new Parameter();
    tParam.name = "modelViewMatrix";
    tParam.type = Type.FLOAT_MAT4;
    tParam.semantic = rtc ? Semantic.CESIUM_RTC_MODELVIEW : Semantic.MODELVIEW;
    parameters.Add(tParam);
    var uni = new Uniform();
    uni.name = "u_modelViewMatrix";
    uni.param = tParam.name;
    uniforms.Add(uni);

    tParam = new Parameter();
    tParam.name = "projectionMatrix";
    tParam.type = Type.FLOAT_MAT4;
    tParam.semantic = Semantic.PROJECTION;
    parameters.Add(tParam);
    uni = new Uniform();
    uni.name = "u_projectionMatrix";
    uni.param = tParam.name;
    uniforms.Add(uni);

    tParam = new Parameter();
    tParam.name = "normalMatrix";
    tParam.type = Type.FLOAT_MAT3;
    tParam.semantic = Semantic.MODELVIEWINVERSETRANSPOSE;
    parameters.Add(tParam);
    uni = new Uniform();
    uni.name = "u_normalMatrix";
    uni.param = tParam.name;
    uniforms.Add(uni);
  }

  public override IEnumerable<GlTF_ReferencedObject> IterReferences() {
    yield return G.Lookup<GlTF_Program>(program);
    foreach (var p in parameters) {
      if (p.node != null) {
        yield return G.Lookup(p.node);
      }
    }
  }

  public override void WriteTopLevel() {
    BeginGltfObject();

    // This has a bunch of object references in it which won't work in gltf2.
    // Thankfully, gltf2 doesn't (yet?) support Technique.
    Debug.Assert(!G.Gltf2);

    G.CNI.WriteNamedReference<GlTF_Program>("program", program);

    G.CNI.WriteKeyAndIndentIn("parameters", "{");
    foreach (var p in parameters) {
      G.CNI.WriteKeyAndIndentIn(p.name, "{");
      G.CNI.WriteNamedInt("type", (int)p.type);
      if (p.semantic != Semantic.UNKNOWN) {
        G.CNI.WriteNamedString("semantic", p.semantic.ToString());
      }
      if (p.node != null) {
        G.CNI.WriteNamedReference("node", p.node);
      }
      G.NewlineAndIndentOut("}");  // parameters.<p.name>
    }
    G.NewlineAndIndentOut("}");  // parameters

    G.CNI.WriteKeyAndIndentIn("attributes", "{");
    foreach (var a in attributes) {
      G.CNI.WriteNamedString(a.name, a.param);
    }
    G.NewlineAndIndentOut("}");  // attributes

    G.CNI.WriteKeyAndIndentIn("uniforms", "{");
    foreach (var u in uniforms) {
      G.CNI.WriteNamedString(u.name, u.param);
    }
    G.NewlineAndIndentOut("}");  // uniforms

    G.CNI.WriteKeyAndIndentIn("states", "{");
    if (states != null && states.enable != null) {
      G.CNI.WriteKeyAndIndentIn("enable", "[");
      foreach (var en in states.enable) {
        CommaNL(); Indent(); jsonWriter.Write((int) en);
      }
      G.NewlineAndIndentOut("]");  // states.enable
    }
    if (states != null && states.functions.Count > 0) {
      G.CNI.WriteKeyAndIndentIn("functions", "{");
      foreach (var fun in states.functions) {
        CommaNL(); Indent(); jsonWriter.Write("\"" + fun.Key + "\": ");
        fun.Value.Write();
      }
      G.NewlineAndIndentOut("}");  // states.functions
    }

    G.NewlineAndIndentOut("}");  // states

    EndGltfObject();
  }
}
