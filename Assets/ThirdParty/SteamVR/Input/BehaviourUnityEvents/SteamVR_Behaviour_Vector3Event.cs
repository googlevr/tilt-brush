//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using System;
using UnityEngine;
using UnityEngine.Events;

namespace Valve.VR
{
    [Serializable]
    public class SteamVR_Behaviour_Vector3Event : UnityEvent<SteamVR_Behaviour_Vector3, SteamVR_Input_Sources, Vector3, Vector3> { }
}