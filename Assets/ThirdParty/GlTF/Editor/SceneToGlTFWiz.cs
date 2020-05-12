// This original implementation is left here for reference, but it's been replaced by
// google/GlTF_EditorExporter.cs (with most of the logic now residing in
// google/GlTF_ScriptableExporter.cs).

#if false
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
/***************************************************************************
GlamExport
 - Unity3D Scriptable Wizard to export Hierarchy or Project objects as glTF


****************************************************************************/

using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text;
using System.Reflection;


public class SceneToGlTFWiz : EditorWindow {
  //	static public List<GlTF_Accessor> Accessors;
  //	static public List<GlTF_BufferView> BufferViews;
  static public GlTF_Writer writer;

  private const string KEY_PATH = "GlTFPath";
  private const string KEY_FILE = "GlTFFile";

  static public string path = "?";
  private static XmlDocument xdoc;
  private static string savedPath = EditorPrefs.GetString(KEY_PATH, "/");
  private static string savedFile = EditorPrefs.GetString(KEY_FILE, "test.gltf");

  private static Preset preset = new Preset();
  public static UnityEngine.TextAsset presetAsset;

  public interface RTCCallback {
    double[] GetCenter(Transform transform);
  }
  public interface RotationCallback {
    Matrix4x4 GetBoundsRotationMatrix(Transform transform);
    Matrix4x4 GetNodeRotationMatrix(Transform transform);
  }
  public static MonoScript rtcScript;
  public static MonoScript rotScript;
  public static bool unpackTexture = false;
  public static bool copyShaders = false;

  [MenuItem("File/Export/glTF")]
  private static void CreateWizard() {
    savedPath = EditorPrefs.GetString(KEY_PATH, "/");
    savedFile = EditorPrefs.GetString(KEY_FILE, "test.gltf");
    path = savedPath + "/" + savedFile;
    //		ScriptableWizard.DisplayWizard("Export Selected Stuff to glTF", typeof(SceneToGlTFWiz), "Export");

    SceneToGlTFWiz window = (SceneToGlTFWiz) EditorWindow.GetWindow(typeof(SceneToGlTFWiz));
    window.Show();
  }

  private void OnWizardUpdate() {
    //		Texture[] txs = Selection.GetFiltered(Texture, SelectionMode.Assets);
    //		Debug.Log("found "+txs.Length);
  }

  private void OnWizardCreate() // Create (Export) button has been hit (NOT wizard has been created!)
  {
    /*
        Object[] deps = EditorUtility.CollectDependencies  (trs);
        foreach (Object o in deps)
        {
          Debug.Log("obj "+o.name+"  "+o.GetType());
        }
    */
    var ext = GlTF_Writer.binary ? (GlTF_Writer.b3dm ? "b3dm" : "glb") : "gltf";
    path = EditorUtility.SaveFilePanel("Save glTF file as", savedPath, savedFile, ext);
    if (path.Length != 0) {
      Transform[] trs = Selection.GetTransforms(SelectionMode.Deep);

      Transform root = null;
      // find root, the one with no parent
      for (var i = 0; i < trs.Length; ++i) {
        if (trs[i].parent == null) {
          root = trs[i];
          break;
        }
      }

      Export(path, trs, root);
    }
  }

  private void OnGUI() {
    GUILayout.Label("Export Options");
    GlTF_Writer.binary = GUILayout.Toggle(GlTF_Writer.binary, "Binary GlTF");
    if (GlTF_Writer.binary) {
      GlTF_Writer.b3dm = GUILayout.Toggle(GlTF_Writer.b3dm, "B3dm");
    } else {
      GlTF_Writer.b3dm = false;
    }

    copyShaders = GUILayout.Toggle(copyShaders, "Copy shaders");

    presetAsset = EditorGUILayout.ObjectField("Preset file", presetAsset, typeof(UnityEngine.TextAsset), false) as UnityEngine.TextAsset;
    rtcScript = EditorGUILayout.ObjectField("Cesium RTC Callback", rtcScript, typeof(MonoScript), false) as MonoScript;
    rotScript = EditorGUILayout.ObjectField("Rotation Callback", rotScript, typeof(MonoScript), false) as MonoScript;

    if (rtcScript != null) {
      var name = typeof(RTCCallback).FullName;
      var ci = rtcScript.GetClass().GetInterface(name);
      if (ci == null) {
        rtcScript = null;
      }
    }

    if (rotScript != null) {
      var name = typeof(RotationCallback).FullName;
      var ci = rotScript.GetClass().GetInterface(name);
      if (ci == null) {
        rotScript = null;
      }
    }

    if (GUILayout.Button("Export")) {
      OnWizardCreate();
    }
  }

