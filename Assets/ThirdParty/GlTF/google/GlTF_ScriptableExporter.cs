using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using JetBrains.Annotations;
using TiltBrush;

using static TiltBrush.ExportUtils;

// Provides the ability to convert TB geometry into glTF format and save to disk. See the class
// GlTFEditorExporter for a simple usage example.
public sealed class GlTF_ScriptableExporter : IDisposable {
  // Static API

  // Returns Unity Renderer, if any, given Transform.
  private static Renderer GetRenderer(Transform tr) {
    Debug.Assert(tr != null);
    Renderer mr = tr.GetComponent<MeshRenderer>();
    if (mr == null) {
      mr = tr.GetComponent<SkinnedMeshRenderer>();
    }
    return mr;
  }

  // Returns a (Unity) Mesh, if any, given Transform tr. Note that tr must also have a
  // Renderer. Otherwise, returns null.
  private static Mesh GetMesh(Transform tr) {
    Debug.Assert(tr != null);
    var mr = GetRenderer(tr);
    Mesh mesh = null;
    if (mr != null) {
      var t = mr.GetType();
      if (t == typeof(MeshRenderer)) {
        MeshFilter mf = tr.GetComponent<MeshFilter>();
        if (mf == null) {
          return null;
        }
        mesh = mf.sharedMesh;
      } else if (t == typeof(SkinnedMeshRenderer)) {
        SkinnedMeshRenderer smr = mr as SkinnedMeshRenderer;
        mesh = smr.sharedMesh;
      }
    }
    return mesh;
  }

  // Adds a glTF attribute, as described by name, type, and semantic, to the given technique tech.
  private static void AddAttribute(string name, GlTF_Technique.Type type,
                                   GlTF_Technique.Semantic semantic, GlTF_Technique tech) {
    GlTF_Technique.Parameter tParam = new GlTF_Technique.Parameter();
    tParam.name = name;
    tParam.type = type;
    tParam.semantic = semantic;
    tech.parameters.Add(tParam);
    GlTF_Technique.Attribute tAttr = new GlTF_Technique.Attribute();
    tAttr.name = "a_" + name;
    tAttr.param = tParam.name;
    tech.attributes.Add(tAttr);
  }

  // Instance API

  private Dictionary<IExportableMaterial, GlTF_Technique.States> m_techniqueStates =
      new Dictionary<IExportableMaterial, GlTF_Technique.States>();

  // Handles low-level write operations into glTF files.
  private GlTF_Globals m_globals;
  // Output path to .gltf file.
  private string m_outPath;

  // As each GeometryPool is emitted, the glTF mesh is memoized and shared across all nodes which
  // reference the same pool.
  private Dictionary<TiltBrush.GeometryPool, GlTF_Mesh> m_meshCache =
      new Dictionary<TiltBrush.GeometryPool, GlTF_Mesh>();

  // List of all exported files (so far).
  public HashSet<string> ExportedFiles { get; private set; }
  private CultureInfo m_previousCulture;

  // Total number of triangles exported.
  public int NumTris { get; private set; }
  public GlTF_Globals G { get { return m_globals; } }

  /// Allows the use of absolute http:// URIs; leave this false for maximum compatibility.
  /// This was originally used for development purposes and currently only works in-Editor.
  /// See b/147362851 and https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#uris

  public bool AllowHttpUri { get; set; }

  // temporaryDirectory may be null.
  // If non-null, ownership of the directory is transferred.
  public GlTF_ScriptableExporter(string temporaryDirectory, int gltfVersion) {
    m_globals = new GlTF_Globals(temporaryDirectory, gltfVersion);
    if (TiltBrush.App.PlatformConfig.EnableExportMemoryOptimization) {
      m_globals.EnableFileStream();
    }
    m_previousCulture = Thread.CurrentThread.CurrentCulture;
    Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
  }

  public void Dispose() {
    if (G != null) {
      G.Dispose();
    }
    if (m_previousCulture != null) {
      Thread.CurrentThread.CurrentCulture = m_previousCulture;
    }
  }

  // Call this first, specifying output path in outPath, the glTF preset, and directory with
  // existing assets to be included, sourceDir.
  public void BeginExport(string outPath) {
    this.m_outPath = outPath;
    G.OpenFiles(outPath);
    NumTris = 0;
    ExportedFiles = new HashSet<string>();
  }

