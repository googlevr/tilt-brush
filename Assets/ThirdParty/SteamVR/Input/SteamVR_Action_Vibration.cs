//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using UnityEngine;
using System.Collections;
using System;
using Valve.VR;
using System.Runtime.InteropServices;
using System.Collections.Generic;

#pragma warning disable 0067

namespace Valve.VR
{
    [Serializable]
    /// <summary>
    /// Vibration actions are used to trigger haptic feedback in vr controllers.
    /// </summary>
    public class SteamVR_Action_Vibration : SteamVR_Action_Out<SteamVR_Action_Vibration_Source_Map, SteamVR_Action_Vibration_Source>, ISerializationCallbackReceiver
    {
        public delegate void ActiveChangeHandler(SteamVR_Action_Vibration fromAction, SteamVR_Input_Sources fromSource, bool active);
        public delegate void ExecuteHandler(SteamVR_Action_Vibration fromAction, SteamVR_Input_Sources fromSource, float secondsFromNow, float durationSeconds, float frequency, float amplitude);

        /// <summary><strong>[SteamVR_Input_Sources.Any]</strong> This event fires whenever a change happens in the action</summary>
        public event ActiveChangeHandler onActiveChange
        { add { sourceMap[SteamVR_Input_Sources.Any].onActiveChange += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onActiveChange -= value; } }

        /// <summary><strong>[SteamVR_Input_Sources.Any]</strong> This event fires whenever a change happens in the action</summary>
        public event ActiveChangeHandler onActiveBindingChange
        { add { sourceMap[SteamVR_Input_Sources.Any].onActiveBindingChange += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onActiveBindingChange -= value; } }

        /// <summary><strong>[SteamVR_Input_Sources.Any]</strong> This event fires whenever this action is executed</summary>
        public event ExecuteHandler onExecute
        { add { sourceMap[SteamVR_Input_Sources.Any].onExecute += value; } remove { sourceMap[SteamVR_Input_Sources.Any].onExecute -= value; } }


        public SteamVR_Action_Vibration() { }


        /// <summary>
        /// Trigger the haptics at a certain time for a certain length
        /// </summary>
        /// <param name="secondsFromNow">How long from the current time to execute the action (in seconds - can be 0)</param>
        /// <param name="durationSeconds">How long the haptic action should last (in seconds)</param>
        /// <param name="frequency">How often the haptic motor should bounce (0 - 320 in hz. The lower end being more useful)</param>
        /// <param name="amplitude">How intense the haptic action should be (0 - 1)</param>
        /// <param name="inputSource">The device you would like to execute the haptic action. Any if the action is not device specific.</param>
        public void Execute(float secondsFromNow, float durationSeconds, float frequency, float amplitude, SteamVR_Input_Sources inputSource)
        {
            sourceMap[inputSource].Execute(secondsFromNow, durationSeconds, frequency, amplitude);
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

        /// <summary>Executes a function when the execute method of this action (with the specified inputSource) is called. This happens when the action is bound or unbound</summary>
        /// <param name="functionToCall">A local function that receives the boolean action who's active state changes and the corresponding input source</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void AddOnExecuteListener(ExecuteHandler functionToCall, SteamVR_Input_Sources inputSource)
        {
            sourceMap[inputSource].onExecute += functionToCall;
        }

        /// <summary>Stops executing the function setup by the corresponding AddListener</summary>
        /// <param name="functionToStopCalling">The local function that you've setup to receive update events</param>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public void RemoveOnExecuteListener(ExecuteHandler functionToStopCalling, SteamVR_Input_Sources inputSource)
        {
            sourceMap[inputSource].onExecute -= functionToStopCalling;
        }

        /// <summary>
        /// Returns the last time this action was executed
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public override float GetTimeLastChanged(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].timeLastExecuted;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            InitAfterDeserialize();
        }

        public override bool IsUpdating(SteamVR_Input_Sources inputSource)
        {
            return sourceMap.IsUpdating(inputSource);
        }
    }

    public class SteamVR_Action_Vibration_Source_Map : SteamVR_Action_Source_Map<SteamVR_Action_Vibration_Source>
    {
        public bool IsUpdating(SteamVR_Input_Sources inputSource)
        {
            return sources[inputSource].timeLastExecuted != 0;
        }
    }

