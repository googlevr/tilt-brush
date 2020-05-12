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

public class SketchOriginTool : BaseTool {
  private Vector3 m_ParentPosition;

  override public void EnableTool(bool bEnable) {
    base.EnableTool(bEnable);

    if (m_Parent != null) {
      if (bEnable) {
        m_ParentPosition = m_Parent.position;
        m_Parent.position = SketchControlsScript.m_Instance.GetSketchOrigin();
      } else {
        SketchControlsScript.m_Instance.SetSketchOrigin(m_Parent.position);
        m_Parent.position = m_ParentPosition;
      }
    }
  }

  override public void UpdateTool() {
    base.UpdateTool();

    if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate)) {
      m_RequestExit = true;
    }
  }
}
}  // namespace TiltBrush