  public void SetMetadata(string generator, string copyright) {
    Debug.Assert(G != null);
    G.Generator = generator;
    G.Copyright = copyright;
  }

  // Call this last.
  // Returns array of successfully-exported files
  public string[] EndExport() {
    // sanity check because this code is bug-riddled
    foreach (var pair in G.nodes) {
      if (pair.Key != pair.Value.name) {
        Debug.LogWarningFormat("Buggy key/value in nodes: {0} {1}", pair.Key, pair.Value.name);
      }
    }

    G.Write();
    G.CloseFiles();
    m_meshCache.Clear();
    ExportedFiles.UnionWith(G.ExportedFiles);
    Debug.LogFormat("Wrote files:\n  {0}", String.Join("\n  ", ExportedFiles.ToArray()));
    Debug.LogFormat("Saved {0} triangle(s) to {1}.", NumTris, m_outPath);
    return ExportedFiles.ToArray();
  }

  // Export a single shader float uniform
  public void ExportShaderUniform(
      IExportableMaterial exportableMaterial, string name, float value) {
    GlTF_Material mtl = G.materials[exportableMaterial];
    var float_val = new GlTF_Material.FloatKV {
        key = name,
        value = value
    };
    mtl.values.Add(float_val);
    AddUniform(exportableMaterial, float_val.key,
               GlTF_Technique.Type.FLOAT, GlTF_Technique.Semantic.UNKNOWN);
  }

  // Export a single shader color uniform
  public void ExportShaderUniform(
      IExportableMaterial exportableMaterial, string name, Color value) {
    GlTF_Material mtl = G.materials[exportableMaterial];
    var color_val = new GlTF_Material.ColorKV { key = name, color = value };
    mtl.values.Add(color_val);
    AddUniform(exportableMaterial, color_val.key,
               GlTF_Technique.Type.FLOAT_VEC4, GlTF_Technique.Semantic.UNKNOWN);
  }

  // Export a single shader vector uniform
  public void ExportShaderUniform(
      IExportableMaterial exportableMaterial, string name, Vector4 value) {
    GlTF_Material mtl = G.materials[exportableMaterial];
    var vec_val = new GlTF_Material.VectorKV { key = name, vector = value };
    mtl.values.Add(vec_val);
    AddUniform(exportableMaterial, vec_val.key,
               GlTF_Technique.Type.FLOAT_VEC4, GlTF_Technique.Semantic.UNKNOWN);
  }

  // Should be called per material.
  public void ExportAmbientLight(IExportableMaterial exportableMaterial, Color color) {
    GlTF_Material mtl = G.materials[exportableMaterial];
    var val = new GlTF_Material.ColorKV {
        key = "ambient_light_color",
        color = color
    };
    mtl.values.Add(val);
    AddUniform(exportableMaterial, val.key,
               GlTF_Technique.Type.FLOAT_VEC4, GlTF_Technique.Semantic.UNKNOWN);
  }

  // This does once-per-light work, as well as once-per-material-per-light work.
  // So this ends up being called multiple times with the same parameters, except
  // for matObjName.
  // matObjName is the name of the material being exported
  // lightObjectName is the name of the light
  public void ExportLight(LightPayload payload, IExportableMaterial exportableMaterial) {
    ObjectName lightNodeName = new ObjectName(payload.legacyUniqueName); // does need to be unique

    // Add the light to the scene -- this does _not_ need to be done per-material.
    // As a result, the node will generally have already been created.
    GlTF_Node node = GlTF_Node.GetOrCreate(G, lightNodeName, payload.xform, null, out _);
    node.lightNameThatDoesNothing = payload.name;

    // The names of the uniforms can be anything, really. Named after the light is the most
    // logical choice, but note that nobody checks that two lights don't have the same name.
    // Thankfully for Tilt Brush, they don't.
    // This should probably have used a guaranteed-unique name from the start but I don't want
    // to change it now because it'd break my diffs and be kind of ugly.
    string lightUniformPrefix = payload.name;
    AddUniform(exportableMaterial, lightUniformPrefix + "_matrix",
               GlTF_Technique.Type.FLOAT_MAT4, GlTF_Technique.Semantic.MODELVIEW, node);

    // Add light color.
    GlTF_Material mtl = G.materials[exportableMaterial];
    var val = new GlTF_Material.ColorKV {
        key = lightUniformPrefix + "_color",
        color = payload.lightColor
    };
    mtl.values.Add(val);
    AddUniform(exportableMaterial, val.key,
               GlTF_Technique.Type.FLOAT_VEC4, GlTF_Technique.Semantic.UNKNOWN, node);
  }

