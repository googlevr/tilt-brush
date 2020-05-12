//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using UnityEngine;
using System.Collections;
using System;
using Valve.VR;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;

namespace Valve.VR
{
    /// <summary>
    /// Action sets are logical groupings of actions. Multiple sets can be active at one time.
    /// </summary>
    [Serializable]
    public class SteamVR_ActionSet : IEquatable<SteamVR_ActionSet>, ISteamVR_ActionSet, ISerializationCallbackReceiver
    {
        public SteamVR_ActionSet() { }

        [SerializeField]
        private string actionSetPath;

        [NonSerialized]
        protected SteamVR_ActionSet_Data setData;


        /// <summary>All actions within this set (including out actions)</summary>
        public SteamVR_Action[] allActions
        {
            get
            {
                if (initialized == false)
                    Initialize();

                return setData.allActions;
            }
        }

        /// <summary>All IN actions within this set that are NOT pose or skeleton actions</summary>
        public ISteamVR_Action_In[] nonVisualInActions
        {
            get
            {
                if (initialized == false)
                    Initialize();

                return setData.nonVisualInActions;
            }
        }

        /// <summary>All pose and skeleton actions within this set</summary>
        public ISteamVR_Action_In[] visualActions
        {
            get
            {
                if (initialized == false)
                    Initialize();

                return setData.visualActions;
            }
        }

        /// <summary>All pose actions within this set</summary>
        public SteamVR_Action_Pose[] poseActions
        {
            get
            {
                if (initialized == false)
                    Initialize();

                return setData.poseActions;
            }
        }

        /// <summary>All skeleton actions within this set</summary>
        public SteamVR_Action_Skeleton[] skeletonActions
        {
            get
            {
                if (initialized == false)
                    Initialize();

                return setData.skeletonActions;
            }
        }

        /// <summary>All out actions within this set</summary>
        public ISteamVR_Action_Out[] outActionArray
        {
            get
            {
                if (initialized == false)
                    Initialize();

                return setData.outActionArray;
            }
        }


        /// <summary>The full path to this action set (ex: /actions/in/default)</summary>
        public string fullPath
        {
            get
            {
                if (initialized == false)
                    Initialize();

                return setData.fullPath;
            }
        }
        public string usage
        {
            get
            {
                if (initialized == false)
                    Initialize();

                return setData.usage;
            }
        }

        public ulong handle
        {
            get
            {
                if (initialized == false)
                    Initialize();

                return setData.handle;
            }
        }

        [NonSerialized]
        protected bool initialized = false;


        public static CreateType Create<CreateType>(string newSetPath) where CreateType : SteamVR_ActionSet, new()
        {
            CreateType actionSet = new CreateType();
            actionSet.PreInitialize(newSetPath);
            return actionSet;
        }
        public static CreateType CreateFromName<CreateType>(string newSetName) where CreateType : SteamVR_ActionSet, new()
        {
            CreateType actionSet = new CreateType();
            actionSet.PreInitialize(SteamVR_Input_ActionFile_ActionSet.GetPathFromName(newSetName));
            return actionSet;
        }

        public void PreInitialize(string newActionPath)
        {
            actionSetPath = newActionPath;

            setData = new SteamVR_ActionSet_Data();
            setData.fullPath = actionSetPath;
            setData.PreInitialize();

            initialized = true;
        }

        public virtual void FinishPreInitialize()
        {
            setData.FinishPreInitialize();
        }

        /// <summary>
        /// Initializes the handle for the action
        /// </summary>
        public virtual void Initialize(bool createNew = false, bool throwErrors = true)
        {
            if (createNew)
            {
                setData.Initialize();
            }
            else
            {
                setData = SteamVR_Input.GetActionSetDataFromPath(actionSetPath);

                if (setData == null)
                {
#if UNITY_EDITOR
                    if (throwErrors)
                    {
                        if (string.IsNullOrEmpty(actionSetPath))
                        {
                            Debug.LogError("<b>[SteamVR]</b> Action has not been assigned.");
                        }
                        else
                        {
                            Debug.LogError("<b>[SteamVR]</b> Could not find action with path: " + actionSetPath);
                        }
                    }
#endif
                }
            }

            initialized = true;
        }

