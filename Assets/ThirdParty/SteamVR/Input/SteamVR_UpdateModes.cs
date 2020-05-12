//======= Copyright (c) Valve Corporation, All rights reserved. ===============


namespace Valve.VR
{
    public enum SteamVR_UpdateModes
    {
        Nothing = (1 << 0),
        OnUpdate = (1 << 1),
        OnFixedUpdate = (1 << 2),
        OnPreCull = (1 << 3),
        OnLateUpdate = (1 << 4),
    }
}