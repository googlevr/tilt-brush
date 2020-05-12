//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using UnityEngine;
using System.Collections;
using System;
using Valve.VR;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Valve.VR
{
    [Serializable]
    public abstract class SteamVR_Action_Out<SourceMap, SourceElement> : SteamVR_Action<SourceMap, SourceElement>, ISteamVR_Action_Out
        where SourceMap : SteamVR_Action_Source_Map<SourceElement>, new()
        where SourceElement : SteamVR_Action_Out_Source, new()
    {
    }
    
    public abstract class SteamVR_Action_Out_Source : SteamVR_Action_Source, ISteamVR_Action_Out_Source
    {
    }

    public interface ISteamVR_Action_Out : ISteamVR_Action, ISteamVR_Action_Out_Source
    {
    }

    public interface ISteamVR_Action_Out_Source : ISteamVR_Action_Source
    {
    }
}