        public string GetPath()
        {
            return actionSetPath;
        }

        /// <summary>
        /// Returns whether the set is currently active or not.
        /// </summary>
        /// <param name="source">The device to check. Any means all devices here (not left or right, but all)</param>
        public bool IsActive(SteamVR_Input_Sources source = SteamVR_Input_Sources.Any)
        {
            return setData.IsActive(source);
        }

        /// <summary>
        /// Returns the last time this action set was changed (set to active or inactive)
        /// </summary>
        /// <param name="source">The device to check. Any means all devices here (not left or right, but all)</param>
        public float GetTimeLastChanged(SteamVR_Input_Sources source = SteamVR_Input_Sources.Any)
        {
            return setData.GetTimeLastChanged(source);
        }

        /// <summary>
        /// Activate this set so its actions can be called
        /// </summary>
        /// <param name="disableAllOtherActionSets">Disable all other action sets at the same time</param>
        /// <param name="priority">The priority of this action set. If you have two actions bound to the same input (button) the higher priority set will override the lower priority. If they are the same priority both will execute.</param>
        /// <param name="activateForSource">Will activate this action set only for the specified source. Any if you want to activate for everything</param>
        public void Activate(SteamVR_Input_Sources activateForSource = SteamVR_Input_Sources.Any, int priority = 0, bool disableAllOtherActionSets = false)
        {
            setData.Activate(activateForSource, priority, disableAllOtherActionSets);
        }

        /// <summary>
        /// Deactivate the action set so its actions can no longer be called
        /// </summary>
        public void Deactivate(SteamVR_Input_Sources forSource = SteamVR_Input_Sources.Any)
        {
            setData.Deactivate(forSource);
        }

        /// <summary>Gets the last part of the path for this action. Removes "actions" and direction.</summary>
        public string GetShortName()
        {
            return setData.GetShortName();
        }

        VRActiveActionSet_t[] emptySetCache = new VRActiveActionSet_t[0];
        VRActiveActionSet_t[] setCache = new VRActiveActionSet_t[1];
        /// <summary>
        /// Shows all the bindings for the actions in this set.
        /// </summary>
        /// <param name="originToHighlight">Highlights the binding of the passed in action (or the first action in the set if none is specified)</param>
        /// <returns></returns>
        public bool ShowBindingHints(ISteamVR_Action_In originToHighlight = null)
        {
            if (originToHighlight == null)
            {
                for (int actionIndex = 0; actionIndex < allActions.Length; actionIndex++)
                {
                    if (allActions[actionIndex].direction == SteamVR_ActionDirections.In && allActions[actionIndex].active)
                    {
                        originToHighlight = (ISteamVR_Action_In)allActions[actionIndex];
                        break;
                    }
                }
            }


            if (originToHighlight != null)
            {
                setCache[0].ulActionSet = this.handle;
                OpenVR.Input.ShowBindingsForActionSet(setCache, 1, originToHighlight.activeOrigin);
                return true;
            }

            return false;
        }

        public void HideBindingHints()
        {
            OpenVR.Input.ShowBindingsForActionSet(emptySetCache, 0, 0);
        }
        

        public bool ReadRawSetActive(SteamVR_Input_Sources inputSource)
        {
            return setData.ReadRawSetActive(inputSource);
        }

        public float ReadRawSetLastChanged(SteamVR_Input_Sources inputSource)
        {
            return setData.ReadRawSetLastChanged(inputSource);
        }

        public int ReadRawSetPriority(SteamVR_Input_Sources inputSource)
        {
            return setData.ReadRawSetPriority(inputSource);
        }

        public SteamVR_ActionSet_Data GetActionSetData()
        {
            return setData;
        }

        public CreateType GetCopy<CreateType>() where CreateType : SteamVR_ActionSet, new()
        {
            if (SteamVR_Input.ShouldMakeCopy()) //no need to make copies at runtime
            {
                CreateType actionSet = new CreateType();
                actionSet.actionSetPath = this.actionSetPath;
                actionSet.setData = this.setData;
                actionSet.initialized = true;
                return actionSet;
            }
            else
            {
                return (CreateType)this;
            }
        }

        public bool Equals(SteamVR_ActionSet other)
        {
            if (ReferenceEquals(null, other))
                return false;

            return this.actionSetPath == other.actionSetPath;
        }

