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
using UnityEngine;

namespace TiltBrush {

public class CameraConfig : MonoBehaviour {
  const string CAMERA_FOV = "Camera_Fov";
  const string CAMERA_SMOOTHING = "Camera_Smoothing";
  const string POST_EFFECTS = "Camera_PostEffects";
  const string WATERMARK = "Camera_Watermark";

  static public event Action FovChanged;
  static public event Action PostEffectsChanged;
  static public event Action WatermarkChanged;

  static private float m_Fov;
  static private float m_Smoothing;
  static private bool m_PostEffects;
  static private bool m_Watermark;

  // TODO : Unused right now, but will be useful for testing later.
  static public void DeletePrefs() {
    PlayerPrefs.DeleteKey(CAMERA_FOV);
    PlayerPrefs.DeleteKey(CAMERA_SMOOTHING);
    PlayerPrefs.DeleteKey(POST_EFFECTS);
    PlayerPrefs.DeleteKey(WATERMARK);
  }

  // [10 : 140]
  public const float kFovDefault = 80.0f;
  public const float kFovMin = 10.0f;
  public const float kFovMax = 140.0f;
  static public float Fov { get { return m_Fov; } }
  static public float Fov01 {
    get { return Mathf.InverseLerp(kFovMin, kFovMax, m_Fov); }
    set {
      Debug.Assert(value == Mathf.Clamp01(value));
      m_Fov = Mathf.Lerp(kFovMin, kFovMax, value);
      PlayerPrefs.SetFloat(CAMERA_FOV, m_Fov);
      if (FovChanged != null) {
        FovChanged();
      }
    }
  }

  // [0.8 : 1.0)
  public const float kSmoothingDefault = 0.98f;
  public const float kSmoothingMin = 0.8f;
  public const float kSmoothingMax = 1.0f;
  static public float Smoothing { get { return m_Smoothing; } }
  static public float Smoothing01 {
    get { return Mathf.InverseLerp(kSmoothingMin, kSmoothingMax, m_Smoothing); }
    set {
      Debug.Assert(value == Mathf.Clamp01(value));
      m_Smoothing = Mathf.Min(Mathf.Lerp(kSmoothingMin, kSmoothingMax, value), 0.99f);
      PlayerPrefs.SetFloat(CAMERA_SMOOTHING, m_Smoothing);
    }
  }

  static public bool PostEffects {
    get { return m_PostEffects; }
    set {
      m_PostEffects = value;
      PlayerPrefs.SetInt(POST_EFFECTS, m_PostEffects ? 1 : 0);
      if (PostEffectsChanged != null) {
        PostEffectsChanged();
      }
    }
  }

  static public bool Watermark {
    get { return m_Watermark; }
    set {
      m_Watermark = value;
      PlayerPrefs.SetInt(WATERMARK, m_Watermark ? 1 : 0);
      if (WatermarkChanged != null) {
        WatermarkChanged();
      }
    }
  }

  static public void Init() {
    // Favor the config file over the player prefs.
    if (App.UserConfig.Flags.FovValid) {
      m_Fov = App.UserConfig.Flags.Fov;
    } else if (App.UserConfig.Video.FovValid) {
      m_Fov = App.UserConfig.Video.Fov;
    } else {
      m_Fov = PlayerPrefs.GetFloat(CAMERA_FOV, kFovDefault);
    }

    // Pulling camera smoothing from the config will be replaced by the camera panel.
    // TODO : Remove user config smoothing when camera panel exists.
    m_Smoothing = App.UserConfig.Video.CameraSmoothingValid ?
        App.UserConfig.Video.CameraSmoothing :
        PlayerPrefs.GetFloat(CAMERA_SMOOTHING, kSmoothingDefault);
    m_PostEffects = App.UserConfig.Flags.PostEffectsOnCaptureValid ?
        App.UserConfig.Flags.PostEffectsOnCapture :
        (PlayerPrefs.GetInt(POST_EFFECTS, 1) == 1);
    m_Watermark = App.UserConfig.Flags.ShowWatermarkValid ?
        App.UserConfig.Flags.ShowWatermark :
        (PlayerPrefs.GetInt(WATERMARK, 1) == 1);
  }
}

}