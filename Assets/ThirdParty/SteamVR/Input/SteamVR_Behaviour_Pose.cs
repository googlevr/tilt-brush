//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using Valve.VR;

namespace Valve.VR
{
    /// <summary>
    /// This component simplifies the use of Pose actions. Adding it to a gameobject will auto set that transform's position and rotation every update to match the pose.
    /// Advanced velocity estimation is handled through a buffer of the last 30 updates.
    /// </summary>
    public class SteamVR_Behaviour_Pose : MonoBehaviour
    {
        public SteamVR_Action_Pose poseAction = SteamVR_Input.GetAction<SteamVR_Action_Pose>("Pose");

        [Tooltip("The device this action should apply to. Any if the action is not device specific.")]
        public SteamVR_Input_Sources inputSource;

        [Tooltip("If not set, relative to parent")]
        public Transform origin;

        /// <summary>Returns whether or not the current pose is in a valid state</summary>
        public bool isValid { get { return poseAction[inputSource].poseIsValid; } }

        /// <summary>Returns whether or not the pose action is bound and able to be updated</summary>
        public bool isActive { get { return poseAction[inputSource].active; } }


        /// <summary>This Unity event will fire whenever the position or rotation of this transform is updated.</summary>
        public SteamVR_Behaviour_PoseEvent onTransformUpdated;

        /// <summary>This Unity event will fire whenever the position or rotation of this transform is changed.</summary>
        public SteamVR_Behaviour_PoseEvent onTransformChanged;

        /// <summary>This Unity event will fire whenever the device is connected or disconnected</summary>
        public SteamVR_Behaviour_Pose_ConnectedChangedEvent onConnectedChanged;

        /// <summary>This Unity event will fire whenever the device's tracking state changes</summary>
        public SteamVR_Behaviour_Pose_TrackingChangedEvent onTrackingChanged;

        /// <summary>This Unity event will fire whenever the device's deviceIndex changes</summary>
        public SteamVR_Behaviour_Pose_DeviceIndexChangedEvent onDeviceIndexChanged;


        /// <summary>This C# event will fire whenever the position or rotation of this transform is updated.</summary>
        public UpdateHandler onTransformUpdatedEvent;

        /// <summary>This C# event will fire whenever the position or rotation of this transform is changed.</summary>
        public ChangeHandler onTransformChangedEvent;

        /// <summary>This C# event will fire whenever the device is connected or disconnected</summary>
        public DeviceConnectedChangeHandler onConnectedChangedEvent;

        /// <summary>This C# event will fire whenever the device's tracking state changes</summary>
        public TrackingChangeHandler onTrackingChangedEvent;

        /// <summary>This C# event will fire whenever the device's deviceIndex changes</summary>
        public DeviceIndexChangedHandler onDeviceIndexChangedEvent;


        [Tooltip("Can be disabled to stop broadcasting bound device status changes")]
        public bool broadcastDeviceChanges = true;

        protected int deviceIndex = -1;

        protected SteamVR_HistoryBuffer historyBuffer = new SteamVR_HistoryBuffer(30);
        

        protected virtual void Start()
        {
            if (poseAction == null)
            {
                Debug.LogError("<b>[SteamVR]</b> No pose action set for this component");
                return;
            }

            CheckDeviceIndex();

            if (origin == null)
                origin = this.transform.parent;
        }

        protected virtual void OnEnable()
        {
            SteamVR.Initialize();

            if (poseAction != null)
            {
                poseAction[inputSource].onUpdate += SteamVR_Behaviour_Pose_OnUpdate;
                poseAction[inputSource].onDeviceConnectedChanged += OnDeviceConnectedChanged;
                poseAction[inputSource].onTrackingChanged += OnTrackingChanged;
                poseAction[inputSource].onChange += SteamVR_Behaviour_Pose_OnChange;
            }
        }

        protected virtual void OnDisable()
        {
            if (poseAction != null)
            {
                poseAction[inputSource].onUpdate -= SteamVR_Behaviour_Pose_OnUpdate;
                poseAction[inputSource].onDeviceConnectedChanged -= OnDeviceConnectedChanged;
                poseAction[inputSource].onTrackingChanged -= OnTrackingChanged;
                poseAction[inputSource].onChange -= SteamVR_Behaviour_Pose_OnChange;
            }

            historyBuffer.Clear();
        }

        private void SteamVR_Behaviour_Pose_OnUpdate(SteamVR_Action_Pose fromAction, SteamVR_Input_Sources fromSource)
        {
            UpdateHistoryBuffer();

            UpdateTransform();

            if (onTransformUpdated != null)
                onTransformUpdated.Invoke(this, inputSource);
            if (onTransformUpdatedEvent != null)
                onTransformUpdatedEvent.Invoke(this, inputSource);
        }
        protected virtual void UpdateTransform()
        {
            CheckDeviceIndex();

            if (origin != null)
            {
                transform.position = origin.transform.TransformPoint(poseAction[inputSource].localPosition);
                transform.rotation = origin.rotation * poseAction[inputSource].localRotation;
            }
            else
            {
                transform.localPosition = poseAction[inputSource].localPosition;
                transform.localRotation = poseAction[inputSource].localRotation;
            }
        }

        private void SteamVR_Behaviour_Pose_OnChange(SteamVR_Action_Pose fromAction, SteamVR_Input_Sources fromSource)
        {
            if (onTransformChanged != null)
                onTransformChanged.Invoke(this, fromSource);
            if (onTransformChangedEvent != null)
                onTransformChangedEvent.Invoke(this, fromSource);
        }

