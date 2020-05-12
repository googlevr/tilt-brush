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
using JetBrains.Annotations;
using UnityEngine;

namespace TiltBrush {

using TexcoordInfo = GeometryPool.TexcoordInfo;
using Semantic = GeometryPool.Semantic;
using StrokeGroup = SketchGroupTag;

public static class ExportUtils {
  // Used to refer to built-in textures that we put in Support/
  public const string kBuiltInPrefix = "tiltbrush://";
  public const string kShaderDirectory = "shaders/brushes";
  public const string kProjectRelativeBrushExportRoot = "Support/TiltBrush.com/" + kShaderDirectory;
  public const string kProjectRelativeEnvironmentExportRoot = "Support/TiltBrush.com/environments";
  public const string kProjectRelativeTextureExportRoot = "Support/TiltBrush.com/textures";
  public const string kProjectRelativeSupportBrushTexturesRoot = "Support/" + kShaderDirectory;

  // -------------------------------------------------------------------------------------------- //
  // Brush/Canvas Export Payloads
  // -------------------------------------------------------------------------------------------- //

  /// A canvas and some strokes
  [UsedImplicitly(ImplicitUseTargetFlags.Members)]
  public class ExportCanvas {
    public CanvasScript m_canvas;
    private List<Stroke> m_strokes;

    public ExportCanvas(CanvasScript canvas, IEnumerable<Stroke> strokes) {
      m_canvas = canvas;
      m_strokes = strokes.ToList();
    }

    public ExportCanvas(IGrouping<CanvasScript, Stroke> group) {
      m_canvas = group.Key;
      m_strokes = group.ToList();
    }

    public IEnumerable<ExportGroup> SplitByGroup() {
      return m_strokes.GroupBy(stroke => stroke.Group)
          .Select(g => new ExportGroup(g));
    }

    public IEnumerable<ExportBrush> SplitByBrush() {
      return m_strokes.GroupBy(stroke => stroke.m_BrushGuid)
          .Select(g => new ExportBrush(g));
    }
  }

  /// A group id and some strokes
  public class ExportGroup {
    public StrokeGroup m_group;
    private List<Stroke> m_strokes;

    public ExportGroup(IGrouping<StrokeGroup, Stroke> group) {
      m_group = group.Key;
      m_strokes = group.ToList();
    }

    public IEnumerable<ExportBrush> SplitByBrush() {
      return m_strokes.GroupBy(stroke => stroke.m_BrushGuid)
          .Select(g => new ExportBrush(g));
    }
  }

  /// A brush guid and some strokes
  /// This is the only grouping that can be converted to geometry
  public class ExportBrush {
    public BrushDescriptor m_desc;
    private List<Stroke> m_strokes;

    public ExportBrush(IGrouping<Guid, Stroke> group) {
      m_desc = BrushCatalog.m_Instance.GetBrush(group.Key);
      m_strokes = group.ToList();
    }

    public struct PoolAndStrokes {
      public GeometryPool pool;
      public List<Stroke> strokes;
    }

    // Returns a pool, or null if it's not from a pool.
    private static GeometryPool GetPool(Stroke stroke) {
      if (stroke.m_BatchSubset == null) { return null; }
      return stroke.m_BatchSubset.m_ParentBatch.Geometry;
    }

