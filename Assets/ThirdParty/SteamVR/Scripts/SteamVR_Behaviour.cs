using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

#if UNITY_2017_2_OR_NEWER
    using UnityEngine.XR;
#else
using XRSettings = UnityEngine.VR.VRSettings;
using XRDevice = UnityEngine.VR.VRDevice;
#endif

namespace Valve.VR
{
    public class SteamVR_Behaviour : MonoBehaviour
    {
        private const string openVRDeviceName = "OpenVR";
        public static bool forcingInitialization = false;

        private static SteamVR_Behaviour _instance;
        public static SteamVR_Behaviour instance
        {
            get
            {
                if (_instance == null)
                {
                    Initialize(false);
                }

                return _instance;
            }
        }

        public bool initializeSteamVROnAwake = true;

        public bool doNotDestroy = true;

        [HideInInspector]
        public SteamVR_Render steamvr_render;

        internal static bool isPlaying = false;

        private static bool initializing = false;
        public static void Initialize(bool forceUnityVRToOpenVR = false)
        {
            if (_instance == null && initializing == false)
            {
                initializing = true;
                GameObject steamVRObject = null;

                if (forceUnityVRToOpenVR)
                    forcingInitialization = true;

                SteamVR_Render renderInstance = GameObject.FindObjectOfType<SteamVR_Render>();
                if (renderInstance != null)
                    steamVRObject = renderInstance.gameObject;

                SteamVR_Behaviour behaviourInstance = GameObject.FindObjectOfType<SteamVR_Behaviour>();
                if (behaviourInstance != null)
                    steamVRObject = behaviourInstance.gameObject;

                if (steamVRObject == null)
                {
                    GameObject objectInstance = new GameObject("[SteamVR]");
                    _instance = objectInstance.AddComponent<SteamVR_Behaviour>();
                    _instance.steamvr_render = objectInstance.AddComponent<SteamVR_Render>();
                }
                else
                {
                    behaviourInstance = steamVRObject.GetComponent<SteamVR_Behaviour>();
                    if (behaviourInstance == null)
                        behaviourInstance = steamVRObject.AddComponent<SteamVR_Behaviour>();

                    if (renderInstance != null)
                        behaviourInstance.steamvr_render = renderInstance;
                    else
                    {
                        behaviourInstance.steamvr_render = steamVRObject.GetComponent<SteamVR_Render>();
                        if (behaviourInstance.steamvr_render == null)
                            behaviourInstance.steamvr_render = steamVRObject.AddComponent<SteamVR_Render>();
                    }

                    _instance = behaviourInstance;
                }

                if (_instance != null && _instance.doNotDestroy)
                    GameObject.DontDestroyOnLoad(_instance.transform.root.gameObject);

                initializing = false;
            }
        }

        protected void Awake()
        {
            isPlaying = true;

            if (initializeSteamVROnAwake && forcingInitialization == false)
                InitializeSteamVR();
        }

        public void InitializeSteamVR(bool forceUnityVRToOpenVR = false)
        {
            if (forceUnityVRToOpenVR)
            {
                forcingInitialization = true;

                if (initializeCoroutine != null)
                    StopCoroutine(initializeCoroutine);

                if (XRSettings.loadedDeviceName == openVRDeviceName)
                    EnableOpenVR();
                else
                    initializeCoroutine = StartCoroutine(DoInitializeSteamVR(forceUnityVRToOpenVR));
            }
            else
            {
                SteamVR.Initialize(false);
            }
        }

        private Coroutine initializeCoroutine;

#if UNITY_2018_3_OR_NEWER
        private bool loadedOpenVRDeviceSuccess = false;
        private IEnumerator DoInitializeSteamVR(bool forceUnityVRToOpenVR = false)
        {
            XRDevice.deviceLoaded += XRDevice_deviceLoaded;
            XRSettings.LoadDeviceByName(openVRDeviceName);
            while (loadedOpenVRDeviceSuccess == false)
            {
                yield return null;
            }
            XRDevice.deviceLoaded -= XRDevice_deviceLoaded;
            EnableOpenVR();
        }

        private void XRDevice_deviceLoaded(string deviceName)
        {
            if (deviceName == openVRDeviceName)
            {
                loadedOpenVRDeviceSuccess = true;
            }
            else
            {
                Debug.LogError("<b>[SteamVR]</b> Tried to async load: " + openVRDeviceName + ". Loaded: " + deviceName);
                loadedOpenVRDeviceSuccess = true; //try anyway
            }
        }
#else
        private IEnumerator DoInitializeSteamVR(bool forceUnityVRToOpenVR = false)
        {
            XRSettings.LoadDeviceByName(openVRDeviceName);
            yield return null;
            EnableOpenVR();
        }
#endif

        private void EnableOpenVR()
        {
            XRSettings.enabled = true;
            SteamVR.Initialize(false);
            initializeCoroutine = null;
            forcingInitialization = false;
        }

#if UNITY_EDITOR
        //only stop playing if the unity editor is running
        private void OnDestroy()
        {
            isPlaying = false;
        }
#endif

#if UNITY_2017_1_OR_NEWER
        protected void OnEnable()
        {
		    Application.onBeforeRender += OnBeforeRender;
            SteamVR_Events.System(EVREventType.VREvent_Quit).Listen(OnQuit);
        }
        protected void OnDisable()
        {
		    Application.onBeforeRender -= OnBeforeRender;
            SteamVR_Events.System(EVREventType.VREvent_Quit).Remove(OnQuit);
        }
	    protected void OnBeforeRender() 
        { 
            PreCull();
        }
#else
        protected void OnEnable()
        {
            Camera.onPreCull += OnCameraPreCull;
            SteamVR_Events.System(EVREventType.VREvent_Quit).Listen(OnQuit);
        }
        protected void OnDisable()
        {
            Camera.onPreCull -= OnCameraPreCull;
            SteamVR_Events.System(EVREventType.VREvent_Quit).Remove(OnQuit);
        }
        protected void OnCameraPreCull(Camera cam)
        {
            if (!cam.stereoEnabled)
                return;

            PreCull();
        }
#endif

        protected static int lastFrameCount = -1;
        protected void PreCull()
        {
            // Only update poses on the first camera per frame.
            if (Time.frameCount != lastFrameCount)
            {
                lastFrameCount = Time.frameCount;

                SteamVR_Input.OnPreCull();
            }
        }

        protected void FixedUpdate()
        {
            SteamVR_Input.FixedUpdate();
        }

        protected void LateUpdate()
        {
            SteamVR_Input.LateUpdate();
        }

        protected void Update()
        {
            SteamVR_Input.Update();
        }

        protected void OnQuit(VREvent_t vrEvent)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
		    Application.Quit();
#endif
        }
    }
}
