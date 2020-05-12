//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using UnityEngine;
using System.Collections;
using System;
using Valve.VR;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Collections.Generic;

namespace Valve.VR
{
    public static class SteamVR_Input_Source
    {
        private static Dictionary<SteamVR_Input_Sources, ulong> inputSourceHandlesBySource = new Dictionary<SteamVR_Input_Sources, ulong>(new SteamVR_Input_Sources_Comparer());
        private static Dictionary<ulong, SteamVR_Input_Sources> inputSourceSourcesByHandle = new Dictionary<ulong, SteamVR_Input_Sources>();

        private static Type enumType = typeof(SteamVR_Input_Sources);
        private static Type descriptionType = typeof(DescriptionAttribute);
        
        private static SteamVR_Input_Sources[] allSources;

        public static ulong GetHandle(SteamVR_Input_Sources inputSource)
        {
            if (inputSourceHandlesBySource.ContainsKey(inputSource))
                return inputSourceHandlesBySource[inputSource];

            return 0;
        }
        public static SteamVR_Input_Sources GetSource(ulong handle)
        {
            if (inputSourceSourcesByHandle.ContainsKey(handle))
                return inputSourceSourcesByHandle[handle];

            return SteamVR_Input_Sources.Any;
        }

        public static SteamVR_Input_Sources[] GetAllSources()
        {
            if (allSources == null)
                allSources = (SteamVR_Input_Sources[])System.Enum.GetValues(typeof(SteamVR_Input_Sources));

            return allSources;
        }

        private static string GetPath(string inputSourceEnumName)
        {
            return ((DescriptionAttribute)enumType.GetMember(inputSourceEnumName)[0].GetCustomAttributes(descriptionType, false)[0]).Description;
        }

        public static void Initialize()
        {
            List<SteamVR_Input_Sources> allSourcesList = new List<SteamVR_Input_Sources>();
            string[] enumNames = System.Enum.GetNames(enumType);
            inputSourceHandlesBySource = new Dictionary<SteamVR_Input_Sources, ulong>(new SteamVR_Input_Sources_Comparer());
            inputSourceSourcesByHandle = new Dictionary<ulong, SteamVR_Input_Sources>();

            for (int enumIndex = 0; enumIndex < enumNames.Length; enumIndex++)
            {
                string path = GetPath(enumNames[enumIndex]);

                ulong handle = 0;
                EVRInputError err = OpenVR.Input.GetInputSourceHandle(path, ref handle);

                if (err != EVRInputError.None)
                    Debug.LogError("<b>[SteamVR]</b> GetInputSourceHandle (" + path + ") error: " + err.ToString());

                if (enumNames[enumIndex] == SteamVR_Input_Sources.Any.ToString()) //todo: temporary hack
                {
                    inputSourceHandlesBySource.Add((SteamVR_Input_Sources)enumIndex, 0);
                    inputSourceSourcesByHandle.Add(0, (SteamVR_Input_Sources)enumIndex);
                }
                else
                {
                    inputSourceHandlesBySource.Add((SteamVR_Input_Sources)enumIndex, handle);
                    inputSourceSourcesByHandle.Add(handle, (SteamVR_Input_Sources)enumIndex);
                }

                allSourcesList.Add((SteamVR_Input_Sources)enumIndex);
            }
            
            allSources = allSourcesList.ToArray();
        }
    }
}