    public class SteamVR_Action_Vibration_Source : SteamVR_Action_Out_Source
    {
        /// <summary>Event fires when the active state (ActionSet active and binding active) changes</summary>
        public event SteamVR_Action_Vibration.ActiveChangeHandler onActiveChange;

        /// <summary>Event fires when the active state of the binding changes</summary>
        public event SteamVR_Action_Vibration.ActiveChangeHandler onActiveBindingChange;

        /// <summary>Event fires whenever this action is executed</summary>
        public event SteamVR_Action_Vibration.ExecuteHandler onExecute;

        //todo: fix the active state of out actions
        /// <summary>Returns true if this action is bound and the ActionSet is active</summary>
        public override bool active { get { return activeBinding && setActive; } }

        /// <summary>Returns true if the action is bound</summary>
        public override bool activeBinding { get { return true; } }


        /// <summary>Returns true if the action was bound and the ActionSet was active during the previous update</summary>
        public override bool lastActive { get; protected set; }

        /// <summary>Returns true if the action was bound during the previous update</summary>
        public override bool lastActiveBinding { get { return true; } }

        /// <summary>The last time the execute method was called on this action</summary>
        public float timeLastExecuted { get; protected set; }

        protected SteamVR_Action_Vibration vibrationAction;


        /// <summary>
        /// <strong>[Should not be called by user code]</strong> 
        /// Initializes the handle for the inputSource, and any other related SteamVR data.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            lastActive = true;
        }

        public override void Preinitialize(SteamVR_Action wrappingAction, SteamVR_Input_Sources forInputSource)
        {
            base.Preinitialize(wrappingAction, forInputSource);

            vibrationAction = (SteamVR_Action_Vibration)wrappingAction;
        }


        /// <summary>
        /// Trigger the haptics at a certain time for a certain length
        /// </summary>
        /// <param name="secondsFromNow">How long from the current time to execute the action (in seconds - can be 0)</param>
        /// <param name="durationSeconds">How long the haptic action should last (in seconds)</param>
        /// <param name="frequency">How often the haptic motor should bounce (0 - 320 in hz. The lower end being more useful)</param>
        /// <param name="amplitude">How intense the haptic action should be (0 - 1)</param>
        /// <param name="inputSource">The device you would like to execute the haptic action. Any if the action is not device specific.</param>
        public void Execute(float secondsFromNow, float durationSeconds, float frequency, float amplitude)
        {
            if (SteamVR_Input.isStartupFrame)
                return;

            timeLastExecuted = Time.realtimeSinceStartup;

            EVRInputError err = OpenVR.Input.TriggerHapticVibrationAction(handle, secondsFromNow, durationSeconds, frequency, amplitude, inputSourceHandle);

            //Debug.Log(string.Format("[{5}: haptic] secondsFromNow({0}), durationSeconds({1}), frequency({2}), amplitude({3}), inputSource({4})", secondsFromNow, durationSeconds, frequency, amplitude, inputSource, this.GetShortName()));

            if (err != EVRInputError.None)
                Debug.LogError("<b>[SteamVR]</b> TriggerHapticVibrationAction (" + fullPath + ") error: " + err.ToString() + " handle: " + handle.ToString());

            if (onExecute != null)
                onExecute.Invoke(vibrationAction, inputSource, secondsFromNow, durationSeconds, frequency, amplitude);
        }
    }


    /// <summary>
    /// Vibration actions are used to trigger haptic feedback in vr controllers.
    /// </summary>
    public interface ISteamVR_Action_Vibration : ISteamVR_Action_Out
    {
        /// <summary>
        /// Trigger the haptics at a certain time for a certain length
        /// </summary>
        /// <param name="secondsFromNow">How long from the current time to execute the action (in seconds - can be 0)</param>
        /// <param name="durationSeconds">How long the haptic action should last (in seconds)</param>
        /// <param name="frequency">How often the haptic motor should bounce (0 - 320 in hz. The lower end being more useful)</param>
        /// <param name="amplitude">How intense the haptic action should be (0 - 1)</param>
        /// <param name="inputSource">The device you would like to execute the haptic action. Any if the action is not device specific.</param>
        void Execute(float secondsFromNow, float durationSeconds, float frequency, float amplitude, SteamVR_Input_Sources inputSource);
    }
}

#pragma warning restore 0067