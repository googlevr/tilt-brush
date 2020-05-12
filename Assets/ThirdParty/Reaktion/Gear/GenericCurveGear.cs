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
using UnityEngine.Events;
using System.Collections;

namespace Reaktion {

[AddComponentMenu("Reaktion/Gear/Generic Curve Gear")]
public class GenericCurveGear : MonoBehaviour
{
    public enum OptionType { Bool, Int, Float, Vector }

    [System.Serializable] public class BoolEvent : UnityEvent<bool> {}
    [System.Serializable] public class IntEvent : UnityEvent<int> {}
    [System.Serializable] public class FloatEvent : UnityEvent<float> {}
    [System.Serializable] public class VectorEvent : UnityEvent<Vector3> {}

    public ReaktorLink reaktor;

    public OptionType optionType = OptionType.Float;
    public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);
    public Vector3 origin;
    public Vector3 direction = Vector3.right;

    public BoolEvent boolTarget;
    public IntEvent intTarget;
    public FloatEvent floatTarget;
    public VectorEvent vectorTarget;

    void Awake()
    {
        reaktor.Initialize(this);
    }

    void Update()
    {
        var o = curve.Evaluate(reaktor.Output);
        if (optionType == OptionType.Bool)
        {
            boolTarget.Invoke(0.5f <= o);
        }
        else if (optionType == OptionType.Int)
        {
            intTarget.Invoke((int)o);
        }
        else if (optionType == OptionType.Vector)
        {
            vectorTarget.Invoke(origin + direction * o);
        }
        else
        {
            floatTarget.Invoke(o);
        }
    }
}

} // namespace Reaktion