    // Yields strokes in the same order as in the input, but adds an additional side effect:
    // When the user consumes the stroke they will likely force its batch to become resident.
    // This wrapper undoes that, preserving the state of the Batch's residency.
    private IEnumerable<Stroke>
        PreserveBatchResidency(IEnumerable<Stroke> input) {
      // We could do the "make it resident, make it not resident" dance on a stroke-by-stroke
      // basis, but that would be ridiculously wasteful. Optimize by grouping adjacent strokes
      // together if they share the same batch.
      //
      // When a sketch first loads, this optimization works extremely well because batches are
      // contiguous in the stroke list. The more the user mutates (select, unselect, recolor)
      // the less this will be true.  There's not a lot we can do about it unless we want to
      // sort the strokes by batch rather than by draw time (or whatever criteria the incoming
      // iterator uses), which I think would be too invasive.
      foreach (IGrouping<GeometryPool, Stroke> group in
          input.GroupBy(stroke => GetPool(stroke))) {
        GeometryPool pool = group.Key;

        // If we have to bring the pool into memory, save off enough data so we can
        // push it back out.
        Mesh previousBackingMesh = null;
        if (pool != null && !pool.IsGeometryResident) {
          previousBackingMesh = pool.GetBackingMesh();
          // We currently only eject batch-owned GeometryPool to Mesh, so this is unlikely to trip;
          // but we eventually may want to eject them to disk to save even more memory.
          if (previousBackingMesh == null) {
            Debug.LogWarning("Not yet able to eject pool back to file; leaving it in memory");
          }
          pool.EnsureGeometryResident();
        }

        foreach (Stroke stroke in group) {
          yield return stroke;
        }

        if (previousBackingMesh != null) {
          pool.MakeGeometryNotResident(previousBackingMesh);
        }
      }
    }

    // Puts the passed timestamp data into pool.texcoord2.
    // It's assumed that the pool does not already have anything in texcoord2.
    private static void AugmentWithTimestamps(GeometryPool pool, ref List<Vector3> timestamps) {
      if (App.UserConfig.Export.ExportStrokeTimestamp) {
        GeometryPool.VertexLayout withTxc2 = pool.Layout;
        if (withTxc2.texcoord2.size == 0 && timestamps.Count == pool.NumVerts) {
          withTxc2.texcoord2 = new TexcoordInfo { size = 3, semantic = Semantic.Timestamp };
          pool.Layout = withTxc2;
          pool.m_Texcoord2.v3 = timestamps;
          timestamps = null;  // don't let caller reuse the list
        } else {
          Debug.LogError("Internal error; cannot add timestamps");
        }
      }
    }

    // Appends timestamp data to the passed List<Vector3>.
    //
    // The timestamps are laid out like so:
    //   x = start of stroke
    //   y = end of stroke
    //   z = Roughly interpolated between the start and end of the stroke
    // I say "roughly interpolated" because we make the assumption that each control point
    // creates a fixed number of vertices. It's correct only to a first approximation.
    private static void AppendTimestamps(
        Stroke stroke, int numVerts,
        List<Vector3> timestamps) {
      Vector3 ts = new Vector3(stroke.HeadTimestampMs * .001f,
                               stroke.TailTimestampMs * .001f,
                               0);
      foreach (float interpolated in MathUtils.LinearResampleCurve(
          stroke.m_ControlPoints.Select(cp => cp.m_TimestampMs * .001f).ToArray(),
          numVerts)) {
        ts.z = interpolated;
        timestamps.Add(ts);
      }
    }