        public override bool Equals(object other)
        {
            if (ReferenceEquals(null, other))
            {
                if (string.IsNullOrEmpty(this.actionSetPath)) //if we haven't set a path, say this action set is equal to null
                    return true;
                return false;
            }

            if (ReferenceEquals(this, other))
                return true;

            if (other is SteamVR_ActionSet)
                return this.Equals((SteamVR_ActionSet)other);

            return false;
        }

        public override int GetHashCode()
        {
            if (actionSetPath == null)
                return 0;
            else
                return actionSetPath.GetHashCode();
        }

        public static bool operator !=(SteamVR_ActionSet set1, SteamVR_ActionSet set2)
        {
            return !(set1 == set2);
        }

        public static bool operator ==(SteamVR_ActionSet set1, SteamVR_ActionSet set2)
        {
            bool set1null = (ReferenceEquals(null, set1) || string.IsNullOrEmpty(set1.actionSetPath) || set1.GetActionSetData() == null);
            bool set2null = (ReferenceEquals(null, set2) || string.IsNullOrEmpty(set2.actionSetPath) || set2.GetActionSetData() == null);

            if (set1null && set2null)
                return true;
            else if (set1null != set2null)
                return false;

            return set1.Equals(set2);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (setData != null)
            {
                if (setData.fullPath != actionSetPath)
                {
                    setData = SteamVR_Input.GetActionSetDataFromPath(actionSetPath);
                }
            }

            if (initialized == false)
                Initialize(false, false);
        }
    }
    /// <summary>
    /// Action sets are logical groupings of actions. Multiple sets can be active at one time.
    /// </summary>
    public class SteamVR_ActionSet_Data : ISteamVR_ActionSet
    {
        public SteamVR_ActionSet_Data() { }

        /// <summary>All actions within this set (including out actions)</summary>
        public SteamVR_Action[] allActions { get; set; }

        /// <summary>All IN actions within this set that are NOT pose or skeleton actions</summary>
        public ISteamVR_Action_In[] nonVisualInActions { get; set; }

        /// <summary>All pose and skeleton actions within this set</summary>
        public ISteamVR_Action_In[] visualActions { get; set; }

        /// <summary>All pose actions within this set</summary>
        public SteamVR_Action_Pose[] poseActions { get; set; }

        /// <summary>All skeleton actions within this set</summary>
        public SteamVR_Action_Skeleton[] skeletonActions { get; set; }

        /// <summary>All out actions within this set</summary>
        public ISteamVR_Action_Out[] outActionArray { get; set; }


        /// <summary>The full path to this action set (ex: /actions/in/default)</summary>
        public string fullPath { get; set; }
        public string usage { get; set; }


        public ulong handle { get; set; }

        protected Dictionary<SteamVR_Input_Sources, bool> rawSetActive = new Dictionary<SteamVR_Input_Sources, bool>(new SteamVR_Input_Sources_Comparer());

        protected Dictionary<SteamVR_Input_Sources, float> rawSetLastChanged = new Dictionary<SteamVR_Input_Sources, float>(new SteamVR_Input_Sources_Comparer());

        protected Dictionary<SteamVR_Input_Sources, int> rawSetPriority = new Dictionary<SteamVR_Input_Sources, int>(new SteamVR_Input_Sources_Comparer());

        protected bool initialized = false;

        public void PreInitialize()
        {
            SteamVR_Input_Sources[] sources = SteamVR_Input_Source.GetAllSources();

            for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
            {
                SteamVR_Input_Sources source = sources[sourceIndex];
                rawSetActive.Add(source, false);
                rawSetLastChanged.Add(source, 0);
                rawSetPriority.Add(source, 0);
            }
        }