  // Exports camera into glTF.
  // Unused -- if we want to export cameras, they should get their own section in the Payload
  // and the payload-creation code will take care of doing basis conversions, coordinate
  // space conversions like AsScene[], etc.
#if false
  public void ExportCamera(Transform tr) {
    GlTF_Node node = MakeNode(tr);
    Debug.Assert(tr.GetComponent<Camera>() != null);
    if (tr.GetComponent<Camera>().orthographic) {
      GlTF_Orthographic cam;
      cam = new GlTF_Orthographic(G);
      cam.type = "orthographic";
      cam.zfar = tr.GetComponent<Camera>().farClipPlane;
      cam.znear = tr.GetComponent<Camera>().nearClipPlane;
      cam.name = tr.name;
      G.cameras.Add(cam);
    } else {
      GlTF_Perspective cam;
      cam = new GlTF_Perspective(G);
      cam.type = "perspective";
      cam.zfar = tr.GetComponent<Camera>().farClipPlane;
      cam.znear = tr.GetComponent<Camera>().nearClipPlane;
      cam.aspect_ratio = tr.GetComponent<Camera>().aspect;
      cam.yfov = tr.GetComponent<Camera>().fieldOfView;
      cam.name = tr.name;
      G.cameras.Add(cam);
    }
    if (!G.nodes.ContainsKey(tr.name)) {
      G.nodes.Add(tr.name, node);
    }
  }
#endif

  // Returns the gltf mesh that corresponds to the payload, or null.
  // Currently, null is only returned if the payload's 'geometry pool is empty.
  // Pass a localXf to override the default, which is to use base.xform
  public GlTF_Node ExportMeshPayload(
      SceneStatePayload payload,
      BaseMeshPayload meshPayload,
      [CanBeNull] GlTF_Node parent,
      Matrix4x4? localXf = null) {
    var node = ExportMeshPayload_NoMaterial(meshPayload, parent, localXf);

    if (node != null) {
      IExportableMaterial exportableMaterial = meshPayload.exportableMaterial;
      if (!G.materials.ContainsKey(exportableMaterial)) {
        var prims = node.m_mesh?.primitives;
        var attrs = (prims != null && prims.Count > 0) ? prims[0].attributes : null;
        if (attrs != null) {
          ExportMaterial(payload, meshPayload.MeshNamespace, exportableMaterial, attrs);
          Debug.Assert(G.materials.ContainsKey(exportableMaterial));
        }
      }
    }

    return node;
  }

  // Doesn't do material export; for that see ExportMeshPayload
  private GlTF_Node ExportMeshPayload_NoMaterial(
      BaseMeshPayload mesh,
      [CanBeNull] GlTF_Node parent,
      Matrix4x4? localXf = null) {

    ObjectName meshNameAndId = new ObjectName(mesh.legacyUniqueName);
    GeometryPool pool = mesh.geometry;
    Matrix4x4 xf = localXf ?? mesh.xform;
    // Create a Node and (usually) a Mesh, both named after meshNameAndId.
    // This is safe because the namespaces for Node and Mesh are distinct.
    // If we have already seen the GeometryPool, the Mesh will be reused.
    // In this (less common) case, the Node and Mesh will have different names.

    // We don't actually ever use the "VERTEXID" attribute, even in gltf1.
    // It's time to cut it away.
    // Also, in gltf2, it needs to be called _VERTEXID anyway since it's a custom attribute
    GlTF_VertexLayout gltfLayout = new GlTF_VertexLayout(G, pool.Layout);

    int numTris = pool.NumTriIndices / 3;
    if (numTris < 1) {
      return null;
    }

    NumTris += numTris;

    GlTF_Mesh gltfMesh;

    // Share meshes for any repeated geometry pool.
    if (!m_meshCache.TryGetValue(pool, out gltfMesh)) {
      gltfMesh = new GlTF_Mesh(G);
      gltfMesh.name = GlTF_Mesh.GetNameFromObject(meshNameAndId);
      gltfMesh.PresentationNameOverride = mesh.geometryName;
      m_meshCache.Add(pool, gltfMesh);

      // Populate mesh data only once.
      AddMeshDependencies(meshNameAndId, mesh.exportableMaterial, gltfMesh, gltfLayout);
      gltfMesh.Populate(pool);
      G.meshes.Add(gltfMesh);
    }

    // The mesh may or may not be shared, but every mesh will have a distinct node to allow them
    // to have unique transforms.
    GlTF_Node node = GlTF_Node.GetOrCreate(G, meshNameAndId, xf, parent, out _);
    node.m_mesh = gltfMesh;
    node.PresentationNameOverride = mesh.nodeName;
    return node;
  }