    /// Converts strokes to multiple GeometryPools whose size is <= vertexLimit.
    /// The default vertexLimit is the maximum size allowed by Unity.
    /// If a single stroke exceeds the vertex limit, the stroke will be ignored.
    /// TODO: dangerous! vertexLimit should be a soft limit, with a hard limit of 65k
    public IEnumerable<PoolAndStrokes> ToGeometryBatches(int vertexLimit=65534) {
      var layout = BrushCatalog.m_Instance.GetBrush(m_desc.m_Guid).VertexLayout;
      var pool = new GeometryPool();
      var strokes = new List<Stroke>();
      // Timestamps are kept separate and only stitched in when we emit a Pool.
      // This is because GeometryPool.Append() doesn't know what to do if the
      // source and dest layouts are different.
      List<Vector3> timestamps = new List<Vector3>();
      pool.Layout = layout;
      foreach (var stroke in PreserveBatchResidency(m_strokes)) {
        while (true) {
          if (!stroke.IsGeometryEnabled) { continue; }
          int oldNumVerts = pool.NumVerts;
          if (pool.Append(stroke, vertexLimit)) {
            strokes.Add(stroke);
            if (App.UserConfig.Export.ExportStrokeTimestamp) {
              AppendTimestamps(stroke, pool.NumVerts - oldNumVerts, timestamps);
            }
            // common case: it fits
            break;
          } else if (pool.m_Vertices.Count > 0) {
            // it doesn't fit, but we can make a bit of forward progress by flushing the buffer
            AugmentWithTimestamps(pool, ref timestamps);
            yield return new PoolAndStrokes { pool = pool, strokes = strokes };
            pool = new GeometryPool();
            pool.Layout = layout;
            strokes = new List<Stroke>();
            timestamps = new List<Vector3>();
            // loop around for another go
          } else {
            // very uncommon case: stroke won't fit even in an empty buffer.
            // Should never happen with the default vertexLimit.
            Debug.LogWarning("Cannot export stroke that exceeds vertex limit");
            // No choice but to ignore the stroke
            break;
          }
        }
      }
      if (pool.m_Vertices.Count > 0) {
        AugmentWithTimestamps(pool, ref timestamps);
        yield return new PoolAndStrokes { pool = pool, strokes = strokes };
      }
    }
  }

  // -------------------------------------------------------------------------------------------- //
  // SceneState Export Payloads
  // The state objects below are pure-state populated by the ExportCollector.
  // -------------------------------------------------------------------------------------------- //

  // GetInstanceID() is not determinstic.
  // The order in which objects are exported is somewhat determinstic.
  // This class uses the order of export to determine the id.
  public class DeterministicIdGenerator {
    private Dictionary<int, int> m_instanceIdToId = new Dictionary<int, int>();
    private int m_nextAvailable = 1;
    public int GetIdFromInstanceId(UnityEngine.Object obj) {
      int instanceId = obj.GetInstanceID();
      if (m_instanceIdToId.ContainsKey(instanceId)) {
        return m_instanceIdToId[instanceId];
      } else {
        var ret = m_nextAvailable;
        m_nextAvailable += 1;
        m_instanceIdToId[instanceId] = ret;
        return ret;
      }
    }
  }
  /// The current exportable SceneState of Tilt Brush.
  public class SceneStatePayload {
    // Metadata.
    public string generator = "Tilt Brush {0}.{1}";
    public DeterministicIdGenerator idGenerator = new DeterministicIdGenerator();

    // Space Bases.
    public readonly AxisConvention axes;
    public readonly bool reverseWinding;
    public readonly float exportUnitsFromAppUnits = App.UNITS_TO_METERS;

    // Entity Manifests.
    public EnvPayload env = new EnvPayload();
    public LightsPayload lights = new LightsPayload();
    public List<GroupPayload> groups = new List<GroupPayload>();
    public GroupIdMapping groupIdMapping = new GroupIdMapping();
    // These actually contain the model/image data
    public List<ModelMeshPayload> modelMeshes = new List<ModelMeshPayload>();
    public List<ImageQuadPayload> imageQuads = new List<ImageQuadPayload>();
    // These are bare nodes representing things that we currently can't export
    public List<XformPayload> referenceThings = new List<XformPayload>();
    public readonly string temporaryDirectory = null;

    // If you pass a temporary directory, it may (or may not) be used
    // for memory optimizations during the export. In this case, take
    // care to call Destroy() if you want the payload to clean up after itself.
    public SceneStatePayload(AxisConvention axes, string temporaryDirectory) {
      this.axes = axes;
      this.temporaryDirectory = temporaryDirectory;
      // The determinant can be used to detect if the basis-change has a mirroring.
      // This matters because a mirroring turns the triangles inside-out, requiring
      // us to flip their winding to preserve the surface orientation.
      this.reverseWinding = (GetFromUnity_Axes(this).determinant < 0);
    }

    // Tears down the payload as safely as possible; throws no exceptions
    public void Destroy() {
      if (groups != null) {
        foreach (var item in groups) {
          item.Destroy();
        }
      }
      if (modelMeshes != null) {
        foreach (var item in modelMeshes) {
          item.Destroy();
        }
      }
    }
  }

