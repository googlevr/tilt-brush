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

/// Tilt Brush-specific abstraction over inputs that are commonly
/// provided by VR controllers. Mostly used with ControllerInfo.GetVrInput()
public enum VrInput {
  Button01,           // Pad_Left on Vive, X or A buttons on Rift.
  Button02,           // Pad_Right on Vive, Y or B buttons on Rift.
  Button03,           // Menu Button on Vive, Y or B on Rift
  Button04,           // Full-pad click on Vive, X or A on the Rift.

  // --------------------------------------------------------- //
  // Vive up/down quads, only used in experimental
  // --------------------------------------------------------- //
  Button05,           // Quad_Up on vive, Y and B on Rift
  Button06,           // Quad_Down on vive, X and A on Rift
  // --------------------------------------------------------- //

  Directional,        // Thumbstick if one exists; otherwise touchpad.
  Trigger,            // Trigger Button on Vive, Primary Trigger Button on Rift.
  Grip,               // Grip Button on Vive, Secondary Trigger on Rift.
  Any,
  Thumbstick,         // TODO: standardize spelling: ThumbStick or Thumbstick?
  Touchpad
}
} // namespace TiltBrush
