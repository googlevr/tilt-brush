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
    /// An analog action with two values generally from -1 to 1. Also provides a delta since the last update.
    /// </summary>
    public class SteamVR_Action_Vector2 : SteamVR_Action_In<SteamVR_Action_Vector2_Source_Map, SteamVR_Action_Vector2_Source>, ISteamVR_Action_Vector2, ISerializationCallbackReceiver
    {
        public delegate void AxisHandler(SteamVR_Action_Vector2 fromAction, SteamVR_Input_Sources fromSource, Vector2 axis, Vector2 delta);
        public delegate void ActiveChangeHandler(SteamVR_Action_Vector2 fromAction, SteamVR_Input_Sources fromSource, bool active);
        public delegate void ChangeHandler(SteamVR_Action_Vector2 fromAction, SteamVR_Input_Sources fromSource, Vector2 axis, Vector2 delta);
        public delegate void UpdateHandler(SteamVR_Action_Vector2 fromAction, SteamVR_Input_Sources fromSource, Vector2 axis, Vector2 delta);

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> This event fires whenever the axis changes by more than the specified changeTolerance</summary>
        public event ChangeHandler onChange
        { add { sourceMap[SteamVR_Input_Sources.Any].onChange += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onChange -= value; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> This event fires whenever the action is updated</summary>
        public event UpdateHandler onUpdate
        { add { sourceMap[SteamVR_Input_Sources.Any].onUpdate += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onUpdate -= value; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> This event will fire whenever the Vector2 value of the action is non-zero</summary>
        public event AxisHandler onAxis
        { add { sourceMap[SteamVR_Input_Sources.Any].onAxis += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onAxis -= value; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Event fires when the active state (ActionSet active and binding active) changes</summary>
        public event ActiveChangeHandler onActiveChange
        { add { sourceMap[SteamVR_Input_Sources.Any].onActiveChange += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onActiveChange -= value; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Event fires when the active state of the binding changes</summary>
        public event ActiveChangeHandler onActiveBindingChange
        { add { sourceMap[SteamVR_Input_Sources.Any].onActiveBindingChange += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onActiveBindingChange -= value; } }


        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The current Vector2 value of the action.
        /// Note: Will only return non-zero if the action is also active.</summary>
        public Vector2 axis { get { return sourceMap[SteamVR_Input_Sources.Any].axis; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The Vector2 value of the action from the previous update.
        /// Note: Will only return non-zero if the action is also active.</summary>
        public Vector2 lastAxis { get { return sourceMap[SteamVR_Input_Sources.Any].lastAxis; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The Vector2 value difference between this update and the previous update.
        /// Note: Will only return non-zero if the action is also active.</summary>
        public Vector2 delta { get { return sourceMap[SteamVR_Input_Sources.Any].delta; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> The Vector2 value difference between the previous update and update before that. 
        /// Note: Will only return non-zero if the action is also active.</summary>
        public Vector2 lastDelta { get { return sourceMap[SteamVR_Input_Sources.Any].lastDelta; } }


        public SteamVR_Action_Vector2() { }

        /// <summary>The current Vector2 value of the action</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public Vector2 GetAxis(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].axis;
        }

        /// <summary>The Vector2 value difference between this update and the previous update.</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public Vector2 GetAxisDelta(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].delta;
        }

        /// <summary>The Vector2 value of the action from the previous update.</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public Vector2 GetLastAxis(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].lastAxis;
        }

        /// <summary>The Vector2 value difference between the previous update and update before that. </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public Vector2 GetLastAxisDelta(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].lastDelta;
        }

        /// <summary>Executes a function when the *functional* active state of this action (with the specified inputSource) changes. 
        /// This happens when the action is bound or unbound, or when the ActionSet changes state.</summary>
        /// <param name="functionToCall">A local function that receives the boolean action who's active state changes and the corresponding input source</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void AddOnActiveChangeListener(ActiveChangeHandler functionToCall, SteamVR_Input_Sources inputSource)
        {
            sourceMap[inputSource].onActiveChange += functionToCall;
        }

        /// <summary>Stops executing a function when the *functional* active state of this action (with the specified inputSource) changes. 
        /// This happens when the action is bound or unbound, or when the ActionSet changes state.</summary>
        /// <param name="functionToStopCalling">The local function that you've setup to receive update events</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void RemoveOnActiveChangeListener(ActiveChangeHandler functionToStopCalling, SteamVR_Input_Sources inputSource)
        {
            sourceMap[inputSource].onActiveChange -= functionToStopCalling;
        }

        /// <summary>Executes a function when the active state of this action (with the specified inputSource) changes. This happens when the action is bound or unbound</summary>
        /// <param name="functionToCall">A local function that receives the boolean action who's active state changes and the corresponding input source</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void AddOnActiveBindingChangeListener(ActiveChangeHandler functionToCall, SteamVR_Input_Sources inputSource)
        {
            sourceMap[inputSource].onActiveBindingChange += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="functionToStopCalling">The local function that you've setup to receive update events</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void RemoveOnActiveBindingChangeListener(ActiveChangeHandler functionToStopCalling, SteamVR_Input_Sources inputSource)
        {
            sourceMap[inputSource].onActiveBindingChange -= functionToStopCalling;
        }

        /// <summary>Executes a function when the axis changes by more than the specified changeTolerance</summary>
        /// <param name="functionToCall">A local function that receives the boolean action who's state has changed, the corresponding input source, and the new value</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void AddOnChangeListener(ChangeHandler functionToCall, SteamVR_Input_Sources inputSource)
        {
            sourceMap[inputSource].onChange += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="functionToStopCalling">The local function that you've setup to receive on change events</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void RemoveOnChangeListener(ChangeHandler functionToStopCalling, SteamVR_Input_Sources inputSource)
        {
            sourceMap[inputSource].onChange -= functionToStopCalling;
        }

        /// <summary>Executes a function when the state of this action (with the specified inputSource) is updated.</summary>
        /// <param name="functionToCall">A local function that receives the boolean action who's state has changed, the corresponding input source, and the new value</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void AddOnUpdateListener(UpdateHandler functionToCall, SteamVR_Input_Sources inputSource)
        {
            sourceMap[inputSource].onUpdate += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="functionToStopCalling">The local function that you've setup to receive update events</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void RemoveOnUpdateListener(UpdateHandler functionToStopCalling, SteamVR_Input_Sources inputSource)
        {
            sourceMap[inputSource].onUpdate -= functionToStopCalling;
        }

        /// <summary>Executes a function when the Vector2 value of the action is non-zero.</summary>
        /// <param name="functionToCall">A local function that receives the boolean action who's state has changed, the corresponding input source, and the new value</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void AddOnAxisListener(AxisHandler functionToCall, SteamVR_Input_Sources inputSource)
        {
            sourceMap[inputSource].onAxis += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="functionToStopCalling">The local function that you've setup to receive update events</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void RemoveOnAxisListener(AxisHandler functionToStopCalling, SteamVR_Input_Sources inputSource)
        {
            sourceMap[inputSource].onAxis -= functionToStopCalling;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            InitAfterDeserialize();
        }
    }

    /// <summary>
    /// Boolean actions are either true or false. There is an onStateUp and onStateDown event for the rising and falling edge.
    /// </summary>
    public class SteamVR_Action_Vector2_Source_Map : SteamVR_Action_In_Source_Map<SteamVR_Action_Vector2_Source>
    {
    }

    public class SteamVR_Action_Vector2_Source : SteamVR_Action_In_Source, ISteamVR_Action_Vector2
    {
        protected static uint actionData_size = 0;

        /// <summary>The amount the axis needs to change before a change is detected</summary>
        public float changeTolerance = Mathf.Epsilon;

        /// <summary>Event fires when the value of the action is non-zero</summary>
        public event SteamVR_Action_Vector2.AxisHandler onAxis;

        /// <summary>Event fires when the active state (ActionSet active and binding active) changes</summary>
        public event SteamVR_Action_Vector2.ActiveChangeHandler onActiveChange;

        /// <summary>Event fires when the active state of the binding changes</summary>
        public event SteamVR_Action_Vector2.ActiveChangeHandler onActiveBindingChange;

        /// <summary>This event fires whenever the axis changes by more than the specified changeTolerance</summary>
        public event SteamVR_Action_Vector2.ChangeHandler onChange;

        /// <summary>Event fires when the action is updated</summary>
        public event SteamVR_Action_Vector2.UpdateHandler onUpdate;


        /// <summary>The current Vector2 value of the action. 
        /// Note: Will only return non-zero if the action is also active.</summary>
        public Vector2 axis { get; protected set; }

        /// <summary>The Vector2 value of the action from the previous update. 
        /// Note: Will only return non-zero if the action is also active.</summary>
        public Vector2 lastAxis { get; protected set; }

        /// <summary>The Vector2 value difference between this update and the previous update. 
        /// Note: Will only return non-zero if the action is also active.</summary>
        public Vector2 delta { get; protected set; }

        /// <summary>The Vector2 value difference between the previous update and update before that. 
        /// Note: Will only return non-zero if the action is also active.</summary>
        public Vector2 lastDelta { get; protected set; }


        /// <summary>If the Vector2 value of this action has changed more than the changeTolerance since the last update</summary>
        public override bool changed { get; protected set; }


        /// <summary>If the Vector2 value of this action has changed more than the changeTolerance between the previous update and the update before that</summary>
        public override bool lastChanged { get; protected set; }

        /// <summary>The handle to the origin of the component that was used to update the value for this action</summary>
        public override ulong activeOrigin
        {
            get
            {
                if (active)
                    return actionData.activeOrigin;

                return 0;
            }
        }

        /// <summary>The handle to the origin of the component that was used to update the value for this action (for the previous update)</summary>
        public override ulong lastActiveOrigin { get { return lastActionData.activeOrigin; } }

        /// <summary>Returns true if this action is bound and the ActionSet is active</summary>
        public override bool active { get { return activeBinding && action.actionSet.IsActive(inputSource); } }

        /// <summary>Returns true if the action is bound</summary>
        public override bool activeBinding { get { return actionData.bActive; } }


        /// <summary>Returns true if the action was bound and the ActionSet was active during the previous update</summary>
        public override bool lastActive { get; protected set; }

        /// <summary>Returns true if the action was bound during the previous update</summary>
        public override bool lastActiveBinding { get { return lastActionData.bActive; } }


        protected InputAnalogActionData_t actionData = new InputAnalogActionData_t();
        protected InputAnalogActionData_t lastActionData = new InputAnalogActionData_t();

        protected SteamVR_Action_Vector2 vector2Action;

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> Sets up the internals of the action source before SteamVR has been initialized.
        /// </summary>
        public override void Preinitialize(SteamVR_Action wrappingAction, SteamVR_Input_Sources forInputSource)
        {
            base.Preinitialize(wrappingAction, forInputSource);
            vector2Action = (SteamVR_Action_Vector2)wrappingAction;
        }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> 
        /// Initializes the handle for the inputSource, the action data size, and any other related SteamVR data.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            if (actionData_size == 0)
                actionData_size = (uint)Marshal.SizeOf(typeof(InputAnalogActionData_t));
        }

        /// <summary><strong>[Should not be called by user code]</strong> 
        /// Updates the data for this action and this input source. Sends related events.
        /// </summary>
        public override void UpdateValue()
        {
            lastActionData = actionData;
            lastActive = active;
            lastAxis = axis;
            lastDelta = delta;

            EVRInputError err = OpenVR.Input.GetAnalogActionData(handle, ref actionData, actionData_size, SteamVR_Input_Source.GetHandle(inputSource));
            if (err != EVRInputError.None)
                Debug.LogError("<b>[SteamVR]</b> GetAnalogActionData error (" + fullPath + "): " + err.ToString() + " handle: " + handle.ToString());

            updateTime = Time.realtimeSinceStartup;
            axis = new Vector2(actionData.x, actionData.y);
            delta = new Vector2(actionData.deltaX, actionData.deltaY);

            changed = false;

            if (active)
            {
                if (delta.magnitude > changeTolerance)
                {
                    changed = true;
                    changedTime = Time.realtimeSinceStartup + actionData.fUpdateTime; //fUpdateTime is the time from the time the action was called that the action changed

                    if (onChange != null)
                        onChange.Invoke(vector2Action, inputSource, axis, delta);
                }

                if (axis != Vector2.zero)
                {
                    if (onAxis != null)
                        onAxis.Invoke(vector2Action, inputSource, axis, delta);
                }

                if (onUpdate != null)
                {
                    onUpdate.Invoke(vector2Action, inputSource, axis, delta);
                }
            }


            if (onActiveBindingChange != null && lastActiveBinding != activeBinding)
                onActiveBindingChange.Invoke(vector2Action, inputSource, activeBinding);

            if (onActiveChange != null && lastActive != active)
                onActiveChange.Invoke(vector2Action, inputSource, activeBinding);
        }
    }
    
    public interface ISteamVR_Action_Vector2 : ISteamVR_Action_In_Source
    {
        /// <summary>The current float value of the action. 
        /// Note: Will only return non-zero if the action is also active.</summary>
        Vector2 axis { get; }

        /// <summary>The float value of the action from the previous update. 
        /// Note: Will only return non-zero if the action is also active.</summary>
        Vector2 lastAxis { get; }

        /// <summary>The float value difference between this update and the previous update. 
        /// Note: Will only return non-zero if the action is also active.</summary>
        Vector2 delta { get; }

        /// <summary>The float value difference between the previous update and update before that. 
        /// Note: Will only return non-zero if the action is also active.</summary>
        Vector2 lastDelta { get; }
    }
}