  public static BoundsDouble Export(string path, Transform[] trs, Transform root) {
    double minHeight = 0, maxHeight = 0;
    return Export(path, trs, root, out minHeight, out maxHeight);
  }

  public static BoundsDouble Export(string path, Transform[] trs, Transform root, out double minHeight, out double maxHeight) {
    minHeight = 0;
    maxHeight = 0;

    writer = new GlTF_Writer();
    writer.Init();

    if (presetAsset != null) {
      string psPath = AssetDatabase.GetAssetPath(presetAsset);
      if (psPath != null) {
        psPath = psPath.Remove(0, "Assets".Length);
        psPath = Application.dataPath + psPath;
        preset.Load(psPath);
      }
    }

    savedPath = Path.GetDirectoryName(path);
    savedFile = Path.GetFileNameWithoutExtension(path);

    EditorPrefs.SetString(KEY_PATH, savedPath);
    EditorPrefs.SetString(KEY_FILE, savedFile);

    Debug.Log("attempting to save to " + path);
    writer.OpenFiles(path);

    if (rtcScript != null && root != null) {
      var instance = Activator.CreateInstance(rtcScript.GetClass());
      var rtc = instance as RTCCallback;
      if (rtc != null) {
        writer.RTCCenter = rtc.GetCenter(root);
      }
    }

    RotationCallback rotCallback = null; ;
    if (rotScript != null) {
      var instance = Activator.CreateInstance(rotScript.GetClass());
      rotCallback = instance as RotationCallback;
    }

    if (unpackTexture) {
      // prepass, for texture unpacker
      TextureUnpacker.Reset();
      foreach (Transform tr in trs) {
        TextureUnpacker.CheckPackedTexture(tr, preset);
      }
      TextureUnpacker.Build();
    }

    BoundsDouble bb = new BoundsDouble();

    // first, collect objects in the scene, add to lists
    foreach (Transform tr in trs) {
      if (tr.GetComponent<Camera>() != null) {
        if (tr.GetComponent<Camera>().orthographic) {
          GlTF_Orthographic cam;
          cam = new GlTF_Orthographic();
          cam.type = "orthographic";
          cam.zfar = tr.GetComponent<Camera>().farClipPlane;
          cam.znear = tr.GetComponent<Camera>().nearClipPlane;
          cam.name = tr.name;
          //cam.orthographic.xmag = tr.camera.
          GlTF_Writer.cameras.Add(cam);
        } else {
          GlTF_Perspective cam;
          cam = new GlTF_Perspective();
          cam.type = "perspective";
          cam.zfar = tr.GetComponent<Camera>().farClipPlane;
          cam.znear = tr.GetComponent<Camera>().nearClipPlane;
          cam.aspect_ratio = tr.GetComponent<Camera>().aspect;
          cam.yfov = tr.GetComponent<Camera>().fieldOfView;
          cam.name = tr.name;
          GlTF_Writer.cameras.Add(cam);
        }
      }

      if (tr.GetComponent<Light>() != null) {
        switch (tr.GetComponent<Light>().type) {
          case LightType.Point:
            GlTF_PointLight pl = new GlTF_PointLight();
            pl.color = new GlTF_ColorRGB(tr.GetComponent<Light>().color);
            pl.name = tr.name;
            GlTF_Writer.lights.Add(pl);
            break;

          case LightType.Spot:
            GlTF_SpotLight sl = new GlTF_SpotLight();
            sl.color = new GlTF_ColorRGB(tr.GetComponent<Light>().color);
            sl.name = tr.name;
            GlTF_Writer.lights.Add(sl);
            break;

          case LightType.Directional:
            GlTF_DirectionalLight dl = new GlTF_DirectionalLight();
            dl.color = new GlTF_ColorRGB(tr.GetComponent<Light>().color);
            dl.name = tr.name;
            GlTF_Writer.lights.Add(dl);
            break;

          case LightType.Area:
            GlTF_AmbientLight al = new GlTF_AmbientLight();
            al.color = new GlTF_ColorRGB(tr.GetComponent<Light>().color);
            al.name = tr.name;
            GlTF_Writer.lights.Add(al);
            break;
        }
      }

      Mesh m = GetMesh(tr);
      if (m != null) {
        GlTF_Mesh mesh = new GlTF_Mesh();
        mesh.name = GlTF_Mesh.GetNameFromObject(m);

        GlTF_Accessor positionAccessor = new GlTF_Accessor(GlTF_Accessor.GetNameFromObject(m, "position"), GlTF_Accessor.Type.VEC3, GlTF_Accessor.ComponentType.FLOAT);
        positionAccessor.bufferView = GlTF_Writer.vec3BufferView;
        GlTF_Writer.accessors.Add(positionAccessor);

        GlTF_Accessor normalAccessor = null;
        if (m.normals.Length > 0) {
          normalAccessor = new GlTF_Accessor(GlTF_Accessor.GetNameFromObject(m, "normal"), GlTF_Accessor.Type.VEC3, GlTF_Accessor.ComponentType.FLOAT);
          normalAccessor.bufferView = GlTF_Writer.vec3BufferView;
          GlTF_Writer.accessors.Add(normalAccessor);
        }

        GlTF_Accessor uv0Accessor = null;
        if (m.uv.Length > 0) {
          uv0Accessor = new GlTF_Accessor(GlTF_Accessor.GetNameFromObject(m, "uv0"), GlTF_Accessor.Type.VEC2, GlTF_Accessor.ComponentType.FLOAT);
          uv0Accessor.bufferView = GlTF_Writer.vec2BufferView;
          GlTF_Writer.accessors.Add(uv0Accessor);
        }

        GlTF_Accessor uv1Accessor = null;
        if (m.uv2.Length > 0) {
          uv1Accessor = new GlTF_Accessor(GlTF_Accessor.GetNameFromObject(m, "uv1"), GlTF_Accessor.Type.VEC2, GlTF_Accessor.ComponentType.FLOAT);
          uv1Accessor.bufferView = GlTF_Writer.vec2BufferView;
          GlTF_Writer.accessors.Add(uv1Accessor);
        }

        GlTF_Accessor uv2Accessor = null;
        if (m.uv3.Length > 0) {
          uv2Accessor = new GlTF_Accessor(GlTF_Accessor.GetNameFromObject(m, "uv2"), GlTF_Accessor.Type.VEC2, GlTF_Accessor.ComponentType.FLOAT);
          uv2Accessor.bufferView = GlTF_Writer.vec2BufferView;
          GlTF_Writer.accessors.Add(uv2Accessor);
        }

        GlTF_Accessor uv3Accessor = null;
        if (m.uv4.Length > 0) {
          uv3Accessor = new GlTF_Accessor(GlTF_Accessor.GetNameFromObject(m, "uv3"), GlTF_Accessor.Type.VEC2, GlTF_Accessor.ComponentType.FLOAT);
          uv3Accessor.bufferView = GlTF_Writer.vec2BufferView;
          GlTF_Writer.accessors.Add(uv3Accessor);
        }

        var smCount = m.subMeshCount;
        for (var i = 0; i < smCount; ++i) {
          GlTF_Primitive primitive = new GlTF_Primitive();
          primitive.name = GlTF_Primitive.GetNameFromObject(m, i);
          primitive.index = i;
          GlTF_Attributes attributes = new GlTF_Attributes();
          attributes.positionAccessor = positionAccessor;
          attributes.normalAccessor = normalAccessor;
          attributes.texCoord0Accessor = uv0Accessor;
          attributes.texCoord1Accessor = uv1Accessor;
          attributes.texCoord2Accessor = uv2Accessor;
          attributes.texCoord3Accessor = uv3Accessor;
          primitive.attributes = attributes;
          GlTF_Accessor indexAccessor = new GlTF_Accessor(GlTF_Accessor.GetNameFromObject(m, "indices_" + i), GlTF_Accessor.Type.SCALAR, GlTF_Accessor.ComponentType.USHORT);
          indexAccessor.bufferView = GlTF_Writer.ushortBufferView;
          GlTF_Writer.accessors.Add(indexAccessor);
          primitive.indices = indexAccessor;

          var mr = GetRenderer(tr);
          var sm = mr.sharedMaterials;
          if (i < sm.Length && sm[i] != null) {
            var mat = sm[i];
            var matName = GlTF_Material.GetNameFromObject(mat);
            primitive.materialName = matName;
            if (!GlTF_Writer.materials.ContainsKey(matName)) {
              GlTF_Material material = new GlTF_Material();
              material.name = matName;
              GlTF_Writer.materials.Add(material.name, material);

              //technique
              var s = mat.shader;
              var techName = GlTF_Technique.GetNameFromObject(s);
              material.instanceTechniqueName = techName;
              if (!GlTF_Writer.techniques.ContainsKey(techName)) {
                GlTF_Technique tech = new GlTF_Technique();
                tech.name = techName;

                GlTF_Technique.States ts = null;
                if (preset.techniqueStates.ContainsKey(s.name)) {
                  ts = preset.techniqueStates[s.name];
                } else if (preset.techniqueStates.ContainsKey("*")) {
                  ts = preset.techniqueStates["*"];
                }

                if (ts == null) {
                  // Unless otherwise specified by a preset file, enable z-buffering.
                  ts = new GlTF_Technique.States();
                  const int DEPTH_TEST = 2929;
                  // int CULL_FACE = 2884;
                  ts.enable = new int[1] { DEPTH_TEST };
                }

                tech.states = ts;

                GlTF_Technique.Parameter tParam = new GlTF_Technique.Parameter();
                tParam.name = "position";
                tParam.type = GlTF_Technique.Type.FLOAT_VEC3;
                tParam.semantic = GlTF_Technique.Semantic.POSITION;
                tech.parameters.Add(tParam);
                GlTF_Technique.Attribute tAttr = new GlTF_Technique.Attribute();
                tAttr.name = "a_position";
                tAttr.param = tParam.name;
                tech.attributes.Add(tAttr);

                if (normalAccessor != null) {
                  tParam = new GlTF_Technique.Parameter();
                  tParam.name = "normal";
                  tParam.type = GlTF_Technique.Type.FLOAT_VEC3;
                  tParam.semantic = GlTF_Technique.Semantic.NORMAL;
                  tech.parameters.Add(tParam);
                  tAttr = new GlTF_Technique.Attribute();
                  tAttr.name = "a_normal";
                  tAttr.param = tParam.name;
                  tech.attributes.Add(tAttr);
                }

                if (uv0Accessor != null) {
                  tParam = new GlTF_Technique.Parameter();
                  tParam.name = "texcoord0";
                  tParam.type = GlTF_Technique.Type.FLOAT_VEC2;
                  tParam.semantic = GlTF_Technique.Semantic.TEXCOORD_0;
                  tech.parameters.Add(tParam);
                  tAttr = new GlTF_Technique.Attribute();
                  tAttr.name = "a_texcoord0";
                  tAttr.param = tParam.name;
                  tech.attributes.Add(tAttr);
                }

                if (uv1Accessor != null) {
                  tParam = new GlTF_Technique.Parameter();
                  tParam.name = "texcoord1";
                  tParam.type = GlTF_Technique.Type.FLOAT_VEC2;
                  tParam.semantic = GlTF_Technique.Semantic.TEXCOORD_1;
                  tech.parameters.Add(tParam);
                  tAttr = new GlTF_Technique.Attribute();
                  tAttr.name = "a_texcoord1";
                  tAttr.param = tParam.name;
                  tech.attributes.Add(tAttr);
                }

                if (uv2Accessor != null) {
                  tParam = new GlTF_Technique.Parameter();
                  tParam.name = "texcoord2";
                  tParam.type = GlTF_Technique.Type.FLOAT_VEC2;
                  tParam.semantic = GlTF_Technique.Semantic.TEXCOORD_2;
                  tech.parameters.Add(tParam);
                  tAttr = new GlTF_Technique.Attribute();
                  tAttr.name = "a_texcoord2";
                  tAttr.param = tParam.name;
                  tech.attributes.Add(tAttr);
                }

                if (uv3Accessor != null) {
                  tParam = new GlTF_Technique.Parameter();
                  tParam.name = "texcoord3";
                  tParam.type = GlTF_Technique.Type.FLOAT_VEC2;
                  tParam.semantic = GlTF_Technique.Semantic.TEXCOORD_3;
                  tech.parameters.Add(tParam);
                  tAttr = new GlTF_Technique.Attribute();
                  tAttr.name = "a_texcoord3";
                  tAttr.param = tParam.name;
                  tech.attributes.Add(tAttr);
                }

                tech.AddDefaultUniforms(writer.RTCCenter != null);

                GlTF_Writer.techniques.Add(techName, tech);

                int spCount = ShaderUtil.GetPropertyCount(s);
                for (var j = 0; j < spCount; ++j) {
                  var pName = ShaderUtil.GetPropertyName(s, j);
                  var pType = ShaderUtil.GetPropertyType(s, j);
                  // Debug.Log(pName + " " + pType);

                  GlTF_Technique.Uniform tUni;
                  if (pType == ShaderUtil.ShaderPropertyType.Color) {
                    tParam = new GlTF_Technique.Parameter();
                    tParam.name = pName;
                    tParam.type = GlTF_Technique.Type.FLOAT_VEC4;
                    tech.parameters.Add(tParam);
                    tUni = new GlTF_Technique.Uniform();
                    tUni.name = pName;
                    tUni.param = tParam.name;
                    tech.uniforms.Add(tUni);
                  } else if (pType == ShaderUtil.ShaderPropertyType.Vector) {
                    tParam = new GlTF_Technique.Parameter();
                    tParam.name = pName;
                    tParam.type = GlTF_Technique.Type.FLOAT_VEC4;
                    tech.parameters.Add(tParam);
                    tUni = new GlTF_Technique.Uniform();
                    tUni.name = pName;
                    tUni.param = tParam.name;
                    tech.uniforms.Add(tUni);
                  } else if (pType == ShaderUtil.ShaderPropertyType.Float ||
                      pType == ShaderUtil.ShaderPropertyType.Range) {
                    tParam = new GlTF_Technique.Parameter();
                    tParam.name = pName;
                    tParam.type = GlTF_Technique.Type.FLOAT;
                    tech.parameters.Add(tParam);
                    tUni = new GlTF_Technique.Uniform();
                    tUni.name = pName;
                    tUni.param = tParam.name;
                    tech.uniforms.Add(tUni);
                  } else if (pType == ShaderUtil.ShaderPropertyType.TexEnv) {
                    var td = ShaderUtil.GetTexDim(s, j);
                    if (td == UnityEngine.Rendering.TextureDimension.Tex2D) {
                      tParam = new GlTF_Technique.Parameter();
                      tParam.name = pName;
                      tParam.type = GlTF_Technique.Type.SAMPLER_2D;
                      tech.parameters.Add(tParam);
                      tUni = new GlTF_Technique.Uniform();
                      tUni.name = pName;
                      tUni.param = tParam.name;
                      tech.uniforms.Add(tUni);
                    }
                  }
                }

                // create program
                GlTF_Program program = new GlTF_Program();
                program.name = GlTF_Program.GetNameFromObject(s);
                tech.program = program.name;
                foreach (var attr in tech.attributes) {
                  program.attributes.Add(attr.name);
                }
                GlTF_Writer.programs.Add(program);

                // shader
                GlTF_Shader vs = new GlTF_Shader();
                vs.name = GlTF_Shader.GetNameFromObject(s, GlTF_Shader.Type.Vertex);
                program.vertexShader = vs.name;
                vs.type = GlTF_Shader.Type.Vertex;
                vs.uri = preset.GetVertexShader(s.name);
                GlTF_Writer.shaders.Add(vs);

                GlTF_Shader fs = new GlTF_Shader();
                fs.name = GlTF_Shader.GetNameFromObject(s, GlTF_Shader.Type.Fragment);
                program.fragmentShader = fs.name;
                fs.type = GlTF_Shader.Type.Fragment;
                fs.uri = preset.GetFragmentShader(s.name);
                GlTF_Writer.shaders.Add(fs);
              }

              int spCount2 = ShaderUtil.GetPropertyCount(s);
              for (var j = 0; j < spCount2; ++j) {
                var pName = ShaderUtil.GetPropertyName(s, j);
                var pType = ShaderUtil.GetPropertyType(s, j);

                if (pType == ShaderUtil.ShaderPropertyType.Color) {
                  var matCol = new GlTF_Material.ColorValue();
                  matCol.name = pName;
                  matCol.color = mat.GetColor(pName);
                  material.values.Add(matCol);
                } else if (pType == ShaderUtil.ShaderPropertyType.Vector) {
                  var matVec = new GlTF_Material.VectorValue();
                  matVec.name = pName;
                  matVec.vector = mat.GetVector(pName);
                  material.values.Add(matVec);
                } else if (pType == ShaderUtil.ShaderPropertyType.Float ||
                    pType == ShaderUtil.ShaderPropertyType.Range) {
                  var matFloat = new GlTF_Material.FloatValue();
                  matFloat.name = pName;
                  matFloat.value = mat.GetFloat(pName);
                  material.values.Add(matFloat);
                } else if (pType == ShaderUtil.ShaderPropertyType.TexEnv) {
                  var td = ShaderUtil.GetTexDim(s, j);
                  if (td == UnityEngine.Rendering.TextureDimension.Tex2D) {
                    var t = mat.GetTexture(pName);
                    if (t == null) {
                      continue;
                    }
                    var val = new GlTF_Material.StringValue();
                    val.name = pName;
                    string texName = null;
                    texName = GlTF_Texture.GetNameFromObject(t);
                    val.value = texName;
                    material.values.Add(val);
                    if (!GlTF_Writer.textures.ContainsKey(texName)) {
                      var texPath = ExportTexture(t, savedPath);
                      GlTF_Image img = new GlTF_Image();
                      img.name = GlTF_Image.GetNameFromObject(t);
                      img.uri = texPath;
                      GlTF_Writer.images.Add(img);

                      GlTF_Sampler sampler;
                      var samplerName = GlTF_Sampler.GetNameFromObject(t);
                      if (GlTF_Writer.samplers.ContainsKey(samplerName)) {
                        sampler = GlTF_Writer.samplers[samplerName];
                      } else {
                        sampler = new GlTF_Sampler(t);
                        sampler.name = samplerName;
                        GlTF_Writer.samplers[samplerName] = sampler;
                      }

                      GlTF_Texture texture = new GlTF_Texture();
                      texture.name = texName;
                      texture.source = img.name;
                      texture.samplerName = samplerName;

                      GlTF_Writer.textures.Add(texName, texture);
                    }
                  }
                }
              }
            }
          }

          mesh.primitives.Add(primitive);
        }

        mesh.Populate(m);
        GlTF_Writer.meshes.Add(mesh);
        if (unpackTexture) {
          TextureUnpacker.ProcessMesh(mesh);
        }

        // calculate bounding box transform
        if (root != null) {
          Matrix4x4 brot = Matrix4x4.identity;
          if (rotCallback != null) {
            brot = rotCallback.GetBoundsRotationMatrix(root);
          }

          var pos = tr.position - root.position; // relative to parent
          var objMat = Matrix4x4.TRS(pos, tr.rotation, tr.lossyScale);

          //read vertices
          var ms = positionAccessor.bufferView.memoryStream;
          var offset = (int) positionAccessor.byteOffset;
          var len = positionAccessor.count;
          var buffer = new byte[len * 12];
          var mspos = ms.Position;
          ms.Position = offset;
          ms.Read(buffer, 0, buffer.Length);

          minHeight = double.MaxValue;
          maxHeight = double.MinValue;

          double[] c = writer.RTCCenter;

          double[] minPos = new double[3];
          minPos[0] = double.MaxValue;
          minPos[1] = double.MaxValue;
          minPos[2] = double.MaxValue;

          double[] maxPos = new double[3];
          maxPos[0] = double.MinValue;
          maxPos[1] = double.MinValue;
          maxPos[2] = double.MinValue;

          for (int j = 0; j < len; ++j) {
            var x = System.BitConverter.ToSingle(buffer, j * 12);
            var y = System.BitConverter.ToSingle(buffer, j * 12 + 4);
            var z = System.BitConverter.ToSingle(buffer, j * 12 + 8);

            // local rotation
            var lx = objMat.m00 * x + objMat.m01 * y + objMat.m02 * z;
            var ly = objMat.m10 * x + objMat.m11 * y + objMat.m12 * z;
            var lz = objMat.m20 * x + objMat.m21 * y + objMat.m22 * z;

            minHeight = Math.Min(minHeight, ly);
            maxHeight = Math.Max(maxHeight, ly);

            // to world
            double wx = brot.m00 * lx + brot.m01 * ly + brot.m02 * lz;
            double wy = brot.m10 * lx + brot.m11 * ly + brot.m12 * lz;
            double wz = brot.m20 * lx + brot.m21 * ly + brot.m22 * lz;

            // local translation to world
            double tx = brot.m00 * pos.x + brot.m01 * pos.y + brot.m02 * pos.z;
            double ty = brot.m10 * pos.x + brot.m11 * pos.y + brot.m12 * pos.z;
            double tz = brot.m20 * pos.x + brot.m21 * pos.y + brot.m22 * pos.z;

            wx += tx;
            wy += ty;
            wz += tz;

            if (c != null) {
              wx += c[0];
              wy += c[1];
              wz += c[2];
            }

            minPos[0] = Math.Min(minPos[0], wx);
            minPos[1] = Math.Min(minPos[1], wy);
            minPos[2] = Math.Min(minPos[2], wz);

            maxPos[0] = Math.Max(maxPos[0], wx);
            maxPos[1] = Math.Max(maxPos[1], wy);
            maxPos[2] = Math.Max(maxPos[2], wz);
          }

          ms.Position = mspos;

          BoundsDouble tbb = new BoundsDouble();
          tbb.Encapsulate(new BoundsDouble(minPos, maxPos));
          bb.Encapsulate(tbb);
        }
      }

      Animation a = tr.GetComponent<Animation>();

      //				Animator a = tr.GetComponent<Animator>();
      if (a != null) {
        AnimationClip[] clips = AnimationUtility.GetAnimationClips(tr.gameObject);
        int nClips = clips.Length;
        //					int nClips = a.GetClipCount();
        for (int i = 0; i < nClips; i++) {
          GlTF_Animation anim = new GlTF_Animation(a.name);
          anim.Populate(clips[i]);
          GlTF_Writer.animations.Add(anim);
        }
      }


      // next, build hierarchy of nodes
      GlTF_Node node = new GlTF_Node();

      Matrix4x4 rotMat = Matrix4x4.identity;
      if (root != null && rotCallback != null) {
        rotMat = rotCallback.GetNodeRotationMatrix(root);
      }

      if (tr == root) {
        Matrix4x4 mat = Matrix4x4.identity;
        mat.m22 = -1; // flip z axis

        if (rotMat != Matrix4x4.identity) {
          mat = rotMat;
        }

        // do not use global position if rtc is defined
        Vector3 pos = Vector3.zero;
        if (writer.RTCCenter == null) {
          pos = tr.localPosition;
        }

        mat = mat * Matrix4x4.TRS(pos, tr.localRotation, tr.localScale);
        node.matrix = new GlTF_Matrix(mat);
      } else {
        node.hasParent = true;
        if (tr.localPosition != Vector3.zero)
          node.translation = new GlTF_Translation(tr.localPosition);
        if (tr.localScale != Vector3.one)
          node.scale = new GlTF_Scale(tr.localScale);
        if (tr.localRotation != Quaternion.identity)
          node.rotation = new GlTF_Rotation(tr.localRotation);
      }

      node.name = GlTF_Node.GetNameFromObject(tr);
      if (tr.GetComponent<Camera>() != null) {
        node.cameraName = tr.name;
      } else if (tr.GetComponent<Light>() != null)
        node.lightName = tr.name;
      else if (m != null) {
        node.meshNames.Add(GlTF_Mesh.GetNameFromObject(m));
      }

      foreach (Transform t in tr.transform) {
        var found = false;
        foreach (var check in trs) {
          if (t == check) {
            found = true;
            break;
          }
        }
        if (found) {
          node.childrenNames.Add(GlTF_Node.GetNameFromObject(t));
        }
      }

      GlTF_Writer.nodes.Add(node);
    }

    if (copyShaders && preset.shaderDir != null) {
      var sd = Path.Combine(Application.dataPath, preset.shaderDir);
      foreach (var shader in GlTF_Writer.shaders) {
        var srcPath = Path.Combine(sd, shader.uri);
        if (File.Exists(srcPath)) {
          var dstPath = Path.Combine(savedPath, shader.uri);
          File.Copy(srcPath, dstPath, true);
        }
      }
    }

    // third, add meshes etc to byte stream, keeping track of buffer offsets
    writer.Write();
    writer.CloseFiles();
    return bb;
  }

