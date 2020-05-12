//======= Copyright (c) Valve Corporation, All rights reserved. ===============
//
// Purpose: Used to render an external camera of vr player (split front/back).
//
//=============================================================================

using UnityEngine;
using System.Collections;

namespace Valve.VR
{
    public class SteamVR_ExternalCamera_LegacyManager
    {
        public static bool hasCamera { get { return cameraIndex != -1; } }

        public static int cameraIndex = -1;

        private static SteamVR_Events.Action newPosesAction = null;

        public static void SubscribeToNewPoses()
        {
            if (newPosesAction == null)
                newPosesAction = SteamVR_Events.NewPosesAction(OnNewPoses);

            newPosesAction.enabled = true;
        }

        private static void OnNewPoses(TrackedDevicePose_t[] poses)
        {
            if (cameraIndex != -1)
                return;

            int controllercount = 0;
            for (int index = 0; index < poses.Length; index++)
            {
                if (poses[index].bDeviceIsConnected)
                {
                    ETrackedDeviceClass deviceClass = OpenVR.System.GetTrackedDeviceClass((uint)index);
                    if (deviceClass == ETrackedDeviceClass.Controller || deviceClass == ETrackedDeviceClass.GenericTracker)
                    {
                        controllercount++;
                        if (controllercount >= 3)
                        {
                            cameraIndex = index;
                            break;
                        }
                    }
                }
            }
        }
    }
}