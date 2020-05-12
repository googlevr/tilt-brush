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

[AddComponentMenu("Reaktion/Utility/Self Destruction")]
public class SelfDestruction : MonoBehaviour
{
    public enum ConditionType { Distance, Bounds, Time, ParticleSystem }
    public enum ReferenceType { Origin, Point, InitialPosition, GameObject, GameObjectName }

    public ConditionType conditionType = ConditionType.Distance;
    public ReferenceType referenceType = ReferenceType.InitialPosition;

    public float maxDistance = 10;
    public Bounds bounds = new Bounds(Vector3.zero, new Vector3(10, 10, 10));
    public float lifetime = 5;

    public Vector3 referencePoint;
    public GameObject referenceObject;
    public string referenceName;

    float timer;
    Vector3 initialPoint;
    GameObject referenceObjectCache; // used to dereference referenceName

    Vector3 GetReferencePoint()
    {
        bool runtime = Application.isPlaying;

        if (referenceType == ReferenceType.Point)
            return referencePoint;

        if (referenceType == ReferenceType.InitialPosition)
            return runtime ? initialPoint : transform.position;

        if (referenceType == ReferenceType.GameObject)
            if (referenceObject != null)
                return referenceObject.transform.position;

        if (referenceType == ReferenceType.GameObjectName)
        {
            if (!runtime || referenceObjectCache == null)
                referenceObjectCache = GameObject.Find(referenceName);
            if (referenceObjectCache != null)
                return referenceObjectCache.transform.position;
        }

        // Default reference point
        return Vector3.zero;
    }

    bool IsAlive()
    {
        if (conditionType == ConditionType.Distance)
            return Vector3.Distance(transform.position, GetReferencePoint()) <= maxDistance;

        if (conditionType == ConditionType.Bounds)
            return bounds.Contains(transform.position - GetReferencePoint());

        if (conditionType == ConditionType.Time)
            return timer < lifetime;

        // conditionType == ConditionType.ParticleSystem:
        return GetComponent<ParticleSystem>() != null && GetComponent<ParticleSystem>().IsAlive();
    }

    void Start()
    {
        initialPoint = transform.position;
    }

    void Update()
    {
        timer += Time.deltaTime;
    }

    void LateUpdate() 
    {
        if (!IsAlive()) Object.Destroy(gameObject);   
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        if (conditionType == ConditionType.Distance)
            Gizmos.DrawWireSphere(GetReferencePoint(), maxDistance);

        if (conditionType == ConditionType.Bounds)
            Gizmos.DrawWireCube(GetReferencePoint() + bounds.center, bounds.size);
    }
}

} // namespace Reaktion