  private static string toGlTFname(string name) {
    // remove spaces and illegal chars, replace with underscores
    string correctString = name.Replace(" ", "_");
    // make sure it doesn't start with a number
    return correctString;
  }

  private static bool isInheritedFrom(Type t, Type baseT) {
    if (t == baseT)
      return true;
    t = t.BaseType;
    while (t != null && t != typeof(System.Object)) {
      if (t == baseT)
        return true;
      t = t.BaseType;
    }
    return false;
  }

  private static Renderer GetRenderer(Transform tr) {
    Renderer mr = tr.GetComponent<MeshRenderer>();
    if (mr == null) {
      mr = tr.GetComponent<SkinnedMeshRenderer>();
    }
    return mr;
  }

  private static Mesh GetMesh(Transform tr) {
    var mr = GetRenderer(tr);
    Mesh m = null;
    if (mr != null) {
      var t = mr.GetType();
      if (t == typeof(MeshRenderer)) {
        MeshFilter mf = tr.GetComponent<MeshFilter>();
        m = mf.sharedMesh;
      } else if (t == typeof(SkinnedMeshRenderer)) {
        SkinnedMeshRenderer smr = mr as SkinnedMeshRenderer;
        m = smr.sharedMesh;
      }
    }
    return m;
  }

  private static string ExportTexture(Texture texture, string path) {
    var assetPath = AssetDatabase.GetAssetPath(texture);
    var fn = Path.GetFileName(assetPath);
    var t = texture as Texture2D;
    var name = GlTF_Texture.GetNameFromObject(t);
    if (t != null) {
      var ext = Path.GetExtension(assetPath);
      if (ext == ".dds") {
        var ct = Type.GetType("Imaging.DDSReader.DDS");
        if (ct != null) {
          var srcPath = GetAssetFullPath(assetPath);
          //					var srcTex = Imaging.DDSReader.DDS.LoadImage(srcPath, true);
          Texture2D srcTex = ct.GetMethod("LoadImage", new Type[] { typeof(string), typeof(bool) }).Invoke(null, new object[] { srcPath, true }) as Texture2D;
          fn = Path.GetFileNameWithoutExtension(assetPath) + ".png";
          var dstPath = Path.Combine(path, fn);

          Texture2D curTex = null;
          if (unpackTexture) {
            curTex = TextureUnpacker.ProcessTexture(name, srcTex);
          } else {
            curTex = srcTex;
          }


          var b = curTex.EncodeToPNG();
          File.WriteAllBytes(dstPath, b);
          if (curTex != srcTex) {
            Texture2D.DestroyImmediate(curTex);
          }
          Texture2D.DestroyImmediate(srcTex);
        } else {
          fn = Path.GetFileNameWithoutExtension(assetPath) + ".png";
          var dstPath = Path.Combine(path, fn);
          Texture2D t2 = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false);
          t2.SetPixels(t.GetPixels());
          t2.Apply();

          Texture2D curTex = null;
          if (unpackTexture) {
            curTex = TextureUnpacker.ProcessTexture(name, t2);
          } else {
            curTex = t2;
          }

          var b = curTex.EncodeToPNG();
          File.WriteAllBytes(dstPath, b);
          if (curTex != t2) {
            Texture2D.DestroyImmediate(curTex);
          }
          Texture2D.DestroyImmediate(t2);
        }
      } else {
        var dstPath = Path.Combine(path, fn);
        if (unpackTexture) {
          // load src texture from path to prevent access error
          var read = File.ReadAllBytes(assetPath);
          Texture2D copyTex = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false);
          copyTex.LoadImage(read);
          copyTex.Apply();

          // size could be different from import settings
          if (t.width != copyTex.width || t.height != copyTex.height) {
            GltfTextureScale.Bilinear(copyTex, t.width, t.height);
          }

          Texture2D curTex = TextureUnpacker.ProcessTexture(name, copyTex);

          byte[] b = null;
          if (ext == ".jpg") {
            b = curTex.EncodeToJPG();
          } else if (ext == ".png") {
            b = curTex.EncodeToPNG();
          }

          if (b != null) {
            File.WriteAllBytes(dstPath, b);
          } else {
            Debug.LogError("Unsupported file format for: " + fn);
          }

          if (curTex != copyTex) {
            Texture2D.DestroyImmediate(curTex);
          }
          Texture2D.DestroyImmediate(copyTex);
        } else {
          File.Copy(assetPath, dstPath, true);
        }
      }
    }
    return fn;
  }

  private static string GetAssetFullPath(string path) {
    if (path != null) {
      path = path.Remove(0, "Assets".Length);
      path = Application.dataPath + path;
    }
    return path;
  }
}
#endif
#endif
