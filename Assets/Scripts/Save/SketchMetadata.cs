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

using Newtonsoft.Json;
using System;
using System.Linq;
using UnityEngine;

namespace TiltBrush {
/* metadata.json format changes and additions


==  v7.0: Single models introduced

New list added called Models
This list could theoretically contain as many models as the user liked, but we cut it back.
In the wild, this list should only ever be of length zero or one, never null.

 "Models": [
   {
     "FilePath": "C:\\Users\\Person\\Documents\\Tilt Brush\\Models\\Lili.obj",
     "Position": [ -6.38644, 12.7471752, 8.778034 ],
     "Rotation": [ 0.018738009, -0.861053467, -0.00643645832, -0.5081283 ],
     "Scale":    [ 0.649798400, 0.649798400, 0.649798400 ]
   }
 ]


==  v7.5: Multiple models and images introduced

ModelIndex replaces Models
ImageIndex added
These lists contained one entry for every unique object. Each entry in the list contained
a transform for each instance of the object in the sketch.
Both ModelIndex and ImageIndex can be null, and will never be zero length.

 "ImageIndex": [
   {
     "FileName": "Blue And teal gradient.png",
     "AspectRatio": 1.77777779,
     "Transforms": [ [ [ 86.13458, 78.30929, 58.5603828 ], [ 0.0259972736, -0.291850924, -0.00451487023, -0.956099868 ], 198.431992 ] ]
   }
 ]

 "ModelIndex": [
   {
     "FilePath": "Media Library\\Models\\3a. limbs 3\\5.obj",
     "Transforms": [ [ [ -3.953864, 3.135213, -9.598504 ], [ 0.06734027, 0.287244231, -0.08482468, -0.9517147 ], 1.37809682 ]
     ]
   },
   ...
 ]


==  v7.5-experimental: Guides introduced

Guides are saved as a list of all models, one tranform per instance.
Their shapes are saved as indices since we can guarantee consistency in those.

 "Guides": [
   {
     "Type": 0,
     "Transform": [ [ -3.60909033, 9.355544, -0.7895982 ], [ 0.161795944, 0.100251839, -0.007717509, 0.9816884 ], 2.0 ]
   }
 ]


==  v7.5b: Guides updated.

List turned into an index.
Type stored as a string.
Size stored as transform.scale + Custom.

   "GuideIndex": [
     {
       "Type": "Capsule",
       "States": [
         {
           "Transform": [ [ 4.216275, 19.4874744, -0.2948996 ], [ 0.101220131, 0.190763831, -0.1001101, 0.971257746 ], 4 ],
           "Custom": [ .5, 2, .5 ]
         }
       ]
     }
   ]


==  v8: Guides updated

Extents stored explicitly.
Transform.scale is always 0.
Backwards-compatibility for 7.5b guides (finally removed in v19)
Type accidentally written out in hashed format :-P

 "GuideIndex": [
   {
     "Type": "pmhghhkcdmp",
     "States": [
       {
         "Transform": [ [ 4.216275, 19.4874744, -0.2948996 ], [ 0.101220131, 0.190763831, -0.1001101, 0.971257746 ], 0.0 ],
         "Extents": [ 2.50599027, 9.098843, 2.50599027 ]
       }
     ]
   },
 ]

==  v8.2: Prepare for fixing Guides.Type

Guides.Type supports human-readable values, although hashed values are still written


==  v9.0b: Added initial version of Palette

  "Palette": {
    "Colors": [
      {
        "r": 50,
        "g": 50,
        "b": 230,
        "a": 255
      }
    ]
  }


==  v9.0: Removed Palette from save file until 9.1

==  v9.1: Changed Palette format to use alpha-less and more-compact color values

  "Palette": { "Entries": [ [ 50, 50, 230, 255 ] ] }

The 9.0b-style "Colors" field is ignored.
The alpha value is preserved when serializing, but will always be 255.
The alpha value is forced to 255 when deserializing.


==  v10.0: Added CustomLights and CustomEnvironment to save file

  "Environment": {
    "GradientColors": [ [ 100, 100, 100, 255 ], [ 60, 64, 90, 255 ]
    ],
    "GradientSkew": [ 0.0, 0.0, 0.0, 1.0 ],
    "FogColor": [ 31, 31, 78, 255 ],
    "FogDensity": 0.00141058769,
    "ReflectionIntensity": 0.3
  }

  "Lights": {
    "Ambient": [ 173, 173, 173, 255 ],
    "Shadow": {
      "Orientation": [ 0.5, 0.0, 0.0, 0.8660254 ],
      "Color": [ 32.0, 31.8180237, 3.81469727E-06, 1.0 ]
    },
    "NoShadow": {
      "Orientation": [ 0.883022249, -0.321393818, 0.116977736, 0.321393818 ],
      "Color": [ 0.03125, 0.000550092547, 0.0, 1.0 ]
    }
  }

The "GradientColors" field of Environment is null if the gradient has not been accessed.
Orientation of lights is stored in scene space.
Colors stored as integer RGB are guaranteed to be LDR. Those as floating point RGB may be HDR.

  "SourceId": "abcdefg"

If this sketch is derived from a Poly asset then SourceId is the id of that original asset.


==  v12.0: Added Set to save file

The set of a sketch comprises of props (models) that have been brought in unscaled and have
the root pivot placed at the origin.
File paths are relative to the Models directory.

  "Set": [
    "Andy/Andy.obj",
    "Tiltasaurus/Tiltasaurus.obj"
  ]


==  v13.0: Added AssetId to Models

Models can be loaded via AssetId, a lookup value for assets stored in Poly.
FilePath and AssetId should never both be valid, but AssetId is preferred in that errant case.

Transforms are maintained for backward compatability for models. They can be read in but are no
longer written out. RawTransforms replaces it, where the new translation represents the pivot of
the models as specified in the original file instead of the center of the mesh bounds, and the
new scale represents the multiplier on the size of the original mesh instead of the normalization.

M13 was never actually released to the public.

 "ModelIndex": [
   {
     "FilePath": null,
     "AssetId": "bzolM7RH0n6",
     "InSet": false,
     "Transforms": null,
     "RawTransforms": [ [ [ -3.953864, 3.135213, -9.598504 ], [ 0.06734027, 0.287244231, -0.08482468, -0.9517147 ], 1.37809682 ]
     ]
   },

If a sketch has been uploaded to Poly we store the asset id so that future uploads can update
the existing asset rather than creating a new one.

Controls for adding a model to a set were hidden behind a flag in M12 and disabled in M14.

==  v14.2: Deprecate Set

Models that are in the Set of the v12 metadata will be written out in ModelIndex with InSet = true.

==  v15.0: Added Version, PinStates for models, images, and guides, TintStates for images

SchemaVersion = 1

TiltModels75: Added PinStates[] and TintStates[]. Each is a bool[] with the same length as Transforms[].
Files with SchemaVersion < 1 are upgraded to have PinStates and TintStates (both set to true)
    ...
      "PinStates": [
        true,
        true,
        false
      ],
      "TintStates": [
        true,
        true,
        false
      ],
      "Transforms": [ [ [ 2.81081581, 5.47280025, 6.80550051 ], [ -0.0380054265, -0.219284073, 0.00313296262, -0.9749155 ], 2.41691542 ],
                    [ [ 3.466383, 5.71923733, 6.45537853 ], [ -0.0334184356, -0.237726957, 0.0122604668, -0.9706796 ], 0.800769 ],
                    [ [ 4.00411844, 5.71402025, 6.12121439 ], [ -0.03800745, -0.354895175, 0.00206097914, -0.9341309 ], 0.760676444 ]
      ]
    ...

Guide.State: added bool Pinned.
Files with SchemaVersion < 1 are upgraded with Pinned = true.

==  v19.0: Remove Guides.State.Custom, TiltModels75.InSet

Guides.State.Custom only existed in the 7.5b file format, and was never released to the public.
Should have been removed long ago!

InSet is converted to a pinned model with identity transform.

==  v22.0: Reference videos added. (TiltVideo[] Videos)
           Camera paths added. (CameraPathMetadata[] CameraPaths)
           Added GroupIds for TiltModels75, Guides.State, TiltImage75, and TiltVideo.

  "CameraPaths": [
    {
      "PathKnots": [
        {
          "Xf": [ [ 2.81081581, 5.47280025, 6.80550051 ], [ -0.0380054265, -0.219284073, 0.00313296262, -0.9749155 ], 2.41691542 ],
          "Speed": 2.81081581
        },
        {
          ...
        }
      ],
      "RotationKnots": [
        {
          "Xf": [ [ 2.81081581, 5.47280025, 6.80550051 ], [ -0.0380054265, -0.219284073, 0.00313296262, -0.9749155 ], 2.41691542 ],
          "KnotIndex": 0,
          "KnotT": 0.2,
        },
        {
          ...
        }
      "SpeedKnots": [
        {
          "Xf": [ [ 2.81081581, 5.47280025, 6.80550051 ], [ -0.0380054265, -0.219284073, 0.00313296262, -0.9749155 ], 2.41691542 ],
          "KnotIndex": 0,
          "KnotT": 0.2,
          "Speed": 2.81081581
        },
        {
          ...
        }
      ],
    },
    {
      ...
    }
  ],

PathKnots, RotationKnots, and SpeedKnots may all have 0 elements.  RotationKnots and SpeedKnots
will not have >0 elements if PathKnots has 0 elements.
 */


// From v7.0
[Serializable]
public class TiltModels70 {
  /// Absolute path to model. Relative paths are not supported.
  public string FilePath { get; set; }
  public Vector3 Position { get; set; }
  public Quaternion Rotation { get; set; }
  public Vector3 Scale { get; set; }

