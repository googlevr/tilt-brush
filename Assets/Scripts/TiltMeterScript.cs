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

[Serializable]
public struct TiltMeterState {
  public string m_Description;
  public Color m_Color;
}

public class TiltMeterScript : MonoBehaviour {
  static public TiltMeterScript m_Instance;

  [SerializeField] private TiltMeterState[] m_MeterStates;
  [SerializeField] private string m_MaxMeterDescription;
  [SerializeField] private bool m_BrushSizeAffectsCost = false;
  [SerializeField] private float m_WidgetCostScalar = 0.002f;

  private int m_MeterIndex;
  private float m_MeterAmount;
  // Single tier of tiltmeter is 250,000 control points of doubletapered at size 1.
  private float m_MeterAmountFull = 250000f;

  void Awake() {
    m_Instance = this;

    m_MeterIndex = 0;
    m_MeterAmount = 0.0f;
  }

  public float GetUnifiedValue() {
    return m_MeterIndex * m_MeterAmountFull + m_MeterAmount;
  }

  public float GetMeterFullRatio() {
    if (m_MeterIndex > m_MeterStates.Length - 1) {
      return 1.0f;
    }
    return m_MeterAmount / m_MeterAmountFull;
  }

  public Color GetMeterBGColor() {
    bool bMeterIndexOverMax = m_MeterIndex > m_MeterStates.Length - 1;
    int iMeterBGIndex = m_MeterIndex - 1;
    if (iMeterBGIndex < 0) {
      return Color.grey * 0.5f;
    } else if (bMeterIndexOverMax) {
      return m_MeterStates[m_MeterStates.Length - 1].m_Color;
    }
    return m_MeterStates[iMeterBGIndex].m_Color;
  }

  public Color GetMeterColor() {
    //it's possible for meter index to be beyond the state high bounds, so plan for it
    int iCappedIndex = Mathf.Min(m_MeterIndex, m_MeterStates.Length - 1);
    return m_MeterStates[iCappedIndex].m_Color;
  }

  public Color GetMeterColorAbsolute(float value) {
    // Get the color from a lerp between states, on a scale of 0 to 1.
    float perState = 1.0f / (float)m_MeterStates.Length;
    int state = 0;
    while (value > perState) {
      ++state;
      value -= perState;
    }
    if (state >= m_MeterStates.Length - 1) {
      return m_MeterStates[m_MeterStates.Length - 1].m_Color;
    }
    return Color.Lerp(m_MeterStates[state].m_Color, m_MeterStates[state + 1].m_Color,
        value / perState);
  }

  public string GetMeterText() {
    bool bMeterIndexOverMax = m_MeterIndex > m_MeterStates.Length - 1;
    int iCappedIndex = Mathf.Min(m_MeterIndex, m_MeterStates.Length - 1);
    if (bMeterIndexOverMax) {
      int iAmountOverCap = m_MeterIndex - iCappedIndex;
      if (iAmountOverCap > 1) {
        return m_MaxMeterDescription + " x" + iAmountOverCap.ToString();
      } else {
        return m_MaxMeterDescription;
      }
    }
    return m_MeterStates[iCappedIndex].m_Description;
  }

  public void ResetMeter() {
    m_MeterIndex = 0;
    m_MeterAmount = 0.0f;
  }

  public void AdjustMeter(Stroke stroke, bool up) {
    switch (stroke.m_Type) {
    case Stroke.Type.NotCreated:
      throw new InvalidOperationException();
    case Stroke.Type.BrushStroke:
      AdjustMeter(stroke.m_Object, stroke.m_BrushSize, up);
      break;
    case Stroke.Type.BatchedBrushStroke:
      AdjustMeter(stroke.m_BatchSubset, stroke.m_BrushSize, up);
      break;
    }
  }

  public void AdjustMeterWithWidget(int iWidgetCost, bool up) {
    AdjustMeter(iWidgetCost * m_WidgetCostScalar * (up ? 1.0f : -1.0f));
  }

  private void AdjustMeter(BatchSubset subset, float fBrushSize, bool bAddToMeter) {
    //make brush size not matter if we're ignorning it
    if (!m_BrushSizeAffectsCost) {
      fBrushSize = 1.0f;
    }

    //figure out how much this batch costs
    BrushDescriptor brush = BrushCatalog.m_Instance.GetBrush(
        subset.m_ParentBatch.ParentPool.m_BrushGuid);
    float fCost = BaseBrushScript.GetStrokeCost(brush, subset.m_VertLength, fBrushSize);
    AdjustMeter(bAddToMeter ? fCost : -fCost);
  }

  private void AdjustMeter(GameObject rBrushStroke, float fBrushSize, bool bAddToMeter) {
    // Make brush size not matter if we're ignoring it
    if (!m_BrushSizeAffectsCost) {
      fBrushSize = 1.0f;
    }

    // If this brush stroke had a valid cost, adjust the meter
    BaseBrushScript rBrush = rBrushStroke.GetComponent<BaseBrushScript>();
    if (rBrush != null) {
      float fCost = BaseBrushScript.GetStrokeCost(
          rBrush.Descriptor, rBrush.GetNumUsedVerts(), fBrushSize);
      if (fCost > 0.0f) {
        AdjustMeter(bAddToMeter ? fCost : -fCost);
      }
    }
  }

  void AdjustMeter(float fAmount) {
    //adjust meter amount and potentially adjust meter index
    m_MeterAmount += fAmount;

    //check upper bounds
    if (m_MeterAmount >= m_MeterAmountFull) {
      int iWrapCount = (int)(m_MeterAmount / m_MeterAmountFull);
      m_MeterIndex += iWrapCount;
      m_MeterAmount -= (iWrapCount * m_MeterAmountFull);
    } //check lower bounds
    else if (m_MeterAmount < 0.0f) {
      int iWrapCount = (int)(Mathf.Abs(m_MeterAmount) / m_MeterAmountFull) + 1;
      m_MeterIndex -= iWrapCount;
      m_MeterAmount += (iWrapCount * m_MeterAmountFull);
    }

    //sanity checks
    if (m_MeterIndex < 0) {
      m_MeterIndex = 0;
      m_MeterAmount = 0.0f;
    }
  }
}
}  // namespace TiltBrush