        protected virtual void OnDeviceConnectedChanged(SteamVR_Action_Pose changedAction, SteamVR_Input_Sources changedSource, bool connected)
        {
            CheckDeviceIndex();

            if (onConnectedChanged != null)
                onConnectedChanged.Invoke(this, inputSource, connected);
            if (onConnectedChangedEvent != null)
                onConnectedChangedEvent.Invoke(this, inputSource, connected);
        }

        protected virtual void OnTrackingChanged(SteamVR_Action_Pose changedAction, SteamVR_Input_Sources changedSource, ETrackingResult trackingChanged)
        {
            if (onTrackingChanged != null)
                onTrackingChanged.Invoke(this, inputSource, trackingChanged);
            if (onTrackingChangedEvent != null)
                onTrackingChangedEvent.Invoke(this, inputSource, trackingChanged);
        }

        protected virtual void CheckDeviceIndex()
        {
            if (poseAction[inputSource].active && poseAction[inputSource].deviceIsConnected)
            {
                int currentDeviceIndex = (int)poseAction[inputSource].trackedDeviceIndex;

                if (deviceIndex != currentDeviceIndex)
                {
                    deviceIndex = currentDeviceIndex;

                    if (broadcastDeviceChanges)
                    {
                        this.gameObject.BroadcastMessage("SetInputSource", inputSource, SendMessageOptions.DontRequireReceiver);
                        this.gameObject.BroadcastMessage("SetDeviceIndex", deviceIndex, SendMessageOptions.DontRequireReceiver);
                    }

                    if (onDeviceIndexChanged != null)
                        onDeviceIndexChanged.Invoke(this, inputSource, deviceIndex);
                    if (onDeviceIndexChangedEvent != null)
                        onDeviceIndexChangedEvent.Invoke(this, inputSource, deviceIndex);
                }
            }
        }

        /// <summary>
        /// Returns the device index for the device bound to the pose. 
        /// </summary>
        public int GetDeviceIndex()
        {
            if (deviceIndex == -1)
                CheckDeviceIndex();

            return deviceIndex;
        }

        /// <summary>Returns the current velocity of the pose (as of the last update)</summary>
        public Vector3 GetVelocity()
        {
            return poseAction[inputSource].velocity;
        }

        /// <summary>Returns the current angular velocity of the pose (as of the last update)</summary>
        public Vector3 GetAngularVelocity()
        {
            return poseAction[inputSource].angularVelocity;
        }

        /// <summary>Returns the velocities of the pose at the time specified. Can predict in the future or return past values.</summary>
        public bool GetVelocitiesAtTimeOffset(float secondsFromNow, out Vector3 velocity, out Vector3 angularVelocity)
        {
            return poseAction[inputSource].GetVelocitiesAtTimeOffset(secondsFromNow, out velocity, out angularVelocity);
        }

        /// <summary>Uses previously recorded values to find the peak speed of the pose and returns the corresponding velocity and angular velocity</summary>
        public void GetEstimatedPeakVelocities(out Vector3 velocity, out Vector3 angularVelocity)
        {
            int top = historyBuffer.GetTopVelocity(10, 1);

            historyBuffer.GetAverageVelocities(out velocity, out angularVelocity, 2, top);
        }

        protected int lastFrameUpdated;
        protected void UpdateHistoryBuffer()
        {
            int currentFrame = Time.frameCount;
            if (lastFrameUpdated != currentFrame)
            {
                historyBuffer.Update(poseAction[inputSource].localPosition, poseAction[inputSource].localRotation, poseAction[inputSource].velocity, poseAction[inputSource].angularVelocity);
                lastFrameUpdated = currentFrame;
            }
        }

        /// <summary>
        /// Gets the localized name of the device that the action corresponds to. 
        /// </summary>
        /// <param name="localizedParts">
        /// <list type="bullet">
        /// <item><description>VRInputString_Hand - Which hand the origin is in. E.g. "Left Hand"</description></item>
        /// <item><description>VRInputString_ControllerType - What kind of controller the user has in that hand.E.g. "Vive Controller"</description></item>
        /// <item><description>VRInputString_InputSource - What part of that controller is the origin. E.g. "Trackpad"</description></item>
        /// <item><description>VRInputString_All - All of the above. E.g. "Left Hand Vive Controller Trackpad"</description></item>
        /// </list>
        /// </param>
        public string GetLocalizedName(params EVRInputStringBits[] localizedParts)
        {
            if (poseAction != null)
                return poseAction.GetLocalizedOriginPart(inputSource, localizedParts);
            return null;
        }

        public delegate void ActiveChangeHandler(SteamVR_Behaviour_Pose fromAction, SteamVR_Input_Sources fromSource, bool active);
        public delegate void ChangeHandler(SteamVR_Behaviour_Pose fromAction, SteamVR_Input_Sources fromSource);
        public delegate void UpdateHandler(SteamVR_Behaviour_Pose fromAction, SteamVR_Input_Sources fromSource);
        public delegate void TrackingChangeHandler(SteamVR_Behaviour_Pose fromAction, SteamVR_Input_Sources fromSource, ETrackingResult trackingState);
        public delegate void ValidPoseChangeHandler(SteamVR_Behaviour_Pose fromAction, SteamVR_Input_Sources fromSource, bool validPose);
        public delegate void DeviceConnectedChangeHandler(SteamVR_Behaviour_Pose fromAction, SteamVR_Input_Sources fromSource, bool deviceConnected);
        public delegate void DeviceIndexChangedHandler(SteamVR_Behaviour_Pose fromAction, SteamVR_Input_Sources fromSource, int newDeviceIndex);
    }
}