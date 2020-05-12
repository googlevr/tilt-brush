//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using UnityEngine;
using System.Collections;
using System;

using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;

namespace Valve.VR
{
    [Serializable]
    /// <summary>
    /// Skeleton Actions are our best approximation of where your hands are while holding vr controllers and pressing buttons. We give you 31 bones to help you animate hand models.
    /// For more information check out this blog post: https://steamcommunity.com/games/250820/announcements/detail/1690421280625220068
    /// </summary>
    public class SteamVR_Action_Skeleton : SteamVR_Action_Pose_Base<SteamVR_Action_Skeleton_Source_Map, SteamVR_Action_Skeleton_Source>, ISteamVR_Action_Skeleton_Source, ISerializationCallbackReceiver
    {
        public const int numBones = 31;

        public delegate void ActiveChangeHandler(SteamVR_Action_Skeleton fromAction, bool active);
        public delegate void ChangeHandler(SteamVR_Action_Skeleton fromAction);
        public delegate void UpdateHandler(SteamVR_Action_Skeleton fromAction);
        public delegate void TrackingChangeHandler(SteamVR_Action_Skeleton fromAction, ETrackingResult trackingState);
        public delegate void ValidPoseChangeHandler(SteamVR_Action_Skeleton fromAction, bool validPose);
        public delegate void DeviceConnectedChangeHandler(SteamVR_Action_Skeleton fromAction, bool deviceConnected);

        /// <summary>Event fires when the active state (ActionSet active and binding active) changes</summary>
        public event ActiveChangeHandler onActiveChange
        { add { sourceMap[SteamVR_Input_Sources.Any].onActiveChange += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onActiveChange -= value; } }

        /// <summary>Event fires when the active state of the binding changes</summary>
        public event ActiveChangeHandler onActiveBindingChange
        { add { sourceMap[SteamVR_Input_Sources.Any].onActiveBindingChange += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onActiveBindingChange -= value; } }

        /// <summary>Event fires when the state of the pose or bones moves more than the changeTolerance</summary>
        public event ChangeHandler onChange
        { add { sourceMap[SteamVR_Input_Sources.Any].onChange += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onChange -= value; } }

        /// <summary>Event fires when the action is updated</summary>
        public event UpdateHandler onUpdate
        { add { sourceMap[SteamVR_Input_Sources.Any].onUpdate += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onUpdate -= value; } }

        /// <summary>Event fires when the state of the tracking system that is used to create pose data (position, rotation, etc) changes</summary>
        public event TrackingChangeHandler onTrackingChanged
        { add { sourceMap[SteamVR_Input_Sources.Any].onTrackingChanged += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onTrackingChanged -= value; } }

        /// <summary>Event fires when the state of the pose data retrieved for this action changes validity (good/bad data from the tracking source)</summary>
        public event ValidPoseChangeHandler onValidPoseChanged
        { add { sourceMap[SteamVR_Input_Sources.Any].onValidPoseChanged += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onValidPoseChanged -= value; } }

        /// <summary>Event fires when the device bound to this action is connected or disconnected</summary>
        public event DeviceConnectedChangeHandler onDeviceConnectedChanged
        { add { sourceMap[SteamVR_Input_Sources.Any].onDeviceConnectedChanged += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onDeviceConnectedChanged -= value; } }

        public SteamVR_Action_Skeleton() { }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> 
        /// Updates the skeleton action data
        /// </summary>
        public virtual void UpdateValue(bool skipStateAndEventUpdates)
        {
            sourceMap[SteamVR_Input_Sources.Any].UpdateValue(skipStateAndEventUpdates);
        }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> 
        /// Updates the skeleton action data without firing events
        /// </summary>
        public void UpdateValueWithoutEvents()
        {
            sourceMap[SteamVR_Input_Sources.Any].UpdateValue(true);
        }

        /// <summary>
        /// Update a transform's local position and local roation to match the pose from the most recent update
        /// </summary>
        /// <param name="transformToUpdate">The transform of the object to be updated</param>
        public void UpdateTransform(Transform transformToUpdate)
        {
            base.UpdateTransform(SteamVR_Input_Sources.Any, transformToUpdate);
        }

        #region skeleton source properties
        /// <summary>An array of the positions of the bones from the most recent update. Relative to skeletalTransformSpace. See SteamVR_Skeleton_JointIndexes for bone indexes.</summary>
        public Vector3[] bonePositions { get { return sourceMap[SteamVR_Input_Sources.Any].bonePositions; } }

        /// <summary>An array of the rotations of the bones from the most recent update. Relative to skeletalTransformSpace. See SteamVR_Skeleton_JointIndexes for bone indexes.</summary>
        public Quaternion[] boneRotations { get { return sourceMap[SteamVR_Input_Sources.Any].boneRotations; } }

        /// <summary>From the previous update: An array of the positions of the bones from the most recent update. Relative to skeletalTransformSpace. See SteamVR_Skeleton_JointIndexes for bone indexes.</summary>
        public Vector3[] lastBonePositions { get { return sourceMap[SteamVR_Input_Sources.Any].lastBonePositions; } }

        /// <summary>From the previous update: An array of the rotations of the bones from the most recent update. Relative to skeletalTransformSpace. See SteamVR_Skeleton_JointIndexes for bone indexes.</summary>
        public Quaternion[] lastBoneRotations { get { return sourceMap[SteamVR_Input_Sources.Any].lastBoneRotations; } }

        /// <summary>The range of motion the we're using to get bone data from. With Controller being your hand while holding the controller.</summary>
        public EVRSkeletalMotionRange rangeOfMotion
        {
            get { return sourceMap[SteamVR_Input_Sources.Any].rangeOfMotion; }
            set { sourceMap[SteamVR_Input_Sources.Any].rangeOfMotion = value; }
        }

        /// <summary>The space to get bone data in. Parent space by default</summary>
        public EVRSkeletalTransformSpace skeletalTransformSpace
        {
            get { return sourceMap[SteamVR_Input_Sources.Any].skeletalTransformSpace; }
            set { sourceMap[SteamVR_Input_Sources.Any].skeletalTransformSpace = value; }
        }

        /// <summary>The type of summary data that will be retrieved by default. FromAnimation is smoothed data to based on the skeletal animation system. FromDevice is as recent from the device as we can get - may be different data from smoothed. </summary>
        public EVRSummaryType summaryDataType
        {
            get { return sourceMap[SteamVR_Input_Sources.Any].summaryDataType; }
            set { sourceMap[SteamVR_Input_Sources.Any].summaryDataType = value; }
        }

        /// <summary>
        /// Get the accuracy level of the skeletal tracking data. 
        /// <para/>* Estimated: Body part location can’t be directly determined by the device. Any skeletal pose provided by the device is estimated based on the active buttons, triggers, joysticks, or other input sensors. Examples include the Vive Controller and gamepads.
        /// <para/>* Partial: Body part location can be measured directly but with fewer degrees of freedom than the actual body part.Certain body part positions may be unmeasured by the device and estimated from other input data.Examples include Knuckles or gloves that only measure finger curl
        /// <para/>* Full: Body part location can be measured directly throughout the entire range of motion of the body part.Examples include hi-end mocap systems, or gloves that measure the rotation of each finger segment.
        /// </summary>
        public EVRSkeletalTrackingLevel skeletalTrackingLevel
        {
            get { return sourceMap[SteamVR_Input_Sources.Any].skeletalTrackingLevel; }
        }

        /// <summary>A 0-1 value representing how curled the thumb is. 0 being straight, 1 being fully curled.</summary>
        public float thumbCurl { get { return sourceMap[SteamVR_Input_Sources.Any].thumbCurl; } }

        /// <summary>A 0-1 value representing how curled the index finger is. 0 being straight, 1 being fully curled.</summary>
        public float indexCurl { get { return sourceMap[SteamVR_Input_Sources.Any].indexCurl; } }

        /// <summary>A 0-1 value representing how curled the middle finger is. 0 being straight, 1 being fully curled.</summary>
        public float middleCurl { get { return sourceMap[SteamVR_Input_Sources.Any].middleCurl; } }

        /// <summary>A 0-1 value representing how curled the ring finger is. 0 being straight, 1 being fully curled.</summary>
        public float ringCurl { get { return sourceMap[SteamVR_Input_Sources.Any].ringCurl; } }

        /// <summary>A 0-1 value representing how curled the pinky finger is. 0 being straight, 1 being fully curled.</summary>
        public float pinkyCurl { get { return sourceMap[SteamVR_Input_Sources.Any].pinkyCurl; } }

        /// <summary>A 0-1 value representing the size of the gap between the thumb and index fingers</summary>
        public float thumbIndexSplay { get { return sourceMap[SteamVR_Input_Sources.Any].thumbIndexSplay; } }

        /// <summary>A 0-1 value representing the size of the gap between the index and middle fingers</summary>
        public float indexMiddleSplay { get { return sourceMap[SteamVR_Input_Sources.Any].indexMiddleSplay; } }

        /// <summary>A 0-1 value representing the size of the gap between the middle and ring fingers</summary>
        public float middleRingSplay { get { return sourceMap[SteamVR_Input_Sources.Any].middleRingSplay; } }

