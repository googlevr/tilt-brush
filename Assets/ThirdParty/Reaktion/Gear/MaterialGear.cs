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

[AddComponentMenu("Reaktion/Gear/Material Gear")]
public class MaterialGear : MonoBehaviour
{
    public enum TargetType { Color, Float, Vector, Texture }

    public ReaktorLink reaktor;

    public int materialIndex;

    public string targetName = "_Color";
    public TargetType targetType = TargetType.Color;

    public float threshold = 0.5f;

    public Gradient colorGradient;
    public float colorGradientMultiplier;

    public AnimationCurve floatCurve = AnimationCurve.Linear(0, 0, 1, 1);

    public Vector4 vectorFrom = Vector4.zero;
    public Vector4 vectorTo = Vector4.one;

    public Texture textureLow;
    public Texture textureHigh;

    Material material;

    void Awake()
    {
        reaktor.Initialize(this);

        if (materialIndex == 0)
            material = GetComponent<Renderer>().material;
        else
            material = GetComponent<Renderer>().materials[materialIndex];

        UpdateMaterial(0);
    }

    void Update()
    {
        UpdateMaterial(reaktor.Output);
    }

    void UpdateMaterial(float param)
    {
        switch (targetType)
        {
        case TargetType.Color:
            material.SetColor(targetName, colorGradient.Evaluate(param) * colorGradientMultiplier);
            break;
        case TargetType.Float:
            material.SetFloat(targetName, floatCurve.Evaluate(param));
            break;
        case TargetType.Vector:
            material.SetVector(targetName, Vector4.Lerp(vectorFrom, vectorTo, param));
            break;
        case TargetType.Texture:
            material.SetTexture(targetName, param < threshold ? textureLow : textureHigh);
            break;
        }
    }
}

} // namespace Reaktion
