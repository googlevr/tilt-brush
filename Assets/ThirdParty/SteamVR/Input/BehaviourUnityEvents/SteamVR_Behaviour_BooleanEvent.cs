//======= Copyright (c) Valve Corporation, All rights reserved. ===============
using System;
using UnityEngine.Events;

namespace Valve.VR
{
    [Serializable]
    public class SteamVR_Behaviour_BooleanEvent : UnityEvent<SteamVR_Behaviour_Boolean, SteamVR_Input_Sources, bool> { }
}