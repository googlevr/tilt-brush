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
public class DeleteStrokeCommand : BaseCommand {
  private Stroke m_TargetStroke;
  private bool m_SilenceFirstAudio;

  private Vector3 CommandAudioPosition {
    get { return GetPositionForCommand(m_TargetStroke); }
  }

  public DeleteStrokeCommand(Stroke stroke, BaseCommand parent = null)
      : base(parent) {
    m_TargetStroke = stroke;
    m_SilenceFirstAudio = true;
  }

  public override bool NeedsSave { get { return true; } }

  protected override void OnRedo() {
    if (!m_SilenceFirstAudio) {
      AudioManager.m_Instance.PlayUndoSound(CommandAudioPosition);
    }
    m_SilenceFirstAudio = false;

    switch (m_TargetStroke.m_Type) {
    case Stroke.Type.BrushStroke:
      BaseBrushScript rBrushScript =
          m_TargetStroke.m_Object.GetComponent<BaseBrushScript>();
      if (rBrushScript) {
        rBrushScript.HideBrush(true);
      }
      break;
    case Stroke.Type.BatchedBrushStroke:
      var batch = m_TargetStroke.m_BatchSubset.m_ParentBatch;
      batch.DisableSubset(m_TargetStroke.m_BatchSubset);
      break;
    case Stroke.Type.NotCreated:
      Debug.LogError("Unexpected: redo delete NotCreated stroke");
      break;
    }

    TiltMeterScript.m_Instance.AdjustMeter(m_TargetStroke, up: false);
  }

  protected override void OnUndo() {
    if (!m_SilenceFirstAudio) {
      AudioManager.m_Instance.PlayRedoSound(CommandAudioPosition);
    }
    m_SilenceFirstAudio = false;

    switch (m_TargetStroke.m_Type) {
    case Stroke.Type.BrushStroke:
      BaseBrushScript rBrushScript = m_TargetStroke.m_Object.GetComponent<BaseBrushScript>();
      if (rBrushScript) {
        rBrushScript.HideBrush(false);
      }
      break;
    case Stroke.Type.BatchedBrushStroke:
      m_TargetStroke.m_BatchSubset.m_ParentBatch.EnableSubset(m_TargetStroke.m_BatchSubset);
      break;
    case Stroke.Type.NotCreated:
      Debug.LogError("Unexpected: undo delete NotCreated stroke");
      break;
    }

    TiltMeterScript.m_Instance.AdjustMeter(m_TargetStroke, up: true);
  }
}
} // namespace TiltBrush