        /// <summary>A 0-1 value representing the size of the gap between the ring and pinky fingers</summary>
        public float ringPinkySplay { get { return sourceMap[SteamVR_Input_Sources.Any].ringPinkySplay; } }


        /// <summary>[Previous Update] A 0-1 value representing how curled the thumb is. 0 being straight, 1 being fully curled.</summary>
        public float lastThumbCurl { get { return sourceMap[SteamVR_Input_Sources.Any].lastThumbCurl; } }

        /// <summary>[Previous Update] A 0-1 value representing how curled the index finger is. 0 being straight, 1 being fully curled.</summary>
        public float lastIndexCurl { get { return sourceMap[SteamVR_Input_Sources.Any].lastIndexCurl; } }

        /// <summary>[Previous Update] A 0-1 value representing how curled the middle finger is. 0 being straight, 1 being fully curled.</summary>
        public float lastMiddleCurl { get { return sourceMap[SteamVR_Input_Sources.Any].lastMiddleCurl; } }

        /// <summary>[Previous Update] A 0-1 value representing how curled the ring finger is. 0 being straight, 1 being fully curled.</summary>
        public float lastRingCurl { get { return sourceMap[SteamVR_Input_Sources.Any].lastRingCurl; } }

        /// <summary>[Previous Update] A 0-1 value representing how curled the pinky finger is. 0 being straight, 1 being fully curled.</summary>
        public float lastPinkyCurl { get { return sourceMap[SteamVR_Input_Sources.Any].lastPinkyCurl; } }

        /// <summary>[Previous Update] A 0-1 value representing the size of the gap between the thumb and index fingers</summary>
        public float lastThumbIndexSplay { get { return sourceMap[SteamVR_Input_Sources.Any].lastThumbIndexSplay; } }

        /// <summary>[Previous Update] A 0-1 value representing the size of the gap between the index and middle fingers</summary>
        public float lastIndexMiddleSplay { get { return sourceMap[SteamVR_Input_Sources.Any].lastIndexMiddleSplay; } }

        /// <summary>[Previous Update] A 0-1 value representing the size of the gap between the middle and ring fingers</summary>
        public float lastMiddleRingSplay { get { return sourceMap[SteamVR_Input_Sources.Any].lastMiddleRingSplay; } }

        /// <summary>[Previous Update] A 0-1 value representing the size of the gap between the ring and pinky fingers</summary>
        public float lastRingPinkySplay { get { return sourceMap[SteamVR_Input_Sources.Any].lastRingPinkySplay; } }

        /// <summary>0-1 values representing how curled the specified finger is. 0 being straight, 1 being fully curled. For indexes see: SteamVR_Skeleton_FingerIndexes</summary>
        public float[] fingerCurls { get { return sourceMap[SteamVR_Input_Sources.Any].fingerCurls; } }

        /// <summary>0-1 values representing how splayed the specified finger and it's next index'd finger is. For indexes see: SteamVR_Skeleton_FingerIndexes</summary>
        public float[] fingerSplays { get { return sourceMap[SteamVR_Input_Sources.Any].fingerSplays; } }

        /// <summary>[Previous Update] 0-1 values representing how curled the specified finger is. 0 being straight, 1 being fully curled. For indexes see: SteamVR_Skeleton_FingerIndexes</summary>
        public float[] lastFingerCurls { get { return sourceMap[SteamVR_Input_Sources.Any].lastFingerCurls; } }

        /// <summary>[Previous Update] 0-1 values representing how splayed the specified finger and it's next index'd finger is. For indexes see: SteamVR_Skeleton_FingerIndexes</summary>
        public float[] lastFingerSplays { get { return sourceMap[SteamVR_Input_Sources.Any].lastFingerSplays; } }

        /// <summary>Separate from "changed". If the pose for this skeleton action has changed (root position/rotation)</summary>
        public bool poseChanged { get { return sourceMap[SteamVR_Input_Sources.Any].poseChanged; } }

        /// <summary>Skips processing the full per bone data and only does the summary data</summary>
        public bool onlyUpdateSummaryData { get { return sourceMap[SteamVR_Input_Sources.Any].onlyUpdateSummaryData; } set { sourceMap[SteamVR_Input_Sources.Any].onlyUpdateSummaryData = value; } }
        #endregion  

        #region pose functions with SteamVR_Input_Sources.Any

        /// <summary>True if this action is bound and the ActionSet is active</summary>
        public bool GetActive()
        {
            return sourceMap[SteamVR_Input_Sources.Any].active;
        }

        /// <summary>True if the ActionSet that contains this action is active</summary>
        public bool GetSetActive()
        {
            return actionSet.IsActive(SteamVR_Input_Sources.Any);
        }

        /// <summary>
        /// SteamVR keeps a log of past poses so you can retrieve old poses or estimated poses in the future by passing in a secondsFromNow value that is negative or positive.
        /// </summary>
        /// <param name="secondsFromNow">The time offset in the future (estimated) or in the past (previously recorded) you want to get data from</param>
        /// <returns>true if we successfully returned a pose</returns>
        public bool GetVelocitiesAtTimeOffset(float secondsFromNow, out Vector3 velocity, out Vector3 angularVelocity)
        {
            return sourceMap[SteamVR_Input_Sources.Any].GetVelocitiesAtTimeOffset(secondsFromNow, out velocity, out angularVelocity);
        }

        /// <summary>
        /// SteamVR keeps a log of past poses so you can retrieve old poses or estimated poses in the future by passing in a secondsFromNow value that is negative or positive.
        /// </summary>
        /// <param name="secondsFromNow">The time offset in the future (estimated) or in the past (previously recorded) you want to get data from</param>
        /// <returns>true if we successfully returned a pose</returns>
        public bool GetPoseAtTimeOffset(float secondsFromNow, out Vector3 position, out Quaternion rotation, out Vector3 velocity, out Vector3 angularVelocity)
        {
            return sourceMap[SteamVR_Input_Sources.Any].GetPoseAtTimeOffset(secondsFromNow, out position, out rotation, out velocity, out angularVelocity);
        }

        /// <summary>The local position of the pose relative to the universe origin</summary>
        public Vector3 GetLocalPosition()
        {
            return sourceMap[SteamVR_Input_Sources.Any].localPosition;
        }

        /// <summary>The local rotation of the pose relative to the universe origin</summary>
        public Quaternion GetLocalRotation()
        {
            return sourceMap[SteamVR_Input_Sources.Any].localRotation;
        }

        /// <summary>The local velocity of the pose relative to the universe origin</summary>
        public Vector3 GetVelocity()
        {
            return sourceMap[SteamVR_Input_Sources.Any].velocity;
        }

        /// <summary>The local angular velocity of the pose relative to the universe origin</summary>
        public Vector3 GetAngularVelocity()
        {
            return sourceMap[SteamVR_Input_Sources.Any].angularVelocity;
        }

        /// <summary>True if the device bound to this action is connected</summary>
        public bool GetDeviceIsConnected()
        {
            return sourceMap[SteamVR_Input_Sources.Any].deviceIsConnected;
        }

        /// <summary>True if the pose retrieved for this action is valid (good data from the tracking source)</summary>
        public bool GetPoseIsValid()
        {
            return sourceMap[SteamVR_Input_Sources.Any].poseIsValid;
        }

        /// <summary>The state of the tracking system that is used to create pose data (position, rotation, etc)</summary>
        public ETrackingResult GetTrackingResult()
        {
            return sourceMap[SteamVR_Input_Sources.Any].trackingState;
        }



        /// <summary>The last local position of the pose relative to the universe origin</summary>
        public Vector3 GetLastLocalPosition()
        {
            return sourceMap[SteamVR_Input_Sources.Any].lastLocalPosition;
        }

        /// <summary>The last local rotation of the pose relative to the universe origin</summary>
        public Quaternion GetLastLocalRotation()
        {
            return sourceMap[SteamVR_Input_Sources.Any].lastLocalRotation;
        }

        /// <summary>The last local velocity of the pose relative to the universe origin</summary>
        public Vector3 GetLastVelocity()
        {
            return sourceMap[SteamVR_Input_Sources.Any].lastVelocity;
        }

        /// <summary>The last local angular velocity of the pose relative to the universe origin</summary>
        public Vector3 GetLastAngularVelocity()
        {
            return sourceMap[SteamVR_Input_Sources.Any].lastAngularVelocity;
        }

        /// <summary>True if the device bound to this action was connected during the previous update</summary>
        public bool GetLastDeviceIsConnected()
        {
            return sourceMap[SteamVR_Input_Sources.Any].lastDeviceIsConnected;
        }

        /// <summary>True if the pose was valid during the previous update</summary>
        public bool GetLastPoseIsValid()
        {
            return sourceMap[SteamVR_Input_Sources.Any].lastPoseIsValid;
        }

        /// <summary>The tracking state for this pose during the previous update</summary>
        public ETrackingResult GetLastTrackingResult()
        {
            return sourceMap[SteamVR_Input_Sources.Any].lastTrackingState;
        }
        #endregion

