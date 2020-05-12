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

public class LocalModelButton : ModelButton {
  // ReSharper disable once NotAccessedField.Local
  // Buttons in the prefab still have a "..." menu, but it's currently unused
  [SerializeField] private BaseButton m_MenuButton;

  override protected void RequestModelPreloadInternal(string reason) {
    StartCoroutine(m_Model.LoadFullyCoroutine(reason));
  }

  /// We don't want the preload any more; try to tear it down.
  override protected void CancelRequestModelPreload() {
    // Unlike Poly's loads, these loads aren't queued and so can't be just plucked from the queue.
    // So just let it keep going.
  }
}
}  // namespace TiltBrush