  public TiltModels75 Upgrade() {
    string relativePath;
    try {
      relativePath = WidgetManager.GetModelSubpath(FilePath);
    } catch (ArgumentException) {
      relativePath = null;
    }
    // I guess keep it around, so we don't lose information.
    if (relativePath == null) {
      relativePath = FilePath;
    }

    return new TiltModels75 {
        FilePath = relativePath,
        PinStates = new[] { true },
        Transforms = new[] { TrTransform.TRS(Position, Rotation, Scale.x) }
    };
  }
}

// Use for v7.5 and on
// Missing models are normally preserved, with these exceptions:
// - lost if a pre-M13 .tilt is saved in M13+
// - InSet models are lost if saved, at least through M18 (b/65633544)
[Serializable]
public class TiltModels75 {
  /// Relative path to model from Media Library.
  /// e.g. Media Library/Models/subdirectory/model.obj
  /// With 14.0 on, this is unused if AssetId is valid.
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public string FilePath { get; set; }

  /// AssetId for Poly.
  /// Added in 14.0, this is preferred over FilePath.
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public string AssetId { get; set; }

  // True if showing the untransformed, non-interactable model.
  // Added in M13 in 97e210f041e20b87c72c87bafb71d7d399d46c13. Never released to public.
  // Turned into RawTransforms in M19.
  [JsonProperty(
      PropertyName = "InSet",  // Used to be called InSet
      DefaultValueHandling = DefaultValueHandling.Ignore  // Don't write "false" values into the .tilt any more
      )]
  [System.ComponentModel.DefaultValue(false)]
  public bool InSet_deprecated { get; set; }

