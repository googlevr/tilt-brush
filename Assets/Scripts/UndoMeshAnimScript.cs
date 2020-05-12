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

public class UndoMeshAnimScript : UndoBaseAnimScript {
  private Vector3 m_vOriginalPos_CS;

  void Awake() {
    OnAwake();
  }

  public void Init() {
    InitForHiding();
    m_vOriginalPos_CS = Coords.AsCanvas[transform].translation;
  }

  override protected void AnimateHiding() {
    Vector3 vTargetPos_CS = GetAnimationTarget_CS();

    var xf_CS = Coords.AsCanvas[transform];
    xf_CS.translation = Vector3.Lerp(m_vOriginalPos_CS, vTargetPos_CS, m_HiddenAmount);
    xf_CS.scale = Mathf.Lerp(1.0f, 0.0f, m_HiddenAmount);
    Coords.AsCanvas[transform] = xf_CS;
  }
}
}  // namespace TiltBrush