        public void FinishPreInitialize()
        {
            List<SteamVR_Action> allActionsList = new List<SteamVR_Action>();
            List<ISteamVR_Action_In> nonVisualInActionsList = new List<ISteamVR_Action_In>();
            List<ISteamVR_Action_In> visualActionsList = new List<ISteamVR_Action_In>();
            List<SteamVR_Action_Pose> poseActionsList = new List<SteamVR_Action_Pose>();
            List<SteamVR_Action_Skeleton> skeletonActionsList = new List<SteamVR_Action_Skeleton>();
            List<ISteamVR_Action_Out> outActionList = new List<ISteamVR_Action_Out>();

            if (SteamVR_Input.actions == null)
            {
                Debug.LogError("<b>[SteamVR Input]</b> Actions not initialized!");
                return;
            }

            for (int actionIndex = 0; actionIndex < SteamVR_Input.actions.Length; actionIndex++)
            {
                SteamVR_Action action = SteamVR_Input.actions[actionIndex];

                if (action.actionSet.GetActionSetData() == this)
                {
                    allActionsList.Add(action);

                    if (action is ISteamVR_Action_Boolean || action is ISteamVR_Action_Single || action is ISteamVR_Action_Vector2 || action is ISteamVR_Action_Vector3)
                    {
                        nonVisualInActionsList.Add((ISteamVR_Action_In)action);
                    }
                    else if (action is SteamVR_Action_Pose)
                    {
                        visualActionsList.Add((ISteamVR_Action_In)action);
                        poseActionsList.Add((SteamVR_Action_Pose)action);
                    }
                    else if (action is SteamVR_Action_Skeleton)
                    {
                        visualActionsList.Add((ISteamVR_Action_In)action);
                        skeletonActionsList.Add((SteamVR_Action_Skeleton)action);
                    }
                    else if (action is ISteamVR_Action_Out)
                    {
                        outActionList.Add((ISteamVR_Action_Out)action);
                    }
                    else
                    {
                        Debug.LogError("<b>[SteamVR Input]</b> Action doesn't implement known interface: " + action.fullPath);
                    }
                }
            }

            allActions = allActionsList.ToArray();
            nonVisualInActions = nonVisualInActionsList.ToArray();
            visualActions = visualActionsList.ToArray();
            poseActions = poseActionsList.ToArray();
            skeletonActions = skeletonActionsList.ToArray();
            outActionArray = outActionList.ToArray();
        }

        public void Initialize()
        {
            ulong newHandle = 0;
            EVRInputError err = OpenVR.Input.GetActionSetHandle(fullPath.ToLower(), ref newHandle);
            handle = newHandle;

            if (err != EVRInputError.None)
                Debug.LogError("<b>[SteamVR]</b> GetActionSetHandle (" + fullPath + ") error: " + err.ToString());

            initialized = true;
        }

        /// <summary>
        /// Returns whether the set is currently active or not.
        /// </summary>
        /// <param name="source">The device to check. Any means all devices here (not left or right, but all)</param>
        public bool IsActive(SteamVR_Input_Sources source = SteamVR_Input_Sources.Any)
        {
            if (initialized)
                return rawSetActive[source] || rawSetActive[SteamVR_Input_Sources.Any];

            return false;
        }

        /// <summary>
        /// Returns the last time this action set was changed (set to active or inactive)
        /// </summary>
        /// <param name="source">The device to check. Any means all devices here (not left or right, but all)</param>
        public float GetTimeLastChanged(SteamVR_Input_Sources source = SteamVR_Input_Sources.Any)
        {
            if (initialized)
                return rawSetLastChanged[source];
            return 0;
        }

        /// <summary>
        /// Activate this set so its actions can be called
        /// </summary>
        /// <param name="disableAllOtherActionSets">Disable all other action sets at the same time</param>
        /// <param name="priority">The priority of this action set. If you have two actions bound to the same input (button) the higher priority set will override the lower priority. If they are the same priority both will execute.</param>
        /// <param name="activateForSource">Will activate this action set only for the specified source. Any if you want to activate for everything</param>
        public void Activate(SteamVR_Input_Sources activateForSource = SteamVR_Input_Sources.Any, int priority = 0, bool disableAllOtherActionSets = false)
        {
            if (disableAllOtherActionSets)
                SteamVR_ActionSet_Manager.DisableAllActionSets();

            if (rawSetActive[activateForSource] == false)
            {
                rawSetActive[activateForSource] = true;
                SteamVR_ActionSet_Manager.SetChanged();

                rawSetLastChanged[activateForSource] = Time.realtimeSinceStartup;
            }

            if (rawSetPriority[activateForSource] != priority)
            {
                rawSetPriority[activateForSource] = priority;
                SteamVR_ActionSet_Manager.SetChanged();

                rawSetLastChanged[activateForSource] = Time.realtimeSinceStartup;
            }
        }