  // This should be deprecated in favor of putting a group id in all the payloads
  public class GroupPayload {
    public UInt32 id;
    public List<BrushMeshPayload> brushMeshes = new List<BrushMeshPayload>();
    public void Destroy() {
      if (brushMeshes != null) {
        foreach (var item in brushMeshes) {
          item.Destroy();
        }
      }
    }
  }

  public class EnvPayload {
    public Guid guid;
    public string description;
    public Cubemap skyCubemap;
    public bool useGradient;
    public Color skyColorA;
    public Color skyColorB;
    public Vector3 skyGradientDir;
    public Color fogColor;
    public float fogDensity;
  }

  public class LightPayload {
    public LightType type;
    public string legacyUniqueName;  // guaranteed unique but maybe not friendly
    public string name;
    public Color lightColor;
    public Matrix4x4 xform;
  }

  [UsedImplicitly(ImplicitUseTargetFlags.Members)]
  public class LightsPayload {
    public Color ambientColor;  // currently unused
    public List<LightPayload> lights = new List<LightPayload>();
  }

  // Common to all MeshPayload.
  // This is an _instance_ of a mesh, and corresponds to a GameObject/MeshRenderer
  public abstract class BaseMeshPayload {
    // A unique not-very-human-readable string.
    // "nodeName" and "geometryName" are _mostly_ unique except in edge cases like two distinct
    // widgets having the same GetExportName().
    // Keeping this around also means I can diff vs old gltf1 files for testing purposes.
    public string legacyUniqueName;

    // A pleasant-looking name for the node
    public string nodeName;
    public Matrix4x4 xform = Matrix4x4.identity;
    // Not owned by this instance; ownership is potentially shared with other BaseMeshPayloads
    public GeometryPool geometry;
    // A pleasant-looking name for the geometry
    public string geometryName;
    public IExportableMaterial exportableMaterial;

    public readonly UInt32 group;

    protected BaseMeshPayload(uint groupId) { this.group = groupId; }

    /// Returns a string used to keep different models from having overlapping names.
    /// Examples uses:
    /// - Keep texture names from colliding
    /// - Keep material names from colliding -- in particular this is an issue for
    ///   the name "defaultShadingGroup" which shows up in Poly-obj and Media Library-obj
    ///   imports a lot.
    public abstract string MeshNamespace { get; }

    // Danger! This destroys resources shared by the entire SceneStatePayload.
    // It is only public so it can be called during SceneStatePayload shutdown.
    public void Destroy() {
      if (geometry != null) {
        geometry.Destroy();
        geometry = null;
      }
    }
  }

  public class ImageQuadPayload : BaseMeshPayload {
    public override string MeshNamespace => "media";
    public ImageQuadPayload(uint groupId) : base(groupId) {}
  }

  // MeshPayload for a poly or media library model; might also be used for
  // exporting environments from the editor?
  public class ModelMeshPayload : BaseMeshPayload {
    public Model model;

    // Enumerates the instances of a Model in the scene.
    // Group by (model, modelId) to collect payloads from the same instance.
    public int modelId;

    // These next two are base.xform in factored form: parentXform * localXform == base.xform

    // The transform of the parent ModelWidget.
    // Payloads with the same model and modelId come from the same parent,
    // therefore they have the same parentXform.
    public Matrix4x4 parentXform;

    // This is slightly redundant because it's the same as instanceXform.inverse * base.xform.
    // It's stored explicitly for slight added precision.
    public Matrix4x4 localXform;

    public override string MeshNamespace {
      get {
        if (model.GetLocation().GetLocationType() == Model.Location.Type.PolyAssetId) {
          return model.AssetId;  // blows up if type is not PolyAssetId
        } else {
          string path = Path.GetFileNameWithoutExtension(model.RelativePath);
          return System.Text.RegularExpressions.Regex.Replace(
              path, @"[^a-zA-Z0-9_-]+", "");
        }
      }
    }
    public ModelMeshPayload(uint groupId) : base(groupId) {}
  }