  // True if model should be pinned on load. Added in M15.
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public bool[] PinStates { get; set; }

  /// Prior to M13, never null or empty; but an empty array is allowed on read.
  /// Post M13, always null.
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public TrTransform[] Transforms { get; set; }

  /// Prior to M13, always null.
  /// Post M13, never null or empty; but an empty array is allowed on read.
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public TrTransform[] RawTransforms {
    get { return m_rawTransforms; }
    set { m_rawTransforms = MetadataUtils.Sanitize(value); }
  }
  // Only for use by MetadataUtils.cs
  [JsonIgnore]
  public TrTransform[] m_rawTransforms;

  /// used to bridge the gap between strict Tilt Brush and not-so-strict json
  [JsonIgnore]
  public Model.Location Location {
    get {
      if (AssetId != null) {
        return Model.Location.PolyAsset(AssetId, null);
      } else if (FilePath != null) {
        return Model.Location.File(FilePath);
      } else {
        return new Model.Location();  // invalid location
      }
    }
    set {
      if (value.GetLocationType() == Model.Location.Type.LocalFile) {
        FilePath = value.RelativePath;
        AssetId = null;
      } else if (value.GetLocationType() == Model.Location.Type.PolyAssetId) {
        FilePath = null;
        AssetId = value.AssetId;
      }
    }
  }

  // Group IDs for widgets. 0 for ungrouped items. Added in M22.
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public uint[] GroupIds { get; set; }
}

[Serializable]
public class Guides {
  // In 8.x we mistakenly wrote out hashed names for these shapes.
  const string kHashedCube = "ldeocipaedc";
  const string kHashedSphere = "jminadgooco";
  const string kHashedCapsule = "pmhghhkcdmp";

