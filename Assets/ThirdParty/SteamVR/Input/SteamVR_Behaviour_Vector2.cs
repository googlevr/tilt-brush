//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using UnityEngine.Events;

namespace Valve.VR
{
    /// <summary>
    /// Simplifies the use of the Vector2 action. Provides an onChange event that fires whenever the vector2 changes.
    /// </summary>
    public class SteamVR_Behaviour_Vector2 : MonoBehaviour
    {
        /// <summary>The vector2 action to get data from</summary>
        public SteamVR_Action_Vector2 vector2Action;

        /// <summary>The device this action applies to. Any if the action is not device specific.</summary>
        [Tooltip("The device this action should apply to. Any if the action is not device specific.")]
        public SteamVR_Input_Sources inputSource;

        /// <summary>Unity event that fires whenever the action's value has changed since the last update.</summary>
        [Tooltip("Fires whenever the action's value has changed since the last update.")]
        public SteamVR_Behaviour_Vector2Event onChange;

        /// <summary>Unity event that fires whenever the action's value has been updated</summary>
        [Tooltip("Fires whenever the action's value has been updated.")]
        public SteamVR_Behaviour_Vector2Event onUpdate;

        /// <summary>Unity event that fires whenever the action's value has been updated and is non-zero</summary>
        [Tooltip("Fires whenever the action's value has been updated and is non-zero.")]
        public SteamVR_Behaviour_Vector2Event onAxis;

        /// <summary>C# event that fires whenever the action's value has changed since the last update.</summary>
        public ChangeHandler onChangeEvent;

        /// <summary>C# event that fires whenever the action's value has been updated</summary>
        public UpdateHandler onUpdateEvent;

        /// <summary>C# event that fires whenever the action's value has been updated and is non-zero</summary>
        public AxisHandler onAxisEvent;

        /// <summary>Returns whether this action is bound and the action set is active</summary>
        public bool isActive { get { return vector2Action.GetActive(inputSource); } }

        protected virtual void OnEnable()
        {
            if (vector2Action == null)
            {
                Debug.LogError("[SteamVR] Vector2 action not set.", this);
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
            vector2Action[inputSource].onUpdate += SteamVR_Behaviour_Vector2_OnUpdate;
            vector2Action[inputSource].onChange += SteamVR_Behaviour_Vector2_OnChange;
            vector2Action[inputSource].onAxis += SteamVR_Behaviour_Vector2_OnAxis;
        }

        protected void RemoveHandlers()
        {
            if (vector2Action != null)
            {
                vector2Action[inputSource].onUpdate -= SteamVR_Behaviour_Vector2_OnUpdate;
                vector2Action[inputSource].onChange -= SteamVR_Behaviour_Vector2_OnChange;
                vector2Action[inputSource].onAxis -= SteamVR_Behaviour_Vector2_OnAxis;
            }
        }

        private void SteamVR_Behaviour_Vector2_OnUpdate(SteamVR_Action_Vector2 fromAction, SteamVR_Input_Sources fromSource, Vector2 newAxis, Vector2 newDelta)
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

        private void SteamVR_Behaviour_Vector2_OnChange(SteamVR_Action_Vector2 fromAction, SteamVR_Input_Sources fromSource, Vector2 newAxis, Vector2 newDelta)
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

        private void SteamVR_Behaviour_Vector2_OnAxis(SteamVR_Action_Vector2 fromAction, SteamVR_Input_Sources fromSource, Vector2 newAxis, Vector2 newDelta)
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
            if (vector2Action != null)
                return vector2Action.GetLocalizedOriginPart(inputSource, localizedParts);
            return null;
        }

        public delegate void AxisHandler(SteamVR_Behaviour_Vector2 fromAction, SteamVR_Input_Sources fromSource, Vector2 newAxis, Vector2 newDelta);
        public delegate void ChangeHandler(SteamVR_Behaviour_Vector2 fromAction, SteamVR_Input_Sources fromSource, Vector2 newAxis, Vector2 newDelta);
        public delegate void UpdateHandler(SteamVR_Behaviour_Vector2 fromAction, SteamVR_Input_Sources fromSource, Vector2 newAxis, Vector2 newDelta);
    }
}