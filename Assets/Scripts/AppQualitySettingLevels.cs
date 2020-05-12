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
using System.Linq;
using UnityEngine;

namespace TiltBrush {
  /// Scriptable object that contains the app quality settings at several levels for a platform.
  [CreateAssetMenu(fileName = "QualityLevels", menuName = "App Quality Settings", order = 1)]
  public class AppQualitySettingLevels : ScriptableObject {

    public enum BloomMode {
      None,
      Fast,
      Full,
      Mobile,
    }

    [Serializable]
    public class AppQualitySettings {
      [SerializeField] private BloomMode m_Bloom = BloomMode.None;
      [SerializeField] private bool m_Hdr = false;
      [SerializeField] private bool m_Fxaa = true;
      [SerializeField] private int m_MsaaLevel = 1;
      [SerializeField] private int m_MaxLod = 99999999;
      [SerializeField] private AnisotropicFiltering m_Anisotropic = AnisotropicFiltering.Enable;
      [Header("Mobile Bloom Settings")]
      [SerializeField] private string m_BloomLevels = "2,2,2";
      [Header("Render Buffer Scale")]
      [SerializeField] private float m_ViewportScale = 1.0f;
      [SerializeField] private float m_EyeTextureScale = 1.0f;
      [Header("Stroke Simplification Settings")]
      [SerializeField] private float m_StrokeSimplification = 0;
      [SerializeField] private int m_TargetMaxControlPoints = 500000;
      [SerializeField] private float m_MaxSimplification = 150;
      [SerializeField] private float m_MaxSimplificationUserStrokes = 10;

      [Header("Mobile Quality Settings")]
      [SerializeField, Range(1,5)] private int m_GpuLevel = 4;
      [SerializeField, Range(0,3)] private int m_FixedFoveationLevel = 0;

      // Even as a private field, Unity will serialize this with a default value (empty array)
      // Unless I put the NoSerializedAttribute on there.
      [NonSerialized] private int[] m_BloomLevelsAsInts = null;

      public BloomMode Bloom { get { return m_Bloom; } }
      public bool Hdr { get { return m_Hdr; } }
      public bool Fxaa { get { return m_Fxaa; } }
      public int MsaaLevel { get { return m_MsaaLevel; } }
      public int MaxLod { get { return m_MaxLod; } }
      public AnisotropicFiltering Anisotropic { get { return m_Anisotropic; } }
      public int FixedFoveationLevel { get { return m_FixedFoveationLevel; } }
      public int GpuLevel { get { return m_GpuLevel; } }
      public float ViewportScale { get { return m_ViewportScale; } }
      public float EyeTextureScale { get { return m_EyeTextureScale; } }

      public int[] BloomLevels {
        get {
          if (m_BloomLevelsAsInts == null) {
            m_BloomLevelsAsInts = m_BloomLevels.Split(',').Select(int.Parse).ToArray();
          }
          return m_BloomLevelsAsInts;
        }
      }

      public float StrokeSimplification { get { return m_StrokeSimplification; } }
      public int TargetMaxControlPoints { get { return m_TargetMaxControlPoints; } }
      public float MaxSimplification { get { return m_MaxSimplification; } }
      public float MaxSimplificationUserStrokes { get { return m_MaxSimplificationUserStrokes; } }
    }

    [Serializable]
    public class TiltMeterPair {
      public BrushDescriptor Brush;
      public float Weight;
    }

    [SerializeField] private AppQualitySettings[] m_qualityLevels;
    [SerializeField] private TiltMeterPair[] m_tiltMeterWeights;
    [SerializeField] private int m_maxPolySketchTriangles = int.MaxValue;
    [SerializeField] private int m_warningPolySketchTriangles = int.MaxValue;
    private Dictionary<Guid, float> m_tiltMeterMap;

    [Header("Mobile Dynamic Quality Settings")]
    [SerializeField] private float m_HigherQualityGpuTrigger;
    [SerializeField] private float m_HigherQualityFpsTrigger;
    [SerializeField] private float m_LowerQualityGpuTrigger;
    [SerializeField] private float m_LowerQualityFpsTrigger;
    [SerializeField] private int m_FramesForLowerQuality = 10;
    [SerializeField] private int m_FramesForHigherQuality = 30;
    [SerializeField] private float m_BloomFadeTime = 0.5f;


    public int FramesForLowerQuality {
      get { return m_FramesForLowerQuality; }
    }
    public int FramesForHigherQuality {
      get { return m_FramesForHigherQuality; }
    }
    public float BloomFadeTime {
      get { return m_BloomFadeTime; }
    }

    public float HigherQualityGpuTrigger { get { return m_HigherQualityGpuTrigger; } }
    public float HigherQualityFpsTrigger { get { return m_HigherQualityFpsTrigger; } }
    public float LowerQualityGpuTrigger { get { return m_LowerQualityGpuTrigger; } }
    public float LowerQualityFpsTrigger { get { return m_LowerQualityFpsTrigger; } }

    public AppQualitySettings this[int index] {
      get {
        index = Mathf.Min(index, m_qualityLevels.Length - 1);
        return m_qualityLevels[index];
      }
    }

    public int Length {
      get { return m_qualityLevels.Length; }
    }

    public int MaxPolySketchTriangles {
      get { return m_maxPolySketchTriangles; }
    }

    public int WarningPolySketchTriangles {
      get { return m_warningPolySketchTriangles; }
    }

    public float GetWeightForBrush(Guid brush) {
      float weight;
      if (!m_tiltMeterMap.TryGetValue(brush, out weight)) {
        return 1f;
      }
      return weight;
    }

    [System.Reflection.Obfuscation(Exclude=true)]
    private void OnEnable() {
      m_tiltMeterMap = new Dictionary<Guid, float>();
      foreach (var pair in m_tiltMeterWeights) {
        m_tiltMeterMap.Add(pair.Brush.m_Guid, pair.Weight);
      }
    }

  }
}