  // Custom dimensions for non-uniformly scaled stencils
  [Serializable]
  public class State {
    public TrTransform Transform { get; set; }
    public Vector3 Extents { get; set; }
    // True if guide should be pinned on load. Added in M15.
    public bool Pinned { get; set; }
    // Group ID for widget. 0 for ungrouped items. Added in M22.
    public uint GroupId { get; set; }
  }

  // This is the accessor used by Json.NET for reading/writing the "Type" field.
  // The getter is used for writing; the setter for reading.
  [JsonProperty(PropertyName = "Type")]
  private string SerializedType {
    get {
      string ret = Type.ToString();
      if (! Char.IsUpper(ret[0])) {
        // Must be an obfuscated value, or a numeric value (ie, an int that doesn't map
        // to a valid enum name), neither of which is expected. Die early rather than
        // generate garbage output, so we can catch it in testing.
        Debug.LogErrorFormat("Writing bad stencil value {0}", ret);
        return "Cube";
      }
      return ret;
    }

    set {
      try {
        Type = (StencilType)Enum.Parse(typeof(StencilType), value, true);
      } catch (ArgumentException e) {
        // Support the 8.x names for these
        switch (value) {
        case kHashedCube:    Type = StencilType.Cube;    break;
        case kHashedSphere:  Type = StencilType.Sphere;  break;
        case kHashedCapsule: Type = StencilType.Capsule; break;
        default:
          // TODO: log a user visible warning?
          Debug.LogException(e);
          Type = StencilType.Cube;
          break;
        }
      }
    }
  }

  [JsonIgnore]
  public StencilType Type = StencilType.Cube;

  public State[] States { get; set; }
}

[Serializable]
public class Palette {
  // Protect Tilt Brush from getting bad alpha values from the user.
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  private Color32[] Entries {
    get { return Colors; }
    set {
      if (value != null) {
        for (int i = 0; i < value.Length; ++i) {
          value[i].a = 255;
        }
      }
      Colors = value;
    }
  }

  [JsonIgnore]
  public Color32[] Colors { get; set; }
}

[Serializable]
public class CustomLights {
  [Serializable]
  public class DirectionalLight {
    public Quaternion Orientation { get; set; }
    public Color Color { get; set; }
  }

  public Color32 Ambient { get; set; }
  public DirectionalLight Shadow { get; set; }
  public DirectionalLight NoShadow { get; set; }
}

[Serializable]
public class CustomEnvironment {
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public Color32[] GradientColors { get; set; }
  public Quaternion GradientSkew { get; set; }
  public Color32 FogColor { get; set; }
  public float FogDensity { get; set; }
  public float ReflectionIntensity { get; set; }
}

[Serializable]
public class CameraPathPositionKnotMetadata {
  public TrTransform Xf;
  public float TangentMagnitude;
}

[Serializable]
public class CameraPathRotationKnotMetadata {
  public TrTransform Xf;
  public float PathTValue;
}

[Serializable]
public class CameraPathSpeedKnotMetadata {
  public TrTransform Xf;
  public float PathTValue;
  public float Speed;
}

[Serializable]
public class CameraPathFovKnotMetadata {
  public TrTransform Xf;
  public float PathTValue;
  public float Fov;
}

[Serializable]
public class CameraPathMetadata {
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public CameraPathPositionKnotMetadata[] PathKnots { get; set; }
  public CameraPathRotationKnotMetadata[] RotationKnots { get; set; }
  public CameraPathSpeedKnotMetadata[] SpeedKnots { get; set; }
  public CameraPathFovKnotMetadata[] FovKnots { get; set; }
}

// TODO: deprecate (7.5b-only)
// Left just to avoid breaking trusted testers' art
[Serializable]
public class TiltImages75b {
  /// Absolute path to image; any path ending in .png or .jpg will work though.
  public string FilePath { get; set; }
  public TrTransform Transform { get; set; }
  /// width / height
  public float AspectRatio { get; set; }