  // MeshPayload for something drawn with a brush.
  // Strokes are currently only used when exporting to USD
  public class BrushMeshPayload : BaseMeshPayload {
    public List<Stroke> strokes;
    public override string MeshNamespace => "brush";
    public BrushMeshPayload(uint groupId) : base(groupId) {}
  }

  public class XformPayload {
    public readonly UInt32 group;
    // This name isn't (currently) guaranteed to be unique among XformPayloads
    public string name;
    public Matrix4x4 xform;

    public XformPayload(uint groupId) { this.group = groupId; }
  }

  // -------------------------------------------------------------------------------------------- //
  // Data Collection Helpers
  // -------------------------------------------------------------------------------------------- //

  /// Filters and returns geometry in a convenient format for export.
  /// Returns geometry for the main canvas
  public static ExportCanvas ExportMainCanvas() {
    // This is probably the more-useful one; it assumes we only have
    // one interesting canvas (true, from a user perspective) and merges
    // the selection canvas into its target canvas.
    //
    // It makes the assumption that the selection canvas is not playing
    // fast-and-loose, and keeps its transform identical to its target
    // canvas (ie, its transform is always identity).
    //
    // Of course, the selection canvas does play fast-and-loose, but
    // we smack the canvas into place (via a deselect/reselect) before
    // getting here.
    var allowedBrushGuids = new HashSet<Guid>(
        BrushCatalog.m_Instance.AllBrushes
        .Where(b => b.m_AllowExport)
        .Select(b => (Guid)b.m_Guid));
    var main = App.Scene.MainCanvas;
    var selection = App.Scene.SelectionCanvas;
    var mainStrokes = SketchMemoryScript.AllStrokes()
        .Where(stroke => allowedBrushGuids.Contains(stroke.m_BrushGuid) &&
                         stroke.IsGeometryEnabled &&
                         (stroke.Canvas == main || stroke.Canvas == selection));
    return new ExportCanvas(main, mainStrokes.ToList());
  }

  /// Filters and returns geometry in a convenient format for export.
  public static List<ExportCanvas> ExportAllCanvases() {
    var allowedBrushGuids = new HashSet<Guid>(
        BrushCatalog.m_Instance.AllBrushes
        .Where(b => b.m_AllowExport)
        .Select(b => (Guid)b.m_Guid));

    return SketchMemoryScript.AllStrokes()
        .Where(stroke => allowedBrushGuids.Contains(stroke.m_BrushGuid) &&
                         stroke.IsGeometryEnabled)
        .GroupBy(stroke => stroke.Canvas)
        .Select(canvasStrokes => new ExportCanvas(canvasStrokes))
        .ToList();
  }

  // -------------------------------------------------------------------------------------------- //
  // Functional Conversion Helpers
  // -------------------------------------------------------------------------------------------- //

  /// Applies change-of-basis conversions and distance conversions to a GeometryPool.
  public static void ConvertUnitsAndChangeBasis(GeometryPool pool, SceneStatePayload payload) {
    ConvertScaleAndChangeBasis(pool, payload.exportUnitsFromAppUnits, GetFromUnity_Axes(payload));
  }

