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

namespace Reaktion {

public class TurbulentMotion : MonoBehaviour
{
    // Noise parameters.
    public float density      = 0.1f;
    public Vector3 linearFlow = Vector3.up * 0.2f;

    // Amplitude and coefficient (wave number).
    public Vector3 displacement = Vector3.one;
    public Vector3 rotation     = Vector3.one * 60.0f;
    public Vector3 scale        = Vector3.zero;
    public float coeffDisplacement = 1.0f;
    public float coeffRotation     = 1.1f;
    public float coeffScale        = 1.2f;

    // Misc settings.
    public bool useLocalCoordinate  = true;

    // Initial states.
    Vector3    initialPosition;
    Quaternion initialRotation;
    Vector3    initialScale;

    void OnEnable()
    {
        // Store the initial states when it's enabled.
        if (useLocalCoordinate)
        {
            initialPosition = transform.localPosition;
            initialRotation = transform.localRotation;
        }
        else
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }
        initialScale = transform.localScale;

        // Apply the initial state.
        ApplyTransform();
    }

    void Update()
    {
        ApplyTransform();
    }

    void ApplyTransform()
    {
        // Noise position.
        var np = initialPosition * density + linearFlow * Time.time;

        // Offset for the noise position.
        var offs = new Vector3(13, 17, 19);

        // Displacement.
        if (displacement != Vector3.zero)
        {
            // Noise position for the displacement.
            var npd = np * coeffDisplacement;

            // Get noise values.
            var vd = new Vector3(
                displacement.x == 0.0f ? 0.0f : displacement.x * Perlin.Noise(npd),
                displacement.y == 0.0f ? 0.0f : displacement.y * Perlin.Noise(npd + offs),
                displacement.z == 0.0f ? 0.0f : displacement.z * Perlin.Noise(npd + offs * 2)
            );

            // Apply the displacement.
            if (useLocalCoordinate)
                transform.localPosition = initialPosition + vd;
            else
                transform.position = initialPosition + vd;
        }

        // Rotation.
        if (rotation != Vector3.zero)
        {
            // Noise position for the rotation.
            var npr = np * coeffRotation;

            // Get noise values.
            var vr = new Vector3(
                rotation.x == 0.0f ? 0.0f : rotation.x * Perlin.Noise(npr + offs * 3),
                rotation.y == 0.0f ? 0.0f : rotation.y * Perlin.Noise(npr + offs * 4),
                rotation.z == 0.0f ? 0.0f : rotation.z * Perlin.Noise(npr + offs * 5)
            );

            // Apply the rotation.
            if (useLocalCoordinate)
                transform.localRotation = Quaternion.Euler(vr) * initialRotation;
            else
                transform.rotation = Quaternion.Euler(vr) * initialRotation;
        }

        // Scale.
        if (scale != Vector3.zero)
        {
            // Noise position for the scale.
            var nps = np * coeffScale;

            // Get noise values.
            var vs = new Vector3(
                scale.x == 0.0f ? 1.0f : Mathf.Lerp(1.0f, (Perlin.Noise(nps + offs * 6) * 1.25f + 1) * 0.5f, scale.x),
                scale.y == 0.0f ? 1.0f : Mathf.Lerp(1.0f, (Perlin.Noise(nps + offs * 7) * 1.25f + 1) * 0.5f, scale.y),
                scale.z == 0.0f ? 1.0f : Mathf.Lerp(1.0f, (Perlin.Noise(nps + offs * 8) * 1.25f + 1) * 0.5f, scale.z)
            );

            // Apply the scale.
            transform.localScale = Vector3.Scale(initialScale, vs);
        }
    }
}

} // namespace Reaktion
