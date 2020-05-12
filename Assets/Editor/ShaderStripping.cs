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

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiltBrush {

// Simple example of stripping of a debug build configuration
class ShaderStripping : IPreprocessShaders {
  readonly ShaderKeyword kLIGHTMAP_ON            = new ShaderKeyword("LIGHTMAP_ON");
  readonly ShaderKeyword kDIRLIGHTMAP_COMBINED   = new ShaderKeyword("DIRLIGHTMAP_COMBINED");
  readonly ShaderKeyword kDYNAMICLIGHTMAP_ON     = new ShaderKeyword("DYNAMICLIGHTMAP_ON");
  readonly ShaderKeyword kLIGHTMAP_SHADOW_MIXING = new ShaderKeyword("LIGHTMAP_SHADOW_MIXING");
  readonly ShaderKeyword kSHADOWS_SHADOWMASK     = new ShaderKeyword("SHADOWS_SHADOWMASK");

  readonly ShaderKeyword kFOG_LINEAR             = new ShaderKeyword("FOG_LINEAR");
  readonly ShaderKeyword kFOG_EXP                = new ShaderKeyword("FOG_EXP");
  readonly ShaderKeyword kFOG_EXP2               = new ShaderKeyword("FOG_EXP2");

  readonly ShaderKeyword kDIRECTIONAL            = new ShaderKeyword("DIRECTIONAL");
  readonly ShaderKeyword kPOINT                  = new ShaderKeyword("POINT");
  readonly ShaderKeyword kSPOT                   = new ShaderKeyword("SPOT");
  readonly ShaderKeyword kPOINT_COOKIE           = new ShaderKeyword("POINT_COOKIE");
  readonly ShaderKeyword kDIRECTIONAL_COOKIE     = new ShaderKeyword("DIRECTIONAL_COOKIE");

  readonly ShaderKeyword kSHADOWS_CUBE           = new ShaderKeyword("SHADOWS_CUBE");

  readonly ShaderKeyword kODS_RENDER             = new ShaderKeyword("ODS_RENDER");
  readonly ShaderKeyword kODS_RENDER_CM          = new ShaderKeyword("ODS_RENDER_CM");

  readonly ShaderKeyword kHDR_EMULATED           = new ShaderKeyword("HDR_EMULATED");
  readonly ShaderKeyword kHDR_SIMPLE             = new ShaderKeyword("HDR_SIMPLE");

  readonly ShaderKeyword kSELECTION_ON		       = new ShaderKeyword("SELECTION_ON");
  readonly ShaderKeyword kEDGING_ON		           = new ShaderKeyword("EDGING_ON");
  readonly ShaderKeyword kHIGHLIGHT_ON		       = new ShaderKeyword("HIGHLIGHT_ON");

  // IOrderedCallback API
  public int callbackOrder => 0;

  // Returns true if we should keep this shader.
  private bool ShouldKeepShader(ShaderCompilerData scd) {
    ShaderKeywordSet keywordSet = scd.shaderKeywordSet;
    // Tilt Brush doesn't use any baked lighting
    if (keywordSet.IsEnabled(kLIGHTMAP_ON) ||
        keywordSet.IsEnabled(kDIRLIGHTMAP_COMBINED) ||
        keywordSet.IsEnabled(kDYNAMICLIGHTMAP_ON) ||
        keywordSet.IsEnabled(kLIGHTMAP_SHADOW_MIXING)) {
      return false;
    }

    // Tilt Brush uses only use exponential fog
    if (keywordSet.IsEnabled(kFOG_LINEAR) ||
        keywordSet.IsEnabled(kFOG_EXP2)) {
      return false;
    }

    // Tilt Brush uses only use directional lights
    if (keywordSet.IsEnabled(kPOINT) ||
        keywordSet.IsEnabled(kSPOT) ||
        keywordSet.IsEnabled(kPOINT_COOKIE) ||
        keywordSet.IsEnabled(kDIRECTIONAL_COOKIE)) {
      return false;
    }

    // TODO: what about
    // HDR_EMULATED HDR_SIMPLE
    //   Some are Mobile- or Desktop-specific
    //
    // SELECTION_ON, EDGING_ON, HIGHLIGHT_ON
    //   Some are Mobile- or Desktop-specific

    if (scd.platformKeywordSet.IsEnabled(BuiltinShaderDefine.SHADER_API_MOBILE)) {
      // Mobile-only filters

      if (keywordSet.IsEnabled(kODS_RENDER) ||
          keywordSet.IsEnabled(kODS_RENDER_CM)) {
        return false;
      }
    } else {
      // Desktop-only filters

      // None defined yet
    }

    return true;
  }

  // IList<> doesn't have AddRange :-P
  private static void Assign<T>(IList<T> dst, List<T> src) {
    dst.Clear();
    foreach (var elt in src) {
      dst.Add(elt);
    }
  }

  // IPreprocessShaders API
  public void OnProcessShader(
      Shader shader,
      ShaderSnippetData snippet,
      IList<ShaderCompilerData> datas) {
    List<ShaderCompilerData> filtered = datas.Where(ShouldKeepShader).ToList();
    if (filtered.Count != datas.Count) {
      // Debug.Log($"Filter shaders {shader}: {datas.Count} -> {filtered.Count}");
      Assign(datas, filtered);
    }
  }
}

}