  private static void ConvertScaleAndChangeBasis(
      GeometryPool pool,
      float unitChange,
      Matrix4x4 basisChange) {
    // If there's translation, it's ambiguous whether scale is applied before or after
    // (probably the user means after, but still)
    Debug.Assert((Vector3)basisChange.GetColumn(3) == Vector3.zero);
    Matrix4x4 basisAndUnitChange = Matrix4x4.Scale(unitChange * Vector3.one) * basisChange;

#if false // this code is pendantic but is useful to describe what's _really_ going on here
    // xfBivector is the transform to use for normals, tangents, and other cross-products.
    // Extracting rotation from a mat4 is hard, so take advantage of the fact that basisChange
    // is a (maybe improper) rotation, an improper rotation being a rotation with scale -1.
    // Matrix4x4 xfBivector = ExtractRotation(basisAndUnitChange);
    Matrix4x4 xfBivector = basisChange;
    if (basisChange.determinant < 0) {
      // remove the -1 uniform scale by giving it yet more -1 uniform scale.
      xfBivector = Matrix4x4.Scale(-Vector3.one) * xfBivector;
    }

    // The exporter flips triangle winding when handedness flips, but it leaves the job unfinished;
    // it needs to also flip the normals and tangents, since they're calculated from cross products.
    // Detect that and kludge in an extra -1 scale to finish the job.
    if (basisChange.determinant < 0) {
      xfBivector = Matrix4x4.Scale(-Vector3.one) * xfBivector;
    }
#else
    Matrix4x4 xfBivector = basisChange;  // The mirroring and the winding-flip cancel each other out
#endif
    pool.ApplyTransform(basisAndUnitChange, xfBivector, unitChange, 0, pool.NumVerts);
  }

  /// Convert the vertex colors to linear
  public static List<Color> ConvertToLinearColorspace(List<Color32> srgb) {
    return new List<Color>(srgb.Select(c32 => ((Color)c32).linear));
  }

  public static void ConvertToSrgbColorspace(Color[] linear) {
    for (int i = 0; i < linear.Length; i++) {
      linear[i] = linear[i].gamma;
    }
  }

// Unused and untested
#if false
  /// Convert the vertex Color32s to linear
  /// Beware that you will lose information when quantizing down from
  /// linear-float32 to linear-uint8.
  public static void ConvertToLinearColorspace(MeshPayload meshes) {
    for (int i = 0; i < meshes.Count; i++) {
      var pool = meshes.geometry[i];
      for (int j = 0; j < pool.m_Colors.Count; j++) {
        pool.m_Colors[j] = ((Color)pool.m_Colors[j]).linear;
      }
    }
  }
  /// Convert the light colors to linear
  /// Beware that you will lose information when quantizing down from
  /// linear-float32 to linear-uint8.
  public static void ConvertToLinearColorspace(LightPayload lights) {
    // XXX: these values can be > 1 because they are pre-multiplied by intensity
    // Is it more appropriate to convert the base color from sRGB -> Linear,
    // _then_ multiply by intensity?
    lights.ambientColor = lights.ambientColor.linear;
    for (int i = 0; i < lights.Count; i++) {
      lights.lightColor[i] = lights.lightColor[i].linear;
    }
  }
  /// Convert the environment (sky/fog) colors to linear
  public static void ConvertToLinearColorspace(EnvPayload env) {
    env.fogColor = env.fogColor.linear;
    env.skyColorA = env.skyColorA.linear;
    env.skyColorB = env.skyColorB.linear;
  }
#endif


  /// Flips winding order by swapping indices indexA and indexB.
  /// indexA and indexB must be in the range [0, 2] and not equal to each other.
  public static void ReverseTriangleWinding(GeometryPool pool, int indexA, int indexB) {
    if (indexA == indexB || indexA < 0 || indexA > 2) {
      throw new ArgumentException("indexA");
    }
    if (indexB < 0 || indexB > 2) {
      throw new ArgumentException("indexB");
    }
    var tris = pool.m_Tris;
    int count = tris.Count;
    for (int i = 0; i < count; i += 3) {
      var tmp = tris[i + indexA];
      tris[i + indexA] = tris[i + indexB];
      tris[i + indexB] = tmp;
    }
  }

  /// Given a transform, returns that transform in another basis.
  /// The new basis is specified by outputFromInput.
  public static Matrix4x4 ChangeBasis(
      Matrix4x4 xfInput,
      Matrix4x4 outputFromInput, Matrix4x4 inputFromOutput) {
    return outputFromInput * xfInput * inputFromOutput;
  }

