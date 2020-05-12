//
// Reaktion - An audio reactive animation toolkit for Unity.
//
// Copyright (C) 2013, 2014 Keijiro Takahashi
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
using UnityEngine;
using System.Collections;

// An implementation of the state variable filter (SVF)
//
// Originally designed by H. Chamberlin and improved by P. Dutilleux.
// For further details, see the paper by B. Frei.
//
// http://courses.cs.washington.edu/courses/cse490s/11au/Readings/Digital_Sound_Generation_2.pdf

namespace Reaktion {

[AddComponentMenu("Reaktion/Utility/Band Pass Filter")]
public class BandPassFilter : MonoBehaviour
{
    [Range(0.0f, 1.0f)]
    public float cutoff = 0.5f;
    
    [Range(1.0f, 10.0f)]
    public float q = 1.0f;

    // DSP variables
    float vF;
    float vD;
    float vZ1;
    float vZ2;
    float vZ3;

    // Cutoff frequency in Hz
    public float CutoffFrequency {
        get { return Mathf.Pow(2, 10 * cutoff - 10) * 15000; }
    }

    void Awake()
    {
        Update();
    }
    
    void Update()
    {
        var f = 2 / 1.85f * Mathf.Sin(Mathf.PI * CutoffFrequency / AudioSettings.outputSampleRate);
        vD = 1 / q;
        vF = (1.85f - 0.75f * vD * f) * f;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        for (var i = 0; i < data.Length; i += channels)
        {
            var si = data[i];
            
            var _vZ1 = 0.5f * si;
            var _vZ3 = vZ2 * vF + vZ3;
            var _vZ2 = (_vZ1 + vZ1 - _vZ3 - vZ2 * vD) * vF + vZ2;
            
            for (var c = 0; c < channels; c++)
                data[i + c] = _vZ2;

            vZ1 = _vZ1;
            vZ2 = _vZ2;
            vZ3 = _vZ3;
        }
    }
}

} // namespace Reaktion