        /// <summary>
        /// Deactivate the action set so its actions can no longer be called
        /// </summary>
        public void Deactivate(SteamVR_Input_Sources forSource = SteamVR_Input_Sources.Any)
        {
            if (rawSetActive[forSource] != false)
            {
                rawSetLastChanged[forSource] = Time.realtimeSinceStartup;
                SteamVR_ActionSet_Manager.SetChanged();
            }

            rawSetActive[forSource] = false;
            rawSetPriority[forSource] = 0;
        }

        private string cachedShortName;

        /// <summary>Gets the last part of the path for this action. Removes "actions" and direction.</summary>
        public string GetShortName()
        {
            if (cachedShortName == null)
            {
                cachedShortName = SteamVR_Input_ActionFile.GetShortName(fullPath);
            }

            return cachedShortName;
        }

        public bool ReadRawSetActive(SteamVR_Input_Sources inputSource)
        {
            return rawSetActive[inputSource];
        }

        public float ReadRawSetLastChanged(SteamVR_Input_Sources inputSource)
        {
            return rawSetLastChanged[inputSource];
        }

        public int ReadRawSetPriority(SteamVR_Input_Sources inputSource)
        {
            return rawSetPriority[inputSource];
        }
    }
    /// <summary>
    /// Action sets are logical groupings of actions. Multiple sets can be active at one time.
    /// </summary>
    public interface ISteamVR_ActionSet
    {
        /// <summary>All actions within this set (including out actions)</summary>
        SteamVR_Action[] allActions { get; }

        /// <summary>All IN actions within this set that are NOT pose or skeleton actions</summary>
        ISteamVR_Action_In[] nonVisualInActions { get; }

        /// <summary>All pose and skeleton actions within this set</summary>
        ISteamVR_Action_In[] visualActions { get; }

        /// <summary>All pose actions within this set</summary>
        SteamVR_Action_Pose[] poseActions { get; }

        /// <summary>All skeleton actions within this set</summary>
        SteamVR_Action_Skeleton[] skeletonActions { get; }

        /// <summary>All out actions within this set</summary>
        ISteamVR_Action_Out[] outActionArray { get; }


        /// <summary>The full path to this action set (ex: /actions/in/default)</summary>
        string fullPath { get; }

        /// <summary>How the binding UI should display this set</summary>
        string usage { get; }

        ulong handle { get; }

        bool ReadRawSetActive(SteamVR_Input_Sources inputSource);
        float ReadRawSetLastChanged(SteamVR_Input_Sources inputSource);
        int ReadRawSetPriority(SteamVR_Input_Sources inputSource);


        /// <summary>
        /// Returns whether the set is currently active or not.
        /// </summary>
        /// <param name="source">The device to check. Any means all devices here (not left or right, but all)</param>
        bool IsActive(SteamVR_Input_Sources source = SteamVR_Input_Sources.Any);

        /// <summary>
        /// Returns the last time this action set was changed (set to active or inactive)
        /// </summary>
        /// <param name="source">The device to check. Any means all devices here (not left or right, but all)</param>
        float GetTimeLastChanged(SteamVR_Input_Sources source = SteamVR_Input_Sources.Any);

        /// <summary>
        /// Activate this set so its actions can be called
        /// </summary>
        /// <param name="disableAllOtherActionSets">Disable all other action sets at the same time</param>
        /// <param name="priority">The priority of this action set. If you have two actions bound to the same input (button) the higher priority set will override the lower priority. If they are the same priority both will execute.</param>
        /// <param name="activateForSource">Will activate this action set only for the specified source. Any if you want to activate for everything</param>
        void Activate(SteamVR_Input_Sources activateForSource = SteamVR_Input_Sources.Any, int priority = 0, bool disableAllOtherActionSets = false);

        /// <summary>Deactivate the action set so its actions can no longer be called</summary>
        void Deactivate(SteamVR_Input_Sources forSource = SteamVR_Input_Sources.Any);

        /// <summary>Gets the last part of the path for this action. Removes "actions" and direction.</summary>
        string GetShortName();
    }
}