  /// Given a transform, returns that transform in another basis.
  /// The new basis is specified by outputFromInput.
  ///
  /// Not guaranteed to work if the change-of-basis matrix has non-uniform
  /// scale. Otherwise, the resulting transform will not "fit" in a TrTransform.
  public static TrTransform ChangeBasis(
      TrTransform xfInput,
      Matrix4x4 outputFromInput, Matrix4x4 inputFromOutput) {
    // It might make this a little more accurate if outputFromInput were a TrTransform.
    // Although... outputFromInput and inputFromOutput expressed as Matrix4x4 are always
    // infinitely precise, for axis convention changes at least. Doing the same with a quat
    // might involve sqrt(2)s. But maybe not for the common case of unity -> fbx / gltf?
    Matrix4x4 m = outputFromInput * xfInput.ToMatrix4x4() * inputFromOutput;
    return TrTransform.FromMatrix4x4(m);
  }

  /// Given a transform, returns that transform in another basis.
  /// The new basis is specified by outputFromInput.
  ///
  /// Not guaranteed to work if the change-of-basis matrix has non-axis-aligned
  /// scale, or if the rotation portion can't be expressed as the product of 90 degree
  /// rotations about an axis. Otherwise, the resulting scale will not "fit" in a Vec3.
  public static void ChangeBasis(
      Vector3 inputTranslation, Quaternion inputRotation, Vector3 inputScale,
      out Vector3 translation, out Quaternion rotation, out Vector3 scale,
      Matrix4x4 outputFromInput, Matrix4x4 inputFromOutput) {
    TrTransform output = ChangeBasis(
        TrTransform.TR(inputTranslation, inputRotation),
        outputFromInput, inputFromOutput);
    translation = output.translation;
    rotation = output.rotation;
    // Scale is a bit trickier.
    Matrix4x4 m = outputFromInput * Matrix4x4.Scale(inputScale) * inputFromOutput;
    scale = new Vector3(m[0,0], m[1,1], m[2,2]);
    m[0,0] = m[1,1] = m[2,2] = 1;
    Debug.Assert(m.isIdentity);
  }

  /// Given a TrTransform, returns that transform as a mat4 in another basis.
  /// The new basis is specified by the payload.
  /// This changes both axes and units.
  public static Matrix4x4 ChangeBasis(
      TrTransform xfInput, SceneStatePayload payload) {
    Matrix4x4 basis = AxisConvention.GetFromUnity(payload.axes);
    Matrix4x4 basisInverse = AxisConvention.GetToUnity(payload.axes);
    return ChangeBasis(xfInput, basis, basisInverse)
        .TransformBy(TrTransform.S(payload.exportUnitsFromAppUnits))
        .ToMatrix4x4();
  }

  /// Given a transform, returns that transform in another basis.
  /// Since it's a Transform, xfInput is in Global (Room) space, Unity axes, decimeters.
  /// The new basis is: Scene space, with the Payload's axes and units.
  public static Matrix4x4 ChangeBasis(
      Transform xfInput, SceneStatePayload payload) {
    Matrix4x4 basis = AxisConvention.GetFromUnity(payload.axes);
    Matrix4x4 basisInverse = AxisConvention.GetToUnity(payload.axes);
    return ChangeBasis(App.Scene.AsScene[xfInput], basis, basisInverse)
        .TransformBy(TrTransform.S(payload.exportUnitsFromAppUnits))
        .ToMatrix4x4();
  }

  /// Returns a basis-change matrix that transforms from Unity axis conventions to
  /// the conventions specified in the payload.
  ///
  /// Does *not* perform unit conversion, hence the name.
  public static Matrix4x4 GetFromUnity_Axes(SceneStatePayload payload) {
    return AxisConvention.GetFromUnity(payload.axes);
  }

  // -------------------------------------------------------------------------------------------- //
  // Texture Collection Helper
  // -------------------------------------------------------------------------------------------- //

