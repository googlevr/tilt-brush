//
// KinoVignette - Natural vignetting effect
//
// Copyright (C) 2015 Keijiro Takahashi
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

// Modified by the Tilt Brush Authors.

using UnityEngine;

namespace Kino
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Kino Image Effects/Vignette")]
    public class Vignette : MonoBehaviour
    {
        #region Public Properties

        // Natural vignetting falloff
        [SerializeField, Range(0.0f, 1.0f)]
        float _falloff = 0.5f;

        [SerializeField] private float m_ChromaticAberration;
        [SerializeField] private Material m_material;

        public float intensity {
            get { return _falloff; }
            set { _falloff = value; }
        }
        
        #endregion

        #region MonoBehaviour Functions

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var cam = GetComponent<Camera>();
            m_material.SetVector("_Aspect", new Vector2(cam.aspect, 1));
            m_material.SetFloat("_Falloff", _falloff);
            m_material.SetFloat("_ChromaticAberration", m_ChromaticAberration);

            Graphics.Blit(source, destination, m_material, 0);
        }

        #endregion
    }
}
