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

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace TiltBrush {
public static class MetadataUtils {
  public struct WidgetMetadata {
    public TrTransform xf;
    public bool pinned;
    public bool tinted;
    public uint groupId;
  }

  /// Sanitizes potentially-invalid data coming from the .tilt file.
  /// Returns an array of valid TrTransforms; may return the input array.
  public static TrTransform[] Sanitize(TrTransform[] data) {
    if (data != null) {
      for (int i = 0; i < data.Length; ++i) {
        if (!data[i].IsFinite()) {
          Debug.LogWarningFormat("Found non-finite TrTransform: {0}", data[i]);
          return data.Where(xf => xf.IsFinite()).ToArray();
        }
      }
    }

    return data;
  }

  private static float ByTranslation(WidgetMetadata meta) {
    return Vector3.Dot(meta.xf.translation, new Vector3(256 * 256, 256, 1));
  }

  private static float ByTranslation(TrTransform xf) {
    return Vector3.Dot(xf.translation, new Vector3(256 * 256, 256, 1));
  }

  private static string ByModelLocation(TiltModels75 models) {
    if (models.AssetId != null) {
      return "AssetId:" + models.AssetId;
    } else if (models.FilePath != null) {
      return "FilePath:" + models.FilePath;
    }
    Debug.LogWarning("Attempted to save model without asset id or filepath");
    return "";
  }

  public static CameraPathMetadata[] GetCameraPaths() {
    return WidgetManager.m_Instance.CameraPathWidgets
        .Where(cpw => cpw.WidgetScript.ShouldSerialize())
        .Select(cpw => cpw.WidgetScript.AsSerializable())
        .ToArray();
  }

  public static TiltModels75[] GetTiltModels(GroupIdMapping groupIdMapping) {
    var widgets =
      WidgetManager.m_Instance.ModelWidgets.Where(w => w.gameObject.activeSelf).ToArray();
    if (widgets.Length == 0 && !ModelCatalog.m_Instance.MissingModels.Any()) {
      return null;
    }
    var widgetModels = widgets.Select(w => w.Model).Distinct();

    Dictionary<Model.Location, List<WidgetMetadata>> modelLocationMap =
        new Dictionary<Model.Location, List<WidgetMetadata>>();
    foreach (var model in widgetModels) {
      modelLocationMap[model.GetLocation()] = new List<WidgetMetadata>();
    }
    foreach (var widget in widgets) {
      WidgetMetadata newEntry = new WidgetMetadata();
      newEntry.xf = widget.GetSaveTransform();
      newEntry.pinned = widget.Pinned;
      newEntry.groupId = groupIdMapping.GetId(widget.Group);
      modelLocationMap[widget.Model.GetLocation()].Add(newEntry);
    }

    List<TiltModels75> models = new List<TiltModels75>();
    foreach (var elem in modelLocationMap) {
      var val = new TiltModels75 {
        Location = elem.Key,
      };

      // Order and align the metadata.
      WidgetMetadata[] ordered = elem.Value.OrderBy(ByTranslation).ToArray();
      val.PinStates = new bool[ordered.Length];
      val.RawTransforms = new TrTransform[ordered.Length];
      val.GroupIds = new uint[ordered.Length];
      for (int i = 0; i < ordered.Length; ++i) {
        val.PinStates[i] = ordered[i].pinned;
        val.RawTransforms[i] = ordered[i].xf;
        val.GroupIds[i] = ordered[i].groupId;
      }
      models.Add(val);
    }

    return models
      .Concat(ModelCatalog.m_Instance.MissingModels)
      .OrderBy(ByModelLocation).ToArray();
  }

  public static TiltVideo[] GetTiltVideos(GroupIdMapping groupIdMapping) {
    return WidgetManager.m_Instance.VideoWidgets.Where(x => x.gameObject.activeSelf).
        Select(x => ConvertVideoToTiltVideo(x)).ToArray();

    TiltVideo ConvertVideoToTiltVideo(VideoWidget widget) {
      TiltVideo video = new TiltVideo {
          FilePath = widget.Video.PersistentPath,
          AspectRatio = widget.Video.Aspect,
          Pinned = widget.Pinned,
          Transform = widget.SaveTransform,
          GroupId = groupIdMapping.GetId(widget.Group),
      };
      if (widget.VideoController != null) {
        video.Paused = !widget.VideoController.Playing;
        video.Time = widget.VideoController.Time;
        video.Volume = widget.VideoController.Volume;
      }
      return video;
    }
  }

  public static Guides[] GetGuideIndex(GroupIdMapping groupIdMapping) {
    var stencils =
      WidgetManager.m_Instance.StencilWidgets.Where(s => s.gameObject.activeSelf).ToList();
    if (stencils.Count == 0) {
      return null;
    }
    Dictionary<StencilType, List<Guides.State>> guides =
      new Dictionary<StencilType, List<Guides.State>>();
    foreach (var stencil in stencils) {
      if (!guides.ContainsKey(stencil.Type)) {
        guides[stencil.Type] = new List<Guides.State>();
      }
      guides[stencil.Type].Add(stencil.GetSaveState(groupIdMapping));
    }
    List<Guides> guideIndex = new List<Guides>();
    foreach (var elem in guides) {
      guideIndex.Add(new Guides {
        Type = elem.Key,
        States = elem.Value.OrderBy(s => ByTranslation(s.Transform)).ToArray()
      });
    }
    return guideIndex.OrderBy(g => g.Type).ToArray();
  }

  public static TiltImages75[] GetTiltImages(GroupIdMapping groupIdMapping) {
    var imports = WidgetManager.m_Instance.ImageWidgets
      .Where(w => w.gameObject.activeSelf).ToArray();
    if (imports.Length == 0) {
      return null;
    }

    // From the list of image widgets in the sketch, create a map that contains a unique
    // entry per image, with associated metadata (transform and pin state) stored as arrays.
    Dictionary<string, List<WidgetMetadata>> imagesByFileName =
        new Dictionary<string, List<WidgetMetadata>>();
    Dictionary<string, float> aspectRatios = new Dictionary<string, float>();
    foreach (var image in imports) {
      string fileName = image.FileName;
      if (image.AspectRatio == null) {
        Debug.LogError("Trying to save partially-initialized image {fileName}");
      }
      if (!imagesByFileName.ContainsKey(fileName)) {
        imagesByFileName[fileName] = new List<WidgetMetadata>();
        aspectRatios[fileName] = image.AspectRatio ?? 1;
      }
      WidgetMetadata newEntry = new WidgetMetadata();
      newEntry.xf = image.SaveTransform;
      newEntry.pinned = image.Pinned;
      newEntry.tinted = image.UseLegacyTint;
      newEntry.groupId = groupIdMapping.GetId(image.Group);
      imagesByFileName[fileName].Add(newEntry);
    }

    // Build the save metadata from our unique map.
    List<TiltImages75> imageIndex = new List<TiltImages75>();
    foreach (var elem in imagesByFileName) {
      var val = new TiltImages75 {
        FileName = elem.Key,
        AspectRatio = aspectRatios[elem.Key]
      };

      // Order and align the metadata.
      WidgetMetadata[] ordered =
          elem.Value.OrderBy(ByTranslation).ToArray();
      val.PinStates = new bool[ordered.Length];
      val.TintStates = new bool[ordered.Length];
      val.Transforms = new TrTransform[ordered.Length];
      val.GroupIds = new uint[ordered.Length];
      for (int i = 0; i < ordered.Length; ++i) {
        val.PinStates[i] = ordered[i].pinned;
        val.TintStates[i] = ordered[i].tinted;
        val.Transforms[i] = ordered[i].xf;
        val.GroupIds[i] = ordered[i].groupId;
      }
      imageIndex.Add(val);
    }
    return imageIndex.OrderBy(i => i.FileName).ToArray();
  }

  public static void VerifyMetadataVersion(SketchMetadata data) {
    Upgrade_Set_ModelIndexInSet(data);

    if (data.SchemaVersion < 1) {
      UpgradeSchema_0to1(data);
    }
    if (data.SchemaVersion < 2) {
      UpgradeSchema_1to2(data);
    }
  }

  // Converts data.Set_deprecated[] to data.ModelIndex[].InSet
  static void Upgrade_Set_ModelIndexInSet(SketchMetadata data) {
    if (data.Set_deprecated == null) {
      return;
    }

    TiltModels75[] index = data.ModelIndex;

    string[] set = data.Set_deprecated;
    if (index == null) { index = new TiltModels75[] { }; }
    List<string> setOnly = new List<string>(set);
    foreach (TiltModels75 m in index) {
      if (set.Contains(m.FilePath)) {
        m.InSet_deprecated = true;
        setOnly.Remove(m.FilePath);
      }
    }
    index = index.Concat(setOnly.Select(s => new TiltModels75 {
      FilePath = s,
      InSet_deprecated = true
    })).ToArray();

    data.ModelIndex = index;
    data.Set_deprecated = null;
  }

  /// SchemaVersion 1 was released in M15.
  /// It adds bool[] TiltModels75.PinStates, bool[] TiltModels75.TintStates, bool Guides.State.Pinned
  static void UpgradeSchema_0to1(SketchMetadata data) {
    // Pin flags were not written out previous to v15, so default guides to pinned.
    if (data.GuideIndex != null) {
      for (int i = 0; i < data.GuideIndex.Length; ++i) {
        for (int j = 0; j < data.GuideIndex[i].States.Length; ++j) {
          data.GuideIndex[i].States[j].Pinned = true;
        }
      }
    }

    // Default images to pinned, tinted, and not grouped.
    if (data.ImageIndex != null) {
      for (int i = 0; i < data.ImageIndex.Length; ++i) {
        int numXfs = data.ImageIndex[i].Transforms.Length;
        data.ImageIndex[i].PinStates = Enumerable.Repeat(true, numXfs).ToArray();
        data.ImageIndex[i].TintStates = Enumerable.Repeat(true, numXfs).ToArray();
        data.ImageIndex[i].GroupIds = Enumerable.Repeat(0u, numXfs).ToArray();
      }
    }

    // Default models to pinned, if they're local.  Poly assets are unpinned.
    if (data.ModelIndex != null) {
      for (int i = 0; i < data.ModelIndex.Length; ++i) {
        if (data.ModelIndex[i].PinStates == null) {
          int numXfs = (data.ModelIndex[i].Transforms != null) ?
              data.ModelIndex[i].Transforms.Length :
              data.ModelIndex[i].RawTransforms.Length;
          data.ModelIndex[i].PinStates = new bool[numXfs];
        }

        for (int j = 0; j < data.ModelIndex[i].PinStates.Length; ++j) {
          data.ModelIndex[i].PinStates[j] = (data.ModelIndex[i].Location.GetLocationType() !=
              Model.Location.Type.PolyAssetId);
        }
      }
    }

    data.SchemaVersion = 1;
  }

  // Append value to array, creating array if necessary
  static T[] SafeAppend<T>(T[] array, T value) {
    T[] asArray = { value };
    return (array == null) ? asArray : array.Concat(asArray).ToArray();
  }

  // SchemaVersion 2: M19
  // Converts data.ModelIndex[].InSet to data.ModelIndex[].RawTransforms[]
  // This changes the behavior slightly: sets are immovable, but RawTransforms[] can be unpinned.
  // This essentially removes the experimental Set feature.
  static void UpgradeSchema_1to2(SketchMetadata data) {
    Debug.Assert(data.SchemaVersion == 1);
    if (data.ModelIndex == null) { return; }
    foreach (TiltModels75 tm75 in data.ModelIndex) {
      if (!tm75.InSet_deprecated) { continue; }
      // Only one of Transforms[] or RawTransforms[] can be non-null.
      // Therefore, we can only append to RawTransforms[] if Transforms[] is null.
      if (tm75.Transforms != null) {
        Debug.LogError("Cannot upgrade InSet if Transforms[] is non-null");
        continue;
      }
      tm75.InSet_deprecated = false;
      tm75.m_rawTransforms = SafeAppend(tm75.m_rawTransforms, TrTransform.identity);
      tm75.PinStates = SafeAppend(tm75.PinStates, true);
    }
    data.SchemaVersion = 2;
  }
}
} // namespace TiltBrush
