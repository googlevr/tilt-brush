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

[AddComponentMenu("Reaktion/Utility/Object Particle System")]
[RequireComponent(typeof(ParticleSystem))]
public class ObjectParticleSystem : MonoBehaviour
{
    public GameObject prefab;
    public int maxParticles = 100;

    ParticleSystem.Particle[] particles;
    GameObject[] pool;

    void Start()
    {
        var count = Mathf.Min(maxParticles, GetComponent<ParticleSystem>().maxParticles);

        particles = new ParticleSystem.Particle[count];
        pool = new GameObject[count];

        for (var i = 0; i < count; i++)
            pool[i] = Instantiate(prefab) as GameObject;
    }

    void LateUpdate()
    {
        var count = GetComponent<ParticleSystem>().GetParticles(particles);

        for (var i = 0; i < count; i++)
        {
            var p = particles [i];
            var o = pool[i];

            o.GetComponent<Renderer>().enabled = true;

            o.transform.position = prefab.transform.position + p.position;
            o.transform.localRotation = Quaternion.AngleAxis(p.rotation, p.axisOfRotation) * prefab.transform.rotation;
            o.transform.localScale = prefab.transform.localScale * p.size;
        }

        for (var i = count; i < pool.Length; i++)
            pool[i].GetComponent<Renderer>().enabled = false;
    }
}

} // namespace Reaktion
