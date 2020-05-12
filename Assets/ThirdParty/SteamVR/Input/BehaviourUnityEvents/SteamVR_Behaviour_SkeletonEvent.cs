//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using System;
using UnityEngine.Events;

namespace Valve.VR
{
    [Serializable]
    public class SteamVR_Behaviour_SkeletonEvent : UnityEvent<SteamVR_Behaviour_Skeleton, SteamVR_Input_Sources> { }
}