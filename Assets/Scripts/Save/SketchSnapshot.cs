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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

using Newtonsoft.Json;

namespace TiltBrush {

public class SketchSnapshot {
  private const int kNanoSecondsPerSnapshotSlice = 250;

  private byte[] m_ThumbnailBytes;
  private byte[] m_HiResBytes;
  private TrTransform m_LastThumbnail_SS;
  private List<SketchWriter.AdjustedMemoryBrushStroke> m_Strokes;
  private SketchMetadata m_Metadata;

  private JsonSerializer m_JsonSerializer;
  private SaveIconCaptureScript m_SaveIconCapture;
  private GroupIdMapping m_GroupIdMapping;

  public byte[] Thumbnail {
    get { return m_ThumbnailBytes; }
    set { m_ThumbnailBytes = value; }
  }

  public TrTransform LastThumbnail_SS { get { return m_LastThumbnail_SS; } }

  public string SourceId { set { m_Metadata.SourceId = value; } }

  public string AssetId {
    get => m_Metadata.AssetId;
    set => m_Metadata.AssetId = value;
  }

  // This does not return a fully-constructed SketchSnapshot.
  // You must run timeslicedConstructor to completion before the snapshot is usable.
  // Also, if you want thumbnail icons, you want SaveLoadScript.CreateSnapshotWithIcons() instead.
  public SketchSnapshot(
      JsonSerializer jsonSerializer,
      SaveIconCaptureScript saveIconCapture,
      out IEnumerator<Timeslice> timeslicedConstructor) {
    m_JsonSerializer = jsonSerializer;
    m_SaveIconCapture = saveIconCapture;
    m_GroupIdMapping = new GroupIdMapping();
    timeslicedConstructor = TimeslicedConstructor();
  }

  private IEnumerator<Timeslice> TimeslicedConstructor() {
    System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
    stopwatch.Start();
    long maxTicks =
        (System.Diagnostics.Stopwatch.Frequency * kNanoSecondsPerSnapshotSlice) / 1000000;
    var strokes = SketchMemoryScript.AllStrokes();
    int numStrokes = SketchMemoryScript.AllStrokesCount();
    m_Strokes = new List<SketchWriter.AdjustedMemoryBrushStroke>(numStrokes);
    foreach (var strokeSnapshot in SketchWriter.EnumerateAdjustedSnapshots(strokes)) {
      if (stopwatch.ElapsedTicks > maxTicks) {
        stopwatch.Reset();
        yield return null;
        stopwatch.Start();
      }
      m_Strokes.Add(strokeSnapshot);
    }
    stopwatch.Stop();

    // Note: This assumes Room space == Global space.
    TrTransform xfThumbnail_RS = SketchControlsScript.m_Instance.GetSaveIconTool()
        .LastSaveCameraRigState.GetLossyTrTransform();

    bool hasAuthor = !string.IsNullOrEmpty(App.UserConfig.User.Author);

    m_Metadata = new SketchMetadata {
//      BrushIndex = brushGuids.ToArray(), // Need to do this on actual save!
      EnvironmentPreset = SceneSettings.m_Instance.GetDesiredPreset().m_Guid.ToString("D"),
      AudioPreset = null,
      ThumbnailCameraTransformInRoomSpace = xfThumbnail_RS,
      Authors = hasAuthor ? new [] { App.UserConfig.User.Author } : null,
      ModelIndex = MetadataUtils.GetTiltModels(m_GroupIdMapping),
      ImageIndex = MetadataUtils.GetTiltImages(m_GroupIdMapping),
      Videos = MetadataUtils.GetTiltVideos(m_GroupIdMapping),
      Mirror = PointerManager.m_Instance.SymmetryWidgetToMirror(),
      GuideIndex = MetadataUtils.GetGuideIndex(m_GroupIdMapping),
      Palette = CustomColorPaletteStorage.m_Instance.GetPaletteForSaving(),
      Lights = LightsControlScript.m_Instance.CustomLights,
      Environment = SceneSettings.m_Instance.CustomEnvironment,
      SceneTransformInRoomSpace = Coords.AsRoom[App.Instance.m_SceneTransform],
      CanvasTransformInSceneSpace = App.Scene.AsScene[App.Instance.m_CanvasTransform],
      SourceId =
          SaveLoadScript.m_Instance.TransferredSourceIdFrom(SaveLoadScript.m_Instance.SceneFile),
      AssetId = SaveLoadScript.m_Instance.SceneFile.AssetId,
      CameraPaths = MetadataUtils.GetCameraPaths(),
      SchemaVersion = SketchMetadata.kSchemaVersion,
      ApplicationName = App.kAppDisplayName,
      ApplicationVersion = App.Config.m_VersionNumber,
    };
  }

