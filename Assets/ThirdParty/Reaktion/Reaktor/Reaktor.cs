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

namespace Reaktion {

[AddComponentMenu("Reaktion/Reaktor/Reaktor")]
public class Reaktor : MonoBehaviour
{
    // Audio input settings.
    public InjectorLink injector;
    public AnimationCurve audioCurve = AnimationCurve.Linear(0, 0, 1, 1);

    // Remote control settings.
    public Remote gain;
    public Remote offset;

    // General options.
    public float sensitivity = 0.95f;
    public float decaySpeed = 0.5f;

    // Audio input options.
    public bool showAudioOptions = false;
    public float headroom = 1.0f;
    public float dynamicRange = 17.0f;
    public float lowerBound = -60.0f;
    public float falldown = 0.5f;

    // Output properties.
    public float Output   { get { return output; } }
    public float Peak     { get { return peak; } }
    public float RawInput { get { return rawInput; } }
    public float Gain     { get { return gain.level; } }
    public float Offset   { get { return offset.level; } }

    // Editor properties.
    public float Override {
        get { return Mathf.Clamp01(fakeInput); }
        set { fakeInput = value; }
    }

    public bool IsOverridden {
        get { return fakeInput >= 0.0f; }
    }

    public bool Bang {
        get { return fakeInput > 1.0f; }
        set { fakeInput = value ? 10.0f : -1.0f; }
    }

    public static int ActiveInstanceCount {
        get { return activeInstanceCount; }
    }

    // Internal state variables.
    [HideInInspector]
    public float output;
    [HideInInspector]
    public float outputAccumulated = 0.0f;
    [HideInInspector]
    public float outputDb = 0.0f;
    float peak;
    float rawInput;
    float fakeInput = -1.0f;

    // Instance counter.
    static int activeInstanceCount;

    void Start()
    {
        injector.Initialize(this);
        gain.Reset(1);
        offset.Reset(0);

        // Begins with the lowest level.
        peak = lowerBound + dynamicRange + headroom;
        rawInput = -1e12f;
    }

    void Update()
    {
        float input = 0.0f;

        // Audio input.
        rawInput = injector.DbLevel;

        // Check the peak level.
        peak -= Time.deltaTime * falldown;
        peak = Mathf.Max(peak, Mathf.Max(rawInput, lowerBound + dynamicRange + headroom));
        
        // Normalize the input level.
        input = (rawInput - peak + headroom + dynamicRange) / dynamicRange;
        input = audioCurve.Evaluate(Mathf.Clamp01(input));

        // Remote controls.
        gain.Update();
        offset.Update();

        input *= gain.level;
        input += offset.level;

        // Make output.
        input = Mathf.Clamp01(fakeInput < 0.0f ? input : fakeInput);

        if (sensitivity < 1.0f)
        {
            var coeff = Mathf.Pow(sensitivity, 2.3f) * -128;
            input -= (input - output) * Mathf.Exp(coeff * Time.deltaTime);
        }

        var speed = decaySpeed < 1.0f ? decaySpeed * 10 + 0.5f : 100.0f;
        output = Mathf.Max(input, output - Time.deltaTime * speed);
        outputAccumulated += output;
        outputDb = injector.DbLevel;
    }

    void OnEnable()
    {
        activeInstanceCount++;
    }

    void OnDisable()
    {
        activeInstanceCount--;
    }

    // Stop overriding.
    public void StopOverride()
    {
        fakeInput = -1.0f;
    }
}

} // namespace Reaktion
