// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using UnityEngine;

using static TiltBrush.ExportUtils;

namespace TiltBrush {

// Exports scene to glTF format. Work in progress.
public class ExportGlTF {
  public struct ExportResults {
    public bool success;
    public int numTris;
    public string[] exportedFiles;
  }

  // The ExportManifest is a record of what brushes are available for export and their associated
  // textures and material parameters.
  [Serializable]
  public class ExportManifest {
    public string tiltBrushVersion;
    public string tiltBrushBuildStamp;
    public Dictionary<Guid, ExportedBrush> brushes = new Dictionary<Guid, ExportedBrush>();
  }

  [Serializable]
  public class ExportedBrush {
    public Guid guid;
    public string name;
    /// All brush files are found in folderName: vertexShader, fragmentShader, and textures.
    public string folderName;
    public string shaderVersion;
    /// Versioned file name of vertex shader; relative to folderName
    public string vertexShader;
    /// Versioned file name of fragment shader; relative to folderName
    public string fragmentShader;
    public ExportableMaterialBlendMode blendMode;
    public bool enableCull;
    public Dictionary<string, string> textures = new Dictionary<string, string>();
    public Dictionary<string, Vector2> textureSizes = new Dictionary<string, Vector2>();
    public Dictionary<string, float> floatParams = new Dictionary<string, float>();
    public Dictionary<string, Vector3> vectorParams = new Dictionary<string, Vector3>();
    public Dictionary<string, Color> colorParams = new Dictionary<string, Color>();
  }

  private GlTF_ScriptableExporter m_exporter;

  // This exports the scene into glTF. Brush strokes are exported in the style of the FBX exporter
  // by building individual meshes for the brush strokes, merging them by brush type, and exporting
  // the merged meshes. The merged meshes are split at 64k vert boundaries as required by Unity.
  // Also, scene lights are exported.
  // Pass:
  //   doExtras - true to add a bunch of poly-specific metadata to the scene
  //   selfContained - true to force a more-compatible gltf that doesn't have http:// URIs
  //     The drawback is that the result is messier and contains data that TBT does not need.
  public ExportResults ExportBrushStrokes(
      string outputFile, AxisConvention axes, bool binary, bool doExtras,
      bool includeLocalMediaContent, int gltfVersion,
      bool selfContained=false) {
    var payload = ExportCollector.GetExportPayload(
        axes,
        includeLocalMediaContent: includeLocalMediaContent,
        temporaryDirectory: Path.Combine(Application.temporaryCachePath, "exportgltf"));
    return ExportHelper(payload, outputFile, binary, doExtras: doExtras, gltfVersion: gltfVersion,
                        allowHttpUri: !selfContained);
  }
#if false
  // This exports a game object into glTF. Brush strokes are exported in the style of the FBX
  // exporter by building individual meshes for the game object, merging them by brush type, and
  // exporting the merged meshes. The merged meshes are split at 64k vert boundaries as required by
  // Unity. Environment data can also be exported.
  public ExportResults ExportGameObject(GameObject gameObject, string outputFile,
                                        Environment env = null,
                                        bool binary = false) {

    var payload = ExportUtils.GetSceneStateForGameObjectForExport(
        gameObject,
        AxisConvention.kGltfAccordingToPoly,
        env);

    return ExportHelper(payload, outputFile, binary, doExtras: false);
  }
#endif
  private ExportResults ExportHelper(
      SceneStatePayload payload,
      string outputFile,
      bool binary,
      bool doExtras,
      int gltfVersion,
      bool allowHttpUri) {
    // TODO: Ownership of this temp directory is sloppy.
    // Payload and export share the same dir and we assume that the exporter:
    // 1. will not write files whose names conflict with payload's
    // 2. will clean up the entire directory when done
    // This works, as long as the payload isn't used for more than one export (it currently isn't)
    using (var exporter = new GlTF_ScriptableExporter(payload.temporaryDirectory, gltfVersion)) {
      exporter.AllowHttpUri = allowHttpUri;
      try {
        m_exporter = exporter;
        exporter.G.binary = binary;

        exporter.BeginExport(outputFile);
        exporter.SetMetadata(payload.generator, copyright: null);
        if (doExtras) {
          SetExtras(exporter, payload);
        }

        if (payload.env.skyCubemap != null) {
          // Add the skybox texture to the export.
          string texturePath = ExportUtils.GetTexturePath(payload.env.skyCubemap);
          string textureFilename = Path.GetFileName(texturePath);
          exporter.G.extras["TB_EnvironmentSkybox"] =
              ExportFileReference.CreateLocal(texturePath, textureFilename);
        }

        WriteObjectsAndConnections(exporter, payload);

        string[] exportedFiles = exporter.EndExport();
        return new ExportResults {
            success = true,
            exportedFiles = exportedFiles,
            numTris = exporter.NumTris
        };
      } catch (InvalidOperationException e) {
        OutputWindowScript.Error("glTF export failed", e.Message);
        // TODO: anti-pattern. Let the exception bubble up so caller can log it properly
        // Actually, InvalidOperationException is now somewhat expected in experimental, since
        // the gltf exporter does not check IExportableMaterial.SupportsDetailedMaterialInfo.
        // But we still want the logging for standalone builds.
        Debug.LogException(e);
        return new ExportResults {success = false};
      } catch (IOException e) {
        OutputWindowScript.Error("glTF export failed", e.Message);
        return new ExportResults {success = false};
      } finally {
        payload.Destroy();
        // The lifetime of ExportGlTF, GlTF_ScriptableExporter, and GlTF_Globals instances
        // is identical. This is solely to be pedantic.
        m_exporter = null;
      }
    }
  }

