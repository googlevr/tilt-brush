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

#pragma warning disable 618

namespace Reaktion {

[AddComponentMenu("Reaktion/Gear/Particle System Gear")]
public class ParticleSystemGear : MonoBehaviour
{
    public ReaktorLink reaktor;

    public Trigger burst;
    public int burstNumber = 10;

    public Modifier emissionRate = Modifier.Linear(0, 20);

    public Modifier size = Modifier.Linear(0.5f, 1.5f);

    ParticleSystem.Particle[] tempArray;
    
    void Awake()
    {
        reaktor.Initialize(this);
    }

    void Update()
    {
        if (burst.Update(reaktor.Output))
        {
            GetComponent<ParticleSystem>().Emit(burstNumber);
            GetComponent<ParticleSystem>().Play();
        }

        if (emissionRate.enabled)
            GetComponent<ParticleSystem>().emissionRate = emissionRate.Evaluate(reaktor.Output);

        if (size.enabled)
            ResizeParticles(size.Evaluate(reaktor.Output));
    }

    void ResizeParticles(float newSize)
    {
        if (tempArray == null || tempArray.Length != GetComponent<ParticleSystem>().maxParticles)
            tempArray = new ParticleSystem.Particle[GetComponent<ParticleSystem>().maxParticles];

        var count = GetComponent<ParticleSystem>().GetParticles(tempArray);

        for (var i = 0; i < count; i++)
            tempArray[i].size = newSize;

        GetComponent<ParticleSystem>().SetParticles(tempArray, count);
    }
}

} // namespace Reaktion
