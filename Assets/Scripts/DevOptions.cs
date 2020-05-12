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

// This file serves multiple purposes, in order of importance:
//
// - It provides a centralized place for development-only configuration options.
//   These are loosely defined as settings which have an unambiguously correct
//   value in standalone released builds, but which developers need to change.
//
//   Good examples: bools that enable experimental or in-development features
//   Bad examples: floats that tune the size of UI elements
//
// - It provides a way for developers to override the scene with local values for
//   these options. Modifying the scene file is also acceptable as long as the
//   developer is careful not to submit unintended changes.
//
// - It provides a way for developers to override these options in standalone
//   builds, eg when demoing builds on the road. Out of an abundance of caution,
//   this feature is currently disabled.
//
// The scene is still used to store the values of these options; as with other
// MonoBehaviors, take the default values with a grain of salt.

#if UNITY_EDITOR
# define ALLOW_EXTERNAL_CFG_FILE
#endif

using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace TiltBrush {

[Serializable]
public enum TiltFormat { Directory, Inherit, Zip };

// This class awakens before all our other script classes, so it is
// safe for it to override values that have been serialized into the scene.
public class DevOptions : MonoBehaviour {
  const string OPTIONS_FILE_NAME = "../DevOptions.json";
  static public DevOptions I = null;

  public bool BufferBeforeZip = false;
  public bool BufferAfterZip = false;
  public bool ResaveLegacyScenes = true;
  public bool UseAutoProfiler = false;
  public bool AllowStripBreak = true;
  public TiltFormat PreferredTiltFormat = TiltFormat.Inherit;
  public PointerScript.BrushLerp BrushLerp = PointerScript.BrushLerp.Default;

  public void Awake() {
    DevOptions.I = this;
#if ALLOW_EXTERNAL_CFG_FILE
    Load();
#endif
  }

#if ALLOW_EXTERNAL_CFG_FILE
  private void Load(JObject obj) {
    var config = GameObject.Find("/App/Config").GetComponent<Config>();

    MaybeLoad(ref BufferBeforeZip, obj["BufferBeforeZip"]);
    MaybeLoad(ref BufferAfterZip, obj["BufferAfterZip"]);
    MaybeLoad(ref ResaveLegacyScenes, obj["ResaveLegacyScenes"]);
    MaybeLoad(ref UseAutoProfiler, obj["UseAutoProfiler"]);
    MaybeLoadEnum(ref PreferredTiltFormat, obj["PreferredTiltFormat"]);
    MaybeLoadEnum(ref BrushLerp, obj["BrushLerp"]);
    // Note: this now only works for choosing between VR modes.
    // Switching from VR to non-VR requires tweaking PlayerSettings.virtualRealitySupported,
    // which cannot be done at runtime :-/
    MaybeLoadEnum(ref config.m_SdkMode, obj["DisplayMode"]);
  }

  // Extracts a value type (int, bool, etc) from a JToken.
  // If the value is not present or invalid, the ref is not modified.
  private void MaybeLoad<T>(ref T rValue, JToken tok) where T : struct {
    if (tok == null) { return; }
    T? nullOrValue = tok.ToObject<T?>();
    if (nullOrValue == null) { return; }
    rValue = nullOrValue.Value;
  }

  // Extracts a string from a JToken.
  // If the value is not present or invalid, the ref is not modified.
  private void MaybeLoad(ref string rValue, JToken tok) {
    if (tok == null) { return; }
    string tmp = (string)tok;
    if (tmp != null) {
      rValue = tmp;
    }
  }

  // Extracts an enum-typed value from a JToken.
  // If the value is not present or invalid, the ref is not modified.
  private void MaybeLoadEnum<T>(ref T rEnum, JToken tok) {
    if (tok == null) {
      return;
    }
    string s = (string)tok;
    if (s != null) {
      try {
        rEnum = (T)System.Enum.Parse(typeof(T), s, true);
      } catch (System.ArgumentException) {
        Debug.LogFormat("Invalid {0}: '{1}'", typeof(T).FullName, s);
      }
    }
  }

  public void Load() {
    Debug.Assert(Application.isEditor);
    string path = Path.Combine(UnityEngine.Application.dataPath, OPTIONS_FILE_NAME);
    string text;
    try {
      text = File.ReadAllText(path, System.Text.Encoding.UTF8);
    } catch (System.IO.IOException) {
      return;
    }

    Load(JObject.Parse(text));
  }

#endif // ALLOW_EXTERNAL_CFG_FILE
}
}  // namespace TiltBrush