  public IEnumerator<Timeslice> CreateSnapshotIcons(RenderTexture saveIconTexture,
      RenderTexture hiResTexture, RenderTexture[] gifTextures) {
    var tool = SketchControlsScript.m_Instance.GetSaveIconTool();
    var iconXform = tool.LastSaveCameraRigState;
    var prevXform = tool.CurrentCameraRigState;
    var saveIconScreenshotManager = m_SaveIconCapture.GetComponent<ScreenshotManager>();

    if (hiResTexture != null) {
      tool.CurrentCameraRigState = iconXform;
      saveIconScreenshotManager.RenderToTexture(hiResTexture);
      yield return null;
    }

    if (gifTextures != null) {
      Vector3 basePos;
      Quaternion baseRot;
      iconXform.GetLossyTransform(out basePos, out baseRot);
      //position camera for gif shots
      for (int i = 0; i < gifTextures.Length; ++i) {
        m_SaveIconCapture.SetSaveIconTransformForGifFrame(basePos, baseRot, i);
        saveIconScreenshotManager.RenderToTexture(gifTextures[i]);
        yield return null;
      }
    }

    m_ThumbnailBytes = ScreenshotManager.SaveToMemory(saveIconTexture, true);

    if (hiResTexture != null) {
      yield return null;
      m_HiResBytes =  ScreenshotManager.SaveToMemory(hiResTexture, false);
    }

    tool.CurrentCameraRigState = prevXform;

    // We need to save off the thumbnail position so that future quicksaves will know
    // where to take a thumbnail from.
    m_LastThumbnail_SS = App.Scene.Pose.inverse * iconXform.GetLossyTrTransform();
  }

  /// Follows the "force-superseded by" chain backwards until the beginning is reached,
  /// then returns that brush. This brush is considered the maximally-backwards-compatible brush.
  /// If the passed Guid is invalid, returns it verbatim.
  static Guid GetForcePrecededBy(Guid original) {
    var brush = BrushCatalog.m_Instance.GetBrush(original);
    if (brush == null) {
      Debug.LogErrorFormat("Unknown brush guid {0:N}", original);
      return original;
    }
    // The reason this is okay is that at load time we re-upgrade the brush;
    // see GetForceSupersededBy().
    while (brush.m_Supersedes != null && brush.m_LooksIdentical) {
      brush = brush.m_Supersedes;
    }
    return brush.m_Guid;
  }

  /// Returns null on successful completion. If IO or UnauthorizedAccess exceptions are thrown,
  /// returns their messages. Should not normally raise exceptions.
  public string WriteSnapshotToFile(string path) {
    try {
      using (var tiltWriter = new TiltFile.AtomicWriter(path)) {
        if (m_ThumbnailBytes != null) {
          using (var stream = tiltWriter.GetWriteStream(TiltFile.FN_THUMBNAIL)) {
            stream.Write(m_ThumbnailBytes, 0, m_ThumbnailBytes.Length);
          }
        }

        if (m_HiResBytes != null) {
          using (var stream = tiltWriter.GetWriteStream(TiltFile.FN_THUMBNAIL)) {
            stream.Write(m_HiResBytes, 0, m_HiResBytes.Length);
          }
        }

        List<Guid> brushGuids;
        using (var stream = tiltWriter.GetWriteStream(TiltFile.FN_SKETCH)) {
          SketchWriter.WriteMemory(stream, m_Strokes, m_GroupIdMapping, out brushGuids);
        }
        m_Metadata.BrushIndex = brushGuids.Select(GetForcePrecededBy).ToArray();

        using (var jsonWriter = new CustomJsonWriter(new StreamWriter(
            tiltWriter.GetWriteStream(TiltFile.FN_METADATA)))) {
          m_JsonSerializer.Serialize(jsonWriter, m_Metadata);
        }

        tiltWriter.Commit();
      }
    } catch (IOException ex) {
      return ex.Message;
    } catch (UnauthorizedAccessException ex) {
      return ex.Message;
    }
    return null;
  }

}

}  // namespace TiltBrush
