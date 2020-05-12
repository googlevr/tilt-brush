//======= Copyright (c) Valve Corporation, All rights reserved. ===============
//
// Purpose: Access to SteamVR system (hmd) and compositor (distort) interfaces.
//
//=============================================================================

using UnityEngine;
using Valve.VR;
using System.IO;
using System.Linq;

#if UNITY_2017_2_OR_NEWER
    using UnityEngine.XR;
#else
using XRSettings = UnityEngine.VR.VRSettings;
using XRDevice = UnityEngine.VR.VRDevice;
#endif

namespace Valve.VR
{
    public class SteamVR : System.IDisposable
    {
        // Use this to check if SteamVR is currently active without attempting
        // to activate it in the process.
        public static bool active { get { return _instance != null; } }

        // Set this to false to keep from auto-initializing when calling SteamVR.instance.
        private static bool _enabled = true;
        public static bool enabled
        {
            get
            {
                if (!XRSettings.enabled)
                    enabled = false;
                return _enabled;
            }
            set
            {
                _enabled = value;

                if (_enabled)
                {
                    Initialize();
                }
                else
                {
                    SafeDispose();
                }
            }
        }

        private static SteamVR _instance;
        public static SteamVR instance
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    return null;
#endif
                if (!enabled)
                    return null;

                if (_instance == null)
                {
                    _instance = CreateInstance();

                    // If init failed, then auto-disable so scripts don't continue trying to re-initialize things.
                    if (_instance == null)
                        _enabled = false;
                }