  // Pass:
  //   meshNamespace - A string used as the "namespace" of the mesh that owns this material.
  //     Useful for uniquifying names (texture file names, material names, ...) in a
  //     human-friendly way.
  //   hack - attributes of some mesh that uses this material
  private void ExportMaterial(
      SceneStatePayload payload,
      string meshNamespace,
      IExportableMaterial exportableMaterial,
      GlTF_Attributes hack) {
    //
    // Set culling and blending modes.
    //
    GlTF_Technique.States states = new GlTF_Technique.States();
    m_techniqueStates[exportableMaterial] = states;
    // Everyone gets depth test
    states.enable = new[] { GlTF_Technique.Enable.DEPTH_TEST }.ToList();

    if (exportableMaterial.EnableCull) {
      states.enable.Add(GlTF_Technique.Enable.CULL_FACE);
    }

    if (exportableMaterial.BlendMode == ExportableMaterialBlendMode.AdditiveBlend) {
      states.enable.Add(GlTF_Technique.Enable.BLEND);
      // Blend array format: [srcRGB, dstRGB, srcAlpha, dstAlpha]
      states.functions["blendFuncSeparate"] =
        new GlTF_Technique.Value(G, new Vector4(1.0f, 1.0f, 1.0f, 1.0f));  // Additive.
      states.functions["depthMask"] = new GlTF_Technique.Value(G, false);  // No depth write.
      // Note: If we switch bloom to use LDR color, adding the alpha channels won't do.
      // GL_MIN would be a good choice for alpha, but it's unsupported by glTF 1.0.
    } else if (exportableMaterial.BlendMode == ExportableMaterialBlendMode.AlphaBlend) {
      states.enable.Add(GlTF_Technique.Enable.BLEND);
      // Blend array format: [srcRGB, dstRGB, srcAlpha, dstAlpha]
      // These enum values correspond to: [ONE, ONE_MINUS_SRC_ALPHA, ONE, ONE_MINUS_SRC_ALPHA]
      states.functions["blendFuncSeparate"] =
        new GlTF_Technique.Value(G, new Vector4(1.0f, 771.0f, 1.0f, 771.0f));  // Blend.
      states.functions["depthMask"] = new GlTF_Technique.Value(G, true);
    } else {
      // Standard z-buffering: Enable depth write.
      states.functions["depthMask"] = new GlTF_Technique.Value(G, true);
    }

    // First add the material, then export any per-material attributes, such as shader uniforms.
    AddMaterialWithDependencies(exportableMaterial, meshNamespace, hack);

    // Add lighting for this material.
    AddLights(exportableMaterial, payload);

    //
    // Export shader/material parameters.
    //

    foreach (var kvp in exportableMaterial.FloatParams) {
      ExportShaderUniform(exportableMaterial, kvp.Key, kvp.Value);
    }
    foreach (var kvp in exportableMaterial.ColorParams) {
      ExportShaderUniform(exportableMaterial, kvp.Key, kvp.Value);
    }
    foreach (var kvp in exportableMaterial.VectorParams) {
      ExportShaderUniform(exportableMaterial, kvp.Key, kvp.Value);
    }
    foreach (var kvp in exportableMaterial.TextureSizes) {
      float width = kvp.Value.x;
      float height = kvp.Value.y;
      ExportShaderUniform(exportableMaterial, kvp.Key + "_TexelSize",
                          new Vector4(1 / width, 1 / height, width, height));
    }

    //
    // Export textures.
    //
    foreach (var kvp in exportableMaterial.TextureUris) {
      string textureName = kvp.Key;
      string textureUri = kvp.Value;

      ExportFileReference fileRef;
      if (ExportFileReference.IsHttp(textureUri)) {
        // Typically this happens for textures used by BrushDescriptor materials
        fileRef = CreateExportFileReferenceFromHttp(textureUri);
      } else {
        fileRef = ExportFileReference.GetOrCreateSafeLocal(
            G.m_disambiguationContext, textureUri, exportableMaterial.UriBase,
            $"{meshNamespace}_{Path.GetFileName(textureUri)}");
      }

      AddTextureToMaterial(exportableMaterial, textureName, fileRef);
    }
  }

