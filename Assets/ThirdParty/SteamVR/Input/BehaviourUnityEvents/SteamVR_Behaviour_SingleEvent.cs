//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using System;
using UnityEngine.Events;

namespace Valve.VR
{
    [Serializable]
    public class SteamVR_Behaviour_SingleEvent : UnityEvent<SteamVR_Behaviour_Single, SteamVR_Input_Sources, float, float> { }
}