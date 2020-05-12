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
    /// <summary>
    /// In actions are all input type actions. Boolean, Single, Vector2, Vector3, Skeleton, and Pose. 
    /// </summary>
    public abstract class SteamVR_Action_In<SourceMap, SourceElement> : SteamVR_Action<SourceMap, SourceElement>, ISteamVR_Action_In 
        where SourceMap : SteamVR_Action_In_Source_Map<SourceElement>, new() 
        where SourceElement : SteamVR_Action_In_Source, new()
    {
        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Returns true if the action has been changed since the previous update</summary>
        public bool changed { get { return sourceMap[SteamVR_Input_Sources.Any].changed; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Returns true if the action was changed for the previous update cycle</summary>
        public bool lastChanged { get { return sourceMap[SteamVR_Input_Sources.Any].changed; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The time the action was changed (Time.realtimeSinceStartup)</summary>
        public float changedTime { get { return sourceMap[SteamVR_Input_Sources.Any].changedTime; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The time the action was updated (Time.realtimeSinceStartup)</summary>
        public float updateTime { get { return sourceMap[SteamVR_Input_Sources.Any].updateTime; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The handle to the component that triggered the action to be changed</summary>
        public ulong activeOrigin { get { return sourceMap[SteamVR_Input_Sources.Any].activeOrigin; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The handle to the component that triggered the action to be changed in the previous update</summary>
        public ulong lastActiveOrigin { get { return sourceMap[SteamVR_Input_Sources.Any].lastActiveOrigin; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The input source that triggered the action to be changed</summary>
        public SteamVR_Input_Sources activeDevice { get { return sourceMap[SteamVR_Input_Sources.Any].activeDevice; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The device index (used by Render Models) used by the device that triggered the action to be changed</summary>
        public uint trackedDeviceIndex { get { return sourceMap[SteamVR_Input_Sources.Any].trackedDeviceIndex; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The name of the component on the render model that caused the action to be changed (not localized)</summary>
        public string renderModelComponentName { get { return sourceMap[SteamVR_Input_Sources.Any].renderModelComponentName; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The full localized name for the component, controller, and hand that caused the action to be changed</summary>
        public string localizedOriginName { get { return sourceMap[SteamVR_Input_Sources.Any].localizedOriginName; } }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> 
        /// Updates the data for all the input sources the system has detected need to be updated.
        /// </summary>
        public virtual void UpdateValues()
        {
            sourceMap.UpdateValues();
        }

        /// <summary>
        /// The name of the component on the render model that caused the action to be updated (not localized)
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public virtual string GetRenderModelComponentName(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].renderModelComponentName;
        }

        /// <summary>
        /// The input source that triggered the action to be updated last
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public virtual SteamVR_Input_Sources GetActiveDevice(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].activeDevice;
        }

        /// <summary>
        /// Gets the device index for the controller this action is bound to. This can be used for render models or the pose tracking system.
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public virtual uint GetDeviceIndex(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].trackedDeviceIndex;
        }

        /// <summary>
        /// Indicates whether or not the data for this action and specified input source has changed since the last update. Determined by SteamVR or 'changeTolerance'.
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public virtual bool GetChanged(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].changed;
        }

        /// <summary>
        /// The time the action was changed (Time.realtimeSinceStartup)
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public override float GetTimeLastChanged(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].changedTime;
        }


        /// <summary>
        /// Gets the localized name of the device that the action corresponds to. Include as many EVRInputStringBits as you want to add to the localized string
        /// </summary>
        /// <param name="inputSource"></param>
        /// <param name="localizedParts">
        /// <list type="bullet">
        /// <item><description>VRInputString_Hand - Which hand the origin is in. ex: "Left Hand". </description></item>
        /// <item><description>VRInputString_ControllerType - What kind of controller the user has in that hand. ex: "Vive Controller". </description></item>
        /// <item><description>VRInputString_InputSource - What part of that controller is the origin. ex: "Trackpad". </description></item>
        /// <item><description>VRInputString_All - All of the above. ex: "Left Hand Vive Controller Trackpad". </description></item>
        /// </list>
        /// </param>
        public string GetLocalizedOriginPart(SteamVR_Input_Sources inputSource, params EVRInputStringBits[] localizedParts)
        {
            return sourceMap[inputSource].GetLocalizedOriginPart(localizedParts);
        }

        /// <summary>
        /// Gets the localized full name of the device that the action was updated by. ex: "Left Hand Vive Controller Trackpad"
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public string GetLocalizedOrigin(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].GetLocalizedOrigin();
        }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong>
        /// Returns whether the system has determined this source should be updated (based on code calls)
        /// Should only be used if you've set SteamVR_Action.startUpdatingSourceOnAccess to false.
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public override bool IsUpdating(SteamVR_Input_Sources inputSource)
        {
            return sourceMap.IsUpdating(inputSource);
        }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> 
        /// Forces the system to start updating the data for this action and the specified input source.
        /// Should only be used if you've set SteamVR_Action.startUpdatingSourceOnAccess to false.
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void ForceAddSourceToUpdateList(SteamVR_Input_Sources inputSource)
        {
            sourceMap.ForceAddSourceToUpdateList(inputSource);
        }
    }

    public class SteamVR_Action_In_Source_Map<SourceElement> : SteamVR_Action_Source_Map<SourceElement>
        where SourceElement : SteamVR_Action_In_Source, new()
    {
        protected List<SteamVR_Input_Sources> updatingSources = new List<SteamVR_Input_Sources>();

        /// <summary>
        /// <strong>[Should not be called by user code]</strong>
        /// Returns whether the system has determined this source should be updated (based on code calls)
        /// Should only be used if you've set SteamVR_Action.startUpdatingSourceOnAccess to false.
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public bool IsUpdating(SteamVR_Input_Sources inputSource)
        {
            for (int sourceIndex = 0; sourceIndex < updatingSources.Count; sourceIndex++)
            {
                if (inputSource == updatingSources[sourceIndex])
                    return true;
            }

            return false;
        }

        protected override void OnAccessSource(SteamVR_Input_Sources inputSource)
        {
            if (SteamVR_Action.startUpdatingSourceOnAccess)
            {
                ForceAddSourceToUpdateList(inputSource);
            }
        }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> 
        /// Forces the system to start updating the data for this action and the specified input source.
        /// Should only be used if you've set SteamVR_Action.startUpdatingSourceOnAccess to false.
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void ForceAddSourceToUpdateList(SteamVR_Input_Sources inputSource)
        {
            if (sources[inputSource].isUpdating == false)
            {
                updatingSources.Add(inputSource);
                sources[inputSource].isUpdating = true;

                if (SteamVR_Input.isStartupFrame == false)
                    sources[inputSource].UpdateValue();
            }
        }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> 
        /// Updates the data for all the input sources the system has detected need to be updated.
        /// </summary>
        public void UpdateValues()
        {
            for (int sourceIndex = 0; sourceIndex < updatingSources.Count; sourceIndex++)
            {
                sources[updatingSources[sourceIndex]].UpdateValue();
            }
        }
    }

    /// <summary>
    /// In actions are all input type actions. Boolean, Single, Vector2, Vector3, Skeleton, and Pose. 
    /// This class fires onChange and onUpdate events.
    /// </summary>
    public abstract class SteamVR_Action_In_Source : SteamVR_Action_Source, ISteamVR_Action_In_Source
    {
        protected static uint inputOriginInfo_size = 0;

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> 
        /// Forces the system to start updating the data for this action and the specified input source.
        /// Should only be used if you've set SteamVR_Action.startUpdatingSourceOnAccess to false.
        /// </summary>
        public bool isUpdating { get; set; }
        
        /// <summary>The time the action was updated (Time.realtimeSinceStartup)</summary>
        public float updateTime { get; protected set; }

        /// <summary>The handle to the component that triggered the action to be changed</summary>
        public abstract ulong activeOrigin { get; }

        /// <summary>The handle to the component that triggered the action to be changed in the previous update</summary>
        public abstract ulong lastActiveOrigin { get; }

        /// <summary>Returns true if the action has been changed since the previous update</summary>
        public abstract bool changed { get; protected set; }

        /// <summary>Returns true if the action was changed for the previous update cycle</summary>
        public abstract bool lastChanged { get; protected set; }

        /// <summary>The input source that triggered the action to be updated</summary>
        public SteamVR_Input_Sources activeDevice { get { UpdateOriginTrackedDeviceInfo();  return SteamVR_Input_Source.GetSource(inputOriginInfo.devicePath); } }

        /// <summary>The device index (used by Render Models) used by the device that triggered the action to be updated</summary>
        public uint trackedDeviceIndex { get { UpdateOriginTrackedDeviceInfo(); return inputOriginInfo.trackedDeviceIndex; } }

        /// <summary>The name of the component on the render model that caused the action to be updated (not localized)</summary>
        public string renderModelComponentName { get { UpdateOriginTrackedDeviceInfo(); return inputOriginInfo.rchRenderModelComponentName; } }

        /// <summary>
        /// Gets the localized full name of the device that the action was updated by. ex: "Left Hand Vive Controller Trackpad"
        /// </summary>
        public string localizedOriginName { get { UpdateOriginTrackedDeviceInfo(); return GetLocalizedOrigin(); } }


        /// <summary>The Time.realtimeSinceStartup that this action was last changed.</summary>
        public float changedTime { get; protected set; }

        protected int lastOriginGetFrame { get; set; }

        protected InputOriginInfo_t inputOriginInfo = new InputOriginInfo_t();
        protected InputOriginInfo_t lastInputOriginInfo = new InputOriginInfo_t();

        /// <summary><strong>[Should not be called by user code]</strong> Updates the data for this action and this input source</summary>
        public abstract void UpdateValue();

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> Initializes the handle for the action, the size of the InputOriginInfo struct, and any other related SteamVR data.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            if (inputOriginInfo_size == 0)
                inputOriginInfo_size = (uint)Marshal.SizeOf(typeof(InputOriginInfo_t));
        }

        protected void UpdateOriginTrackedDeviceInfo()
        {
            if (lastOriginGetFrame != Time.frameCount) //only get once per frame
            {
                EVRInputError err = OpenVR.Input.GetOriginTrackedDeviceInfo(activeOrigin, ref inputOriginInfo, inputOriginInfo_size);

                if (err != EVRInputError.None)
                    Debug.LogError("<b>[SteamVR]</b> GetOriginTrackedDeviceInfo error (" + fullPath + "): " + err.ToString() + " handle: " + handle.ToString() + " activeOrigin: " + activeOrigin.ToString() + " active: " + active);

                lastInputOriginInfo = inputOriginInfo;
                lastOriginGetFrame = Time.frameCount;
            }
        }

        /// <summary>
        /// Gets the localized name of the device that the action corresponds to. Include as many EVRInputStringBits as you want to add to the localized string
        /// </summary>
        /// <param name="inputSource"></param>
        /// <param name="localizedParts">
        /// <list type="bullet">
        /// <item><description>VRInputString_Hand - Which hand the origin is in. ex: "Left Hand". </description></item>
        /// <item><description>VRInputString_ControllerType - What kind of controller the user has in that hand. ex: "Vive Controller". </description></item>
        /// <item><description>VRInputString_InputSource - What part of that controller is the origin. ex: "Trackpad". </description></item>
        /// <item><description>VRInputString_All - All of the above. ex: "Left Hand Vive Controller Trackpad". </description></item>
        /// </list>
        /// </param>
        public string GetLocalizedOriginPart(params EVRInputStringBits[] localizedParts)
        {
            UpdateOriginTrackedDeviceInfo();

            if (active)
            {
                return SteamVR_Input.GetLocalizedName(activeOrigin, localizedParts);
            }

            return null;
        }

        /// <summary>
        /// Gets the localized full name of the device that the action was updated by. ex: "Left Hand Vive Controller Trackpad"
        /// </summary>
        public string GetLocalizedOrigin()
        {
            UpdateOriginTrackedDeviceInfo();

            if (active)
            {
                return SteamVR_Input.GetLocalizedName(activeOrigin, EVRInputStringBits.VRInputString_All);
            }

            return null;
        }
    }

    public interface ISteamVR_Action_In : ISteamVR_Action, ISteamVR_Action_In_Source
    {
        /// <summary>
        /// <strong>[Should not be called by user code]</strong> 
        /// Updates the data for all the input sources the system has detected need to be updated.
        /// </summary>
        void UpdateValues();

        /// <summary>
        /// The name of the component on the render model that caused the action to be updated (not localized)
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        string GetRenderModelComponentName(SteamVR_Input_Sources inputSource);

        /// <summary>
        /// The input source that triggered the action to be updated last
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        SteamVR_Input_Sources GetActiveDevice(SteamVR_Input_Sources inputSource);

        /// <summary>
        /// Gets the device index for the controller this action is bound to. This can be used for render models or the pose tracking system.
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        uint GetDeviceIndex(SteamVR_Input_Sources inputSource);

        /// <summary>
        /// Indicates whether or not the data for this action and specified input source has changed since the last update. Determined by SteamVR or 'changeTolerance'.
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        bool GetChanged(SteamVR_Input_Sources inputSource);


        /// <summary>
        /// Gets the localized name of the device that the action corresponds to. Include as many EVRInputStringBits as you want to add to the localized string
        /// </summary>
        /// <param name="inputSource"></param>
        /// <param name="localizedParts">
        /// <list type="bullet">
        /// <item><description>VRInputString_Hand - Which hand the origin is in. ex: "Left Hand". </description></item>
        /// <item><description>VRInputString_ControllerType - What kind of controller the user has in that hand. ex: "Vive Controller". </description></item>
        /// <item><description>VRInputString_InputSource - What part of that controller is the origin. ex: "Trackpad". </description></item>
        /// <item><description>VRInputString_All - All of the above. ex: "Left Hand Vive Controller Trackpad". </description></item>
        /// </list>
        /// </param>
        string GetLocalizedOriginPart(SteamVR_Input_Sources inputSource, params EVRInputStringBits[] localizedParts);

        /// <summary>
        /// Gets the localized full name of the device that the action was updated by. ex: "Left Hand Vive Controller Trackpad"
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        string GetLocalizedOrigin(SteamVR_Input_Sources inputSource);
    }

    public interface ISteamVR_Action_In_Source : ISteamVR_Action_Source
    {

        /// <summary>Returns true if the action has been changed in the most recent update</summary>
        bool changed { get; }

        /// <summary>Returns true if the action was changed for the previous update cycle</summary>
        bool lastChanged { get; }

        /// <summary>The Time.realtimeSinceStartup that this action was last changed.</summary>
        float changedTime { get; }

        /// <summary>The time the action was updated (Time.realtimeSinceStartup)</summary>
        float updateTime { get; }

        /// <summary>The handle to the component that triggered the action to be changed</summary>
        ulong activeOrigin { get; }

        /// <summary>The handle to the component that triggered the action to be changed in the previous update</summary>
        ulong lastActiveOrigin { get; }

        /// <summary>The input source that triggered the action to be updated</summary>
        SteamVR_Input_Sources activeDevice { get; }

        /// <summary>The device index (used by Render Models) used by the device that triggered the action to be updated</summary>
        uint trackedDeviceIndex { get; }

        /// <summary>The name of the component on the render model that caused the action to be updated (not localized)</summary>
        string renderModelComponentName { get; }

        /// <summary>Gets the localized full name of the device that the action was updated by. ex: "Left Hand Vive Controller Trackpad"</summary>
        string localizedOriginName { get; }
    }
}