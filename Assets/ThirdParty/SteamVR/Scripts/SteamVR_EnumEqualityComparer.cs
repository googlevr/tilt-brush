//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Valve.VR
{
    struct SteamVREnumEqualityComparer<TEnum> : IEqualityComparer<TEnum> where TEnum : struct
    {
        static class BoxAvoidance
        {
            static readonly Func<TEnum, int> _wrapper;

            public static int ToInt(TEnum enu)
            {
                return _wrapper(enu);
            }

            static BoxAvoidance()
            {
                var p = Expression.Parameter(typeof(TEnum), null);
                var c = Expression.ConvertChecked(p, typeof(int));

                _wrapper = Expression.Lambda<Func<TEnum, int>>(c, p).Compile();
            }
        }

        public bool Equals(TEnum firstEnum, TEnum secondEnum)
        {
            return BoxAvoidance.ToInt(firstEnum) == BoxAvoidance.ToInt(secondEnum);
        }

        public int GetHashCode(TEnum firstEnum)
        {
            return BoxAvoidance.ToInt(firstEnum);
        }
    }

    public struct SteamVR_Input_Sources_Comparer : IEqualityComparer<SteamVR_Input_Sources>
    {
        public bool Equals(SteamVR_Input_Sources x, SteamVR_Input_Sources y)
        {
            return x == y;
        }

        public int GetHashCode(SteamVR_Input_Sources obj)
        {
            return (int)obj;
        }
    }
}