//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using UnityEngine.Events;

namespace Valve.VR
{
    public class SteamVR_Behaviour_Vector3 : MonoBehaviour
    {
        /// <summary>The vector3 action to get data from</summary>
        public SteamVR_Action_Vector3 vector3Action;

        /// <summary>The device this action applies to. Any if the action is not device specific.</summary>
        [Tooltip("The device this action should apply to. Any if the action is not device specific.")]
        public SteamVR_Input_Sources inputSource;

        /// <summary>Unity event that fires whenever the action's value has changed since the last update.</summary>
        [Tooltip("Fires whenever the action's value has changed since the last update.")]
        public SteamVR_Behaviour_Vector3Event onChange;

        /// <summary>Unity event that fires whenever the action's value has been updated</summary>
        [Tooltip("Fires whenever the action's value has been updated.")]
        public SteamVR_Behaviour_Vector3Event onUpdate;

        /// <summary>Unity event that fires whenever the action's value has been updated and is non-zero</summary>
        [Tooltip("Fires whenever the action's value has been updated and is non-zero.")]
        public SteamVR_Behaviour_Vector3Event onAxis;

        /// <summary>C# event that fires whenever the action's value has changed since the last update.</summary>
        public ChangeHandler onChangeEvent;

        /// <summary>C# event that fires whenever the action's value has been updated</summary>
        public UpdateHandler onUpdateEvent;

        /// <summary>C# event that fires whenever the action's value has been updated and is non-zero</summary>
        public AxisHandler onAxisEvent;


        /// <summary>Returns whether this action is bound and the action set is active</summary>
        public bool isActive { get { return vector3Action.GetActive(inputSource); } }

        protected virtual void OnEnable()
        {
            if (vector3Action == null)
            {
                Debug.LogError("[SteamVR] Vector3 action not set.", this);
                return;
            }

            AddHandlers();
        }

        protected virtual void OnDisable()
        {
            RemoveHandlers();
        }

        protected void AddHandlers()
        {
            vector3Action[inputSource].onUpdate += SteamVR_Behaviour_Vector3_OnUpdate;
            vector3Action[inputSource].onChange += SteamVR_Behaviour_Vector3_OnChange;
            vector3Action[inputSource].onAxis += SteamVR_Behaviour_Vector3_OnAxis;
        }

        protected void RemoveHandlers()
        {
            if (vector3Action != null)
            {
                vector3Action[inputSource].onUpdate -= SteamVR_Behaviour_Vector3_OnUpdate;
                vector3Action[inputSource].onChange -= SteamVR_Behaviour_Vector3_OnChange;
                vector3Action[inputSource].onAxis -= SteamVR_Behaviour_Vector3_OnAxis;
            }
        }

        private void SteamVR_Behaviour_Vector3_OnUpdate(SteamVR_Action_Vector3 fromAction, SteamVR_Input_Sources fromSource, Vector3 newAxis, Vector3 newDelta)
        {
            if (onUpdate != null)
            {
                onUpdate.Invoke(this, fromSource, newAxis, newDelta);
            }
            if (onUpdateEvent != null)
            {
                onUpdateEvent.Invoke(this, fromSource, newAxis, newDelta);
            }
        }

        private void SteamVR_Behaviour_Vector3_OnChange(SteamVR_Action_Vector3 fromAction, SteamVR_Input_Sources fromSource, Vector3 newAxis, Vector3 newDelta)
        {
            if (onChange != null)
            {
                onChange.Invoke(this, fromSource, newAxis, newDelta);
            }
            if (onChangeEvent != null)
            {
                onChangeEvent.Invoke(this, fromSource, newAxis, newDelta);
            }
        }

        private void SteamVR_Behaviour_Vector3_OnAxis(SteamVR_Action_Vector3 fromAction, SteamVR_Input_Sources fromSource, Vector3 newAxis, Vector3 newDelta)
        {
            if (onAxis != null)
            {
                onAxis.Invoke(this, fromSource, newAxis, newDelta);
            }
            if (onAxisEvent != null)
            {
                onAxisEvent.Invoke(this, fromSource, newAxis, newDelta);
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
            if (vector3Action != null)
                return vector3Action.GetLocalizedOriginPart(inputSource, localizedParts);
            return null;
        }

        public delegate void AxisHandler(SteamVR_Behaviour_Vector3 fromAction, SteamVR_Input_Sources fromSource, Vector3 newAxis, Vector3 newDelta);
        public delegate void ChangeHandler(SteamVR_Behaviour_Vector3 fromAction, SteamVR_Input_Sources fromSource, Vector3 newAxis, Vector3 newDelta);
        public delegate void UpdateHandler(SteamVR_Behaviour_Vector3 fromAction, SteamVR_Input_Sources fromSource, Vector3 newAxis, Vector3 newDelta);
    }
}