  static string CommaFormattedFloatRGB(Color c) {
    return string.Format("{0}, {1}, {2}", c.r, c.g, c.b);
  }
  static string CommaFormattedVector3(Vector3 v) {
    return string.Format("{0}, {1}, {2}", v.x, v.y, v.z);
  }

  // Populates glTF metadata and scene extras fields.
  private void SetExtras(
      GlTF_ScriptableExporter exporter, ExportUtils.SceneStatePayload payload) {
    Color skyColorA = payload.env.skyColorA;
    Color skyColorB = payload.env.skyColorB;
    Vector3 skyGradientDir = payload.env.skyGradientDir;

    // Scene-level extras:
    exporter.G.extras["TB_EnvironmentGuid"] = payload.env.guid.ToString("D");
    exporter.G.extras["TB_Environment"] = payload.env.description;
    exporter.G.extras["TB_UseGradient"] = payload.env.useGradient ? "true" : "false";
    exporter.G.extras["TB_SkyColorA"] = CommaFormattedFloatRGB(skyColorA);
    exporter.G.extras["TB_SkyColorB"] = CommaFormattedFloatRGB(skyColorB);
    Matrix4x4 exportFromUnity = AxisConvention.GetFromUnity(payload.axes);
    exporter.G.extras["TB_SkyGradientDirection"] = CommaFormattedVector3(
        exportFromUnity * skyGradientDir);
    exporter.G.extras["TB_FogColor"] = CommaFormattedFloatRGB(payload.env.fogColor);
    exporter.G.extras["TB_FogDensity"] = payload.env.fogDensity.ToString();

    // TODO: remove when Poly starts using the new color data
    exporter.G.extras["TB_SkyColorHorizon"] = CommaFormattedFloatRGB(skyColorA);
    exporter.G.extras["TB_SkyColorZenith"] = CommaFormattedFloatRGB(skyColorB);
  }

  // Returns a GlTF_Node; null means "there is no node for this group".
  public GlTF_Node GetGroupNode(uint groupId) {
    GlTF_Globals G = m_exporter.G;
    if (!G.Gltf2 || groupId == 0) {
      // When exporting for Poly be maximally compatible and don't create interior nodes
      return null;
    }
    ObjectName name = new ObjectName($"group_{groupId}");
    return GlTF_Node.GetOrCreate(G, name, Matrix4x4.identity, null, out _);
  }

  private void WriteObjectsAndConnections(GlTF_ScriptableExporter exporter,
                                          SceneStatePayload payload) {
    foreach (BrushMeshPayload meshPayload in payload.groups.SelectMany(g => g.brushMeshes)) {
      exporter.ExportMeshPayload(payload, meshPayload, GetGroupNode(meshPayload.group));
    }

    foreach (var sameInstance in payload.modelMeshes.GroupBy(m => (m.model, m.modelId))) {
      var modelMeshPayloads = sameInstance.ToList();
      if (modelMeshPayloads.Count == 0) { continue; }

      // All of these pieces will come from the same Widget and therefore will have
      // the same group id, root transform, etc
      var first = modelMeshPayloads[0];
      GlTF_Node groupNode = GetGroupNode(first.group);

      if (exporter.G.Gltf2) {
        // Non-Poly exports get a multi-level structure for meshes: transform node on top,
        // all the contents as direct children.
        string rootNodeName = $"model_{first.model.GetExportName()}_{first.modelId}";
        if (modelMeshPayloads.Count == 1 && first.localXform.isIdentity) {
          // Condense the two levels into one; give the top-level node the same name
          // it would have had had it been multi-level.
          GlTF_Node newNode = exporter.ExportMeshPayload(payload, first, groupNode);
          newNode.PresentationNameOverride = rootNodeName;
        } else {
          GlTF_Node parentNode = GlTF_Node.Create(
              exporter.G, rootNodeName,
              first.parentXform, groupNode);
          foreach (var modelMeshPayload in modelMeshPayloads) {
            exporter.ExportMeshPayload(payload, modelMeshPayload, parentNode,
                                       modelMeshPayload.localXform);
          }
        }
      } else {
        // The new code's been tested with Poly and works fine, but out of
        // an abundance of caution, keep Poly unchanged
        foreach (var modelMeshPayload in modelMeshPayloads) {
          exporter.ExportMeshPayload(payload, modelMeshPayload, groupNode);
        }
      }
    }

    foreach (ImageQuadPayload meshPayload in payload.imageQuads) {
      exporter.ExportMeshPayload(payload, meshPayload, GetGroupNode(meshPayload.group));
    }

    foreach (var (xformPayload, i) in payload.referenceThings.WithIndex()) {
      string uniqueName = $"empty_{xformPayload.name}_{i}";
      var node = GlTF_Node.Create(exporter.G, uniqueName, xformPayload.xform, null);
      node.PresentationNameOverride = $"empty_{xformPayload.name}";
    }
  }
}

}  // namespace TiltBrush
