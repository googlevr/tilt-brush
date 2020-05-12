// Copyright 2017 Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;
using UnityEngine.VR;
using System;
using System.Collections;

using Gvr.Internal;

/// Represents the controller's current connection state.
/// All values and semantics below (except for Error) are
/// from gvr_types.h in the GVR C API.
public enum GvrConnectionState
{
    /// Indicates that an error has occurred.
    Error = -1,

    /// Indicates that the controller is disconnected.
    Disconnected = 0,
    /// Indicates that the device is scanning for controllers.
    Scanning = 1,
    /// Indicates that the device is connecting to a controller.
    Connecting = 2,
    /// Indicates that the device is connected to a controller.
    Connected = 3,
};

/// Represents the API status of the current controller state.
/// Values and semantics from gvr_types.h in the GVR C API.
public enum GvrControllerApiStatus
{
    /// A Unity-localized error occurred.
    /// This is the only value that isn't in gvr_types.h.
    Error = -1,

    /// API is happy and healthy. This doesn't mean the controller itself
    /// is connected, it just means that the underlying service is working
    /// properly.
    Ok = 0,

    /// Any other status represents a permanent failure that requires
    /// external action to fix:

    /// API failed because this device does not support controllers (API is too
    /// low, or other required feature not present).
    Unsupported = 1,
    /// This app was not authorized to use the service (e.g., missing permissions,
    /// the app is blacklisted by the underlying service, etc).
    NotAuthorized = 2,
    /// The underlying VR service is not present.
    Unavailable = 3,
    /// The underlying VR service is too old, needs upgrade.
    ApiServiceObsolete = 4,
    /// The underlying VR service is too new, is incompatible with current client.
    ApiClientObsolete = 5,
    /// The underlying VR service is malfunctioning. Try again later.
    ApiMalfunction = 6,
};

/// Represents the controller's current battery level.
/// Values and semantics from gvr_types.h in the GVR C API.
public enum GvrControllerBatteryLevel
{
    /// A Unity-localized error occurred.
    /// This is the only value that isn't in gvr_types.h.
    Error = -1,

    /// The battery state is currently unreported
    Unknown = 0,

    /// Equivalent to 1 out of 5 bars on the battery indicator
    CriticalLow = 1,

    /// Equivalent to 2 out of 5 bars on the battery indicator
    Low = 2,

    /// Equivalent to 3 out of 5 bars on the battery indicator
    Medium = 3,

    /// Equivalent to 4 out of 5 bars on the battery indicator
    AlmostFull = 4,

    /// Equivalent to 5 out of 5 bars on the battery indicator
    Full = 5,
};


/// Main entry point for the Daydream controller API.
///
/// To use this API, add this behavior to a GameObject in your scene, or use the
/// GvrControllerMain prefab. There can only be one object with this behavior on your scene.
///
/// This is a singleton object.
///
/// To access the controller state, simply read the static properties of this class. For example,
/// to know the controller's current orientation, use GvrControllerInput.Orientation.
public class GvrControllerInput : MonoBehaviour
{
    private static GvrControllerInput instance;
    private static IControllerProvider controllerProvider;

    private ControllerState controllerStateLeft = new ControllerState();
    private ControllerState controllerStateRight = new ControllerState();
    private IEnumerator controllerUpdate;
    private WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();
    private Vector2 touchPosCenteredLeft = Vector2.zero;
    private Vector2 touchPosCenteredRight = Vector2.zero;

    /// Event handler for receiving button, track pad, and IMU updates from the controller.
    public static event Action OnControllerInputUpdated;
    public static event Action OnPostControllerInputUpdated;

