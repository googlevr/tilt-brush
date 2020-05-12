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

namespace TiltBrush {

class MenuPopUpWindow : PopUpWindow {
  public override void SetPopupCommandParameters(int iCommandParam, int iCommandParam2) {
    // TODO : Fix this hangnail.
    OptionButton[] optionButtons = GetComponentsInChildren<OptionButton>();
    foreach (OptionButton button in optionButtons) {
      button.SetCommandParameters(iCommandParam, iCommandParam2);
    }
  }
}

}  // namespace TiltBrush
