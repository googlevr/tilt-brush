//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using System.Linq;
using Valve.Newtonsoft.Json;
using System.IO;

namespace Valve.VR
{
    [System.Serializable]
    public class SteamVR_Input_BindingFile
    {
        public string app_key;
        public Dictionary<string, SteamVR_Input_BindingFile_ActionList> bindings = new Dictionary<string, SteamVR_Input_BindingFile_ActionList>();
        public string controller_type;
        public string description;
        public string name;
    }

    [System.Serializable]
    public class SteamVR_Input_BindingFile_ActionList
    {
        public List<SteamVR_Input_BindingFile_Chord> chords = new List<SteamVR_Input_BindingFile_Chord>();
        public List<SteamVR_Input_BindingFile_Pose> poses = new List<SteamVR_Input_BindingFile_Pose>();
        public List<SteamVR_Input_BindingFile_Haptic> haptics = new List<SteamVR_Input_BindingFile_Haptic>();
        public List<SteamVR_Input_BindingFile_Source> sources = new List<SteamVR_Input_BindingFile_Source>();
        public List<SteamVR_Input_BindingFile_Skeleton> skeleton = new List<SteamVR_Input_BindingFile_Skeleton>();
    }

    [System.Serializable]
    public class SteamVR_Input_BindingFile_Chord
    {
        public string output;
        public List<List<string>> inputs = new List<List<string>>();

        public override bool Equals(object obj)
        {
            if (obj is SteamVR_Input_BindingFile_Chord)
            {
                SteamVR_Input_BindingFile_Chord chord = (SteamVR_Input_BindingFile_Chord)obj;

                if (this.output == chord.output && this.inputs != null && chord.inputs != null)
                {
                    if (this.inputs.Count == chord.inputs.Count)
                    {
                        for (int thisIndex = 0; thisIndex < this.inputs.Count; thisIndex++)
                        {
                            if (this.inputs[thisIndex] != null && chord.inputs[thisIndex] != null && this.inputs[thisIndex].Count == chord.inputs[thisIndex].Count)
                            {
                                for (int thisSubIndex = 0; thisSubIndex < this.inputs[thisIndex].Count; thisSubIndex++)
                                {
                                    if (this.inputs[thisIndex][thisSubIndex] != chord.inputs[thisIndex][thisSubIndex])
                                    {
                                        return false;
                                    }
                                }
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    [System.Serializable]
    public class SteamVR_Input_BindingFile_Pose
    {
        public string output;
        public string path;

        public override bool Equals(object obj)
        {
            if (obj is SteamVR_Input_BindingFile_Pose)
            {
                SteamVR_Input_BindingFile_Pose pose = (SteamVR_Input_BindingFile_Pose)obj;
                if (pose.output == this.output && pose.path == this.path)
                    return true;
                return false;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    [System.Serializable]
    public class SteamVR_Input_BindingFile_Haptic
    {
        public string output;
        public string path;

        public override bool Equals(object obj)
        {
            if (obj is SteamVR_Input_BindingFile_Haptic)
            {
                SteamVR_Input_BindingFile_Haptic pose = (SteamVR_Input_BindingFile_Haptic)obj;
                if (pose.output == this.output && pose.path == this.path)
                    return true;
                return false;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    [System.Serializable]
    public class SteamVR_Input_BindingFile_Skeleton
    {
        public string output;
        public string path;

        public override bool Equals(object obj)
        {
            if (obj is SteamVR_Input_BindingFile_Skeleton)
            {
                SteamVR_Input_BindingFile_Skeleton pose = (SteamVR_Input_BindingFile_Skeleton)obj;
                if (pose.output == this.output && pose.path == this.path)
                    return true;
                return false;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    [System.Serializable]
    public class SteamVR_Input_BindingFile_Source
    {
        public string path;
        public string mode;
        public SteamVR_Input_BindingFile_Source_Input_StringDictionary parameters = new SteamVR_Input_BindingFile_Source_Input_StringDictionary();
        public SteamVR_Input_BindingFile_Source_Input inputs = new SteamVR_Input_BindingFile_Source_Input();

        protected const string outputKeyName = "output";

        public string GetOutput()
        {
            foreach (var input in inputs)
            {
                foreach (var entry in input.Value)
                {
                    if (entry.Key == outputKeyName)
                    {
                        return entry.Value;
                    }
                }
            }

            return null;
        }

        public override bool Equals(object obj)
        {
            if (obj is SteamVR_Input_BindingFile_Source)
            {
                SteamVR_Input_BindingFile_Source pose = (SteamVR_Input_BindingFile_Source)obj;
                if (pose.mode == this.mode && pose.path == this.path)
                {
                    bool parametersEqual = false;
                    if (parameters != null && pose.parameters != null)
                    {
                        if (this.parameters.Equals(pose.parameters))
                            parametersEqual = true;
                    }
                    else if (parameters == null && pose.parameters == null)
                        parametersEqual = true;

                    if (parametersEqual)
                    {
                        bool inputsEqual = false;
                        if (inputs != null && pose.inputs != null)
                        {
                            if (this.inputs.Equals(pose.inputs))
                                inputsEqual = true;
                        }
                        else if (inputs == null && pose.inputs == null)
                            inputsEqual = true;

                        return inputsEqual;
                    }
                }

                return false;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    [System.Serializable]
    public class SteamVR_Input_BindingFile_Source_Input : Dictionary<string, SteamVR_Input_BindingFile_Source_Input_StringDictionary>
    {
        public override bool Equals(object obj)
        {
            if (obj is SteamVR_Input_BindingFile_Source_Input)
            {
                SteamVR_Input_BindingFile_Source_Input sourceInput = (SteamVR_Input_BindingFile_Source_Input)obj;

                if (this == sourceInput)
                    return true;
                else
                {
                    if (this.Count == sourceInput.Count)
                    {
                        foreach (var element in this)
                        {
                            if (sourceInput.ContainsKey(element.Key) == false)
                                return false;
                            if (this[element.Key].Equals(sourceInput[element.Key]) == false)
                                return false;
                        }
                        return true;
                    }
                }
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    [System.Serializable]
    public class SteamVR_Input_BindingFile_Source_Input_StringDictionary : Dictionary<string, string>
    {
        public override bool Equals(object obj)
        {
            if (obj is SteamVR_Input_BindingFile_Source_Input_StringDictionary)
            {
                SteamVR_Input_BindingFile_Source_Input_StringDictionary stringDictionary = (SteamVR_Input_BindingFile_Source_Input_StringDictionary)obj;

                if (this == stringDictionary)
                    return true;

                return (this.Count == stringDictionary.Count && !this.Except(stringDictionary).Any());
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}