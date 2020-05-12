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
    /// Boolean actions are either true or false. There are a variety of helper events included that will fire for the given input source. They're prefixed with "on".
    /// </summary>
    public class SteamVR_Action_Boolean : SteamVR_Action_In<SteamVR_Action_Boolean_Source_Map, SteamVR_Action_Boolean_Source>, ISteamVR_Action_Boolean, ISerializationCallbackReceiver
    {
        public delegate void StateDownHandler(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource);
        public delegate void StateUpHandler(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource);
        public delegate void StateHandler(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource);
        public delegate void ActiveChangeHandler(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource, bool active);
        public delegate void ChangeHandler(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource, bool newState);
        public delegate void UpdateHandler(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource, bool newState);

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> This event fires whenever a state changes from false to true or true to false</summary>
        public event ChangeHandler onChange
        { add { sourceMap[SteamVR_Input_Sources.Any].onChange += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onChange -= value; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> This event fires whenever the action is updated</summary>
        public event UpdateHandler onUpdate
        { add { sourceMap[SteamVR_Input_Sources.Any].onUpdate += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onUpdate -= value; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> This event fires whenever the boolean action is true and gets updated</summary>
        public event StateHandler onState
        { add { sourceMap[SteamVR_Input_Sources.Any].onState += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onState -= value; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> This event fires whenever the state of the boolean action has changed from false to true in the most recent update</summary>
        public event StateDownHandler onStateDown
        { add { sourceMap[SteamVR_Input_Sources.Any].onStateDown += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onStateDown -= value; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> This event fires whenever the state of the boolean action has changed from true to false in the most recent update</summary>
        public event StateUpHandler onStateUp
        { add { sourceMap[SteamVR_Input_Sources.Any].onStateUp += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onStateUp -= value; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Event fires when the active state (ActionSet active and binding active) changes</summary>
        public event ActiveChangeHandler onActiveChange
        { add { sourceMap[SteamVR_Input_Sources.Any].onActiveChange += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onActiveChange -= value; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Event fires when the bound state of the binding changes</summary>
        public event ActiveChangeHandler onActiveBindingChange
        { add { sourceMap[SteamVR_Input_Sources.Any].onActiveBindingChange += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onActiveBindingChange -= value; } }


        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> True when the boolean action is true</summary>
        public bool state { get { return sourceMap[SteamVR_Input_Sources.Any].state; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> True when the boolean action is true and the last state was false</summary>
        public bool stateDown { get { return sourceMap[SteamVR_Input_Sources.Any].stateDown; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> True when the boolean action is false and the last state was true</summary>
        public bool stateUp { get { return sourceMap[SteamVR_Input_Sources.Any].stateUp; } }


        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> (previous update) True when the boolean action is true</summary>
        public bool lastState { get { return sourceMap[SteamVR_Input_Sources.Any].lastState; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> (previous update) True when the boolean action is true and the last state was false</summary>
        public bool lastStateDown { get { return sourceMap[SteamVR_Input_Sources.Any].lastStateDown; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> (previous update) True when the boolean action is false and the last state was true</summary>
        public bool lastStateUp { get { return sourceMap[SteamVR_Input_Sources.Any].lastStateUp; } }
        

        public SteamVR_Action_Boolean() { }

        /// <summary>Returns true if the value of the action has been changed to true (from false) in the most recent update.</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public bool GetStateDown(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].stateDown;
        }

        /// <summary>Returns true if the value of the action has been changed to false (from true) in the most recent update.</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public bool GetStateUp(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].stateUp;
        }

        /// <summary>Returns true if the value of the action (state) is currently true</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public bool GetState(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].state;
        }

        /// <summary>[For the previous update] Returns true if the value of the action has been set to true (from false).</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public bool GetLastStateDown(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].lastStateDown;
        }

        /// <summary>[For the previous update] Returns true if the value of the action has been set to false (from true).</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public bool GetLastStateUp(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].lastStateUp;
        }

        /// <summary>[For the previous update] Returns true if the value of the action was true.</summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public bool GetLastState(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].lastState;
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

        /// <summary>Executes a function when the state of this action (with the specified inputSource) changes</summary>
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

        /// <summary>Executes a function when the state of this action (with the specified inputSource) changes to true (from false).</summary>
        /// <param name="functionToCall">A local function that receives the boolean action who's state has changed, the corresponding input source, and the new value</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void AddOnStateDownListener(StateDownHandler functionToCall, SteamVR_Input_Sources inputSource)
        {
            sourceMap[inputSource].onStateDown += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="functionToStopCalling">The local function that you've setup to receive update events</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void RemoveOnStateDownListener(StateDownHandler functionToStopCalling, SteamVR_Input_Sources inputSource)
        {
            sourceMap[inputSource].onStateDown -= functionToStopCalling;
        }

        /// <summary>Executes a function when the state of this action (with the specified inputSource) changes to false (from true).</summary>
        /// <param name="functionToCall">A local function that receives the boolean action who's state has changed, the corresponding input source, and the new value</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void AddOnStateUpListener(StateUpHandler functionToCall, SteamVR_Input_Sources inputSource)
        {
            sourceMap[inputSource].onStateUp += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="functionToStopCalling">The local function that you've setup to receive events</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void RemoveOnStateUpListener(StateUpHandler functionToStopCalling, SteamVR_Input_Sources inputSource)
        {
            sourceMap[inputSource].onStateUp -= functionToStopCalling;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            InitAfterDeserialize();
        }
    }
    
    public class SteamVR_Action_Boolean_Source_Map : SteamVR_Action_In_Source_Map<SteamVR_Action_Boolean_Source>
    {
    }

    public class SteamVR_Action_Boolean_Source : SteamVR_Action_In_Source, ISteamVR_Action_Boolean
    {
        protected static uint actionData_size = 0;

        /// <summary>Event fires when the state of the action changes from false to true</summary>
        public event SteamVR_Action_Boolean.StateDownHandler onStateDown;

        /// <summary>Event fires when the state of the action changes from true to false</summary>
        public event SteamVR_Action_Boolean.StateUpHandler onStateUp;

        /// <summary>Event fires when the state of the action is true and the action gets updated</summary>
        public event SteamVR_Action_Boolean.StateHandler onState;

        /// <summary>Event fires when the active state (ActionSet active and binding active) changes</summary>
        public event SteamVR_Action_Boolean.ActiveChangeHandler onActiveChange;

        /// <summary>Event fires when the active state of the binding changes</summary>
        public event SteamVR_Action_Boolean.ActiveChangeHandler onActiveBindingChange;

        /// <summary>Event fires when the state of the action changes from false to true or true to false</summary>
        public event SteamVR_Action_Boolean.ChangeHandler onChange;

        /// <summary>Event fires when the action is updated</summary>
        public event SteamVR_Action_Boolean.UpdateHandler onUpdate;

        /// <summary>The current value of the boolean action. Note: Will only return true if the action is also active.</summary>
        public bool state { get { return active && actionData.bState; } }

        /// <summary>True when the action's state changes from false to true. Note: Will only return true if the action is also active.</summary>
        /// <remarks>Will only return true if the action is also active.</remarks>
        public bool stateDown { get { return active && actionData.bState && actionData.bChanged; } }

        /// <summary>True when the action's state changes from true to false. Note: Will only return true if the action is also active.</summary>
        /// <remarks>Will only return true if the action is also active.</remarks>
        public bool stateUp { get { return active && actionData.bState == false && actionData.bChanged; } }

        /// <summary>True when the action's state changed during the most recent update. Note: Will only return true if the action is also active.</summary>
        /// <remarks>ActionSet is ignored since get is coming from the native struct.</remarks>
        public override bool changed { get { return active && actionData.bChanged; } protected set { } }


        /// <summary>The value of the action's 'state' during the previous update</summary>
        /// <remarks>Always returns the previous update state</remarks>
        public bool lastState { get { return lastActionData.bState; } }

        /// <summary>The value of the action's 'stateDown' during the previous update</summary>
        /// <remarks>Always returns the previous update state</remarks>
        public bool lastStateDown { get { return lastActionData.bState && lastActionData.bChanged; } }

        /// <summary>The value of the action's 'stateUp' during the previous update</summary>
        /// <remarks>Always returns the previous update state</remarks>
        public bool lastStateUp { get { return lastActionData.bState == false && lastActionData.bChanged; } }

        /// <summary>The value of the action's 'changed' during the previous update</summary>
        /// <remarks>Always returns the previous update state. Set is ignored since get is coming from the native struct.</remarks>
        public override bool lastChanged { get { return lastActionData.bChanged; } protected set { } }

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


        protected InputDigitalActionData_t actionData = new InputDigitalActionData_t();
        protected InputDigitalActionData_t lastActionData = new InputDigitalActionData_t();

        protected SteamVR_Action_Boolean booleanAction;

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> Sets up the internals of the action source before SteamVR has been initialized.
        /// </summary>
        public override void Preinitialize(SteamVR_Action wrappingAction, SteamVR_Input_Sources forInputSource)
        {
            base.Preinitialize(wrappingAction, forInputSource);
            booleanAction = (SteamVR_Action_Boolean)wrappingAction;
        }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> 
        /// Initializes the handle for the inputSource, the action data size, and any other related SteamVR data.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            if (actionData_size == 0)
                actionData_size = (uint)Marshal.SizeOf(typeof(InputDigitalActionData_t));

        }

        /// <summary><strong>[Should not be called by user code]</strong> 
        /// Updates the data for this action and this input source. Sends related events.
        /// </summary>
        public override void UpdateValue()
        {
            lastActionData = actionData;
            lastActive = active;

            EVRInputError err = OpenVR.Input.GetDigitalActionData(action.handle, ref actionData, actionData_size, inputSourceHandle);
            if (err != EVRInputError.None)
                Debug.LogError("<b>[SteamVR]</b> GetDigitalActionData error (" + action.fullPath + "): " + err.ToString() + " handle: " + action.handle.ToString());

            if (changed)
                changedTime = Time.realtimeSinceStartup + actionData.fUpdateTime;

            updateTime = Time.realtimeSinceStartup;

            if (active)
            {
                if (onStateDown != null && stateDown)
                    onStateDown.Invoke(booleanAction, inputSource);

                if (onStateUp != null && stateUp)
                    onStateUp.Invoke(booleanAction, inputSource);

                if (onState != null && state)
                    onState.Invoke(booleanAction, inputSource);

                if (onChange != null && changed)
                    onChange.Invoke(booleanAction, inputSource, state);

                if (onUpdate != null)
                    onUpdate.Invoke(booleanAction, inputSource, state);
            }

            if (onActiveBindingChange != null && lastActiveBinding != activeBinding)
                onActiveBindingChange.Invoke(booleanAction, inputSource, activeBinding);

            if (onActiveChange != null && lastActive != active)
                onActiveChange.Invoke(booleanAction, inputSource, activeBinding);
        }
    }
    
    public interface ISteamVR_Action_Boolean : ISteamVR_Action_In_Source
    {
        /// <summary>The current value of the boolean action. Note: Will only return true if the action is also active.</summary>
        bool state { get; }

        /// <summary>True when the action's state changes from false to true. Note: Will only return true if the action is also active.</summary>
        bool stateDown { get; }

        /// <summary>True when the action's state changes from true to false. Note: Will only return true if the action is also active.</summary>
        bool stateUp { get; }

        /// <summary>The value of the action's 'state' during the previous update</summary>
        /// <remarks>Always returns the previous update state</remarks>
        bool lastState { get; }

        /// <summary>The value of the action's 'stateDown' during the previous update</summary>
        /// <remarks>Always returns the previous update state</remarks>
        bool lastStateDown { get; }

        /// <summary>The value of the action's 'stateUp' during the previous update</summary>
        /// <remarks>Always returns the previous update state</remarks>
        bool lastStateUp { get; }
    }
}