        /// <summary>
        /// The number of bones in the skeleton for this action
        /// </summary>
        public int boneCount { get { return (int)GetBoneCount(); } }

        /// <summary>
        /// Gets the bone positions in local space. This array may be modified later so if you want to hold this data then pass true to get a copy of the data instead of the actual array
        /// </summary>
        /// <param name="copy">This array may be modified later so if you want to hold this data then pass true to get a copy of the data instead of the actual array</param>
        public Vector3[] GetBonePositions(bool copy = false)
        {
            if (copy)
                return (Vector3[])sourceMap[SteamVR_Input_Sources.Any].bonePositions.Clone();
            
            return sourceMap[SteamVR_Input_Sources.Any].bonePositions;
        }

        /// <summary>
        /// Gets the bone rotations in local space. This array may be modified later so if you want to hold this data then pass true to get a copy of the data instead of the actual array
        /// </summary>
        /// <param name="copy">This array may be modified later so if you want to hold this data then pass true to get a copy of the data instead of the actual array</param>
        public Quaternion[] GetBoneRotations(bool copy = false)
        {
            if (copy)
                return (Quaternion[])sourceMap[SteamVR_Input_Sources.Any].boneRotations.Clone();

            return sourceMap[SteamVR_Input_Sources.Any].boneRotations;
        }

        /// <summary>
        /// Gets the bone positions in local space from the previous update. This array may be modified later so if you want to hold this data then pass true to get a copy of the data instead of the actual array
        /// </summary>
        /// <param name="copy">This array may be modified later so if you want to hold this data then pass true to get a copy of the data instead of the actual array</param>
        public Vector3[] GetLastBonePositions(bool copy = false)
        {
            if (copy)
                return (Vector3[])sourceMap[SteamVR_Input_Sources.Any].lastBonePositions.Clone();

            return sourceMap[SteamVR_Input_Sources.Any].lastBonePositions;
        }

        /// <summary>
        /// Gets the bone rotations in local space from the previous update. This array may be modified later so if you want to hold this data then pass true to get a copy of the data instead of the actual array
        /// </summary>
        /// <param name="copy">This array may be modified later so if you want to hold this data then pass true to get a copy of the data instead of the actual array</param>
        public Quaternion[] GetLastBoneRotations(bool copy = false)
        {
            if (copy)
                return (Quaternion[])sourceMap[SteamVR_Input_Sources.Any].lastBoneRotations.Clone();

            return sourceMap[SteamVR_Input_Sources.Any].lastBoneRotations;
        }

        /// <summary>
        /// Set the range of the motion of the bones in this skeleton. Options are "With Controller" as if your hand is holding your VR controller. 
        /// Or "Without Controller" as if your hand is empty. This will set the range for the following update.
        /// </summary>
        public void SetRangeOfMotion(EVRSkeletalMotionRange range)
        {
            sourceMap[SteamVR_Input_Sources.Any].rangeOfMotion = range;
        }

        /// <summary>
        /// Sets the space that you'll get bone data back in. Options are relative to the Model and relative to the Parent bone
        /// </summary>
        /// <param name="space">the space that you'll get bone data back in. Options are relative to the Model and relative to the Parent bone.</param>
        public void SetSkeletalTransformSpace(EVRSkeletalTransformSpace space)
        {
            sourceMap[SteamVR_Input_Sources.Any].skeletalTransformSpace = space;
        }

        /// <summary>
        /// Returns the total number of bones in the skeleton
        /// </summary>
        public uint GetBoneCount()
        {
            return sourceMap[SteamVR_Input_Sources.Any].GetBoneCount();
        }

        /// <summary>
        /// Returns the order of bones in the hierarchy
        /// </summary>
        public int[] GetBoneHierarchy()
        {
            return sourceMap[SteamVR_Input_Sources.Any].GetBoneHierarchy();
        }

        /// <summary>
        /// Returns the name of the bone
        /// </summary>
        public string GetBoneName(int boneIndex)
        {
            return sourceMap[SteamVR_Input_Sources.Any].GetBoneName(boneIndex);
        }

        /// <summary>
        /// Returns an array of positions/rotations that represent the state of each bone in a reference pose.
        /// </summary>
        /// <param name="transformSpace">What to get the position/rotation data relative to, the model, or the bone's parent</param>
        /// <param name="referencePose">Which reference pose to return</param>
        /// <returns></returns>
        public SteamVR_Utils.RigidTransform[] GetReferenceTransforms(EVRSkeletalTransformSpace transformSpace, EVRSkeletalReferencePose referencePose)
        {
            return sourceMap[SteamVR_Input_Sources.Any].GetReferenceTransforms(transformSpace, referencePose);
        }

        /// <summary>
        /// Get the accuracy level of the skeletal tracking data. 
        /// </summary>
        /// <returns>
        /// <list type="bullet">
        /// <item><description>Estimated: Body part location can’t be directly determined by the device. Any skeletal pose provided by the device is estimated based on the active buttons, triggers, joysticks, or other input sensors. Examples include the Vive Controller and gamepads. </description></item>
        /// <item><description>Partial: Body part location can be measured directly but with fewer degrees of freedom than the actual body part.Certain body part positions may be unmeasured by the device and estimated from other input data.Examples include Knuckles or gloves that only measure finger curl</description></item>
        /// <item><description>Full: Body part location can be measured directly throughout the entire range of motion of the body part.Examples include hi-end mocap systems, or gloves that measure the rotation of each finger segment.</description></item>
        /// </list>
        /// </returns>
        public EVRSkeletalTrackingLevel GetSkeletalTrackingLevel()
        {
            return sourceMap[SteamVR_Input_Sources.Any].GetSkeletalTrackingLevel();
        }

        /// <summary>
        /// Returns the finger curl data that we calculate each update. This array may be modified later so if you want to hold this data then pass true to get a copy of the data instead of the actual array
        /// </summary>
        /// <param name="copy">This array may be modified later so if you want to hold this data then pass true to get a copy of the data instead of the actual array</param>
        public float[] GetFingerCurls(bool copy = false)
        {
            if (copy)
                return (float[])sourceMap[SteamVR_Input_Sources.Any].fingerCurls.Clone();
            else
                return sourceMap[SteamVR_Input_Sources.Any].fingerCurls;
        }

        /// <summary>
        /// Returns the finger curl data from the previous update. This array may be modified later so if you want to hold this data then pass true to get a copy of the data instead of the actual array
        /// </summary>
        /// <param name="copy">This array may be modified later so if you want to hold this data then pass true to get a copy of the data instead of the actual array</param>
        public float[] GetLastFingerCurls(bool copy = false)
        {
            if (copy)
                return (float[])sourceMap[SteamVR_Input_Sources.Any].lastFingerCurls.Clone();
            else
                return sourceMap[SteamVR_Input_Sources.Any].lastFingerCurls;
        }

        /// <summary>
        /// Returns the finger splay data that we calculate each update. This array may be modified later so if you want to hold this data then pass true to get a copy of the data instead of the actual array
        /// </summary>
        /// <param name="copy">This array may be modified later so if you want to hold this data then pass true to get a copy of the data instead of the actual array</param>
        public float[] GetFingerSplays(bool copy = false)
        {
            if (copy)
                return (float[])sourceMap[SteamVR_Input_Sources.Any].fingerSplays.Clone();
            else
                return sourceMap[SteamVR_Input_Sources.Any].fingerSplays;
        }

        /// <summary>
        /// Returns the finger splay data from the previous update. This array may be modified later so if you want to hold this data then pass true to get a copy of the data instead of the actual array
        /// </summary>
        /// <param name="copy">This array may be modified later so if you want to hold this data then pass true to get a copy of the data instead of the actual array</param>
        public float[] GetLastFingerSplays(bool copy = false)
        {
            if (copy)
                return (float[])sourceMap[SteamVR_Input_Sources.Any].lastFingerSplays.Clone();
            else
                return sourceMap[SteamVR_Input_Sources.Any].lastFingerSplays;
        }

        /// <summary>
        /// Returns a value indicating how much the passed in finger is currently curled.
        /// </summary>
        /// <param name="finger">The index of the finger to return a curl value for. 0-4. thumb, index, middle, ring, pinky</param>
        /// <returns>0-1 value. 0 being straight, 1 being fully curled.</returns>
        public float GetFingerCurl(int finger)
        {
            return sourceMap[SteamVR_Input_Sources.Any].fingerCurls[finger];
        }

        /// <summary>
        /// Returns a value indicating how the size of the gap between fingers.
        /// </summary>
        /// <param name="fingerGapIndex">The index of the finger gap to return a splay value for. 0 being the gap between thumb and index, 1 being the gap between index and middle, 2 being the gap between middle and ring, and 3 being the gap between ring and pinky.</param>
        /// <returns>0-1 value. 0 being no gap, 1 being "full" gap</returns>
        public float GetSplay(int fingerGapIndex)
        {
            return sourceMap[SteamVR_Input_Sources.Any].fingerSplays[fingerGapIndex];
        }

        /// <summary>
        /// Returns a value indicating how much the passed in finger is currently curled.
        /// </summary>
        /// <param name="finger">The finger to return a curl value for</param>
        /// <returns>0-1 value. 0 being straight, 1 being fully curled.</returns>
        public float GetFingerCurl(SteamVR_Skeleton_FingerIndexEnum finger)
        {
            return GetFingerCurl((int)finger);
        }

