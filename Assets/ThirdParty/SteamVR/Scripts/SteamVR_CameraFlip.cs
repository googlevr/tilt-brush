//======= Copyright (c) Valve Corporation, All rights reserved. ===============
//
// Purpose: Flips the camera output back to normal for D3D.
//
//=============================================================================

using UnityEngine;

namespace Valve.VR
{
    [ExecuteInEditMode]
    public class SteamVR_CameraFlip : MonoBehaviour
    {
        void Awake()
        {
            Debug.Log("<b>[SteamVR]</b> SteamVR_CameraFlip is deprecated in Unity 5.4 - REMOVING");
            DestroyImmediate(this);
        }
    }
}