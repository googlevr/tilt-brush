//======= Copyright (c) Valve Corporation, All rights reserved. ===============
using UnityEngine;
using System.Collections;

namespace Valve.VR.Extras
{
    public class SteamVR_GazeTracker : MonoBehaviour
    {
        public bool isInGaze = false;
        public event GazeEventHandler GazeOn;
        public event GazeEventHandler GazeOff;
        public float gazeInCutoff = 0.15f;
        public float gazeOutCutoff = 0.4f;

        // Contains a HMD tracked object that we can use to find the user's gaze
        protected Transform hmdTrackedObject = null;

        public virtual void OnGazeOn(GazeEventArgs gazeEventArgs)
        {
            if (GazeOn != null)
                GazeOn(this, gazeEventArgs);
        }

        public virtual void OnGazeOff(GazeEventArgs gazeEventArgs)
        {
            if (GazeOff != null)
                GazeOff(this, gazeEventArgs);
        }

        protected virtual void Update()
        {
            // If we haven't set up hmdTrackedObject find what the user is looking at
            if (hmdTrackedObject == null)
            {
                SteamVR_TrackedObject[] trackedObjects = FindObjectsOfType<SteamVR_TrackedObject>();
                foreach (SteamVR_TrackedObject tracked in trackedObjects)
                {
                    if (tracked.index == SteamVR_TrackedObject.EIndex.Hmd)
                    {
                        hmdTrackedObject = tracked.transform;
                        break;
                    }
                }
            }

            if (hmdTrackedObject)
            {
                Ray ray = new Ray(hmdTrackedObject.position, hmdTrackedObject.forward);
                Plane plane = new Plane(hmdTrackedObject.forward, transform.position);

                float enter = 0.0f;
                if (plane.Raycast(ray, out enter))
                {
                    Vector3 intersect = hmdTrackedObject.position + hmdTrackedObject.forward * enter;
                    float dist = Vector3.Distance(intersect, transform.position);
                    //Debug.Log("Gaze dist = " + dist);
                    if (dist < gazeInCutoff && !isInGaze)
                    {
                        isInGaze = true;
                        GazeEventArgs gazeEventArgs;
                        gazeEventArgs.distance = dist;
                        OnGazeOn(gazeEventArgs);
                    }
                    else if (dist >= gazeOutCutoff && isInGaze)
                    {
                        isInGaze = false;
                        GazeEventArgs gazeEventArgs;
                        gazeEventArgs.distance = dist;
                        OnGazeOff(gazeEventArgs);
                    }
                }

            }

        }
    }
    public struct GazeEventArgs
    {
        public float distance;
    }

    public delegate void GazeEventHandler(object sender, GazeEventArgs gazeEventArgs);
}