        /// <summary>
        /// Returns a value indicating how the size of the gap between fingers.
        /// </summary>
        /// <param name="fingerGapIndex">The finger gap to return a splay value for.</param>
        /// <returns>0-1 value. 0 being no gap, 1 being "full" gap</returns>
        public float GetSplay(SteamVR_Skeleton_FingerSplayIndexEnum fingerSplay)
        {
            return GetSplay((int)fingerSplay);
        }

        /// <summary>
        /// Returns a value indicating how much the passed in finger was curled during the previous update
        /// </summary>
        /// <param name="finger">The index of the finger to return a curl value for. 0-4. thumb, index, middle, ring, pinky</param>
        /// <returns>0-1 value. 0 being straight, 1 being fully curled.</returns>
        public float GetLastFingerCurl(int finger)
        {
            return sourceMap[SteamVR_Input_Sources.Any].lastFingerCurls[finger];
        }

        /// <summary>
        /// Returns a value indicating the size of the gap between fingers during the previous update
        /// </summary>
        /// <param name="fingerGapIndex">The index of the finger gap to return a splay value for. 0 being the gap between thumb and index, 1 being the gap between index and middle, 2 being the gap between middle and ring, and 3 being the gap between ring and pinky.</param>
        /// <returns>0-1 value. 0 being no gap, 1 being "full" gap</returns>
        public float GetLastSplay(int fingerGapIndex)
        {
            return sourceMap[SteamVR_Input_Sources.Any].lastFingerSplays[fingerGapIndex];
        }

        /// <summary>
        /// Returns a value indicating how much the passed in finger was curled during the previous update
        /// </summary>
        /// <param name="finger">The finger to return a curl value for</param>
        /// <returns>0-1 value. 0 being straight, 1 being fully curled.</returns>
        public float GetLastFingerCurl(SteamVR_Skeleton_FingerIndexEnum finger)
        {
            return GetLastFingerCurl((int)finger);
        }

        /// <summary>
        /// Returns a value indicating the size of the gap between fingers during the previous update
        /// </summary>
        /// <param name="fingerGapIndex">The finger gap to return a splay value for. </param>
        /// <returns>0-1 value. 0 being no gap, 1 being "full" gap</returns>
        public float GetLastSplay(SteamVR_Skeleton_FingerSplayIndexEnum fingerSplay)
        {
            return GetLastSplay((int)fingerSplay);
        }


        /// <summary>
        /// Gets the localized name of the device that the action corresponds to. Include as many EVRInputStringBits as you want to add to the localized string
        /// </summary>
        /// <param name="localizedParts">
        /// <list type="bullet">
        /// <item><description>VRInputString_Hand - Which hand the origin is in. ex: "Left Hand". </description></item>
        /// <item><description>VRInputString_ControllerType - What kind of controller the user has in that hand. ex: "Vive Controller". </description></item>
        /// <item><description>VRInputString_InputSource - What part of that controller is the origin. ex: "Trackpad". </description></item>
        /// <item><description>VRInputString_All - All of the above. ex: "Left Hand Vive Controller Trackpad". </description></item>
        /// </list>
        /// </param>
        public string GetLocalizedName(params EVRInputStringBits[] localizedParts)
        {
            return sourceMap[SteamVR_Input_Sources.Any].GetLocalizedOriginPart(localizedParts);
        }