  static public string GetTexturePath(Texture texture) {
#if UNITY_EDITOR
    // Copy the raw asset texture file and make sure it's either a PNG or JPG.
    string texturePath = UnityEditor.AssetDatabase.GetAssetPath(texture);
    texturePath = texturePath.Substring("Assets/".Length);
    texturePath = Path.Combine(Application.dataPath, texturePath);
    string extension = Path.GetExtension(texturePath).ToUpper();
    Debug.Assert(extension == ".PNG" || extension == ".JPG" || extension == ".JPEG",
                 String.Format("Texture {0} must be converted to png or jpg format",
                               texturePath));
    return texturePath;
#else
    // Create an uncompressed texture from mainTex as a fallback when the asset database is
    // not available. This only works on 2D textures that are readable.
    string texFilename = Guid.NewGuid().ToString("D") + ".png";
    Texture2D texture2d = (Texture2D)texture;
    Texture2D uncompressedTexture = new Texture2D(
        texture.width, texture.height, TextureFormat.RGBA32, /*mipmap / mipChain:*/ false);
    uncompressedTexture.SetPixels(texture2d.GetPixels());
    byte[] bytes = uncompressedTexture.EncodeToPNG();

    // Save the texture file.
    string texturePath = Path.Combine(Path.Combine(Path.GetDirectoryName(Application.dataPath),
                                                   ExportUtils.kProjectRelativeTextureExportRoot),
                                      texFilename);
    File.WriteAllBytes(texturePath, bytes);
    return texturePath;
#endif
  }

#if UNITY_EDITOR && GAMEOBJ_EXPORT_TO_GLTF
  public static SceneStatePayload GetSceneStateForGameObjectForExport(
      GameObject gameObject, AxisConvention axes, Environment env) {
    return ExportCollector.GetExportPayloadForGameObject(gameObject, axes, env);
  }

  /// Returns a matrix:
  /// - with units converted by the scale specified in the payload
  /// - with basis changed by the axis convention in the payload
  /// - with the scene transform subtracted out (so, converts from room to scene), but
  ///   only if the app is running.
  /// This version also handles non-uniform scale (but stemming from what? the scene xf?)
  public static Matrix4x4 ChangeBasisNonUniformScale(SceneStatePayload payload, Matrix4x4 root) {
    Matrix4x4 basis = ExportUtils.GetBasisMatrix(payload);

    // Pre- and post-multiplying by the following matrix operations is the equivalent of
    // .TransformBy(TrTransform.S(payload.exportUnitsFromAppUnits)).
    Matrix4x4 xfScale = Matrix4x4.Scale(Vector3.one * payload.exportUnitsFromAppUnits);
    Matrix4x4 xfScaleInverse = Matrix4x4.Scale(Vector3.one / payload.exportUnitsFromAppUnits);

    if (Application.isPlaying) {
      // The world to scene matrix here performs the equivalent of App.Scene.AsScene[root]
      // but works for non-uniform scale.
      Matrix4x4 worldToSceneMatrix = App.Scene.transform.worldToLocalMatrix;
      return xfScale * basis * worldToSceneMatrix * root * basis.inverse * xfScaleInverse;
    } else {
      return xfScale * basis * root * basis.inverse * xfScaleInverse;
    }
  }
#endif

  /// Returns a name based on originalName.
  /// The name will be different from any names in usedNames, and will be added to usedNames.
  public static string CreateUniqueName(string originalName, HashSet<string> names) {
    if (string.IsNullOrEmpty(originalName)) {
      throw new ArgumentException("originalName");
    }
    string baseName, ext; {
      // Don't use the Path functions because originalName may not be a valid path.
      int dot = originalName.LastIndexOf('.');
      if (dot < 0) {
        baseName = originalName; ext = "";
      } else {
        baseName = originalName.Substring(0, dot);
        ext = originalName.Substring(dot);
      }
    }

    for (int i = 0; ; ++i) {
      string subscript = (i == 0 ? "" : $"_{i}");
      string attempt = $"{baseName}{subscript}{ext}";
      if (names.Add(attempt)) {
        return attempt;
      }
    }
  }
}
}  // namespace TiltBrush
