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

namespace TiltBrush {

/// Promo: How to adjust the free paint tool size.
/// Completion: User changes the brush size by more than a minimum delta.
/// Conditions: User has drawn for a certain distance. Free paint tool is active, user is not
///             grabbing, painting, or pointing at a panel.
public class BrushSizePromo : BasePromo {
  private Vector3 m_BrushSizeHintLastPos;
  private bool m_BrushSizeHintLastPosSet;
  private float m_BrushSizeHintMovementAmount;
  private float m_BrushSizeHintSwipeTotal;
  private bool m_PreventHint;
  private bool m_PlayedAudioCue;

  public BrushSizePromo() : base(PromoType.BrushSize) {
    m_HintObject = InputManager.Brush.Geometry.BrushSizeHintObject;
    m_BrushSizeHintLastPosSet = false;
  }

  public override string PrefsKey { get { return PromoManager.kPromoPrefix + "BrushSize"; } }

  protected override void OnDisplay() {
    if (m_PreventHint) {
      PromoManager.m_Instance.RecordCompletion(this.PromoType);
      m_Request = RequestingState.ToHide;
    } else {
      m_BrushSizeHintSwipeTotal = 0.0f;
      // Audio cue only plays once. Doesn't play again if the promo is hidden and displayed again
      // (like if user hovers over UI)
      if(!m_PlayedAudioCue) {
        AudioManager.m_Instance.PlayHintAnimateSound(m_HintObject.transform.position);
        m_PlayedAudioCue = true;
      }
    }
  }

  public override void OnIdle() {
    if (m_BrushSizeHintMovementAmount <
        PromoManager.m_Instance.BrushSizeHintShowDistance) {
      // Monitor brush size usage and if the user does it enough, they know what they're doing.
      m_BrushSizeHintSwipeTotal +=
        Mathf.Abs(InputManager.m_Instance.GetAdjustedBrushScrollAmount());
      if (Mathf.Abs(m_BrushSizeHintSwipeTotal) >
          PromoManager.m_Instance.BrushSizeHintPreventSwipeAmount) {
        // TODO: Clean up this logic for M16
        m_PreventHint = true;
        m_Request = RequestingState.ToDisplay;
      } else {
        // Total up the distance the user has painted.
        if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate)) {
          // Lazy init the last pos.
          Vector3 brushSizeHintPos = InputManager.Brush.Transform.position;
          if (!m_BrushSizeHintLastPosSet) {
            m_BrushSizeHintLastPosSet = true;
          } else {
            m_BrushSizeHintMovementAmount += Vector3.Distance(m_BrushSizeHintLastPos,
              brushSizeHintPos);
          }
          m_BrushSizeHintLastPos = brushSizeHintPos;
        } else {
          // Reset last pos if we're not painting.
          m_BrushSizeHintLastPosSet = false;
          // Show the hint if we're not painting and we've drawn enough.
          if (m_BrushSizeHintMovementAmount >
              PromoManager.m_Instance.BrushSizeHintShowDistance) {
            m_Request = RequestingState.ToDisplay;
          }
        }
      }
    } else {
      if (App.Instance.IsInStateThatAllowsPainting() &&
        SketchSurfacePanel.m_Instance.IsDefaultToolEnabled() &&
        !PromoManager.m_Instance.ShouldPausePromos) {
        m_Request = RequestingState.ToDisplay;
      }
    }
  }

  public override void OnActive() {
    if (App.Instance.IsInStateThatAllowsPainting() &&
        SketchSurfacePanel.m_Instance.IsDefaultToolEnabled() &&
        !PromoManager.m_Instance.ShouldPausePromos) {
      // Wait until the user proves they know how to use the brush sizer.
      m_BrushSizeHintSwipeTotal += InputManager.m_Instance.GetAdjustedBrushScrollAmount();
      if (Mathf.Abs(m_BrushSizeHintSwipeTotal) >
          PromoManager.m_Instance.BrushSizeHintCancelSwipeAmount) {
        PromoManager.m_Instance.RecordCompletion(this.PromoType);
        m_Request = RequestingState.ToHide;
      }
    } else {
      m_Request = RequestingState.ToHide;
    }
  }
}
} // namespace TiltBrush