        /// <summary>Fires an event when a device is connected or disconnected.</summary>
        /// <param name="functionToCall">The method you would like to be called when a device is connected. Should take a SteamVR_Action_Pose as a param</param>
        public void AddOnDeviceConnectedChanged(DeviceConnectedChangeHandler functionToCall)
        {
            sourceMap[SteamVR_Input_Sources.Any].onDeviceConnectedChanged += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="functionToStopCalling">The method you would like to stop calling when a device is connected. Should take a SteamVR_Action_Pose as a param</param>
        public void RemoveOnDeviceConnectedChanged(DeviceConnectedChangeHandler functionToStopCalling)
        {
            sourceMap[SteamVR_Input_Sources.Any].onDeviceConnectedChanged -= functionToStopCalling;
        }


        /// <summary>Fires an event when the tracking of the device has changed</summary>
        /// <param name="functionToCall">The method you would like to be called when tracking has changed. Should take a SteamVR_Action_Pose as a param</param>
        public void AddOnTrackingChanged(TrackingChangeHandler functionToCall)
        {
            sourceMap[SteamVR_Input_Sources.Any].onTrackingChanged += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="functionToStopCalling">The method you would like to stop calling when tracking has changed. Should take a SteamVR_Action_Pose as a param</param>
        public void RemoveOnTrackingChanged(TrackingChangeHandler functionToStopCalling)
        {
            sourceMap[SteamVR_Input_Sources.Any].onTrackingChanged -= functionToStopCalling;
        }


        /// <summary>Fires an event when the device now has a valid pose or no longer has a valid pose</summary>
        /// <param name="functionToCall">The method you would like to be called when the pose has become valid or invalid. Should take a SteamVR_Action_Pose as a param</param>
        public void AddOnValidPoseChanged(ValidPoseChangeHandler functionToCall)
        {
            sourceMap[SteamVR_Input_Sources.Any].onValidPoseChanged += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="functionToStopCalling">The method you would like to stop calling when the pose has become valid or invalid. Should take a SteamVR_Action_Pose as a param</param>
        public void RemoveOnValidPoseChanged(ValidPoseChangeHandler functionToStopCalling)
        {
            sourceMap[SteamVR_Input_Sources.Any].onValidPoseChanged -= functionToStopCalling;
        }


        /// <summary>Executes a function when this action's bound state changes</summary>
        public void AddOnActiveChangeListener(ActiveChangeHandler functionToCall)
        {
            sourceMap[SteamVR_Input_Sources.Any].onActiveChange += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="functionToStopCalling">The local function that you've setup to receive update events</param>
        public void RemoveOnActiveChangeListener(ActiveChangeHandler functionToStopCalling)
        {
            sourceMap[SteamVR_Input_Sources.Any].onActiveChange -= functionToStopCalling;
        }

        /// <summary>Executes a function when the state of this action changes</summary>
        /// <param name="functionToCall">A local function that receives the boolean action who's state has changed, the corresponding input source, and the new value</param>
        public void AddOnChangeListener(ChangeHandler functionToCall)
        {
            sourceMap[SteamVR_Input_Sources.Any].onChange += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="functionToStopCalling">The local function that you've setup to receive on change events</param>
        public void RemoveOnChangeListener(ChangeHandler functionToStopCalling)
        {
            sourceMap[SteamVR_Input_Sources.Any].onChange -= functionToStopCalling;
        }

        /// <summary>Executes a function when the state of this action is updated.</summary>
        /// <param name="functionToCall">A local function that receives the boolean action who's state has changed, the corresponding input source, and the new value</param>
        public void AddOnUpdateListener(UpdateHandler functionToCall)
        {
            sourceMap[SteamVR_Input_Sources.Any].onUpdate += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="functionToStopCalling">The local function that you've setup to receive update events</param>
        public void RemoveOnUpdateListener(UpdateHandler functionToStopCalling)
        {
            sourceMap[SteamVR_Input_Sources.Any].onUpdate -= functionToStopCalling;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            InitAfterDeserialize();
        }

        public static Quaternion steamVRFixUpRotation = Quaternion.AngleAxis(Mathf.PI * Mathf.Rad2Deg, Vector3.up);
    }
    
    public class SteamVR_Action_Skeleton_Source_Map : SteamVR_Action_Pose_Source_Map<SteamVR_Action_Skeleton_Source>
    {
        protected override SteamVR_Action_Skeleton_Source GetSourceElementForIndexer(SteamVR_Input_Sources inputSource)
        {
            return sources[SteamVR_Input_Sources.Any]; //just in case somebody tries to access a different element, redirect them to the correct one.
        }
    }

    /// <summary>
    /// Skeleton Actions are our best approximation of where your hands are while holding vr controllers and pressing buttons. We give you 31 bones to help you animate hand models.
    /// For more information check out this blog post: https://steamcommunity.com/games/250820/announcements/detail/1690421280625220068
    /// </summary>
    public class SteamVR_Action_Skeleton_Source : SteamVR_Action_Pose_Source, ISteamVR_Action_Skeleton_Source
    {
        protected static uint skeletonActionData_size = 0;

        /// <summary>Event fires when the active state (ActionSet active and binding active) changes</summary>
        public new event SteamVR_Action_Skeleton.ActiveChangeHandler onActiveChange;

        /// <summary>Event fires when the active state of the binding changes</summary>
        public new event SteamVR_Action_Skeleton.ActiveChangeHandler onActiveBindingChange;

        /// <summary>Event fires when the orientation of the pose or bones changes more than the changeTolerance</summary>
        public new event SteamVR_Action_Skeleton.ChangeHandler onChange;

        /// <summary>Event fires when the action is updated</summary>
        public new event SteamVR_Action_Skeleton.UpdateHandler onUpdate;

        /// <summary>Event fires when the state of the tracking system that is used to create pose data (position, rotation, etc) changes</summary>
        public new event SteamVR_Action_Skeleton.TrackingChangeHandler onTrackingChanged;

        /// <summary>Event fires when the state of the pose data retrieved for this action changes validity (good/bad data from the tracking source)</summary>
        public new event SteamVR_Action_Skeleton.ValidPoseChangeHandler onValidPoseChanged;

        /// <summary>Event fires when the device bound to this action is connected or disconnected</summary>
        public new event SteamVR_Action_Skeleton.DeviceConnectedChangeHandler onDeviceConnectedChanged;


        /// <summary>True if the action is bound</summary>
        public override bool activeBinding { get { return skeletonActionData.bActive; } }

        /// <summary>True if the action's binding was active during the previous update</summary>
        public override bool lastActiveBinding { get { return lastSkeletonActionData.bActive; } }

        /// <summary>An array of the positions of the bones from the most recent update. Relative to skeletalTransformSpace. See SteamVR_Skeleton_JointIndexes for bone indexes.</summary>
        public Vector3[] bonePositions { get; protected set; }

        /// <summary>An array of the rotations of the bones from the most recent update. Relative to skeletalTransformSpace. See SteamVR_Skeleton_JointIndexes for bone indexes.</summary>
        public Quaternion[] boneRotations { get; protected set; }

        /// <summary>From the previous update: An array of the positions of the bones from the most recent update. Relative to skeletalTransformSpace. See SteamVR_Skeleton_JointIndexes for bone indexes.</summary>
        public Vector3[] lastBonePositions { get; protected set; }

        /// <summary>From the previous update: An array of the rotations of the bones from the most recent update. Relative to skeletalTransformSpace. See SteamVR_Skeleton_JointIndexes for bone indexes.</summary>
        public Quaternion[] lastBoneRotations { get; protected set; }


        /// <summary>The range of motion the we're using to get bone data from. With Controller being your hand while holding the controller.</summary>
        public EVRSkeletalMotionRange rangeOfMotion { get; set; }

        /// <summary>The space to get bone data in. Parent space by default</summary>
        public EVRSkeletalTransformSpace skeletalTransformSpace { get; set; }


        /// <summary>The type of summary data that will be retrieved by default. FromAnimation is smoothed data to based on the skeletal animation system. FromDevice is as recent from the device as we can get - may be different data from smoothed. </summary>
        public EVRSummaryType summaryDataType { get; set; }


        /// <summary>A 0-1 value representing how curled the thumb is. 0 being straight, 1 being fully curled.</summary>
        public float thumbCurl { get { return fingerCurls[SteamVR_Skeleton_FingerIndexes.thumb]; } }

        /// <summary>A 0-1 value representing how curled the index finger is. 0 being straight, 1 being fully curled.</summary>
        public float indexCurl { get { return fingerCurls[SteamVR_Skeleton_FingerIndexes.index]; } }

        /// <summary>A 0-1 value representing how curled the middle finger is. 0 being straight, 1 being fully curled.</summary>
        public float middleCurl { get { return fingerCurls[SteamVR_Skeleton_FingerIndexes.middle]; } }

        /// <summary>A 0-1 value representing how curled the ring finger is. 0 being straight, 1 being fully curled.</summary>
        public float ringCurl { get { return fingerCurls[SteamVR_Skeleton_FingerIndexes.ring]; } }

        /// <summary>A 0-1 value representing how curled the pinky finger is. 0 being straight, 1 being fully curled.</summary>
        public float pinkyCurl { get { return fingerCurls[SteamVR_Skeleton_FingerIndexes.pinky]; } }


        /// <summary>A 0-1 value representing the size of the gap between the thumb and index fingers</summary>
        public float thumbIndexSplay { get { return fingerSplays[SteamVR_Skeleton_FingerSplayIndexes.thumbIndex]; } }

        /// <summary>A 0-1 value representing the size of the gap between the index and middle fingers</summary>
        public float indexMiddleSplay { get { return fingerSplays[SteamVR_Skeleton_FingerSplayIndexes.indexMiddle]; } }

        /// <summary>A 0-1 value representing the size of the gap between the middle and ring fingers</summary>
        public float middleRingSplay { get { return fingerSplays[SteamVR_Skeleton_FingerSplayIndexes.middleRing]; } }

        /// <summary>A 0-1 value representing the size of the gap between the ring and pinky fingers</summary>
        public float ringPinkySplay { get { return fingerSplays[SteamVR_Skeleton_FingerSplayIndexes.ringPinky]; } }


        /// <summary>[Previous Update] A 0-1 value representing how curled the thumb is. 0 being straight, 1 being fully curled.</summary>
        public float lastThumbCurl { get { return lastFingerCurls[SteamVR_Skeleton_FingerIndexes.thumb]; } }

        /// <summary>[Previous Update] A 0-1 value representing how curled the index finger is. 0 being straight, 1 being fully curled.</summary>
        public float lastIndexCurl { get { return lastFingerCurls[SteamVR_Skeleton_FingerIndexes.index]; } }

        /// <summary>[Previous Update] A 0-1 value representing how curled the middle finger is. 0 being straight, 1 being fully curled.</summary>
        public float lastMiddleCurl { get { return lastFingerCurls[SteamVR_Skeleton_FingerIndexes.middle]; } }

        /// <summary>[Previous Update] A 0-1 value representing how curled the ring finger is. 0 being straight, 1 being fully curled.</summary>
        public float lastRingCurl { get { return lastFingerCurls[SteamVR_Skeleton_FingerIndexes.ring]; } }

        /// <summary>[Previous Update] A 0-1 value representing how curled the pinky finger is. 0 being straight, 1 being fully curled.</summary>
        public float lastPinkyCurl { get { return lastFingerCurls[SteamVR_Skeleton_FingerIndexes.pinky]; } }


        /// <summary>[Previous Update] A 0-1 value representing the size of the gap between the thumb and index fingers</summary>
        public float lastThumbIndexSplay { get { return lastFingerSplays[SteamVR_Skeleton_FingerSplayIndexes.thumbIndex]; } }

        /// <summary>[Previous Update] A 0-1 value representing the size of the gap between the index and middle fingers</summary>
        public float lastIndexMiddleSplay { get { return lastFingerSplays[SteamVR_Skeleton_FingerSplayIndexes.indexMiddle]; } }

        /// <summary>[Previous Update] A 0-1 value representing the size of the gap between the middle and ring fingers</summary>
        public float lastMiddleRingSplay { get { return lastFingerSplays[SteamVR_Skeleton_FingerSplayIndexes.middleRing]; } }

        /// <summary>[Previous Update] A 0-1 value representing the size of the gap between the ring and pinky fingers</summary>
        public float lastRingPinkySplay { get { return lastFingerSplays[SteamVR_Skeleton_FingerSplayIndexes.ringPinky]; } }


        /// <summary>0-1 values representing how curled the specified finger is. 0 being straight, 1 being fully curled. For indexes see: SteamVR_Skeleton_FingerIndexes</summary>
        public float[] fingerCurls { get; protected set; }

        /// <summary>0-1 values representing how splayed the specified finger and it's next index'd finger is. For indexes see: SteamVR_Skeleton_FingerIndexes</summary>
        public float[] fingerSplays { get; protected set; }

        /// <summary>[Previous Update] 0-1 values representing how curled the specified finger is. 0 being straight, 1 being fully curled. For indexes see: SteamVR_Skeleton_FingerIndexes</summary>
        public float[] lastFingerCurls { get; protected set; }

        /// <summary>[Previous Update] 0-1 values representing how splayed the specified finger and it's next index'd finger is. For indexes see: SteamVR_Skeleton_FingerIndexes</summary>
        public float[] lastFingerSplays { get; protected set; }

        /// <summary>Separate from "changed". If the pose for this skeleton action has changed (root position/rotation)</summary>
        public bool poseChanged { get; protected set; }

        /// <summary>Skips processing the full per bone data and only does the summary data</summary>
        public bool onlyUpdateSummaryData { get; set; }


        protected VRSkeletalSummaryData_t skeletalSummaryData = new VRSkeletalSummaryData_t();
        protected VRSkeletalSummaryData_t lastSkeletalSummaryData = new VRSkeletalSummaryData_t();
        protected SteamVR_Action_Skeleton skeletonAction;

        protected VRBoneTransform_t[] tempBoneTransforms = new VRBoneTransform_t[SteamVR_Action_Skeleton.numBones];

        protected InputSkeletalActionData_t skeletonActionData = new InputSkeletalActionData_t();

        protected InputSkeletalActionData_t lastSkeletonActionData = new InputSkeletalActionData_t();

        protected InputSkeletalActionData_t tempSkeletonActionData = new InputSkeletalActionData_t();

        public override void Preinitialize(SteamVR_Action wrappingAction, SteamVR_Input_Sources forInputSource)
        {
            base.Preinitialize(wrappingAction, forInputSource);
            skeletonAction = (SteamVR_Action_Skeleton)wrappingAction;

            bonePositions = new Vector3[SteamVR_Action_Skeleton.numBones];
            lastBonePositions = new Vector3[SteamVR_Action_Skeleton.numBones];
            boneRotations = new Quaternion[SteamVR_Action_Skeleton.numBones];
            lastBoneRotations = new Quaternion[SteamVR_Action_Skeleton.numBones];

            rangeOfMotion = EVRSkeletalMotionRange.WithController;
            skeletalTransformSpace = EVRSkeletalTransformSpace.Parent;

            fingerCurls = new float[SteamVR_Skeleton_FingerIndexes.enumArray.Length];
            fingerSplays = new float[SteamVR_Skeleton_FingerSplayIndexes.enumArray.Length];

            lastFingerCurls = new float[SteamVR_Skeleton_FingerIndexes.enumArray.Length];
            lastFingerSplays = new float[SteamVR_Skeleton_FingerSplayIndexes.enumArray.Length];
        }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> 
        /// Initializes the handle for the inputSource, the skeletal action data size, and any other related SteamVR data.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            if (skeletonActionData_size == 0)
                skeletonActionData_size = (uint)Marshal.SizeOf(typeof(InputSkeletalActionData_t));
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
        public override void UpdateValue(bool skipStateAndEventUpdates)
        {
            lastActive = active;
            lastSkeletonActionData = skeletonActionData;
            lastSkeletalSummaryData = skeletalSummaryData;

            if (onlyUpdateSummaryData == false)
            {
                for (int boneIndex = 0; boneIndex < SteamVR_Action_Skeleton.numBones; boneIndex++)
                {
                    lastBonePositions[boneIndex] = bonePositions[boneIndex];
                    lastBoneRotations[boneIndex] = boneRotations[boneIndex];
                }
            }

            for (int fingerIndex = 0; fingerIndex < SteamVR_Skeleton_FingerIndexes.enumArray.Length; fingerIndex++)
            {
                lastFingerCurls[fingerIndex] = fingerCurls[fingerIndex];
            }

            for (int fingerIndex = 0; fingerIndex < SteamVR_Skeleton_FingerSplayIndexes.enumArray.Length; fingerIndex++)
            {
                lastFingerSplays[fingerIndex] = fingerSplays[fingerIndex];
            }

            base.UpdateValue(true);
            poseChanged = changed;

            EVRInputError error = OpenVR.Input.GetSkeletalActionData(handle, ref skeletonActionData, skeletonActionData_size);
            if (error != EVRInputError.None)
            {
                Debug.LogError("<b>[SteamVR]</b> GetSkeletalActionData error (" + fullPath + "): " + error.ToString() + " handle: " + handle.ToString());
                return;
            }

            if (active)
            {
                if (onlyUpdateSummaryData == false)
                {
                    error = OpenVR.Input.GetSkeletalBoneData(handle, skeletalTransformSpace, rangeOfMotion, tempBoneTransforms);
                    if (error != EVRInputError.None)
                        Debug.LogError("<b>[SteamVR]</b> GetSkeletalBoneData error (" + fullPath + "): " + error.ToString() + " handle: " + handle.ToString());

                    for (int boneIndex = 0; boneIndex < tempBoneTransforms.Length; boneIndex++)
                    {
                        // SteamVR's coordinate system is right handed, and Unity's is left handed.  The FBX data has its
                        // X axis flipped when Unity imports it, so here we need to flip the X axis as well
                        bonePositions[boneIndex].x = -tempBoneTransforms[boneIndex].position.v0;
                        bonePositions[boneIndex].y = tempBoneTransforms[boneIndex].position.v1;
                        bonePositions[boneIndex].z = tempBoneTransforms[boneIndex].position.v2;

                        boneRotations[boneIndex].x = tempBoneTransforms[boneIndex].orientation.x;
                        boneRotations[boneIndex].y = -tempBoneTransforms[boneIndex].orientation.y;
                        boneRotations[boneIndex].z = -tempBoneTransforms[boneIndex].orientation.z;
                        boneRotations[boneIndex].w = tempBoneTransforms[boneIndex].orientation.w;
                    }

                    // Now that we're in the same handedness as Unity, rotate the root bone around the Y axis
                    // so that forward is facing down +Z

                    boneRotations[0] = SteamVR_Action_Skeleton.steamVRFixUpRotation * boneRotations[0];
                }

                UpdateSkeletalSummaryData(summaryDataType, true);
            }

            if (changed == false)
            {
                for (int boneIndex = 0; boneIndex < tempBoneTransforms.Length; boneIndex++)
                {
                    if (Vector3.Distance(lastBonePositions[boneIndex], bonePositions[boneIndex]) > changeTolerance)
                    {
                        changed = true;
                        break;
                    }

                    if (Mathf.Abs(Quaternion.Angle(lastBoneRotations[boneIndex], boneRotations[boneIndex])) > changeTolerance)
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
                changedTime = Time.realtimeSinceStartup;

            if (skipStateAndEventUpdates == false)
                CheckAndSendEvents();
        }

        /// <summary>
        /// The number of bones in the skeleton for this action
        /// </summary>
        public int boneCount { get { return (int)GetBoneCount(); } }

        /// <summary>
        /// Gets the number of bones in the skeleton for this action
        /// </summary>
        public uint GetBoneCount()
        {
            uint boneCount = 0;
            EVRInputError error = OpenVR.Input.GetBoneCount(handle, ref boneCount);
            if (error != EVRInputError.None)
                Debug.LogError("<b>[SteamVR]</b> GetBoneCount error (" + fullPath + "): " + error.ToString() + " handle: " + handle.ToString());

            return boneCount;
        }

        /// <summary>
        /// Gets the ordering of the bone hierarchy
        /// </summary>
        public int[] boneHierarchy { get { return GetBoneHierarchy(); } }

        /// <summary>
        /// Gets the ordering of the bone hierarchy
        /// </summary>
        public int[] GetBoneHierarchy()
        {
            int boneCount = (int)GetBoneCount();
            int[] parentIndicies = new int[boneCount];

            EVRInputError error = OpenVR.Input.GetBoneHierarchy(handle, parentIndicies);
            if (error != EVRInputError.None)
                Debug.LogError("<b>[SteamVR]</b> GetBoneHierarchy error (" + fullPath + "): " + error.ToString() + " handle: " + handle.ToString());

            return parentIndicies;
        }

        /// <summary>
        /// Gets the name for a bone at the specified index
        /// </summary>
        public string GetBoneName(int boneIndex)
        {
            StringBuilder stringBuilder = new StringBuilder(255);
            EVRInputError error = OpenVR.Input.GetBoneName(handle, boneIndex, stringBuilder, 255);
            if (error != EVRInputError.None)
                Debug.LogError("<b>[SteamVR]</b> GetBoneName error (" + fullPath + "): " + error.ToString() + " handle: " + handle.ToString());

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Returns an array of positions/rotations that represent the state of each bone in a reference pose.
        /// </summary>
        /// <param name="transformSpace">What to get the position/rotation data relative to, the model, or the bone's parent</param>
        /// <param name="referencePose">Which reference pose to return</param>
        /// <returns></returns>
        public SteamVR_Utils.RigidTransform[] GetReferenceTransforms(EVRSkeletalTransformSpace transformSpace, EVRSkeletalReferencePose referencePose)
        {
            SteamVR_Utils.RigidTransform[] transforms = new SteamVR_Utils.RigidTransform[GetBoneCount()];

            VRBoneTransform_t[] boneTransforms = new VRBoneTransform_t[transforms.Length];

            EVRInputError error = OpenVR.Input.GetSkeletalReferenceTransforms(handle, transformSpace, referencePose, boneTransforms);
            if (error != EVRInputError.None)
                Debug.LogError("<b>[SteamVR]</b> GetSkeletalReferenceTransforms error (" + fullPath + "): " + error.ToString() + " handle: " + handle.ToString());

            for (int transformIndex = 0; transformIndex < boneTransforms.Length; transformIndex++)
            {
                Vector3 position = new Vector3(-boneTransforms[transformIndex].position.v0, boneTransforms[transformIndex].position.v1, boneTransforms[transformIndex].position.v2);
                Quaternion rotation = new Quaternion(boneTransforms[transformIndex].orientation.x, -boneTransforms[transformIndex].orientation.y, -boneTransforms[transformIndex].orientation.z, boneTransforms[transformIndex].orientation.w);
                transforms[transformIndex] = new SteamVR_Utils.RigidTransform(position, rotation);
            }

            if (transforms.Length > 0)
            {
                // Now that we're in the same handedness as Unity, rotate the root bone around the Y axis
                // so that forward is facing down +Z
                Quaternion qFixUpRot = Quaternion.AngleAxis(Mathf.PI * Mathf.Rad2Deg, Vector3.up);

                transforms[0].rot = qFixUpRot * transforms[0].rot;
            }

            return transforms;
        }

        /// <summary>
        /// Get the accuracy level of the skeletal tracking data. 
        /// <para/>* Estimated: Body part location can’t be directly determined by the device. Any skeletal pose provided by the device is estimated based on the active buttons, triggers, joysticks, or other input sensors. Examples include the Vive Controller and gamepads.
        /// <para/>* Partial: Body part location can be measured directly but with fewer degrees of freedom than the actual body part.Certain body part positions may be unmeasured by the device and estimated from other input data.Examples include Knuckles or gloves that only measure finger curl
        /// <para/>* Full: Body part location can be measured directly throughout the entire range of motion of the body part.Examples include hi-end mocap systems, or gloves that measure the rotation of each finger segment.
        /// </summary>
        public EVRSkeletalTrackingLevel skeletalTrackingLevel { get { return GetSkeletalTrackingLevel(); } }

        /// <summary>
        /// Get the accuracy level of the skeletal tracking data. 
        /// <para/>* Estimated: Body part location can’t be directly determined by the device. Any skeletal pose provided by the device is estimated based on the active buttons, triggers, joysticks, or other input sensors. Examples include the Vive Controller and gamepads.
        /// <para/>* Partial: Body part location can be measured directly but with fewer degrees of freedom than the actual body part.Certain body part positions may be unmeasured by the device and estimated from other input data.Examples include Knuckles or gloves that only measure finger curl
        /// <para/>* Full: Body part location can be measured directly throughout the entire range of motion of the body part.Examples include hi-end mocap systems, or gloves that measure the rotation of each finger segment.
        /// </summary>
        public EVRSkeletalTrackingLevel GetSkeletalTrackingLevel()
        {
            EVRSkeletalTrackingLevel skeletalTrackingLevel = EVRSkeletalTrackingLevel.VRSkeletalTracking_Estimated;

            EVRInputError error = OpenVR.Input.GetSkeletalTrackingLevel(handle, ref skeletalTrackingLevel);
            if (error != EVRInputError.None)
                Debug.LogError("<b>[SteamVR]</b> GetSkeletalTrackingLevel error (" + fullPath + "): " + error.ToString() + " handle: " + handle.ToString());

            return skeletalTrackingLevel;
        }

        /// <summary>
        /// Get the skeletal summary data structure from OpenVR. 
        /// Contains curl and splay data in finger order: thumb, index, middlg, ring, pinky. 
        /// Easier access at named members: indexCurl, ringSplay, etc.
        /// </summary>
        protected VRSkeletalSummaryData_t GetSkeletalSummaryData(EVRSummaryType summaryType = EVRSummaryType.FromAnimation, bool force = false)
        {
            UpdateSkeletalSummaryData(summaryType, force);
            return skeletalSummaryData;
        }

        /// <summary>
        /// Updates the skeletal summary data structure from OpenVR. 
        /// Contains curl and splay data in finger order: thumb, index, middlg, ring, pinky. 
        /// Easier access at named members: indexCurl, ringSplay, etc.
        /// </summary>
        protected void UpdateSkeletalSummaryData(EVRSummaryType summaryType = EVRSummaryType.FromAnimation, bool force = false)
        {
            if (force || this.summaryDataType != summaryDataType && active)
            {
                EVRInputError error = OpenVR.Input.GetSkeletalSummaryData(handle, summaryType, ref skeletalSummaryData);
                if (error != EVRInputError.None)
                    Debug.LogError("<b>[SteamVR]</b> GetSkeletalSummaryData error (" + fullPath + "): " + error.ToString() + " handle: " + handle.ToString());

                fingerCurls[0] = skeletalSummaryData.flFingerCurl0;
                fingerCurls[1] = skeletalSummaryData.flFingerCurl1;
                fingerCurls[2] = skeletalSummaryData.flFingerCurl2;
                fingerCurls[3] = skeletalSummaryData.flFingerCurl3;
                fingerCurls[4] = skeletalSummaryData.flFingerCurl4;

                //no splay data for thumb
                fingerSplays[0] = skeletalSummaryData.flFingerSplay0;
                fingerSplays[1] = skeletalSummaryData.flFingerSplay1;
                fingerSplays[2] = skeletalSummaryData.flFingerSplay2;
                fingerSplays[3] = skeletalSummaryData.flFingerSplay3;
            }
        }

        protected override void CheckAndSendEvents()
        {
            if (trackingState != lastTrackingState && onTrackingChanged != null)
                onTrackingChanged.Invoke(skeletonAction, trackingState);

            if (poseIsValid != lastPoseIsValid && onValidPoseChanged != null)
                onValidPoseChanged.Invoke(skeletonAction, poseIsValid);

            if (deviceIsConnected != lastDeviceIsConnected && onDeviceConnectedChanged != null)
                onDeviceConnectedChanged.Invoke(skeletonAction, deviceIsConnected);

            if (changed && onChange != null)
                onChange.Invoke(skeletonAction);

            if (active != lastActive && onActiveChange != null)
                onActiveChange.Invoke(skeletonAction, active);

            if (activeBinding != lastActiveBinding && onActiveBindingChange != null)
                onActiveBindingChange.Invoke(skeletonAction, activeBinding);

            if (onUpdate != null)
                onUpdate.Invoke(skeletonAction);
        }
    }

    public interface ISteamVR_Action_Skeleton_Source
    {
        /// <summary>
        /// Get the accuracy level of the skeletal tracking data. 
        /// <para/>* Estimated: Body part location can’t be directly determined by the device. Any skeletal pose provided by the device is estimated based on the active buttons, triggers, joysticks, or other input sensors. Examples include the Vive Controller and gamepads.
        /// <para/>* Partial: Body part location can be measured directly but with fewer degrees of freedom than the actual body part.Certain body part positions may be unmeasured by the device and estimated from other input data.Examples include Knuckles or gloves that only measure finger curl
        /// <para/>* Full: Body part location can be measured directly throughout the entire range of motion of the body part.Examples include hi-end mocap systems, or gloves that measure the rotation of each finger segment.
        /// </summary>
        EVRSkeletalTrackingLevel skeletalTrackingLevel { get; }

        /// <summary>An array of the positions of the bones from the most recent update. Relative to skeletalTransformSpace. See SteamVR_Skeleton_JointIndexes for bone indexes.</summary>
        Vector3[] bonePositions { get; }

        /// <summary>An array of the rotations of the bones from the most recent update. Relative to skeletalTransformSpace. See SteamVR_Skeleton_JointIndexes for bone indexes.</summary>
        Quaternion[] boneRotations { get; }

        /// <summary>From the previous update: An array of the positions of the bones from the most recent update. Relative to skeletalTransformSpace. See SteamVR_Skeleton_JointIndexes for bone indexes.</summary>
        Vector3[] lastBonePositions { get; }

        /// <summary>From the previous update: An array of the rotations of the bones from the most recent update. Relative to skeletalTransformSpace. See SteamVR_Skeleton_JointIndexes for bone indexes.</summary>
        Quaternion[] lastBoneRotations { get; }

        /// <summary>The range of motion the we're using to get bone data from. With Controller being your hand while holding the controller.</summary>
        EVRSkeletalMotionRange rangeOfMotion { get; set; }

        /// <summary>The space to get bone data in. Parent space by default</summary>
        EVRSkeletalTransformSpace skeletalTransformSpace { get; set; }

        /// <summary>Skips processing the full per bone data and only does the summary data</summary>
        bool onlyUpdateSummaryData { get; set; }

        /// <summary>A 0-1 value representing how curled the thumb is. 0 being straight, 1 being fully curled.</summary>
        float thumbCurl { get; }

        /// <summary>A 0-1 value representing how curled the index finger is. 0 being straight, 1 being fully curled.</summary>
        float indexCurl { get; }

        /// <summary>A 0-1 value representing how curled the middle finger is. 0 being straight, 1 being fully curled.</summary>
        float middleCurl { get; }

        /// <summary>A 0-1 value representing how curled the ring finger is. 0 being straight, 1 being fully curled.</summary>
        float ringCurl { get; }

        /// <summary>A 0-1 value representing how curled the pinky finger is. 0 being straight, 1 being fully curled.</summary>
        float pinkyCurl { get; }

        /// <summary>A 0-1 value representing the size of the gap between the thumb and index fingers</summary>
        float thumbIndexSplay { get; }

        /// <summary>A 0-1 value representing the size of the gap between the index and middle fingers</summary>
        float indexMiddleSplay { get; }

        /// <summary>A 0-1 value representing the size of the gap between the middle and ring fingers</summary>
        float middleRingSplay { get; }

        /// <summary>A 0-1 value representing the size of the gap between the ring and pinky fingers</summary>
        float ringPinkySplay { get; }


        /// <summary>[Previous Update] A 0-1 value representing how curled the thumb is. 0 being straight, 1 being fully curled.</summary>
        float lastThumbCurl { get; }

        /// <summary>[Previous Update] A 0-1 value representing how curled the index finger is. 0 being straight, 1 being fully curled.</summary>
        float lastIndexCurl { get; }

        /// <summary>[Previous Update] A 0-1 value representing how curled the middle finger is. 0 being straight, 1 being fully curled.</summary>
        float lastMiddleCurl { get; }

        /// <summary>[Previous Update] A 0-1 value representing how curled the ring finger is. 0 being straight, 1 being fully curled.</summary>
        float lastRingCurl { get; }

        /// <summary>[Previous Update] A 0-1 value representing how curled the pinky finger is. 0 being straight, 1 being fully curled.</summary>
        float lastPinkyCurl { get; }

        /// <summary>[Previous Update] A 0-1 value representing the size of the gap between the thumb and index fingers</summary>
        float lastThumbIndexSplay { get; }

        /// <summary>[Previous Update] A 0-1 value representing the size of the gap between the index and middle fingers</summary>
        float lastIndexMiddleSplay { get; }

        /// <summary>[Previous Update] A 0-1 value representing the size of the gap between the middle and ring fingers</summary>
        float lastMiddleRingSplay { get; }

        /// <summary>[Previous Update] A 0-1 value representing the size of the gap between the ring and pinky fingers</summary>
        float lastRingPinkySplay { get; }



        /// <summary>0-1 values representing how curled the specified finger is. 0 being straight, 1 being fully curled. For indexes see: SteamVR_Skeleton_FingerIndexes</summary>
        float[] fingerCurls { get; }

        /// <summary>0-1 values representing how splayed the specified finger and it's next index'd finger is. For indexes see: SteamVR_Skeleton_FingerIndexes</summary>
        float[] fingerSplays { get; }

        /// <summary>[Previous Update] 0-1 values representing how curled the specified finger is. 0 being straight, 1 being fully curled. For indexes see: SteamVR_Skeleton_FingerIndexes</summary>
        float[] lastFingerCurls { get; }

        /// <summary>[Previous Update] 0-1 values representing how splayed the specified finger and it's next index'd finger is. For indexes see: SteamVR_Skeleton_FingerIndexes</summary>
        float[] lastFingerSplays { get; }
    }

    /// <summary>
    /// The change in range of the motion of the bones in the skeleton. Options are "With Controller" as if your hand is holding your VR controller. 
    /// Or "Without Controller" as if your hand is empty.
    /// </summary>
    public enum SkeletalMotionRangeChange
    {
        None = -1,

        /// <summary>Estimation of bones in hand while holding a controller</summary>
        WithController = 0,

        /// <summary>Estimation of bones in hand while hand is empty (allowing full fist)</summary>
        WithoutController = 1,
    }



    /// <summary>The order of the joints that SteamVR Skeleton Input is expecting.</summary>
    public static class SteamVR_Skeleton_JointIndexes
    {
        public const int root = 0;
        public const int wrist = 1;
        public const int thumbMetacarpal = 2;
        public const int thumbProximal = 2;
        public const int thumbMiddle = 3;
        public const int thumbDistal = 4;
        public const int thumbTip = 5;
        public const int indexMetacarpal = 6;
        public const int indexProximal = 7;
        public const int indexMiddle = 8;
        public const int indexDistal = 9;
        public const int indexTip = 10;
        public const int middleMetacarpal = 11;
        public const int middleProximal = 12;
        public const int middleMiddle = 13;
        public const int middleDistal = 14;
        public const int middleTip = 15;
        public const int ringMetacarpal = 16;
        public const int ringProximal = 17;
        public const int ringMiddle = 18;
        public const int ringDistal = 19;
        public const int ringTip = 20;
        public const int pinkyMetacarpal = 21;
        public const int pinkyProximal = 22;
        public const int pinkyMiddle = 23;
        public const int pinkyDistal = 24;
        public const int pinkyTip = 25;
        public const int thumbAux = 26;
        public const int indexAux = 27;
        public const int middleAux = 28;
        public const int ringAux = 29;
        public const int pinkyAux = 30;

        public static int GetFingerForBone(int boneIndex)
        {
            switch (boneIndex)
            {
                case root:
                case wrist:
                    return -1;

                case thumbMetacarpal:
                case thumbMiddle:
                case thumbDistal:
                case thumbTip:
                case thumbAux:
                    return 0;

                case indexMetacarpal:
                case indexProximal:
                case indexMiddle:
                case indexDistal:
                case indexTip:
                case indexAux:
                    return 1;

                case middleMetacarpal:
                case middleProximal:
                case middleMiddle:
                case middleDistal:
                case middleTip:
                case middleAux:
                    return 2;

                case ringMetacarpal:
                case ringProximal:
                case ringMiddle:
                case ringDistal:
                case ringTip:
                case ringAux:
                    return 3;

                case pinkyMetacarpal:
                case pinkyProximal:
                case pinkyMiddle:
                case pinkyDistal:
                case pinkyTip:
                case pinkyAux:
                    return 4;

                default:
                    return -1;
            }
        }
    }

    public enum SteamVR_Skeleton_JointIndexEnum
    {
        root = SteamVR_Skeleton_JointIndexes.root,
        wrist = SteamVR_Skeleton_JointIndexes.wrist,
        thumbMetacarpal = SteamVR_Skeleton_JointIndexes.thumbMetacarpal,
        thumbProximal = SteamVR_Skeleton_JointIndexes.thumbProximal,
        thumbMiddle = SteamVR_Skeleton_JointIndexes.thumbMiddle,
        thumbDistal = SteamVR_Skeleton_JointIndexes.thumbDistal,
        thumbTip = SteamVR_Skeleton_JointIndexes.thumbTip,
        indexMetacarpal = SteamVR_Skeleton_JointIndexes.indexMetacarpal,
        indexProximal = SteamVR_Skeleton_JointIndexes.indexProximal,
        indexMiddle = SteamVR_Skeleton_JointIndexes.indexMiddle,
        indexDistal = SteamVR_Skeleton_JointIndexes.indexDistal,
        indexTip = SteamVR_Skeleton_JointIndexes.indexTip,
        middleMetacarpal = SteamVR_Skeleton_JointIndexes.middleMetacarpal,
        middleProximal = SteamVR_Skeleton_JointIndexes.middleProximal,
        middleMiddle = SteamVR_Skeleton_JointIndexes.middleMiddle,
        middleDistal = SteamVR_Skeleton_JointIndexes.middleDistal,
        middleTip = SteamVR_Skeleton_JointIndexes.middleTip,
        ringMetacarpal = SteamVR_Skeleton_JointIndexes.ringMetacarpal,
        ringProximal = SteamVR_Skeleton_JointIndexes.ringProximal,
        ringMiddle = SteamVR_Skeleton_JointIndexes.ringMiddle,
        ringDistal = SteamVR_Skeleton_JointIndexes.ringDistal,
        ringTip = SteamVR_Skeleton_JointIndexes.ringTip,
        pinkyMetacarpal = SteamVR_Skeleton_JointIndexes.pinkyMetacarpal,
        pinkyProximal = SteamVR_Skeleton_JointIndexes.pinkyProximal,
        pinkyMiddle = SteamVR_Skeleton_JointIndexes.pinkyMiddle,
        pinkyDistal = SteamVR_Skeleton_JointIndexes.pinkyDistal,
        pinkyTip = SteamVR_Skeleton_JointIndexes.pinkyTip,
        thumbAux = SteamVR_Skeleton_JointIndexes.thumbAux,
        indexAux = SteamVR_Skeleton_JointIndexes.indexAux,
        middleAux = SteamVR_Skeleton_JointIndexes.middleAux,
        ringAux = SteamVR_Skeleton_JointIndexes.ringAux,
        pinkyAux = SteamVR_Skeleton_JointIndexes.pinkyAux,
    }


    /// <summary>The order of the fingers that SteamVR Skeleton Input outputs</summary>
    public class SteamVR_Skeleton_FingerIndexes
    {
        public const int thumb = 0;
        public const int index = 1;
        public const int middle = 2;
        public const int ring = 3;
        public const int pinky = 4;

        public static SteamVR_Skeleton_FingerIndexEnum[] enumArray = (SteamVR_Skeleton_FingerIndexEnum[])System.Enum.GetValues(typeof(SteamVR_Skeleton_FingerIndexEnum));
    }

    /// <summary>The order of the fingerSplays that SteamVR Skeleton Input outputs</summary>
    public class SteamVR_Skeleton_FingerSplayIndexes
    {
        public const int thumbIndex = 0;
        public const int indexMiddle = 1;
        public const int middleRing = 2;
        public const int ringPinky = 3;

        public static SteamVR_Skeleton_FingerSplayIndexEnum[] enumArray = (SteamVR_Skeleton_FingerSplayIndexEnum[])System.Enum.GetValues(typeof(SteamVR_Skeleton_FingerSplayIndexEnum));
    }

    public enum SteamVR_Skeleton_FingerSplayIndexEnum
    {
        thumbIndex = SteamVR_Skeleton_FingerSplayIndexes.thumbIndex,
        indexMiddle = SteamVR_Skeleton_FingerSplayIndexes.indexMiddle,
        middleRing = SteamVR_Skeleton_FingerSplayIndexes.middleRing,
        ringPinky = SteamVR_Skeleton_FingerSplayIndexes.ringPinky,
    }

    public enum SteamVR_Skeleton_FingerIndexEnum
    {
        thumb = SteamVR_Skeleton_FingerIndexes.thumb,
        index = SteamVR_Skeleton_FingerIndexes.index,
        middle = SteamVR_Skeleton_FingerIndexes.middle,
        ring = SteamVR_Skeleton_FingerIndexes.ring,
        pinky = SteamVR_Skeleton_FingerIndexes.pinky,
    }
}