  /// Returns an ExportFileReference given an absolute http:// uri.
  /// Depending on settings, this may become a relative reference in the gltf.
  private ExportFileReference CreateExportFileReferenceFromHttp(string httpUri) {
    if (!AllowHttpUri) {
      string localPath = HostedUriToLocalFilename(httpUri);
      if (localPath != null) {
        return ExportFileReference.GetOrCreateSafeLocal(
            G.m_disambiguationContext,
            Path.GetFileName(localPath), Path.GetDirectoryName(localPath),
            "Brush_" + Path.GetFileName(localPath));
      }
      Debug.LogWarning($"Cannot convert {httpUri} to local");
    }
    return ExportFileReference.CreateHttp(httpUri);
  }

  /// Converts a https://www.tiltbrush.com/shaders/brushes/blah...glsl URI
  /// to a local filename, for use when creating hermetic .gltf exports.
  /// If running from the editor, relies on files that are not present until you've run "Tilt >
  /// glTF > Sync Export Materials".
  /// Returns null on error.
  private static string HostedUriToLocalFilename(string httpUri) {
#if UNITY_EDITOR
    string local = new Regex(@"^https?://[^/]+")
        .Replace(httpUri, Path.Combine(App.SupportPath(), "TiltBrush.com"))
        .Replace("\\", "/");
    if (local == httpUri) {
      throw new ArgumentException(string.Format("Does not look like a http uri: {0}", httpUri));
    }
    if (!File.Exists(local)) {
      Debug.LogWarning($"Missing data for {httpUri}\nShould be in {local}. " +
                       "You may need to run Tilt > glTF > Sync Export Materials.");
      return null;
    }
    return local;
#else
    string local = new Regex(@"^https?://[^/]+")
        .Replace(httpUri, App.SupportPath())
        .Replace("\\", "/");
    return local;
#endif
  }

  // Should be called per-material.
  private void AddLights(IExportableMaterial exportableMaterial,
                         ExportUtils.SceneStatePayload payload) {
#if DEBUG_GLTF_EXPORT
    Debug.LogFormat("Exporting {0} lights.", payload.lights.elements.Count);
#endif
    foreach (var light in payload.lights.lights) {
      // A light requires a node for the matrix, but has no mesh.
      ExportLight(light, exportableMaterial);
    }

    // We include the ambient light color in the material (no transform needed).
    ExportAmbientLight(exportableMaterial, RenderSettings.ambientLight);
  }

