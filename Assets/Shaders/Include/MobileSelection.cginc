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

// Used to provide the selection pattern used to show selected strokes on mobile.

float4 _LeftEyeSelectionColor;
float4 _RightEyeSelectionColor;
float4x4 _InverseLimitedScaleSceneMatrix;
float _PatternSpeed;
float4 _BrushColor;

// Gets the appropriate selection color depending on the eye being rendered.
float4 GetSelectionColor() {
  return unity_StereoEyeIndex * _RightEyeSelectionColor +
                 (1 - unity_StereoEyeIndex) * _LeftEyeSelectionColor;
}

// Given the current material color, override with selection noise if necessary.
float4 AddSelectColor(float4 inColor) {
#if SELECTION_ON
  float4 color = GetSelectionColor();
#else
  float4 color = _BrushColor;
#endif
  return inColor * 0.5 + color * 0.5;
}

#if SELECTION_ON || HIGHLIGHT_ON
    // Macro to put in fragment shader functions
    #define FRAG_MOBILESELECT(color) \
      color = AddSelectColor(color); // NOTOOLKIT
    #define SURF_FRAG_MOBILESELECT(output) \
      output.Emission += AddSelectColor(0);
#else
    #define FRAG_MOBILESELECT(color) // NOTOOLKIT
    #define SURF_FRAG_MOBILESELECT(output) // NOTOOLKIT
#endif