    public enum EmulatorConnectionMode
    {
        OFF,
        USB,
        WIFI,
    }
    /// Indicates how we connect to the controller emulator.
    [GvrInfo("Hold Shift to use the Mouse as the controller.\n\n" +
             "Controls:  Shift +\n" +
             "   • Move Mouse = Change Orientation\n" +
             "   • Left Mouse Button = ClickButton\n" +
             "   • Right Mouse Button = AppButton\n" +
             "   • Middle Mouse Button = HomeButton/Recenter\n" +
             "   • Ctrl = IsTouching\n" +
             "   • Ctrl + Move Mouse = Change TouchPos", 8)]
    [Tooltip("How to connect to the emulator: USB cable (recommended) or WIFI.")]
    public EmulatorConnectionMode emulatorConnectionMode = EmulatorConnectionMode.USB;

    /// Returns the controller's current connection state.
    public static GvrConnectionState State
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.connectionState : GvrConnectionState.Error;
        }
    }

    /// Returns the API status of the current controller state.
    public static GvrControllerApiStatus ApiStatus
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.apiStatus : GvrControllerApiStatus.Error;
        }
    }

    /// Returns true if battery status is supported.
    public static bool SupportsBatteryStatus
    {
        get
        {
            return controllerProvider != null ? controllerProvider.SupportsBatteryStatus : false;
        }
    }

    /// Returns the controller's current orientation in space, as a quaternion.
    /// The space in which the orientation is represented is the usual Unity space, with
    /// X pointing to the right, Y pointing up and Z pointing forward. Therefore, to make an
    /// object in your scene have the same orientation as the controller, simply assign this
    /// quaternion to the GameObject's transform.rotation.
    public static Quaternion LeftOrientation
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.orientation : Quaternion.identity;
        }
    }

    public static Quaternion RightOrientation
    {
        get
        {
            return instance != null ? instance.controllerStateRight.orientation : Quaternion.identity;
        }
    }

    public static Vector3 LeftPosition
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.position : Vector3.zero;
        }
    }

    public static Vector3 RightPosition
    {
        get
        {
            return instance != null ? instance.controllerStateRight.position : Vector3.zero;
        }
    }

    /// Returns the controller's gyroscope reading. The gyroscope indicates the angular
    /// about each of its local axes. The controller's axes are: X points to the right,
    /// Y points perpendicularly up from the controller's top surface and Z lies
    /// along the controller's body, pointing towards the front. The angular speed is given
    /// in radians per second, using the right-hand rule (positive means a right-hand rotation
    /// about the given axis).
    public static Vector3 GyroLeft
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.gyro : Vector3.zero;
        }
    }
    public static Vector3 GyroRight
    {
        get
        {
            return instance != null ? instance.controllerStateRight.gyro : Vector3.zero;
        }
    }
    /// Returns the controller's accelerometer reading. The accelerometer indicates the
    /// effect of acceleration and gravity in the direction of each of the controller's local
    /// axes. The controller's local axes are: X points to the right, Y points perpendicularly
    /// up from the controller's top surface and Z lies along the controller's body, pointing
    /// towards the front. The acceleration is measured in meters per second squared. Note that
    /// gravity is combined with acceleration, so when the controller is resting on a table top,
    /// it will measure an acceleration of 9.8 m/s^2 on the Y axis. The accelerometer reading
    /// will be zero on all three axes only if the controller is in free fall, or if the user
    /// is in a zero gravity environment like a space station.
    public static Vector3 AccelLeft
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.accel : Vector3.zero;
        }
    }
    public static Vector3 AccelRight
    {
        get
        {
            return instance != null ? instance.controllerStateRight.accel : Vector3.zero;
        }
    }
    /// If true, the user is currently touching the controller's touchpad.
    public static bool IsTouchingLeft
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.isTouching : false;
        }
    }

    /// If true, the user just started touching the touchpad. This is an event flag (it is true
    /// for only one frame after the event happens, then reverts to false).
    public static bool TouchDownLeft
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.touchDown : false;
        }
    }

    /// If true, the user just stopped touching the touchpad. This is an event flag (it is true
    /// for only one frame after the event happens, then reverts to false).
    public static bool TouchUpLeft
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.touchUp : false;
        }
    }

    /// Position of the current touch, if touching the touchpad.
    /// If not touching, this is the position of the last touch (when the finger left the touchpad).
    /// The X and Y range is from 0 to 1.
    /// (0, 0) is the top left of the touchpad and (1, 1) is the bottom right of the touchpad.
    public static Vector2 TouchPosLeft
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.touchPos : new Vector2(0.5f, 0.5f);
        }
    }

    /// Position of the current touch, if touching the touchpad.
    /// If not touching, this is the position of the last touch (when the finger left the touchpad).
    /// The X and Y range is from -1 to 1.  (-.707,-.707) is bottom left, (.707,.707) is upper right.
    /// (0, 0) is the center of the touchpad.
    /// The magnitude of the touch vector is guaranteed to be <= 1.
    public static Vector2 TouchPosCenteredLeft
    {
        get
        {
            return instance != null ? instance.touchPosCenteredLeft : Vector2.zero;
        }
    }
    public static Vector2 TouchPosCenteredRight
    {
        get
        {
            return instance != null ? instance.touchPosCenteredRight : Vector2.zero;
        }
    }

    public static bool IsTouchingRight
    {
        get
        {
            return instance != null ? instance.controllerStateRight.isTouching : false;
        }
    }

    /// If true, the user just started touching the touchpad. This is an event flag (it is true
    /// for only one frame after the event happens, then reverts to false).
    public static bool TouchDownRight
    {
        get
        {
            return instance != null ? instance.controllerStateRight.touchDown : false;
        }
    }

    /// If true, the user just stopped touching the touchpad. This is an event flag (it is true
    /// for only one frame after the event happens, then reverts to false).
    public static bool TouchUpRight
    {
        get
        {
            return instance != null ? instance.controllerStateRight.touchUp : false;
        }
    }

    /// Position of the current touch, if touching the touchpad.
    /// If not touching, this is the position of the last touch (when the finger left the touchpad).
    /// The X and Y range is from 0 to 1.
    /// (0, 0) is the top left of the touchpad and (1, 1) is the bottom right of the touchpad.
    public static Vector2 TouchPosRight
    {
        get
        {
            return instance != null ? instance.controllerStateRight.touchPos : new Vector2(0.5f, 0.5f);
        }
    }

    /// If true, the user is currently performing the recentering gesture. Most apps will want
    /// to pause the interaction while this remains true.
    public static bool Recentering
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.recentering : false;
        }
    }

    /// If true, the user just completed the recenter gesture. The controller's orientation is
    /// now being reported in the new recentered coordinate system (the controller's orientation
    /// when recentering was completed was remapped to mean "forward"). This is an event flag
    /// (it is true for only one frame after the event happens, then reverts to false).
    /// The headset is recentered together with the controller.
    public static bool Recentered
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.recentered : false;
        }
    }

    /// If true, the click button (touchpad button) is currently being pressed. This is not
    /// an event: it represents the button's state (it remains true while the button is being
    /// pressed).
    public static bool ClickButtonLeft
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.clickButtonState : false;
        }
    }

    /// If true, the click button (touchpad button) was just pressed. This is an event flag:
    /// it will be true for only one frame after the event happens.
    public static bool ClickButtonDownLeft
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.clickButtonDown : false;
        }
    }

    /// If true, the click button (touchpad button) was just released. This is an event flag:
    /// it will be true for only one frame after the event happens.
    public static bool ClickButtonUpLeft
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.clickButtonUp : false;
        }
    }
    public static bool ClickButtonRight
    {
        get
        {
            return instance != null ? instance.controllerStateRight.clickButtonState : false;
        }
    }

    /// If true, the click button (touchpad button) was just pressed. This is an event flag:
    /// it will be true for only one frame after the event happens.
    public static bool ClickButtonDownRight
    {
        get
        {
            return instance != null ? instance.controllerStateRight.clickButtonDown : false;
        }
    }

    /// If true, the click button (touchpad button) was just released. This is an event flag:
    /// it will be true for only one frame after the event happens.
    public static bool ClickButtonUpRight
    {
        get
        {
            return instance != null ? instance.controllerStateRight.clickButtonUp : false;
        }
    }





    /// If true, the trigger button  is currently being pressed. This is not
    /// an event: it represents the button's state (it remains true while the button is being
    /// pressed).
    public static bool TriggerButtonLeft
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.triggerButtonState : false;
        }
    }

    /// If true, the trigger button was just pressed. This is an event flag:
    /// it will be true for only one frame after the event happens.
    public static bool TriggerButtonDownLeft
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.triggerButtonDown : false;
        }
    }

    /// If true, the trigger button was just released. This is an event flag:
    /// it will be true for only one frame after the event happens.
    public static bool TriggerButtonUpLeft
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.triggerButtonUp : false;
        }
    }
    public static bool TriggerButtonRight
    {
        get
        {
            return instance != null ? instance.controllerStateRight.triggerButtonState : false;
        }
    }

    /// If true, the trigger button was just pressed. This is an event flag:
    /// it will be true for only one frame after the event happens.
    public static bool TriggerButtonDownRight
    {
        get
        {
            return instance != null ? instance.controllerStateRight.triggerButtonDown : false;
        }
    }

    /// If true, the trigger button was just released. This is an event flag:
    /// it will be true for only one frame after the event happens.
    public static bool TriggerButtonUpRight
    {
        get
        {
            return instance != null ? instance.controllerStateRight.triggerButtonUp : false;
        }
    }







    /// If true, the app button (touchpad button) is currently being pressed. This is not
    /// an event: it represents the button's state (it remains true while the button is being
    /// pressed).
    public static bool AppButtonLeft
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.appButtonState : false;
        }
    }

    /// If true, the app button was just pressed. This is an event flag: it will be true for
    /// only one frame after the event happens.
    public static bool AppButtonDownLeft
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.appButtonDown : false;
        }
    }

    /// If true, the app button was just released. This is an event flag: it will be true for
    /// only one frame after the event happens.
    public static bool AppButtonUpLeft
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.appButtonUp : false;
        }
    }
    public static bool AppButtonRight
    {
        get
        {
            return instance != null ? instance.controllerStateRight.appButtonState : false;
        }
    }

    /// If true, the app button was just pressed. This is an event flag: it will be true for
    /// only one frame after the event happens.
    public static bool AppButtonDownRight
    {
        get
        {
            return instance != null ? instance.controllerStateRight.appButtonDown : false;
        }
    }

    /// If true, the app button was just released. This is an event flag: it will be true for
    /// only one frame after the event happens.
    public static bool AppButtonUpRight
    {
        get
        {
            return instance != null ? instance.controllerStateRight.appButtonUp : false;
        }
    }
    /// If true, the home button was just pressed.
    public static bool HomeButtonDownLeft
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.homeButtonDown : false;
        }
    }

    /// If true, the home button is currently being pressed.
    public static bool HomeButtonStateLeft
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.homeButtonState : false;
        }
    }
    public static bool HomeButtonDownRight
    {
        get
        {
            return instance != null ? instance.controllerStateRight.homeButtonDown : false;
        }
    }

    /// If true, the home button is currently being pressed.
    public static bool HomeButtonStateRight
    {
        get
        {
            return instance != null ? instance.controllerStateRight.homeButtonState : false;
        }
    }

    /// If State == GvrConnectionState.Error, this contains details about the error.
    public static string ErrorDetails
    {
        get
        {
            if (instance != null)
            {
                return instance.controllerStateLeft.connectionState == GvrConnectionState.Error ?
                  instance.controllerStateLeft.errorDetails : "";
            }
            else
            {
                return "GvrController instance not found in scene. It may be missing, or it might "
                  + "not have initialized yet.";
            }
        }
    }

    // Returns the GVR C library controller state pointer (gvr_controller_state*).
    /*
    public static IntPtr StatePtr {
      get {
        return instance != null? instance.controllerState.gvrPtr : IntPtr.Zero;
      }
    }
    */

    /// If true, the user is currently touching the controller's touchpad.
    public static bool IsCharging
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.isCharging : false;
        }
    }

    /// If true, the user is currently touching the controller's touchpad.
    public static GvrControllerBatteryLevel BatteryLevel
    {
        get
        {
            return instance != null ? instance.controllerStateLeft.batteryLevel : GvrControllerBatteryLevel.Error;
        }
    }

    void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("More than one GvrController instance was found in your scene. "
              + "Ensure that there is only one GvrControllerInput.");
            this.enabled = false;
            return;
        }
        instance = this;
        if (controllerProvider == null)
        {
            controllerProvider = ControllerProviderFactory.CreateControllerProvider(this);
        }

        // Keep screen on here, since GvrController must be in any GVR scene in order to enable
        // controller capabilities.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    void OnDestroy()
    {
        instance = null;
    }

    private void UpdateController()
    {
        controllerProvider.ReadState(controllerStateLeft, 0);
        controllerProvider.ReadState(controllerStateRight, 1);

#if UNITY_EDITOR
        // If a headset recenter was requested, do it now.
        if (controllerStateLeft.recentered)
        {
            for (int i = 0; i < Camera.allCameras.Length; i++)
            {
                Camera cam = Camera.allCameras[i];
                // Do not reset pitch, which is how it works on the device.
                cam.transform.localRotation = Quaternion.Euler(cam.transform.localRotation.eulerAngles.x, 0, 0);
            }
        }
#endif  // UNITY_EDITOR
    }

    void OnApplicationPause(bool paused)
    {
        if (null == controllerProvider) return;
        if (paused)
        {
            controllerProvider.OnPause();
        }
        else
        {
            controllerProvider.OnResume();
        }
    }

    void OnEnable()
    {
        controllerUpdate = EndOfFrame();
        StartCoroutine(controllerUpdate);
    }

    void OnDisable()
    {
        StopCoroutine(controllerUpdate);
    }

    private void UpdateTouchPosCentered()
    {
        if (instance == null)
        {
            return;
        }

        touchPosCenteredLeft.x = (instance.controllerStateLeft.touchPos.x - 0.5f) * 2.0f;
        touchPosCenteredLeft.y = -(instance.controllerStateLeft.touchPos.y - 0.5f) * 2.0f;

        float magnitude = touchPosCenteredLeft.magnitude;
        if (magnitude > 1)
        {
            touchPosCenteredLeft.x /= magnitude;
            touchPosCenteredLeft.y /= magnitude;
        }

        touchPosCenteredRight.x = (instance.controllerStateRight.touchPos.x - 0.5f) * 2.0f;
        touchPosCenteredRight.y = -(instance.controllerStateRight.touchPos.y - 0.5f) * 2.0f;

        magnitude = touchPosCenteredRight.magnitude;
        if (magnitude > 1)
        {
            touchPosCenteredRight.x /= magnitude;
            touchPosCenteredRight.y /= magnitude;
        }
    }

    IEnumerator EndOfFrame()
    {
        while (true)
        {
            // This must be done at the end of the frame to ensure that all GameObjects had a chance
            // to read transient controller state (e.g. events, etc) for the current frame before
            // it gets reset.
            yield return waitForEndOfFrame;
            UpdateController();
            UpdateTouchPosCentered();

            if (OnControllerInputUpdated != null)
            {
                OnControllerInputUpdated();
            }

            if (OnPostControllerInputUpdated != null)
            {
                OnPostControllerInputUpdated();
            }
        }
    }
}