  public TiltImages75 Upgrade() {
    return new TiltImages75 {
      FileName = System.IO.Path.GetFileName(FilePath),
      AspectRatio = AspectRatio,
      PinStates = new[] { true },
      TintStates = new[] { true },
      Transforms = new[] { Transform },
      GroupIds = new[] { 0u }
    };
  }
}

[Serializable]
public class TiltImages75 {
  /// *.png or *.jpg, should have no path
  public string FileName { get; set; }
  /// width / height
  public float AspectRatio { get; set; }
  // True if image should be pinned on load. Added in M15.
  public bool[] PinStates { get; set; }
  // True if image should use legacy tinting. Added in M15.
  public bool[] TintStates { get; set; }
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public TrTransform[] Transforms { get; set; }
  // Group IDs for widgets. 0 for ungrouped items. Added in M22.
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public uint[] GroupIds { get; set; }
}

[Serializable]
public class Mirror {
  public TrTransform Transform { get; set; }
}

[Serializable]
public class TiltVideo {
  public string FilePath { get; set; } // relative to Media Library folder
  public float AspectRatio { get; set; }
  public bool Pinned;
  public TrTransform Transform;
  public bool Paused { get; set; }
  public float Time { get; set; }
  public float Volume { get; set; }
  // Group ID for widget. 0 for ungrouped items.
  public uint GroupId { get; set; }
}

[Serializable]
// Serializable protects data members obfuscator, but we need to also protect
// method names like ShouldSerializeXxx(...) that are used by Json.NET
[System.Reflection.Obfuscation(Exclude = true)]
public class SketchMetadata {
  static public int kSchemaVersion = 2;

  // Reference to environment GUID.
  public string EnvironmentPreset;
  // Reference to sketch audio GUID.
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public string AudioPreset { get; set; }
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public string[] Authors { get; set; }
  public Guid[] BrushIndex { get; set; }
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public string[] RequiredCapabilities { get; set; }
  public TrTransform ThumbnailCameraTransformInRoomSpace = TrTransform.identity;
  public TrTransform SceneTransformInRoomSpace = TrTransform.identity;
  // Callback for JSON.net (name is magic and special)
  public bool ShouldSerializeSceneTransformInRoomSpace() {
    return SceneTransformInRoomSpace != TrTransform.identity;
  }
  public TrTransform CanvasTransformInSceneSpace = TrTransform.identity;
  // Callback for JSON.net (name is magic and special)
  public bool ShouldSerializeCanvasTransformInSceneSpace() {
    return CanvasTransformInSceneSpace != TrTransform.identity;
  }

  // This was the old name of ThumbnailCameraTransformInRoomSpace.
  [Serializable]
  public struct UnusedSketchTransform {
    public Vector3 position;
    public Quaternion orientation;
  }
  // This is write-only to keep it from being serialized out
  public UnusedSketchTransform ThumbnailCameraTransform {
    set {
      var xf = TrTransform.TR(value.position, value.orientation);
      ThumbnailCameraTransformInRoomSpace = xf;
    }
  }

  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public int SchemaVersion { get; set; }

  /// Deprecated
  /// Only written in 7.0-7.2
  /// Only should ever contains a single model but will create multiples if they are in the list
  /// Write-only so it gets serialized in but not serialized out.
  /// Models and ModelIndex will never coexist in the same .tilt, so we can upgrade in place.
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public TiltModels70[] Models {
    set {
      ModelIndex = value.Select(m70 => m70.Upgrade()).ToArray();
    }
  }

  /// Added in 7.5
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public TiltModels75[] ModelIndex { get; set; }

  // Added in 7.5b; never released to public.
  // Write-only so it gets serialized in but not serialized out.
  // Images and ImageIndex will never coexist in the same .tilt, so we can upgrade in place.
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public TiltImages75b[] Images {
    set {
      ImageIndex = value.Select(i75b => i75b.Upgrade()).ToArray();
    }
  }

  // Added in 7.5
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public TiltImages75[] ImageIndex { get; set; }

  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public Mirror Mirror { get; set; }
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public Guides[] GuideIndex { get; set; }
  // Added in 9.1
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public Palette Palette { get; set; }
  // Added in 10.0
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public CustomLights Lights { get; set; }
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public CustomEnvironment Environment { get; set; }
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public string SourceId { get; set; }
  // Added in 12.0, deprecated in 13.0
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "Set")]
  public string[] Set_deprecated { get; set; }
  // Added in 13.0
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public string AssetId { get; set; }
  // Added in 22.0
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public TiltVideo[] Videos { get; set; }
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public CameraPathMetadata[] CameraPaths { get; set; }
  
  // Added for 24.0b Open-source edition
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public string ApplicationName { get; set; }
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public string ApplicationVersion { get; set; }
}
}// namespace TiltBrush
