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
    /// Pose actions represent a position, rotation, and velocities inside the tracked space. 
    /// SteamVR keeps a log of past poses so you can retrieve old poses with GetPoseAtTimeOffset or GetVelocitiesAtTimeOffset.
    /// You can also pass in times in the future to these methods for SteamVR's best prediction of where the pose will be at that time.
    /// </summary>
    public class SteamVR_Action_Pose : SteamVR_Action_Pose_Base<SteamVR_Action_Pose_Source_Map<SteamVR_Action_Pose_Source>, SteamVR_Action_Pose_Source>, ISerializationCallbackReceiver
    {
        public delegate void ActiveChangeHandler(SteamVR_Action_Pose fromAction, SteamVR_Input_Sources fromSource, bool active);
        public delegate void ChangeHandler(SteamVR_Action_Pose fromAction, SteamVR_Input_Sources fromSource);
        public delegate void UpdateHandler(SteamVR_Action_Pose fromAction, SteamVR_Input_Sources fromSource);
        public delegate void TrackingChangeHandler(SteamVR_Action_Pose fromAction, SteamVR_Input_Sources fromSource, ETrackingResult trackingState);
        public delegate void ValidPoseChangeHandler(SteamVR_Action_Pose fromAction, SteamVR_Input_Sources fromSource, bool validPose);
        public delegate void DeviceConnectedChangeHandler(SteamVR_Action_Pose fromAction, SteamVR_Input_Sources fromSource, bool deviceConnected);

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Event fires when the active state (ActionSet active and binding active) changes</summary>
        public event ActiveChangeHandler onActiveChange
        { add { sourceMap[SteamVR_Input_Sources.Any].onActiveChange += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onActiveChange -= value; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Event fires when the active state of the binding changes</summary>
        public event ActiveChangeHandler onActiveBindingChange
        { add { sourceMap[SteamVR_Input_Sources.Any].onActiveBindingChange += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onActiveBindingChange -= value; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Event fires when the orientation of the pose changes more than the changeTolerance</summary>
        public event ChangeHandler onChange
        { add { sourceMap[SteamVR_Input_Sources.Any].onChange += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onChange -= value; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Event fires when the action is updated</summary>
        public event UpdateHandler onUpdate
        { add { sourceMap[SteamVR_Input_Sources.Any].onUpdate += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onUpdate -= value; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Event fires when the state of the tracking has changed</summary>
        public event TrackingChangeHandler onTrackingChanged
        { add { sourceMap[SteamVR_Input_Sources.Any].onTrackingChanged += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onTrackingChanged -= value; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Event fires when the validity of the pose has changed</summary>
        public event ValidPoseChangeHandler onValidPoseChanged
        { add { sourceMap[SteamVR_Input_Sources.Any].onValidPoseChanged += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onValidPoseChanged -= value; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Event fires when the device bound to this pose is connected or disconnected</summary>
        public event DeviceConnectedChangeHandler onDeviceConnectedChanged
        { add { sourceMap[SteamVR_Input_Sources.Any].onDeviceConnectedChanged += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onDeviceConnectedChanged -= value; } }

        /// <summary>Fires an event when a device is connected or disconnected.</summary>
        /// <param name="inputSource">The device you would like to add an event to. Any if the action is not device specific.</param>
        /// <param name="functionToCall">The method you would like to be called when a device is connected. Should take a SteamVR_Action_Pose as a param</param>
        public void AddOnDeviceConnectedChanged(SteamVR_Input_Sources inputSource, DeviceConnectedChangeHandler functionToCall)
        {
            sourceMap[inputSource].onDeviceConnectedChanged += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="inputSource">The device you would like to remove an event from. Any if the action is not device specific.</param>
        /// <param name="functionToStopCalling">The method you would like to stop calling when a device is connected. Should take a SteamVR_Action_Pose as a param</param>
        public void RemoveOnDeviceConnectedChanged(SteamVR_Input_Sources inputSource, DeviceConnectedChangeHandler functionToStopCalling)
        {
            sourceMap[inputSource].onDeviceConnectedChanged -= functionToStopCalling;
        }


        /// <summary>Fires an event when the tracking of the device has changed</summary>
        /// <param name="inputSource">The device you would like to add an event to. Any if the action is not device specific.</param>
        /// <param name="functionToCall">The method you would like to be called when tracking has changed. Should take a SteamVR_Action_Pose as a param</param>
        public void AddOnTrackingChanged(SteamVR_Input_Sources inputSource, TrackingChangeHandler functionToCall)
        {
            sourceMap[inputSource].onTrackingChanged += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="inputSource">The device you would like to remove an event from. Any if the action is not device specific.</param>
        /// <param name="functionToStopCalling">The method you would like to stop calling when tracking has changed. Should take a SteamVR_Action_Pose as a param</param>
        public void RemoveOnTrackingChanged(SteamVR_Input_Sources inputSource, TrackingChangeHandler functionToStopCalling)
        {
            sourceMap[inputSource].onTrackingChanged -= functionToStopCalling;
        }


        /// <summary>Fires an event when the device now has a valid pose or no longer has a valid pose</summary>
        /// <param name="inputSource">The device you would like to add an event to. Any if the action is not device specific.</param>
        /// <param name="functionToCall">The method you would like to be called when the pose has become valid or invalid. Should take a SteamVR_Action_Pose as a param</param>
        public void AddOnValidPoseChanged(SteamVR_Input_Sources inputSource, ValidPoseChangeHandler functionToCall)
        {
            sourceMap[inputSource].onValidPoseChanged += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="inputSource">The device you would like to remove an event from. Any if the action is not device specific.</param>
        /// <param name="functionToStopCalling">The method you would like to stop calling when the pose has become valid or invalid. Should take a SteamVR_Action_Pose as a param</param>
        public void RemoveOnValidPoseChanged(SteamVR_Input_Sources inputSource, ValidPoseChangeHandler functionToStopCalling)
        {
            sourceMap[inputSource].onValidPoseChanged -= functionToStopCalling;
        }


        /// <summary>Executes a function when this action's bound state changes</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void AddOnActiveChangeListener(SteamVR_Input_Sources inputSource, ActiveChangeHandler functionToCall)
        {
            sourceMap[inputSource].onActiveChange += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="functionToStopCalling">The local function that you've setup to receive update events</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void RemoveOnActiveChangeListener(SteamVR_Input_Sources inputSource, ActiveChangeHandler functionToStopCalling)
        {
            sourceMap[inputSource].onActiveChange -= functionToStopCalling;
        }

        /// <summary>Executes a function when the state of this action (with the specified inputSource) changes</summary>
        /// <param name="functionToCall">A local function that receives the boolean action who's state has changed, the corresponding input source, and the new value</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void AddOnChangeListener(SteamVR_Input_Sources inputSource, ChangeHandler functionToCall)
        {
            sourceMap[inputSource].onChange += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="functionToStopCalling">The local function that you've setup to receive on change events</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void RemoveOnChangeListener(SteamVR_Input_Sources inputSource, ChangeHandler functionToStopCalling)
        {
            sourceMap[inputSource].onChange -= functionToStopCalling;
        }

        /// <summary>Executes a function when the state of this action (with the specified inputSource) is updated.</summary>
        /// <param name="functionToCall">A local function that receives the boolean action who's state has changed, the corresponding input source, and the new value</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void AddOnUpdateListener(SteamVR_Input_Sources inputSource, UpdateHandler functionToCall)
        {
            sourceMap[inputSource].onUpdate += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="functionToStopCalling">The local function that you've setup to receive update events</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void RemoveOnUpdateListener(SteamVR_Input_Sources inputSource, UpdateHandler functionToStopCalling)
        {
            sourceMap[inputSource].onUpdate -= functionToStopCalling;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            InitAfterDeserialize();
        }

        /// <summary>
        /// Sets all pose and skeleton actions to use the specified universe origin.
        /// </summary>
        public static void SetTrackingUniverseOrigin(ETrackingUniverseOrigin newOrigin)
        {
            SetUniverseOrigin(newOrigin);
            OpenVR.Compositor.SetTrackingSpace(newOrigin);
        }
    }

    [Serializable]
    /// <summary>
    /// The base pose action (pose and skeleton inherit from this)
    /// </summary>
    public abstract class SteamVR_Action_Pose_Base<SourceMap, SourceElement> : SteamVR_Action_In<SourceMap, SourceElement>, ISteamVR_Action_Pose
        where SourceMap : SteamVR_Action_Pose_Source_Map<SourceElement>, new()
        where SourceElement : SteamVR_Action_Pose_Source, new()
    {
        /// <summary>
        /// Sets all pose (and skeleton) actions to use the specified universe origin.
        /// </summary>
        protected static void SetUniverseOrigin(ETrackingUniverseOrigin newOrigin)
        {
            for (int actionIndex = 0; actionIndex < SteamVR_Input.actionsPose.Length; actionIndex++)
            {
                SteamVR_Input.actionsPose[actionIndex].sourceMap.SetTrackingUniverseOrigin(newOrigin);
            }

            for (int actionIndex = 0; actionIndex < SteamVR_Input.actionsSkeleton.Length; actionIndex++)
            {
                SteamVR_Input.actionsSkeleton[actionIndex].sourceMap.SetTrackingUniverseOrigin(newOrigin);
            }
        }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The local position of this action relative to the universe origin</summary>
        public Vector3 localPosition { get { return sourceMap[SteamVR_Input_Sources.Any].localPosition; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The local rotation of this action relative to the universe origin</summary>
        public Quaternion localRotation { get { return sourceMap[SteamVR_Input_Sources.Any].localRotation; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The state of the tracking system that is used to create pose data (position, rotation, etc)</summary>
        public ETrackingResult trackingState { get { return sourceMap[SteamVR_Input_Sources.Any].trackingState; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The local velocity of this pose relative to the universe origin</summary>
        public Vector3 velocity { get { return sourceMap[SteamVR_Input_Sources.Any].velocity; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The local angular velocity of this pose relative to the universe origin</summary>
        public Vector3 angularVelocity { get { return sourceMap[SteamVR_Input_Sources.Any].angularVelocity; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> True if the pose retrieved for this action and input source is valid (good data from the tracking source)</summary>
        public bool poseIsValid { get { return sourceMap[SteamVR_Input_Sources.Any].poseIsValid; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> True if the device bound to this action and input source is connected</summary>
        public bool deviceIsConnected { get { return sourceMap[SteamVR_Input_Sources.Any].deviceIsConnected; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The local position for this pose during the previous update</summary>
        public Vector3 lastLocalPosition { get { return sourceMap[SteamVR_Input_Sources.Any].lastLocalPosition; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The local rotation for this pose during the previous update</summary>
        public Quaternion lastLocalRotation { get { return sourceMap[SteamVR_Input_Sources.Any].lastLocalRotation; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The tracking state for this pose during the previous update</summary>
        public ETrackingResult lastTrackingState { get { return sourceMap[SteamVR_Input_Sources.Any].lastTrackingState; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The velocity for this pose during the previous update</summary>
        public Vector3 lastVelocity { get { return sourceMap[SteamVR_Input_Sources.Any].lastVelocity; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The angular velocity for this pose during the previous update</summary>
        public Vector3 lastAngularVelocity { get { return sourceMap[SteamVR_Input_Sources.Any].lastAngularVelocity; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> True if the pose was valid during the previous update</summary>
        public bool lastPoseIsValid { get { return sourceMap[SteamVR_Input_Sources.Any].lastPoseIsValid; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> True if the device bound to this action was connected during the previous update</summary>
        public bool lastDeviceIsConnected { get { return sourceMap[SteamVR_Input_Sources.Any].lastDeviceIsConnected; } }


        public SteamVR_Action_Pose_Base() { }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> 
        /// Updates the data for all the input sources the system has detected need to be updated.
        /// </summary>
        public virtual void UpdateValues(bool skipStateAndEventUpdates)
        {
            sourceMap.UpdateValues(skipStateAndEventUpdates);
        }

        /// <summary>
        /// SteamVR keeps a log of past poses so you can retrieve old poses or estimated poses in the future by passing in a secondsFromNow value that is negative or positive.
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        /// <param name="secondsFromNow">The time offset in the future (estimated) or in the past (previously recorded) you want to get data from</param>
        /// <returns>true if the call succeeded</returns>
        public bool GetVelocitiesAtTimeOffset(SteamVR_Input_Sources inputSource, float secondsFromNow, out Vector3 velocity, out Vector3 angularVelocity)
        {
            return sourceMap[inputSource].GetVelocitiesAtTimeOffset(secondsFromNow, out velocity, out angularVelocity);
        }

        /// <summary>
        /// SteamVR keeps a log of past poses so you can retrieve old poses or estimated poses in the future by passing in a secondsFromNow value that is negative or positive.
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        /// <param name="secondsFromNow">The time offset in the future (estimated) or in the past (previously recorded) you want to get data from</param>
        /// <returns>true if the call succeeded</returns>
        public bool GetPoseAtTimeOffset(SteamVR_Input_Sources inputSource, float secondsFromNow, out Vector3 localPosition, out Quaternion localRotation, out Vector3 velocity, out Vector3 angularVelocity)
        {
            return sourceMap[inputSource].GetPoseAtTimeOffset(secondsFromNow, out localPosition, out localRotation, out velocity, out angularVelocity);
        }

        /// <summary>
        /// Update a transform's local position and local roation to match the pose from the most recent update
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        /// <param name="transformToUpdate">The transform of the object to be updated</param>
        public virtual void UpdateTransform(SteamVR_Input_Sources inputSource, Transform transformToUpdate)
        {
            sourceMap[inputSource].UpdateTransform(transformToUpdate);
        }

        /// <summary>The local position of this action relative to the universe origin</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public Vector3 GetLocalPosition(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].localPosition;
        }

        /// <summary>The local rotation of this action relative to the universe origin</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public Quaternion GetLocalRotation(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].localRotation;
        }

        /// <summary>The local velocity of this pose relative to the universe origin</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public Vector3 GetVelocity(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].velocity;
        }

        /// <summary>The local angular velocity of this pose relative to the universe origin</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public Vector3 GetAngularVelocity(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].angularVelocity;
        }

        /// <summary>True if the device bound to this action and input source is connected</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public bool GetDeviceIsConnected(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].deviceIsConnected;
        }

        /// <summary>True if the pose retrieved for this action and input source is valid (good data from the tracking source)</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public bool GetPoseIsValid(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].poseIsValid;
        }

        /// <summary>The state of the tracking system that is used to create pose data (position, rotation, etc)</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public ETrackingResult GetTrackingResult(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].trackingState;
        }



        /// <summary>The local position for this pose during the previous update</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public Vector3 GetLastLocalPosition(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].lastLocalPosition;
        }

        /// <summary>The local rotation for this pose during the previous update</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public Quaternion GetLastLocalRotation(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].lastLocalRotation;
        }

        /// <summary>The velocity for this pose during the previous update</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public Vector3 GetLastVelocity(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].lastVelocity;
        }

        /// <summary>The angular velocity for this pose during the previous update</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public Vector3 GetLastAngularVelocity(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].lastAngularVelocity;
        }

        /// <summary>True if the device bound to this action was connected during the previous update</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public bool GetLastDeviceIsConnected(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].lastDeviceIsConnected;
        }

        /// <summary>True if the pose was valid during the previous update</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public bool GetLastPoseIsValid(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].lastPoseIsValid;
        }

        /// <summary>The tracking state for this pose during the previous update</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public ETrackingResult GetLastTrackingResult(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].lastTrackingState;
        }
    }

    /// <summary>
    /// Boolean actions are either true or false. There is an onStateUp and onStateDown event for the rising and falling edge.
    /// </summary>
    public class SteamVR_Action_Pose_Source_Map<Source> : SteamVR_Action_In_Source_Map<Source>
        where Source : SteamVR_Action_Pose_Source, new()
    {
        /// <summary>
        /// Sets all pose (and skeleton) actions to use the specified universe origin without going through the sourcemap indexer
        /// </summary>
        public void SetTrackingUniverseOrigin(ETrackingUniverseOrigin newOrigin)
        {
            var sourceEnumerator = sources.GetEnumerator();
            while (sourceEnumerator.MoveNext())
            {
                var sourceElement = sourceEnumerator.Current;
                sourceElement.Value.universeOrigin = newOrigin;
            }
        }

        public virtual void UpdateValues(bool skipStateAndEventUpdates)
        {
            for (int sourceIndex = 0; sourceIndex < updatingSources.Count; sourceIndex++)
            {
                sources[updatingSources[sourceIndex]].UpdateValue(skipStateAndEventUpdates);
            }
        }
    }

    public class SteamVR_Action_Pose_Source : SteamVR_Action_In_Source, ISteamVR_Action_Pose
    {
        public ETrackingUniverseOrigin universeOrigin = ETrackingUniverseOrigin.TrackingUniverseRawAndUncalibrated;

        protected static uint poseActionData_size = 0;

        /// <summary>The distance the pose needs to move/rotate before a change is detected</summary>
        public float changeTolerance = Mathf.Epsilon;

        /// <summary>Event fires when the active state (ActionSet active and binding active) changes</summary>
        public event SteamVR_Action_Pose.ActiveChangeHandler onActiveChange;

        /// <summary>Event fires when the active state of the binding changes</summary>
        public event SteamVR_Action_Pose.ActiveChangeHandler onActiveBindingChange;

        /// <summary>Event fires when the orientation of the pose changes more than the changeTolerance</summary>
        public event SteamVR_Action_Pose.ChangeHandler onChange;

        /// <summary>Event fires when the action is updated</summary>
        public event SteamVR_Action_Pose.UpdateHandler onUpdate;

        /// <summary>Event fires when the state of the tracking system that is used to create pose data (position, rotation, etc) changes</summary>
        public event SteamVR_Action_Pose.TrackingChangeHandler onTrackingChanged;

        /// <summary>Event fires when the state of the pose data retrieved for this action changes validity (good/bad data from the tracking source)</summary>
        public event SteamVR_Action_Pose.ValidPoseChangeHandler onValidPoseChanged;

        /// <summary>Event fires when the device bound to this action is connected or disconnected</summary>
        public event SteamVR_Action_Pose.DeviceConnectedChangeHandler onDeviceConnectedChanged;
        
        

        /// <summary>True when the orientation of the pose has changhed more than changeTolerance in the last update. Note: Will only return true if the action is also active.</summary>
        public override bool changed { get; protected set; }

        /// <summary>The value of the action's 'changed' during the previous update</summary>
        public override bool lastChanged { get; protected set; }

        /// <summary>The handle to the origin of the component that was used to update this pose</summary>
        public override ulong activeOrigin
        {
            get
            {
                if (active)
                    return poseActionData.activeOrigin;

                return 0;
            }
        }

        /// <summary>The handle to the origin of the component that was used to update the value for this action (for the previous update)</summary>
        public override ulong lastActiveOrigin { get { return lastPoseActionData.activeOrigin; } }

        /// <summary>True if this action is bound and the ActionSet is active</summary>
        public override bool active { get { return activeBinding && action.actionSet.IsActive(inputSource); } }

        /// <summary>True if the action is bound</summary>
        public override bool activeBinding { get { return poseActionData.bActive; } }


        /// <summary>If the action was active (ActionSet active and binding active) during the last update</summary>
        public override bool lastActive { get; protected set; }

        /// <summary>If the action's binding was active during the previous update</summary>
        public override bool lastActiveBinding { get { return lastPoseActionData.bActive; } }

        /// <summary>The state of the tracking system that is used to create pose data (position, rotation, etc)</summary>
        public ETrackingResult trackingState { get { return poseActionData.pose.eTrackingResult; } }

        /// <summary>The tracking state for this pose during the previous update</summary>
        public ETrackingResult lastTrackingState { get { return lastPoseActionData.pose.eTrackingResult; } }

        /// <summary>True if the pose retrieved for this action and input source is valid (good data from the tracking source)</summary>
        public bool poseIsValid { get { return poseActionData.pose.bPoseIsValid; } }

        /// <summary>True if the pose was valid during the previous update</summary>
        public bool lastPoseIsValid { get { return lastPoseActionData.pose.bPoseIsValid; } }

        /// <summary>True if the device bound to this action and input source is connected</summary>
        public bool deviceIsConnected { get { return poseActionData.pose.bDeviceIsConnected; } }

        /// <summary>True if the device bound to this action was connected during the previous update</summary>
        public bool lastDeviceIsConnected { get { return lastPoseActionData.pose.bDeviceIsConnected; } }


        /// <summary>The local position of this action relative to the universe origin</summary>
        public Vector3 localPosition { get; protected set; }

        /// <summary>The local rotation of this action relative to the universe origin</summary>
        public Quaternion localRotation { get; protected set; }

        /// <summary>The local position for this pose during the previous update</summary>
        public Vector3 lastLocalPosition { get; protected set; }

        /// <summary>The local rotation for this pose during the previous update</summary>
        public Quaternion lastLocalRotation { get; protected set; }

        /// <summary>The local velocity of this pose relative to the universe origin</summary>
        public Vector3 velocity { get; protected set; }

        /// <summary>The velocity for this pose during the previous update</summary>
        public Vector3 lastVelocity { get; protected set; }

        /// <summary>The local angular velocity of this pose relative to the universe origin</summary>
        public Vector3 angularVelocity { get; protected set; }

        /// <summary>The angular velocity for this pose during the previous update</summary>
        public Vector3 lastAngularVelocity { get; protected set; }
        

        protected InputPoseActionData_t poseActionData = new InputPoseActionData_t();

        protected InputPoseActionData_t lastPoseActionData = new InputPoseActionData_t();

        protected InputPoseActionData_t tempPoseActionData = new InputPoseActionData_t();


        protected SteamVR_Action_Pose poseAction;

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> Sets up the internals of the action source before SteamVR has been initialized.
        /// </summary>
        public override void Preinitialize(SteamVR_Action wrappingAction, SteamVR_Input_Sources forInputSource)
        {
            base.Preinitialize(wrappingAction, forInputSource);
            poseAction = wrappingAction as SteamVR_Action_Pose;
        }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> 
        /// Initializes the handle for the inputSource, the pose action data size, and any other related SteamVR data.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            if (poseActionData_size == 0)
                poseActionData_size = (uint)Marshal.SizeOf(typeof(InputPoseActionData_t));
        }

        /// <summary><strong>[Should not be called by user code]</strong> 
        /// Updates the data for this action and this input source. Sends related events.
        /// </summary>
        public override void UpdateValue()
        {
            UpdateValue(false);
        }

        /// <summary><strong>[Should not be called by user code]</strong> 
        /// Updates the data for this action and this input source. Sends related events.
        /// </summary>
        public virtual void UpdateValue(bool skipStateAndEventUpdates)
        {
            lastChanged = changed;
            lastPoseActionData = poseActionData;
            lastLocalPosition = localPosition;
            lastLocalRotation = localRotation;
            lastVelocity = velocity;
            lastAngularVelocity = angularVelocity;

            EVRInputError err = OpenVR.Input.GetPoseActionDataForNextFrame(handle, universeOrigin, ref poseActionData, poseActionData_size, inputSourceHandle);
            if (err != EVRInputError.None)
            {
                Debug.LogError("<b>[SteamVR]</b> GetPoseActionData error (" + fullPath + "): " + err.ToString() + " Handle: " + handle.ToString() + ". Input source: " + inputSource.ToString());
            }

            if (active)
            {
                SetCacheVariables();
                changed = GetChanged();
            }

            if (changed)
                changedTime = updateTime;

            if (skipStateAndEventUpdates == false)
                CheckAndSendEvents();
        }

        protected void SetCacheVariables()
        {
            localPosition = SteamVR_Utils.GetPosition(poseActionData.pose.mDeviceToAbsoluteTracking);
            localRotation = SteamVR_Utils.GetRotation(poseActionData.pose.mDeviceToAbsoluteTracking);
            velocity = GetUnityCoordinateVelocity(poseActionData.pose.vVelocity);
            angularVelocity = GetUnityCoordinateAngularVelocity(poseActionData.pose.vAngularVelocity);
            updateTime = Time.realtimeSinceStartup;
        }

        protected bool GetChanged()
        {
            if (Vector3.Distance(localPosition, lastLocalPosition) > changeTolerance)
                return true;
            else if (Mathf.Abs(Quaternion.Angle(localRotation, lastLocalRotation)) > changeTolerance)
                return true;
            else if (Vector3.Distance(velocity, lastVelocity) > changeTolerance)
                return true;
            else if (Vector3.Distance(angularVelocity, lastAngularVelocity) > changeTolerance)
                return true;

            return false;
        }

        /// <summary>
        /// SteamVR keeps a log of past poses so you can retrieve old poses or estimated poses in the future by passing in a secondsFromNow value that is negative or positive.
        /// </summary>
        /// <param name="secondsFromNow">The time offset in the future (estimated) or in the past (previously recorded) you want to get data from</param>
        /// <returns>true if we successfully returned a pose</returns>
        public bool GetVelocitiesAtTimeOffset(float secondsFromNow, out Vector3 velocityAtTime, out Vector3 angularVelocityAtTime)
        {
            EVRInputError err = OpenVR.Input.GetPoseActionDataRelativeToNow(handle, universeOrigin, secondsFromNow, ref tempPoseActionData, poseActionData_size, inputSourceHandle);
            if (err != EVRInputError.None)
            {
                Debug.LogError("<b>[SteamVR]</b> GetPoseActionData error (" + fullPath + "): " + err.ToString() + " handle: " + handle.ToString()); //todo: this should be an error

                velocityAtTime = Vector3.zero;
                angularVelocityAtTime = Vector3.zero;
                return false;
            }

            velocityAtTime = GetUnityCoordinateVelocity(tempPoseActionData.pose.vVelocity);
            angularVelocityAtTime = GetUnityCoordinateAngularVelocity(tempPoseActionData.pose.vAngularVelocity);

            return true;
        }

        /// <summary>
        /// SteamVR keeps a log of past poses so you can retrieve old poses or estimated poses in the future by passing in a secondsFromNow value that is negative or positive.
        /// </summary>
        /// <param name="secondsFromNow">The time offset in the future (estimated) or in the past (previously recorded) you want to get data from</param>
        /// <returns>true if we successfully returned a pose</returns>
        public bool GetPoseAtTimeOffset(float secondsFromNow, out Vector3 positionAtTime, out Quaternion rotationAtTime, out Vector3 velocityAtTime, out Vector3 angularVelocityAtTime)
        {
            EVRInputError err = OpenVR.Input.GetPoseActionDataRelativeToNow(handle, universeOrigin, secondsFromNow, ref tempPoseActionData, poseActionData_size, inputSourceHandle);
            if (err != EVRInputError.None)
            {
                Debug.LogError("<b>[SteamVR]</b> GetPoseActionData error (" + fullPath + "): " + err.ToString() + " handle: " + handle.ToString()); //todo: this should be an error

                velocityAtTime = Vector3.zero;
                angularVelocityAtTime = Vector3.zero;
                positionAtTime = Vector3.zero;
                rotationAtTime = Quaternion.identity;
                return false;
            }

            velocityAtTime = GetUnityCoordinateVelocity(tempPoseActionData.pose.vVelocity);
            angularVelocityAtTime = GetUnityCoordinateAngularVelocity(tempPoseActionData.pose.vAngularVelocity);
            positionAtTime = SteamVR_Utils.GetPosition(tempPoseActionData.pose.mDeviceToAbsoluteTracking);
            rotationAtTime = SteamVR_Utils.GetRotation(tempPoseActionData.pose.mDeviceToAbsoluteTracking);

            return true;
        }

        /// <summary>
        /// Update a transform's local position and local roation to match the pose.
        /// </summary>
        /// <param name="transformToUpdate">The transform of the object to be updated</param>
        public void UpdateTransform(Transform transformToUpdate)
        {
            transformToUpdate.localPosition = localPosition;
            transformToUpdate.localRotation = localRotation;
        }
        
        protected virtual void CheckAndSendEvents()
        {
            if (trackingState != lastTrackingState && onTrackingChanged != null)
                onTrackingChanged.Invoke(poseAction, inputSource, trackingState);

            if (poseIsValid != lastPoseIsValid && onValidPoseChanged != null)
                onValidPoseChanged.Invoke(poseAction, inputSource, poseIsValid);

            if (deviceIsConnected != lastDeviceIsConnected && onDeviceConnectedChanged != null)
                onDeviceConnectedChanged.Invoke(poseAction, inputSource, deviceIsConnected);

            if (changed && onChange != null)
                onChange.Invoke(poseAction, inputSource);

            if (active != lastActive && onActiveChange != null)
                onActiveChange.Invoke(poseAction, inputSource, active);

            if (activeBinding != lastActiveBinding && onActiveBindingChange != null)
                onActiveBindingChange.Invoke(poseAction, inputSource, activeBinding);

            if (onUpdate != null)
                onUpdate.Invoke(poseAction, inputSource);
        }

        protected Vector3 GetUnityCoordinateVelocity(HmdVector3_t vector)
        {
            return GetUnityCoordinateVelocity(vector.v0, vector.v1, vector.v2);
        }

        protected Vector3 GetUnityCoordinateAngularVelocity(HmdVector3_t vector)
        {
            return GetUnityCoordinateAngularVelocity(vector.v0, vector.v1, vector.v2);
        }

        protected Vector3 GetUnityCoordinateVelocity(float x, float y, float z)
        {
            Vector3 vector = new Vector3();
            vector.x = x;
            vector.y = y;
            vector.z = -z;
            return vector;
        }

        protected Vector3 GetUnityCoordinateAngularVelocity(float x, float y, float z)
        {
            Vector3 vector = new Vector3();
            vector.x = -x;
            vector.y = -y;
            vector.z = z;
            return vector;
        }
    }

    /// <summary>
    /// Boolean actions are either true or false. There is an onStateUp and onStateDown event for the rising and falling edge.
    /// </summary>
    public interface ISteamVR_Action_Pose : ISteamVR_Action_In_Source
    {
        /// <summary>The local position of this action relative to the universe origin</summary>
        Vector3 localPosition { get; }

        /// <summary>The local rotation of this action relative to the universe origin</summary>
        Quaternion localRotation { get; }

        /// <summary>The state of the tracking system that is used to create pose data (position, rotation, etc)</summary>
        ETrackingResult trackingState { get; }

        /// <summary>The local velocity of this pose relative to the universe origin</summary>
        Vector3 velocity { get; }
        
        /// <summary>The local angular velocity of this pose relative to the universe origin</summary>
        Vector3 angularVelocity { get; }

        /// <summary>True if the pose retrieved for this action and input source is valid (good data from the tracking source)</summary>
        bool poseIsValid { get; }

        /// <summary>True if the device bound to this action and input source is connected</summary>
        bool deviceIsConnected { get; }


        /// <summary>The local position for this pose during the previous update</summary>
        Vector3 lastLocalPosition { get; }

        /// <summary>The local rotation for this pose during the previous update</summary>
        Quaternion lastLocalRotation { get; }

        /// <summary>The tracking state for this pose during the previous update</summary>
        ETrackingResult lastTrackingState { get; }

        /// <summary>The velocity for this pose during the previous update</summary>
        Vector3 lastVelocity { get; }

        /// <summary>The angular velocity for this pose during the previous update</summary>
        Vector3 lastAngularVelocity { get; }

        /// <summary>True if the pose was valid during the previous update</summary>
        bool lastPoseIsValid { get; }

        /// <summary>True if the device bound to this action was connected during the previous update</summary>
        bool lastDeviceIsConnected { get; }
    }
}