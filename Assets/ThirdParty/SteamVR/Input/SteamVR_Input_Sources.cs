//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Valve.VR
{
    public enum SteamVR_Input_Sources
    {
        [Description("/unrestricted")] //todo: check to see if this gets exported: k_ulInvalidInputHandle 
        Any,

        [Description("/user/hand/left")]
        LeftHand,

        [Description("/user/hand/right")]
        RightHand,

        [Description("/user/foot/left")]
        LeftFoot,

        [Description("/user/foot/right")]
        RightFoot,

        [Description("/user/shoulder/left")]
        LeftShoulder,

        [Description("/user/shoulder/right")]
        RightShoulder,

        [Description("/user/waist")]
        Waist,

        [Description("/user/chest")]
        Chest,

        [Description("/user/head")]
        Head,

        [Description("/user/gamepad")]
        Gamepad,

        [Description("/user/camera")]
        Camera,

        [Description("/user/keyboard")]
        Keyboard,
    }
}

namespace Valve.VR.InputSources
{
    using Sources = SteamVR_Input_Sources;
}