                return _instance;
            }
        }

        public enum InitializedStates
        {
            None,
            Initializing,
            InitializeSuccess,
            InitializeFailure,
        }

        public static InitializedStates initializedState = InitializedStates.None;

        public static void Initialize(bool forceUnityVRMode = false)
        {
            if (forceUnityVRMode)
            {
                SteamVR_Behaviour.instance.InitializeSteamVR(true);
                return;
            }
            else
            {
                if (_instance == null)
                {
                    _instance = CreateInstance();
                    if (_instance == null)
                        _enabled = false;
                }
            }

            if (_enabled)
                SteamVR_Behaviour.Initialize(forceUnityVRMode);
        }

        public static bool usingNativeSupport
        {
            get { return XRDevice.GetNativePtr() != System.IntPtr.Zero; }
        }

        public static SteamVR_Settings settings { get; private set; }

        private static void ReportGeneralErrors()
        {
            string errorLog = "<b>[SteamVR]</b> Initialization failed. ";

            if (XRSettings.enabled == false)
                errorLog += "VR may be disabled in player settings. Go to player settings in the editor and check the 'Virtual Reality Supported' checkbox'. ";
            if (XRSettings.supportedDevices != null && XRSettings.supportedDevices.Length > 0)
            {
                if (XRSettings.supportedDevices.Contains("OpenVR") == false)
                    errorLog += "OpenVR is not in your list of supported virtual reality SDKs. Add it to the list in player settings. ";
                else if (XRSettings.supportedDevices.First().Contains("OpenVR") == false)
                    errorLog += "OpenVR is not first in your list of supported virtual reality SDKs. <b>This is okay, but if you have an Oculus device plugged in, and Oculus above OpenVR in this list, it will try and use the Oculus SDK instead of OpenVR.</b> ";
            }
            else
            {
                errorLog += "You have no SDKs in your Player Settings list of supported virtual reality SDKs. Add OpenVR to it. ";
            }

            errorLog += "To force OpenVR initialization call SteamVR.Initialize(true). ";

            Debug.LogWarning(errorLog);
        }

        private static SteamVR CreateInstance()
        {
            initializedState = InitializedStates.Initializing;

            try
            {
                var error = EVRInitError.None;
                if (!SteamVR.usingNativeSupport)
                {
                    ReportGeneralErrors();
                    initializedState = InitializedStates.InitializeFailure;
                    SteamVR_Events.Initialized.Send(false);
                    return null;
                }

                // Verify common interfaces are valid.

                OpenVR.GetGenericInterface(OpenVR.IVRCompositor_Version, ref error);
                if (error != EVRInitError.None)
                {
                    initializedState = InitializedStates.InitializeFailure;
                    ReportError(error);
                    ReportGeneralErrors();
                    SteamVR_Events.Initialized.Send(false);
                    return null;
                }

                OpenVR.GetGenericInterface(OpenVR.IVROverlay_Version, ref error);
                if (error != EVRInitError.None)
                {
                    initializedState = InitializedStates.InitializeFailure;
                    ReportError(error);
                    SteamVR_Events.Initialized.Send(false);
                    return null;
                }

                OpenVR.GetGenericInterface(OpenVR.IVRInput_Version, ref error);
                if (error != EVRInitError.None)
                {
                    initializedState = InitializedStates.InitializeFailure;
                    ReportError(error);
                    SteamVR_Events.Initialized.Send(false);
                    return null;
                }

                settings = SteamVR_Settings.instance;

                if (Application.isEditor)
                    IdentifyEditorApplication();

                SteamVR_Input.IdentifyActionsFile();

                if (SteamVR_Settings.instance.inputUpdateMode != SteamVR_UpdateModes.Nothing || SteamVR_Settings.instance.poseUpdateMode != SteamVR_UpdateModes.Nothing)
                {
                    SteamVR_Input.Initialize();

#if UNITY_EDITOR
                    if (SteamVR_Input.IsOpeningSetup())
                        return null;
#endif
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("<b>[SteamVR]</b> " + e);
                SteamVR_Events.Initialized.Send(false);
                return null;
            }

            _enabled = true;
            initializedState = InitializedStates.InitializeSuccess;
            SteamVR_Events.Initialized.Send(true);
            return new SteamVR();
        }

        static void ReportError(EVRInitError error)
        {
            switch (error)
            {
                case EVRInitError.None:
                    break;
                case EVRInitError.VendorSpecific_UnableToConnectToOculusRuntime:
                    Debug.LogWarning("<b>[SteamVR]</b> Initialization Failed!  Make sure device is on, Oculus runtime is installed, and OVRService_*.exe is running.");
                    break;
                case EVRInitError.Init_VRClientDLLNotFound:
                    Debug.LogWarning("<b>[SteamVR]</b> Drivers not found!  They can be installed via Steam under Library > Tools.  Visit http://steampowered.com to install Steam.");
                    break;
                case EVRInitError.Driver_RuntimeOutOfDate:
                    Debug.LogWarning("<b>[SteamVR]</b> Initialization Failed!  Make sure device's runtime is up to date.");
                    break;
                default:
                    Debug.LogWarning("<b>[SteamVR]</b> " + OpenVR.GetStringForHmdError(error));
                    break;
            }
        }

        // native interfaces
        public CVRSystem hmd { get; private set; }
        public CVRCompositor compositor { get; private set; }
        public CVROverlay overlay { get; private set; }

        // tracking status
        static public bool initializing { get; private set; }
        static public bool calibrating { get; private set; }
        static public bool outOfRange { get; private set; }

        static public bool[] connected = new bool[OpenVR.k_unMaxTrackedDeviceCount];

        // render values
        public float sceneWidth { get; private set; }
        public float sceneHeight { get; private set; }
        public float aspect { get; private set; }
        public float fieldOfView { get; private set; }
        public Vector2 tanHalfFov { get; private set; }
        public VRTextureBounds_t[] textureBounds { get; private set; }
        public SteamVR_Utils.RigidTransform[] eyes { get; private set; }
        public ETextureType textureType;

        // hmd properties
        public string hmd_TrackingSystemName { get { return GetStringProperty(ETrackedDeviceProperty.Prop_TrackingSystemName_String); } }
        public string hmd_ModelNumber { get { return GetStringProperty(ETrackedDeviceProperty.Prop_ModelNumber_String); } }
        public string hmd_SerialNumber { get { return GetStringProperty(ETrackedDeviceProperty.Prop_SerialNumber_String); } }

        public float hmd_SecondsFromVsyncToPhotons { get { return GetFloatProperty(ETrackedDeviceProperty.Prop_SecondsFromVsyncToPhotons_Float); } }
        public float hmd_DisplayFrequency { get { return GetFloatProperty(ETrackedDeviceProperty.Prop_DisplayFrequency_Float); } }

        public string GetTrackedDeviceString(uint deviceId)
        {
            var error = ETrackedPropertyError.TrackedProp_Success;
            var capacity = hmd.GetStringTrackedDeviceProperty(deviceId, ETrackedDeviceProperty.Prop_AttachedDeviceId_String, null, 0, ref error);
            if (capacity > 1)
            {
                var result = new System.Text.StringBuilder((int)capacity);
                hmd.GetStringTrackedDeviceProperty(deviceId, ETrackedDeviceProperty.Prop_AttachedDeviceId_String, result, capacity, ref error);
                return result.ToString();
            }
            return null;
        }

        public string GetStringProperty(ETrackedDeviceProperty prop, uint deviceId = OpenVR.k_unTrackedDeviceIndex_Hmd)
        {
            var error = ETrackedPropertyError.TrackedProp_Success;
            var capactiy = hmd.GetStringTrackedDeviceProperty(deviceId, prop, null, 0, ref error);
            if (capactiy > 1)
            {
                var result = new System.Text.StringBuilder((int)capactiy);
                hmd.GetStringTrackedDeviceProperty(deviceId, prop, result, capactiy, ref error);
                return result.ToString();
            }
            return (error != ETrackedPropertyError.TrackedProp_Success) ? error.ToString() : "<unknown>";
        }

        public float GetFloatProperty(ETrackedDeviceProperty prop, uint deviceId = OpenVR.k_unTrackedDeviceIndex_Hmd)
        {
            var error = ETrackedPropertyError.TrackedProp_Success;
            return hmd.GetFloatTrackedDeviceProperty(deviceId, prop, ref error);
        }


        private static bool runningTemporarySession = false;
        public static bool InitializeTemporarySession(bool initInput = false)
        {
            if (Application.isEditor)
            {
                //bool needsInit = (!active && !usingNativeSupport && !runningTemporarySession);

                EVRInitError initError = EVRInitError.None;
                OpenVR.GetGenericInterface(OpenVR.IVRCompositor_Version, ref initError);
                bool needsInit = initError != EVRInitError.None;

                if (needsInit)
                {
                    EVRInitError error = EVRInitError.None;
                    OpenVR.Init(ref error, EVRApplicationType.VRApplication_Overlay);

                    if (error != EVRInitError.None)
                    {
                        Debug.LogError("<b>[SteamVR]</b> Error during OpenVR Init: " + error.ToString());
                        return false;
                    }

                    IdentifyEditorApplication(false);

                    SteamVR_Input.IdentifyActionsFile(false);

                    runningTemporarySession = true;
                }

                if (initInput)
                {
                    SteamVR_Input.Initialize(true);
                }

                return needsInit;
            }

            return false;
        }

        public static void ExitTemporarySession()
        {
            if (runningTemporarySession)
            {
                OpenVR.Shutdown();
                runningTemporarySession = false;
            }
        }

#if UNITY_EDITOR
        public static void ShowBindingsForEditor()
        {
            bool temporarySession = InitializeTemporarySession(false);

            Valve.VR.EVRSettingsError bindingFlagError = Valve.VR.EVRSettingsError.None;
            Valve.VR.OpenVR.Settings.SetBool(Valve.VR.OpenVR.k_pch_SteamVR_Section, Valve.VR.OpenVR.k_pch_SteamVR_DebugInputBinding, true, ref bindingFlagError);

            if (bindingFlagError != Valve.VR.EVRSettingsError.None)
                Debug.LogError("<b>[SteamVR]</b> Error turning on the debug input binding flag in steamvr: " + bindingFlagError.ToString());

            if (Application.isPlaying == false)
            {
                IdentifyEditorApplication();

                SteamVR_Input.IdentifyActionsFile();
            }

            if (temporarySession)
                ExitTemporarySession();

            string bindingurl = "http://localhost:8998/dashboard/controllerbinding.html?app=" + SteamVR_Settings.instance.editorAppKey;

#if UNITY_STANDALONE_WIN
            SteamVR_Windows_Editor_Helper.BrowserApplication browser = SteamVR_Windows_Editor_Helper.GetDefaultBrowser();
            if (browser == SteamVR_Windows_Editor_Helper.BrowserApplication.Unknown)
            {
                Debug.LogError("<b>[SteamVR]</b> Unfortunately we were unable to detect your default browser. You may need to manually open the controller binding UI from SteamVR if it does not open successfully. SteamVR Menu -> Devices -> Controller Input Binding. Press play in your application to get it running then select it under Current Application.");
            }
            else if (browser == SteamVR_Windows_Editor_Helper.BrowserApplication.Edge)
            {
                Debug.LogError("<b>[SteamVR]</b> Microsoft Edge sometimes has issues with opening localhost webpages. You may need to manually open the controller binding UI from SteamVR if it did not load successfully. SteamVR Menu -> Devices -> Controller Input Binding. Press play in your application to get it running then select it under Current Application.");
            }
#endif
            Application.OpenURL(bindingurl); //todo: update with the actual api call
        }




        public static string GetResourcesFolderPath(bool fromAssetsDirectory = false)
        {
            SteamVR_Settings asset = ScriptableObject.CreateInstance<SteamVR_Settings>();
            UnityEditor.MonoScript scriptAsset = UnityEditor.MonoScript.FromScriptableObject(asset);

            string scriptPath = UnityEditor.AssetDatabase.GetAssetPath(scriptAsset);

            System.IO.FileInfo fi = new System.IO.FileInfo(scriptPath);
            string rootPath = fi.Directory.Parent.ToString();

            string resourcesPath = System.IO.Path.Combine(rootPath, "Resources");

            resourcesPath = resourcesPath.Replace("//", "/");
            resourcesPath = resourcesPath.Replace("\\\\", "\\");
            resourcesPath = resourcesPath.Replace("\\", "/");

            if (fromAssetsDirectory)
            {
                int assetsIndex = resourcesPath.IndexOf("/Assets/");
                resourcesPath = resourcesPath.Substring(assetsIndex + 1);
            }

            return resourcesPath;
        }
#endif


        public const string defaultUnityAppKeyTemplate = "application.generated.unity.{0}.exe";
        public const string defaultAppKeyTemplate = "application.generated.{0}";

        public static string GenerateAppKey()
        {
            string productName = GenerateCleanProductName();

            return string.Format(defaultUnityAppKeyTemplate, productName);
        }

        public static string GenerateCleanProductName()
        {
            string productName = Application.productName;
            if (string.IsNullOrEmpty(productName))
                productName = "unnamed_product";
            else
            {
                productName = System.Text.RegularExpressions.Regex.Replace(Application.productName, "[^\\w\\._]", "");
                productName = productName.ToLower();
            }

            return productName;
        }

        private static string GetManifestFile()
        {
            string currentPath = Application.dataPath;
            int lastIndex = currentPath.LastIndexOf('/');
            currentPath = currentPath.Remove(lastIndex, currentPath.Length - lastIndex);

            string fullPath = Path.Combine(currentPath, "unityProject.vrmanifest");

            FileInfo fullManifestPath = new FileInfo(SteamVR_Settings.instance.actionsFilePath);

            if (File.Exists(fullPath))
            {
                string jsonText = File.ReadAllText(fullPath);
                SteamVR_Input_ManifestFile existingFile = Valve.Newtonsoft.Json.JsonConvert.DeserializeObject<SteamVR_Input_ManifestFile>(jsonText);

                if (existingFile != null && existingFile.applications != null && existingFile.applications.Count > 0 &&
                    existingFile.applications[0].app_key != SteamVR_Settings.instance.editorAppKey)
                {
                    Debug.Log("<b>[SteamVR]</b> Deleting existing VRManifest because it has a different app key.");
                    FileInfo existingInfo = new FileInfo(fullPath);
                    if (existingInfo.IsReadOnly)
                        existingInfo.IsReadOnly = false;
                    existingInfo.Delete();
                }

                if (existingFile != null && existingFile.applications != null && existingFile.applications.Count > 0 &&
                    existingFile.applications[0].action_manifest_path != fullManifestPath.FullName)
                {
                    Debug.Log("<b>[SteamVR]</b> Deleting existing VRManifest because it has a different action manifest path:" +
                        "\nExisting:" + existingFile.applications[0].action_manifest_path +
                        "\nNew: " + fullManifestPath.FullName);
                    FileInfo existingInfo = new FileInfo(fullPath);
                    if (existingInfo.IsReadOnly)
                        existingInfo.IsReadOnly = false;
                    existingInfo.Delete();
                }
            }

            if (File.Exists(fullPath) == false)
            {
                SteamVR_Input_ManifestFile manifestFile = new SteamVR_Input_ManifestFile();
                manifestFile.source = "Unity";
                SteamVR_Input_ManifestFile_Application manifestApplication = new SteamVR_Input_ManifestFile_Application();
                manifestApplication.app_key = SteamVR_Settings.instance.editorAppKey;
                manifestApplication.action_manifest_path = fullManifestPath.FullName;
                manifestApplication.launch_type = "url";
                //manifestApplication.binary_path_windows = SteamVR_Utils.ConvertToForwardSlashes(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                //manifestApplication.binary_path_linux = SteamVR_Utils.ConvertToForwardSlashes(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                //manifestApplication.binary_path_osx = SteamVR_Utils.ConvertToForwardSlashes(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                manifestApplication.url = "steam://launch/";
                manifestApplication.strings.Add("en_us", new SteamVR_Input_ManifestFile_ApplicationString() { name = string.Format("{0} [Testing]", Application.productName) });

                /*
                var bindings = new System.Collections.Generic.List<SteamVR_Input_ManifestFile_Application_Binding>();

                SteamVR_Input.InitializeFile();
                if (SteamVR_Input.actionFile != null)
                {
                    string[] bindingFiles = SteamVR_Input.actionFile.GetFilesToCopy(true);
                    if (bindingFiles.Length == SteamVR_Input.actionFile.default_bindings.Count)
                    {
                        for (int bindingIndex = 0; bindingIndex < bindingFiles.Length; bindingIndex++)
                        {
                            SteamVR_Input_ManifestFile_Application_Binding binding = new SteamVR_Input_ManifestFile_Application_Binding();
                            binding.binding_url = bindingFiles[bindingIndex];
                            binding.controller_type = SteamVR_Input.actionFile.default_bindings[bindingIndex].controller_type;
                            bindings.Add(binding);
                        }
                        manifestApplication.bindings = bindings;
                    }
                    else
                    {
                        Debug.LogError("<b>[SteamVR]</b> Mismatch in available binding files.");
                    }
                }
                else
                {
                    Debug.LogError("<b>[SteamVR]</b> Could not load actions file.");
                }
                */

                manifestFile.applications = new System.Collections.Generic.List<SteamVR_Input_ManifestFile_Application>();
                manifestFile.applications.Add(manifestApplication);

                string json = Valve.Newtonsoft.Json.JsonConvert.SerializeObject(manifestFile, Valve.Newtonsoft.Json.Formatting.Indented,
                    new Valve.Newtonsoft.Json.JsonSerializerSettings { NullValueHandling = Valve.Newtonsoft.Json.NullValueHandling.Ignore });

                File.WriteAllText(fullPath, json);
            }

            return fullPath;
        }

        private static void IdentifyEditorApplication(bool showLogs = true)
        {
            //bool isInstalled = OpenVR.Applications.IsApplicationInstalled(SteamVR_Settings.instance.editorAppKey);

            string manifestPath = GetManifestFile();

            EVRApplicationError addManifestErr = OpenVR.Applications.AddApplicationManifest(manifestPath, true);
            if (addManifestErr != EVRApplicationError.None)
                Debug.LogError("<b>[SteamVR]</b> Error adding vr manifest file: " + addManifestErr.ToString());
            else
            {
                if (showLogs)
                    Debug.Log("<b>[SteamVR]</b> Successfully added VR manifest to SteamVR");
            }

            int processId = System.Diagnostics.Process.GetCurrentProcess().Id;
            EVRApplicationError applicationIdentifyErr = OpenVR.Applications.IdentifyApplication((uint)processId, SteamVR_Settings.instance.editorAppKey);

            if (applicationIdentifyErr != EVRApplicationError.None)
                Debug.LogError("<b>[SteamVR]</b> Error identifying application: " + applicationIdentifyErr.ToString());
            else
            {
                if (showLogs)
                    Debug.Log(string.Format("<b>[SteamVR]</b> Successfully identified process as editor project to SteamVR ({0})", SteamVR_Settings.instance.editorAppKey));
            }
        }

        #region Event callbacks

        private void OnInitializing(bool initializing)
        {
            SteamVR.initializing = initializing;
        }

        private void OnCalibrating(bool calibrating)
        {
            SteamVR.calibrating = calibrating;
        }

        private void OnOutOfRange(bool outOfRange)
        {
            SteamVR.outOfRange = outOfRange;
        }

        private void OnDeviceConnected(int i, bool connected)
        {
            SteamVR.connected[i] = connected;
        }

        private void OnNewPoses(TrackedDevicePose_t[] poses)
        {
            // Update eye offsets to account for IPD changes.
            eyes[0] = new SteamVR_Utils.RigidTransform(hmd.GetEyeToHeadTransform(EVREye.Eye_Left));
            eyes[1] = new SteamVR_Utils.RigidTransform(hmd.GetEyeToHeadTransform(EVREye.Eye_Right));

            for (int i = 0; i < poses.Length; i++)
            {
                var connected = poses[i].bDeviceIsConnected;
                if (connected != SteamVR.connected[i])
                {
                    SteamVR_Events.DeviceConnected.Send(i, connected);
                }
            }

            if (poses.Length > OpenVR.k_unTrackedDeviceIndex_Hmd)
            {
                var result = poses[OpenVR.k_unTrackedDeviceIndex_Hmd].eTrackingResult;

                var initializing = result == ETrackingResult.Uninitialized;
                if (initializing != SteamVR.initializing)
                {
                    SteamVR_Events.Initializing.Send(initializing);
                }

                var calibrating =
                    result == ETrackingResult.Calibrating_InProgress ||
                    result == ETrackingResult.Calibrating_OutOfRange;
                if (calibrating != SteamVR.calibrating)
                {
                    SteamVR_Events.Calibrating.Send(calibrating);
                }

                var outOfRange =
                    result == ETrackingResult.Running_OutOfRange ||
                    result == ETrackingResult.Calibrating_OutOfRange;
                if (outOfRange != SteamVR.outOfRange)
                {
                    SteamVR_Events.OutOfRange.Send(outOfRange);
                }
            }
        }

        #endregion

        private SteamVR()
        {
            hmd = OpenVR.System;
            Debug.Log("<b>[SteamVR]</b> Initialized. Connected to " + hmd_TrackingSystemName + ":" + hmd_SerialNumber);

            compositor = OpenVR.Compositor;
            overlay = OpenVR.Overlay;

            // Setup render values
            uint w = 0, h = 0;
            hmd.GetRecommendedRenderTargetSize(ref w, ref h);
            sceneWidth = (float)w;
            sceneHeight = (float)h;

            float l_left = 0.0f, l_right = 0.0f, l_top = 0.0f, l_bottom = 0.0f;
            hmd.GetProjectionRaw(EVREye.Eye_Left, ref l_left, ref l_right, ref l_top, ref l_bottom);

            float r_left = 0.0f, r_right = 0.0f, r_top = 0.0f, r_bottom = 0.0f;
            hmd.GetProjectionRaw(EVREye.Eye_Right, ref r_left, ref r_right, ref r_top, ref r_bottom);

            tanHalfFov = new Vector2(
                Mathf.Max(-l_left, l_right, -r_left, r_right),
                Mathf.Max(-l_top, l_bottom, -r_top, r_bottom));

            textureBounds = new VRTextureBounds_t[2];

            textureBounds[0].uMin = 0.5f + 0.5f * l_left / tanHalfFov.x;
            textureBounds[0].uMax = 0.5f + 0.5f * l_right / tanHalfFov.x;
            textureBounds[0].vMin = 0.5f - 0.5f * l_bottom / tanHalfFov.y;
            textureBounds[0].vMax = 0.5f - 0.5f * l_top / tanHalfFov.y;

            textureBounds[1].uMin = 0.5f + 0.5f * r_left / tanHalfFov.x;
            textureBounds[1].uMax = 0.5f + 0.5f * r_right / tanHalfFov.x;
            textureBounds[1].vMin = 0.5f - 0.5f * r_bottom / tanHalfFov.y;
            textureBounds[1].vMax = 0.5f - 0.5f * r_top / tanHalfFov.y;

            // Grow the recommended size to account for the overlapping fov
            sceneWidth = sceneWidth / Mathf.Max(textureBounds[0].uMax - textureBounds[0].uMin, textureBounds[1].uMax - textureBounds[1].uMin);
            sceneHeight = sceneHeight / Mathf.Max(textureBounds[0].vMax - textureBounds[0].vMin, textureBounds[1].vMax - textureBounds[1].vMin);

            aspect = tanHalfFov.x / tanHalfFov.y;
            fieldOfView = 2.0f * Mathf.Atan(tanHalfFov.y) * Mathf.Rad2Deg;

            eyes = new SteamVR_Utils.RigidTransform[] {
            new SteamVR_Utils.RigidTransform(hmd.GetEyeToHeadTransform(EVREye.Eye_Left)),
            new SteamVR_Utils.RigidTransform(hmd.GetEyeToHeadTransform(EVREye.Eye_Right)) };

            switch (SystemInfo.graphicsDeviceType)
            {
#if (UNITY_5_4)
                case UnityEngine.Rendering.GraphicsDeviceType.OpenGL2:
#endif
                case UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore:
                case UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2:
                case UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3:
                    textureType = ETextureType.OpenGL;
                    break;
#if !(UNITY_5_4)
			case UnityEngine.Rendering.GraphicsDeviceType.Vulkan:
				textureType = ETextureType.Vulkan;
				break;
#endif
                default:
                    textureType = ETextureType.DirectX;
                    break;
            }

            SteamVR_Events.Initializing.Listen(OnInitializing);
            SteamVR_Events.Calibrating.Listen(OnCalibrating);
            SteamVR_Events.OutOfRange.Listen(OnOutOfRange);
            SteamVR_Events.DeviceConnected.Listen(OnDeviceConnected);
            SteamVR_Events.NewPoses.Listen(OnNewPoses);
        }

        ~SteamVR()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            SteamVR_Events.Initializing.Remove(OnInitializing);
            SteamVR_Events.Calibrating.Remove(OnCalibrating);
            SteamVR_Events.OutOfRange.Remove(OnOutOfRange);
            SteamVR_Events.DeviceConnected.Remove(OnDeviceConnected);
            SteamVR_Events.NewPoses.Remove(OnNewPoses);

            _instance = null;
        }

        // Use this interface to avoid accidentally creating the instance in the process of attempting to dispose of it.
        public static void SafeDispose()
        {
            if (_instance != null)
                _instance.Dispose();
        }
    }
}