  // Adds material and sets up its dependent technique, program, shaders. This should be called
  // after adding meshes, but before populating lights, textures, etc.
  // Pass:
  //   hack - attributes of some mesh that uses this material
  public void AddMaterialWithDependencies(
      IExportableMaterial exportableMaterial,
      string meshNamespace,
      GlTF_Attributes hack) {
    GlTF_Material gltfMtl = G.CreateMaterial(meshNamespace, exportableMaterial);

    // Set up technique.
    GlTF_Technique tech = GlTF_Writer.CreateTechnique(G, exportableMaterial);
    gltfMtl.instanceTechniqueName = tech.name;
    GlTF_Technique.States states = null;
    if (m_techniqueStates.ContainsKey(exportableMaterial)) {
      states = m_techniqueStates[exportableMaterial];
    }

    if (states == null) {
      // Unless otherwise specified the preset, enable z-buffering.
      states = new GlTF_Technique.States();
      states.enable = new[] { GlTF_Technique.Enable.DEPTH_TEST }.ToList();
    }
    tech.states = states;
    AddAllAttributes(tech, exportableMaterial, hack);
    tech.AddDefaultUniforms(G.RTCCenter != null);

    // Add program.
    GlTF_Program program = new GlTF_Program(G);
    program.name = GlTF_Program.GetNameFromObject(exportableMaterial);
    tech.program = program.name;
    foreach (var attr in tech.attributes) {
      program.attributes.Add(attr.name);
    }
    G.programs.Add(program);

    // Add vertex and fragment shaders.
    GlTF_Shader vertShader = new GlTF_Shader(G);
    vertShader.name = GlTF_Shader.GetNameFromObject(exportableMaterial, GlTF_Shader.Type.Vertex);
    program.vertexShader = vertShader.name;
    vertShader.type = GlTF_Shader.Type.Vertex;
    vertShader.uri = ExportFileReference.CreateHttp(exportableMaterial.VertShaderUri);
    G.shaders.Add(vertShader);

    GlTF_Shader fragShader = new GlTF_Shader(G);
    fragShader.name = GlTF_Shader.GetNameFromObject(exportableMaterial, GlTF_Shader.Type.Fragment);
    program.fragmentShader = fragShader.name;
    fragShader.type = GlTF_Shader.Type.Fragment;
    fragShader.uri = ExportFileReference.CreateHttp(exportableMaterial.FragShaderUri);
    G.shaders.Add(fragShader);
  }

  /// Adds a texture parameter + uniform to the specified material.
  /// As a side effect, auto-creates textures, images, and maybe a sampler if necessary.
  /// Pass:
  ///   matObjName - the material
  ///   texParam - name of the material parameter to add
  ///   fileRef - file containing texture data
  public void AddTextureToMaterial(
      IExportableMaterial exportableMaterial, string texParam, ExportFileReference fileRef) {
    GlTF_Material material = G.materials[exportableMaterial];

    GlTF_Sampler sampler = GlTF_Sampler.LookupOrCreate(
        G, GlTF_Sampler.MagFilter.LINEAR, GlTF_Sampler.MinFilter.LINEAR_MIPMAP_LINEAR);

    // The names only matter for gltf1, so keep them similar for easier diffing.
    // Essentially, this names the image and texture after the first material that wanted them.
    string matNameAndParam = $"{exportableMaterial.UniqueName:D}_{texParam}";
    var img = GlTF_Image.LookupOrCreate(G, fileRef, proposedName: matNameAndParam);
    var tex = GlTF_Texture.LookupOrCreate(G, img, sampler, proposedName: matNameAndParam);

    material.values.Add(new GlTF_Material.TextureKV(key: texParam, texture: tex));

    // Add texture-related parameter and uniform.
    AddUniform(exportableMaterial,
               texParam, GlTF_Technique.Type.SAMPLER_2D, GlTF_Technique.Semantic.UNKNOWN, null);
  }

  // Adds a glTF uniform, as described by name, type, and semantic, to the given technique tech. If
  // node is non-null, that is also included (e.g. for lights).
  private void AddUniform(
      IExportableMaterial exportableMaterial,
      string name, GlTF_Technique.Type type,
      GlTF_Technique.Semantic semantic, GlTF_Node node = null) {
    //var techName = GlTF_Technique.GetNameFromObject(matObjName);
    var tech = GlTF_Writer.GetTechnique(G, exportableMaterial);
    GlTF_Technique.Parameter tParam = new GlTF_Technique.Parameter();
    tParam.name = name;
    tParam.type = type;
    tParam.semantic = semantic;
    if (node != null) {
      tParam.node = node;
    }
    tech.parameters.Add(tParam);
    GlTF_Technique.Uniform tUniform = new GlTF_Technique.Uniform();
    tUniform.name = "u_" + name;
    tUniform.param = tParam.name;
    tech.uniforms.Add(tUniform);
  }

