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
public class DownloadPopupWindow : PopUpWindow {
  private int m_SketchIndex;
  [SerializeField] private Renderer m_ProgressBar;

  private GoogleDriveSketchSet.GoogleDriveFileInfo m_SceneFileInfo;
  private TaskAndCts m_DownloadTask;

  override public void SetPopupCommandParameters(int commandParam, int commandParam2) {
    if (commandParam2 != (int) SketchSetType.Drive) {
      return;
    }
    m_SketchIndex = commandParam;
    var sketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.Drive) as GoogleDriveSketchSet;
    m_SceneFileInfo =
        sketchSet.GetSketchSceneFileInfo(commandParam) as GoogleDriveSketchSet.GoogleDriveFileInfo;

    if (m_SceneFileInfo.Available) {
      return;
    }
    m_ProgressBar.material.SetFloat("_Ratio", 0);

    m_DownloadTask = new TaskAndCts();
    m_DownloadTask.Task = m_SceneFileInfo.DownloadAsync(m_DownloadTask.Token);
  }

  protected override void UpdateVisuals() {
    base.UpdateVisuals();
    if (m_SceneFileInfo != null) {
      m_ProgressBar.material.SetFloat("_Ratio", m_SceneFileInfo.Progress);
    }
  }

  protected override void BaseUpdate() {
    base.BaseUpdate();
    if (m_SceneFileInfo?.Available ?? false) {
      if (m_ParentPanel) {
        m_ParentPanel.ResolveDelayedButtonCommand(true);
      }
    }
  }

  public override bool RequestClose(bool bForceClose = false) {
    bool close = base.RequestClose(bForceClose);
    if (close) {
      m_DownloadTask.Cts.Cancel();
    }
    return close;
  }
}
} // namespace TiltBrush