  // Updates glTF technique tech by adding all relevant attributes.
  // Pass:
  //   mesh - (optional) the attributes of some mesh that uses this material, for sanity-checking.
  private void AddAllAttributes(
      GlTF_Technique tech, IExportableMaterial exportableMaterial, GlTF_Attributes mesh) {
    GlTF_VertexLayout layout = new GlTF_VertexLayout(G, exportableMaterial.VertexLayout);

    if (mesh != null) {
      GlTF_VertexLayout meshLayout = mesh.m_layout;
      if (layout != meshLayout) {
        if (meshLayout.GetTexcoordSize(2) > 0) {
          // We funnel timestamps in through GeometryPool.texcoord2 and write them out as
          // _TB_TIMESTAMP.  Thus, the Pool's layout has a texcoord2 but the material's layout does
          // not. This is a mismatch between the mesh data and the material props, but:
          // 1. Timestamp isn't intended to be funneled to the material, so it's correct that the
          //    material layoutdoesn't have texcoord2
          // 2. It's fine if material attrs are a subset of the mesh attrs
          // 3. This only affects gltf1; only materials with techniques need to enum their attrs.
          // Maybe this check should be layout.IsSubset(meshLayout).
          /* ignore this mismatch */
        } else {
          Debug.LogWarning($"Layout for {exportableMaterial.DurableName} doesn't match mesh's");
        }
      }
    }

    // Materials are things that are shared across multiple meshes.
    // Material creation shouldn't depend on data specific to a particular mesh.
    // But it does. It's a hack.
    // Rather than do something reasonable like this:
    //
    //   Create material's technique's attributes based on layout
    //   Create mesh and its accessors based on layout
    //
    // We do this:
    //
    //   Create mesh and its accessors based on layout
    //     Lazily create the material used by the mesh
    //       Create material's technique's attributes based on the accessors
    //       of the last mesh we created
    AddAttribute("position", layout.PositionInfo.techniqueType,
                 GlTF_Technique.Semantic.POSITION, tech);
    if (layout.NormalInfo != null) {
      AddAttribute("normal", layout.NormalInfo.Value.techniqueType,
                   GlTF_Technique.Semantic.NORMAL, tech);
    }
    if (layout.ColorInfo != null) {
      AddAttribute("color", layout.ColorInfo.Value.techniqueType,
                   GlTF_Technique.Semantic.COLOR, tech);
    }
    if (layout.TangentInfo != null) {
      AddAttribute("tangent", layout.TangentInfo.Value.techniqueType,
                   GlTF_Technique.Semantic.TANGENT, tech);
    }
    // TODO: remove; this accessor isn't used. Instead, shaders use texcoord1.w
    if (layout.PackVertexIdIntoTexcoord1W) {
      AddAttribute("vertexId", GlTF_Technique.Type.FLOAT /* hardcoded, but this is gong away */,
                   GlTF_Technique.Semantic.UNKNOWN, tech);
    }
    for (int i = 0; i < 4; ++i) {
      var texcoordInfo = layout.GetTexcoordInfo(i);
      if (texcoordInfo != null) {
        GlTF_Technique.Semantic semantic = GlTF_Technique.Semantic.TEXCOORD_0 + i;
        AddAttribute($"texcoord{i}", texcoordInfo.Value.techniqueType, semantic, tech);
      }
    }
  }

  // Adds to gltfMesh the glTF dependencies (primitive, material, technique, program, shaders)
  // required by unityMesh, using matObjName for naming the various material-related glTF
  // components. This does not add any geometry from the mesh (that's done separately using
  // GlTF_Mesh.Populate()).
  //
  // This does not create the material either. It adds a reference to a material that
  // presumably will be created very soon (if it hasn't previously been created).
  private void AddMeshDependencies(
      ObjectName meshName, IExportableMaterial exportableMaterial, GlTF_Mesh gltfMesh,
      GlTF_VertexLayout gltfLayout) {
    GlTF_Primitive primitive = new GlTF_Primitive(
        new GlTF_Attributes(G, meshName, gltfLayout));

    GlTF_Accessor indexAccessor = G.CreateAccessor(
        GlTF_Accessor.GetNameFromObject(meshName, "indices_0"),
        GlTF_Accessor.Type.SCALAR, GlTF_Accessor.ComponentType.USHORT,
        isNonVertexAttributeAccessor: true);
    primitive.indices = indexAccessor;
    if (gltfMesh.primitives.Count > 0) {
      Debug.LogError("More than one primitive per mesh is unimplemented and unsupported");
    }
    gltfMesh.primitives.Add(primitive);

    // This needs to be a forward-reference (ie, by name) because G.materials[exportableMaterial]
    // may not have been created yet.
    primitive.materialName = GlTF_Material.GetNameFromObject(exportableMaterial);
  }
}
