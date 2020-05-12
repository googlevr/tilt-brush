# SteamVR Unity Plugin - v2.3.2 (sdk 1.4.18)

Copyright (c) Valve Corporation, All rights reserved.


Requirements:

 The SteamVR runtime must be installed. This can be found in Steam under Tools.

 We strongly recommend you opt-in to the SteamVR Beta to make sure your application will work with future versions of SteamVR. Right-click SteamVR inside steam, click Properties, the beta tab, then select the beta branch.


Documentation:

 Documentation can be found online here: https://valvesoftware.github.io/steamvr_unity_plugin/


Quick Start:

 If you want to explore the interaction scene you'll need to open the SteamVR Input window (under the Window Menu), click yes to copy example jsons, then click Save and Generate to create input actions.

 For the most simple example of VR with tracked controllers see the sample scene at ​SteamVR/Simple Sample

 For a more extensive example including picking up, throwing objects, and animated hands see the Interaction System example at ​SteamVR/Interaction System/Samples/Interactions_Example


Support:
 
 If you're having trouble with the plugin the best place to discuss issues is our github here: https://github.com/ValveSoftware/steamvr_unity_plugin/issues/

 If you'd like to discuss features, post guides, and give general feedback please post on the steam forum here: https://steamcommunity.com/app/250820/discussions/7/
 


Changes for 2.3.2
 
 * Updated openvr sdk version to 1.4.18

 * Added SteamVR.asmdef.20192 for Unity 2019.2b and 2019.3a - replace SteamVR.asmdef to fix UI errors in these versions.


Changes for 2.3.1b

 * Added legacy mixed reality camera mode (enabled by default). You can change this in SteamVR/Resources/SteamVR_Settings.

 * Fixing some errors for 2019.1

 * Removing an unused editor class

 * Added low fidelity fallback hand animation for when no skeleton data is available (WinMR)

 * Fixing OpenVR Package not required error for pre 2018.2 versions of unity

 * Fixed serializable event in Throwable

 * Fix for Custom Skeleton not getting initialized

 * Fix for a rare failure in action retrieval (https://github.com/ValveSoftware/steamvr_unity_plugin/pull/431)

 * Allowing folders in the action path. (https://github.com/ValveSoftware/steamvr_unity_plugin/pull/443)

 * Fix for multiple SteamVR_Behaviours initializing (https://github.com/ValveSoftware/steamvr_unity_plugin/pull/435)

 * Stop updating poses if they're not active


Changes for 2.3b

 * Fix for runtime action instantiation generating garbage

 * Fix for setting Universe Origin at runtime. SteamVR.settings.trackingOrigin will automatically set all pose origins and the compositor origin (hmd)

 * Physics objects correctly teleport while held now

 * Fix for left hand being inside out sometimes.

 * Fixed some perf and gc issues in the skeleton

 * Updated poses to support 120hz prediction better

 * Fix for IL2CPP compilation

 * Fix for poor interpolation when dropping objects with left hand

 * Added SteamVR_TrackingReferenceManager to allow showing tracking devices (base stations / cameras)

 * Typo fix for GetStateUp returning GetStateDown

 * Added ability to suppress updating the full skeletal bone data and only update summary data SteamVR_Action_Skeleton.onlyUpdateSummaryData

 * Added access to different skeletal summary data. SteamVR_Action_Skeleton.summaryDataType specifies if you want the summary of the animation data or the summary of the device data. Device data may be faster but different than animation data.

 * Added wrappers for ShowActionOrigin and ShowBindingsForActionSet. See SteamVR_Action.ShowOrigins() .HideOrigins(). SteamVR_ActionSet.ShowBindingHints() .HideBindingHints()


Changes for v2.2.0:
 
 * Removing some unused code


Changes for v2.2RC5:
 
 * Fix for controllers that don't support Skeleton Input yet (WinMR)
 
 * Fixing issue where sometimes SteamVR would load legacy bindings for SteamVR Input projects while in the editor.
 

Changes for v2.2RC4:
 
 * Changed SteamVR_Input.isStartupFrame to return true for the couple frames around startup. This fixes some startup errors temporarily until we have a SteamVR API to determine startup state.

 * Fixed an issue where builds would fail

 * Significantly reduced asset package file size (~50%). Some psds were replaced with pngs, some png res was lowered. The old assets are still on the github repo under old plugin versions.

 * Made Unity 2018.1+ OpenVR package detection and installation more robust.

 * Improved Project Setup experience when using an Oculus headset
 

Changes for v2.2RC3:

 * Minor Breaking Change: SteamVR_Behaviour_ActionType events were incorrectly sending the action instead of the behaviour component they came from.

 * Minor Breaking Change: Simplified the handFollowTransform member to be one variable instead of three

 * Fixed code generation so it deletes unused actionset classes (asks first)

 * Fixed behaviour events disappearing from serialized objects in some unity versions

 * Added a few events to SteamVR_Behaviour_Skeleton

 * Added C# style events to the SteamVR_Behaviour_ActionType components.

 * Added Happy Ball as an example of a complex blending pose that moves the held object

 * Added scaling support for the skeleton poser
 
 * Cleaned up the canvas elements on the Interaction System Sample scene.

 * Skeleton poser is now able to snap/follow interactables

 * Fixed the namespace on a couple small sample scene components

 * When clicking the "Show binding UI" button we will now always try to launch the default browser, though it may fail sometimes (Edge) we have better error messages now

 * Fixed some documentation errors

 * Improved skeleton poser editor ui

 * Fixed an issue with ActionSets not serializing defaults properly


Changes for v2.2RC2:

 * Interactable.isHovering now correctly reports hovering when a hand is over it. There is a new associated field Interactable.hoveringHand.

 * RenderModels should no longer throw an error on immediate reload.

 * Added the SteamVR_Behaviour component to the Player prefab in the Interaction System so it's easier to set it's DoNotDestroy value.

 * Fixed an issue with skeletons complaining that they were getting called too early. Initial action updates now happen a frame after SteamVR_Input initialization.

 * Normalized the behaviours of the throwables in the Interaction System sample scene to do what their description says they should.

 * Fixed an issue with TeleportArea throwing errors without a Teleport component in the scene.

 
Changes for v2.2RC1:

 * Feature: Added SteamVR_Skeleton_Poser component that simplifies creating poses that are easily compatible with the SteamVR Skeletal System. Check the objects in the Interaction System scene for examples or add the component to an interactable. More documentation on this feature will come before release. Example poses will be improved before release.

 * Copied skeletalTrackingLevel, arrays, and finger splays into the Skeleton Behaviour component
 
 * Fixed some related skeleton docs

 * Added an option to importing partial bindings to just replace the current actions instead of merging.
 
 
Changes for v2.2b5:

 * Fixed an issue where the SteamVR_Actions assembly was not being auto referenced by the main assembly. (intellisense would not recognize the class)
 
 * Fixed an issue with nullchecks against unassigned actions returning false incorrectly. (headsetOnHead == null with no action assigned should return true)
 

Changes for v2.2b4:

 * Fixed an issue in builds where actions and action sets were not deserializing correctly.

 * Added an option to turn on the action set debug text generation for builds in the manager.

 * Fixed an issue where automatic SteamVR Input calls on frame 0 would cause errors.
 
 
Changes for v2.2b3:

 * Fixed a named action property generation issue

 * Fixed an issue with not removing missing default binding files from the action manifest


Changes for v2.2b2:

 * Fixed an assembly definition issue during generation.

 * Added a warning to Edge users that they need to manually open the binding ui.


Changes for v2.2:

 * Major Breaking Change: To allow for the new SteamVR plugin to use assembly definition files generated action properties have been moved to the SteamVR_Actions class. Since this was already breaking references to actions we've also created friendlier names. SteamVR_Input.__actions_default_in_GripGrab -> SteamVR_Actions.default_GripGrab

 * Breaking Change: SteamVR_Action_In.GetDeviceComponentName() has been renamed GetRenderModelComponentName because that is more descriptive. This is a non-localized string representing the render model's component. Not necessarily the physical component on the device.

 * Major Change: Added Indexer/property style action data access. Instead of booleanAction.GetStateDown(SteamVR_Input_Sources.LeftHand); you can now use booleanAction[SteamVR_Input_Sources.LeftHand].stateDown; Or if you don't need to restrict to a specific input source just access booleanAction.stateDown;

 * Fix for Mixed Reality camera configs. The Example actions now have a "mixedreality" action set with an "ExternalCamera" pose action. Set this pose to a tracker / controller and mixed reality should work again. By default the camera tracker type is bound to this action. You can also change the pose that's used in SteamVR/Resources/ExternalCamera.

 * When saving / generating actions the plugin will now automatically remove entries in default binding files for actions that no longer exist.

 * Auto loading OpenVR package for projects that don't have it (2018.1+)

 * Only updating actions that has been accessed.

 * Fixed issue where you would get old data from an action recently activated.

 * Fixed some issues with Unity 2018.3+

 * Significant XML style documentation added in preparation for documentation generation

 * Added C# events with autocomplete to all actions. booleanAction[source].onStateDown += yourMethod will auto-generate a method with named variables!

 * Added C# events to all SteamVR_Behaviour_ActionType components

 * Added unrestricted input source shortcuts to actions. booleanAction.state is a short-cut to booleanAction[SteamVR_Input_Sources.Any].state.

 * Faster initialization.

 * Fixed some issues with actions not serializing properly.

 * Added DoNotDestroy checkbox to SteamVR_Behaviour component.

 * Active has been split into Active and ActiveBinding - ActiveBinding indicates the action has an active binding, Active indicates the binding is active as well as the containing action set.

 * Calls to action data will now only return valid data then the action is active. So actionBoolean.stateDown will always be false if the action is inactive.

 * Added delta parameter to Single, Vector2, Vector3 behaviour events

 * Added onState event to Boolean actions that fires when the action is true

 * Added onAxis event to Single, Vector2, and Vector3 actions that fires when the action is non-zero.

 * General input system performance increases


Changes for v2.1.5:

 * Breaking Change: Skeleton actions no longer take a input source as a parameter since this doesn't make sense. A lot of skeleton action method signatures have changed

 * Added lots of new helpful functions to the skeleton behaviour and action classes. Finger curl, finger splay, reference poses, tracking level, bone names, etc.

 * Added SteamVR_Input.GetLocalizedName and SteamVR_Action_In.GetLocalizedName that will return the localized name of the device component to last use that action. "Left Hand Vive Controller Trackpad". You can specify which parts of the name you want as well.

 * Fixed a major slowdown with going in and out of the steamvr dashboard at runtime

Changes for v2.1:

 * Major Breaking Change: Actions and ActionSets are no longer Scriptable Objects. Make sure to delete your existing SteamVR_Input folder with all your generated stuff in it. This means you will have to reset all Actions and Action Sets in all prefabs and scenes.

 * Breaking Change: DefaultAction and DefaultActionSet properties no longer exist. Good news: Generation is now near-instant, doesn't require looping through every prefab and every scene. Set defaults through the following format: public SteamVR_Action_Pose poseAction = SteamVR_Input.GetAction<SteamVR_Action_Pose>("Pose"); or public SteamVR_Action_Boolean interactWithUI = SteamVR_Input.__actions_default_in_InteractUI;

 * Breaking Change: actionSet.inActions and actionSet.outActions are no longer generated. We're moving all the actions directly to the set level. Previously: SteamVR_Input._default.inActions.GripGrab. Now: SteamVR_Input._default.GripGrab. Collisions of an In-Action with the same name as an Out-Action will be handled by prepending "in_" and "out_" to the field names.

 * Breaking Change: More extensive action set management. Swapped out ActivatePrimary and ActivateSecondary for a single Activate call. You can activate as many sets at once as you want.

 * Added ability to create partial input bindings to be used in plugins. Create a partial binding folder and include it in your plugin. When users import your plugin they will be presented with the option to import your actions and bindings.

 * Added better tracker support via access to other user paths. Poses can now be bound to user/foot/left, user/foot/right, etc.

 * Added access to action set priorities. If you activate an action set with a higher priority it will stop actions bound to the same button in lower priority sets from updating

 * Added action set visualization to live editor window

 * Added more extensive string access to actions. SteamVR_Input.GetAction(actionName), GetState(actionName, inputSource), GetFloat(actionName, inputSource), GetVector2(actionName, inputSource)

 * Added proximity sensor action and example. The interaction system will log when you put the headset on / take it off. (binding ui for this still needs fix)

 * Fixed some generation and loading issues with 2018.3

 * Bolded [SteamVR] in Debug.log entries

 * Fixed a rigidbody issue with 2018.3

 * Fixed some issues with delayed loading SteamVR. Added a test scene: SteamVR/Extras/SteamVR_ForceSteamVRMode.unity

 * Readded the laser pointer extra with an example scene: SteamVR/Extras/SteamVR_LaserPointer.unity

 * Fixed auto-enabling of vr mode in recent unity versions. (Reminder you can disable this in: Edit Menu -> Preferences -> SteamVR)

 * Fixed action set renaming so it renames all actions in its set. Does not currently modify default bindings though.

 * Fixed basic fallback hand support

 * Moved automatic enabling of VR in player settings to the SteamVR_Settings.asset file in SteamVR/Resources. This allows better per project settings

 * Gave better error when SteamVR fails to initialize with oculus devices


Changes for v2.0.1:

 * Changed SteamVR to identify only when in editor. SteamVR_Setting.appKey has been replaced with SteamVR_Setting.editorAppKey. This means that downloads from steam will always use proper bindings but builds run separate from Steam will have their own autogenerated app key. Remember to replace your default bindings in the binding ui before creating a build.

 * Fixed bug where hands were not reactivating properly after visiting the dashboard (https://github.com/ValveSoftware/steamvr_unity_plugin/issues/118)

 * Fixed bug with multiple items being grabbed at once (https://github.com/ValveSoftware/steamvr_unity_plugin/issues/121)

 * Fixed bug where Linear Drive would freeze when grabbed twice (https://github.com/ValveSoftware/steamvr_unity_plugin/issues/120)

 * Fixed bug with bindings that were readonly not copying correctly.

 * Fixed some other bugs with multiple pickup types being activated at once.


Changes for v2.0:

 * Updated to SteamVR runtime v1537309452 and SDK version 1.0.16.

 * Removed support for older versions of Unity (v5.4 or newer required).  Previous versions of the plugin can be found here: https://github.com/ValveSoftware/steamvr_unity_plugin/


Changes for v2.0rc4:
 
 * Support for Windows MR (no Skeletal input at this time - driver needs to be updated)

 * Added SteamVR_ActionIn.onActiveChange event (most actions inherit from this class)

 * Added Interactable.activateActionSetOnAttach to activate action sets when you grab an item and deactivate them when they're detached

 * Fixed an issue in the SteamVR Input Live View that made it unreadable

 * Fixed an issue that lead to duplicate SteamVR_Render components in some circumstances

 * Removed Debug UI from release builds using the Interaction System

 * Added ModalThrowable. Allowing different snap locations for grip and pinch pickups

 * Added grenade as an example of the ModalThrowable

 * Added a squishable object for a knuckles force example

 * Fixed an order of operations error where the interactable detach event was being called after the attach event when an item changed hands

 * Fixed an issue with pickup points being in odd places

 * Forcing hover unlock on interactable destruction

 * Added support for Unity 2018.3

 * Fixed an issue with delayed init


Changes for v2.0rc3:
 
 * Added some pdf documentation for the new plugin and input system

 * Added code documentation to most public functions in the input system

 * Removed SteamVR_Camera from prefabs as this is no longer necessary

 * Added simple rc car example ported from Knuckles Tech Demo

 * Added simple platformer example

 * Switched hover highlights to highlight the object being interacted with instead of the controller. Can be reenabled in the player prefab.

 * Fixed some upgrading issues for Unity 2018

 * Added glcore to a few of the shaders to make them opengl compatible

 * Fixed a code generation issue that was generating static members instead of instance members, making actions inside sets unavailable.

 * Added scrollview to LiveWindow

 * Fixed some issues with action sets

 * Added DefaultActionSet attribute to specify a default action set to be assigned to fields/properties on input generation

 * Updated some scriptable object fields to not serialize things unintentionally

 * Made initialization a little more streamlined.

 * Added an event you can subscribe to for when initialization is completed (SteamVR_Events.Initialized(bool)). The bool indicates success

 * Improved editor UI for action / action set assignment

 * Made the example button do something on press

Changes for v2.0rc2:

 * Added built in support for delayed loading of SteamVR. You can now call SteamVR.Initialize() and pass a boolean to force set unity to OpenVR mode.

 * Added a new Simple Sample scene in the root to do super basic testing.

 * Moved SteamVR Input updating to SteamVR_Behaviour which also handles the SteamVR_Render component. This will be added to scenes at runtime.

 * Added ability to explicitly show or hide controller model in the interaction system

 * Gave the interaction system scene a new paint job

 * Auto scaling the teleporter beam to the player size

 * Added a new quickstart pdf

 * Fixed issue for Unity 5.6 not showing controllers

 * Fixed issue for 2018.2 not opening the input window properly

 * Minor performance increases

 * Updated initialization process to support having XR set to none or Oculus initially.
 
 * Moved some of example jsons files into a more reasonable directory.


Changes for v2.0rc1:

 * Namespacing all SteamVR scripts. This will be a breaking change for most projects but this is a major revision.

 * Renamed most of the input classes to have a more reasonable length. Generally removed _Input_ as it's redundant in most places

 * Fixed some issues with newer versions of Unity throwing errors during action generation
 
 * Fixed some issues with scenes not opening properly during generation on newer versions of Unity

 * Removing SteamVR_Settings from plugin, it should be auto generated so new versions of the plugin don't overwrite it

 * Fixed some performance issues surrounding using the legacy input system at the same time as the new input system. This is not a supported scenario.

 * Minor performance increases around render model loading.

 * Removed some legacy system scripts

 * Fixed the button hint system

 * Consolidated the skeleton options


Changes for v1.3b10:

 * Fixed a couple issues that would cause tracking jitter or entire loss of input

 * Fixed an issue with destroying held objects


Changes for v1.3b09:

 * Newly created action sets default to "single" mode allowing action mirroring in the binding UI.

 * Added an example of blending unity animations with the skeleton input system. The sphere on the Equippable table can be grabbed and the hand will blend to an animation.

 * Interactables now hand a hideHandOnAttach bool, a handAnimationOnPickup int that triggers an Animator.SetInt, and setRangeOfMotionOnPickup which will temporarily set the range of motion while an object is attached.

 * Added a tool example for "With Controller" hand animation examples. Equippables can also be flipped depending on the hand that picks them up.

 * Interactables now can tell hands to snap to them on attach. Specify a transform to snap to in Interactable.handFollowTransform and then check handFollowTransformPosition and/or handFollowTransformRotation

 * Added Range of Motion blending to skeleton - Hand.SetSkeletonRangeOfMotion(rangeOfMotion, blendTime)

 * Updated skeleton system to account for coordinate system changes

 * Fixed some perf issues with the old render models (WIP)

 * Fixed some bugs with button hints (WIP)

 * Interactables should now auto detach on destroy.

 * Added slim glove models, an example of an alien hand with 3 fingers, and an alien hand with floppy fingers

 * Hands now initialize a RenderModel object which can contain a hand and a controller. These can be toggled on and off separately

 * Fixed issue with controller highlighters not initializing correctly

 * Added the ability to attach an object to a specific offset from the Hand - Hand.ObjectAttachmentPoint

 * Fixed issue where render model would not show after bringing up compositor

 * Fixed issue with default Throwables. HoverButton now works in local space

 * Fixed issue with velocities and angular velocities not transforming properly


Changes for v1.3b08:

* SteamVR_Input_References has been moved to the generated folder so new plugin updates don't wipe yours. Prior beta users: Please delete your existing one in Assets/SteamVR/Resources/SteamVR_Input_References.asset

* SteamVR_Input is no longer a MonoBehaviour. This fixes scene transition issues as well as event subscription issues.

* The Live Action View has been moved to its own window under the Window menu.

* Some excess configuration options were removed from the settings window, some were moved into SteamVR_Settings if still needed.

* Added a Hover Button that depresses when the controller gets close to it.

* Fixed an issue where default throwables would "auto catch" objects


Changes for v1.3b07:

* Fixed issue with upgrading from the legacy system

* Fixed some line endings

* Fixed generation bug where it wouldn't reopen to the scene you started on

* Auto replacing app key in binding files

* Updating actions and bindings to have one pose and one haptic action

* Gave each app its own app id via a vrmanifest that is generated on import. Can be modified in SteamVR_Settings for release.

* Fixed haptic actions not displaying in play mode

* Temporary fix for skeletons erroring while the dashboard is up


Changes for v1.3b06:

* Added some flower and planting stuff for the tutorial

* Updating knuckles actions and binding jsons

* Added code solution for blending skeleton animations to mechanim, no example yet though

* Updated some helper components to utilize Unity Events properly

* Updated skeleton hierarchy


Changes for v1.3b05:

* Added a knuckles binding for the Grab mode

* Fixed some bugs around skeleton updates and GC alloc

* Fixed a pretty significant perf hit

* Added a blending option to skeletons

* Added some ui to try out skeleton options

* Added a target for the throwing examples

* Updated longbow to only fire arrows with the pinch action

* Updated other interactable examples

* Added some helper methods to hand around showing / hiding controller or the whole hand.

* Fixed some of the throwing examples


Changes for v1.3b04:

* Added some more extensive velocity and angular velocity estimation based on positions and rotations per frame. Normalizing for time between frames.

* Cleaned out and updated the actions + bindings for knuckles/wands/touch.

* Fixed a bug with newly created actions not having a set type

* Updated extra scenes to use the new input system


Changes for v1.3b03:

* Fixed some warnings for unity 2017 / 2018.

* Fixed some editor UI issues for 2018

* Fixed issues with Unity 2017+ not wanting to open scenes from a script reloaded callback


Changes for v1.3b02:

* Added DefaultInputAction attribute to automatically assign actions during action generation.

* Updated default CameraRig prefab to use the new input system and components


Changes for v1.3b01:

* Integrated SteamVR Input System.
https://steamcommunity.com/games/250820/announcements/detail/3809361199426010680

* [InteractionSystem] Added basic examples of the Skeletal API

* [InteractionSystem] Integrated SteamVR Input System. Actions and Action Sets instead of buttons.

* [InteractionSystem] Added Velocity style object interaction

* [InteractionSystem] Fixed some issues from github. Took some pull requests.
https://github.com/ValveSoftware/steamvr_unity_plugin/pull/79
https://github.com/ValveSoftware/steamvr_unity_plugin/pull/73
https://github.com/ValveSoftware/steamvr_unity_plugin/pull/72
https://github.com/ValveSoftware/steamvr_unity_plugin/pull/71
https://github.com/ValveSoftware/steamvr_unity_plugin/pull/67
https://github.com/ValveSoftware/steamvr_unity_plugin/pull/64
https://github.com/ValveSoftware/steamvr_unity_plugin/issues/84
https://github.com/ValveSoftware/steamvr_unity_plugin/issues/78


Changes for v1.2.3:

* Updated to SteamVR runtime v1515522829 and SDK version 1.0.12.

* Updated quickstart guide.

* [General] Fixed deprecation warnings for GUILayer in Unity version 2017.2 and newer (removed associated functionality).

* [LoadLevel] Fixed a crash when using SteamVR_LoadLevel to load a scene which has no cameras in it.

* [RenderModels] Switched from using TextureFormat.ARGB32 to RGBA32 to fix pink texture issue on Vulkan.

* [RenderModels] Fix for not initializing properly if game is paused on startup.
https://github.com/ValveSoftware/steamvr_unity_plugin/issues/62

* [InteractionSystem] Added implemention for ItemPackageSpawner requireTriggerPressToReturn.
https://github.com/ValveSoftware/steamvr_unity_plugin/pull/17/files


Changes for v1.2.2:

* Updated to SteamVR runtime v1497390325 and SDK version 1.0.8.

* [General] Switched caching SteamVR_Events.Actions from Awake to constructors to fix hot-loading of scripts in the Editor.

* [General] Switched remaining coroutines away from using strings (to avoid issues with obfuscators).

* [General] Switched from using deprecated Transform.FindChild to Transform.Find.

* [General] Added #if !UNITY_METRO where required to allow compiling for UWP.

* [UpdatePoses] Switched to using static delegates (Camera.onPreCull or Application.onBeforeRender depending on version) for updating poses.

* [UpdatePoses] Deprecated SteamVR_UpdatePoses component.

* [MixedReality] Added rgba settings to externalcamera.cfg for overriding foreground chroma key (default 0,0,0,0).

* [MixedReality] Exposed SteamVR_ExternalCamera.Config settings in Unity Editor inspector for easy tweaking.

* [MixedReality] Added file watcher to externalcamera.cfg to allow real-time editing.

* [MixedReality] Fixed antialiasing complaint in Unity 5.6+.

* [MixedReality] Added second pass to foreground camera when using PostProcessingBehaviour since those fx screw up the alpha channel.

* [ControllerManager] Added code to protect against double-hiding of controllers.

* [InteractionSystem] Sub-objects now inherit layer and tag of spawning object (ControllerButtonHints, ControllerHoverHighlight, Hand, SpawnRenderModel).


Changes for v1.2.1:

* Updated to SteamVR runtime v1485823399 and SDK version 1.0.6.

* Switched SteamVR_Events.SystemAction from using strings to specify event type over to their associated enum values.

* Fixed an issue with using WWW in static constructors.

* Added Unity Preferences for SteamVR to allow disabling automatic enabling of native OpenVR support in Unity 5.4 or newer.
https://github.com/ValveSoftware/steamvr_unity_plugin/issues/8
https://github.com/ValveSoftware/steamvr_unity_plugin/pull/9

* Added UNITY_SHADER_NO_UPGRADE to all shaders to avoid log spam in later versions of Unity for issues that have already been fixed but the compiler isn't able to detect.

* Specified Vulkan support for Interaction System shaders.

* Fix for crash in Interaction_Example selecting BowPickup:
https://github.com/ValveSoftware/steamvr_unity_plugin/issues/4

* Cleaned up unused fields:
https://github.com/ValveSoftware/steamvr_unity_plugin/issues/2

* Updated Interaction System's LinearDrive to initialize using linearMapping.value.
https://github.com/ValveSoftware/steamvr_unity_plugin/pull/5

* Updated Interaction System documetation to fix a few errors.

* Added an icon for all Interaction System scripts.

* Fixes for SteamVR on Linux.


Changes for v1.2.0:

* Updated to SteamVR runtime v1481926580 and SDK version 1.0.5.

* Replaced SteamVR_Utils.Event with SteamVR_Events.<EventName> to avoid runtime memory allocation associated with use of params object[] args.

* Added SteamVR_Events.<EventName>Action to make it easy to wrap callbacks to avoid memory allocation when components are frequently enabled/disabled at runtime.

* Fixed other miscellaneous runtime memory allocation in SteamVR_Render and SteamVR_RenderModels.  (Suggestions by unity3d user @8bitgoose.)

* Integrated fix for SteamVR_LaserPointer direction (from github user @fredsa).

* Integrated fixes and comments for SteamVR_Teleporter (from github user @natewinck).

* Removed SteamVR_Status and SteamVR_StatusText as they were using SteamVR_Utils.Event with generic strings which is no longer allowed.

* Added SteamVR_Controller.assignAllBeforeIdentified (to allow controller to be assigned before identified as left vs right).  Suggested by github user @chrwoizi.

* Added SteamVR_Controller.UpdateTargets public interface.  This allows spawning the component at runtime.  Suggested by github user @demonixis.

* Fixed bug with SteamVR_TrackedObject when specifying origin.  Suggested by github user @fredsa.

* Fixed issue with head camera reference in SteamVR_Camera.  Suggested by github user @pedrofe.

Known issues:

* The current beta version of Unity 5.6 breaks the normal operation of the SteamVR_UpdatePoses component (required for tracked controllers).
To work around this in the meantime, you will need to manually add the SteamVR_UpdatePoses component to your main camera.


Changes for v1.1.1:

* Updated to SteamVR runtime v1467410709 and SDK version 1.0.2.

* Updated Copyright notice.

* Added SteamVR_TrackedCamera for accessing tracked camera video stream and poses.

* Added SteamVR_TestTrackedCamera scene and associated script to demonstrate how to use SteamVR_TrackedCamera.

* Fix for SteamVR_Fade shader to account for changes in Unity 5.4.

* SteamVR_GameView will now use the compositor's mirror texture to render the companion window (pre-Unity 5.4 only).

* Renamed SteamVR_LoadLevel 'externalApp' to 'internalProcess' to reflect actual functionality.

* Fixed issue with SteamVR_PlayArea material loading due to changes in Unity 5.4.

* Added Screenshot support handling for stereo panoramas generation.

* Removed code that was setting Time.maximumDeltaTime as this was causing issues.


Changes for v1.1.0:

* Fix for error building standalone in SteamVR_LoadLevel.

* Set SteamVR_TrackedObject.isValid to false when disabled.


Changes for v1.0.9:

* Updated to SteamVR runtime v1461626459 and SDK version 0.9.20.

* Updated workshop texture used in sea of cubes example level to use web page from SteamVR (was previously from Portal).

* Updated various SDK changes to Unity in 5.4 betas.

* Added controllerModeState to RenderModel component to control additional features like scrollwheel visibility.

* RenderModels now respond to model skin changes.

* Removed OnGUI and associated help text (i.e. "You may now put on your headset." notification) as this was causing unnecessary overhead.

* Fix to SteamVR_Render not turning back on if all cameras were disabled and then re-enabled.

* Hooked up SteamVR_Render.pauseRendering in Unity 5.4 native OpenVR integration.

* Fix for input_focus event sometimes getting sent inappropriately.

* Fix for timeScale handling.

* Fix for SteamVR_PlayArea not finding its material in editor in Unity 5.4 due to changes in how Unity handles asset loading.

* Miscellaneous fixes to reduce hitching when using SteamVR_LoadLevel to handle scene transitions.

* Hooked up SteamVR_Camera.sceneResolutionScale to Unity 5.4's native vr integration render target scaling.

* Forced SteamVR initialization check in SteamVR_Camera.enable (and bail upon failure) in Unity 5.4 (was already doing this in older builds).

* Better handling of SteamVR_Ears component with old content.

* Keep legacy head object around in case external components were referencing it (was previously getting deleted in Unity 5.4 as the head motion is now applied to the "eyes" object).

* Miscellaneous fixes for SteamVR_TrackedController and SteamVR_Teleporter.

* Fixed up Extra scenes SteamVR_TestThrow and SteamVR_TestIK.

* Added stereo panorama screenshot support to SteamVR_Skybox.

* Removed use of deprecated UnityEditorInternal.VR.VREditor.InitializeVRPlayerSettingsForBuildTarget(BuildTargetGroup.Standalone);


Changes for v1.0.8:

* Updated to SteamVR runtime v1457155403.

* Updated to work with native OpenVR integration introduced in Unity 5.4.  In this and newer versions, openvr_api.dll will be automatically deleted when launching since it ships as part of Unity now.

* C# interop now exports arrays as individual elements to avoid the associated memory allocation overhead passing data back and forth between native and managed code.

* Applications should no longer call GetGenericInterface directly, but instead use the accessors provided by Valve.VR.OpenVR (e.g. OpenVR.System for the IVRSystem interface).

* Added SteamVR_ExternalCamera for filming mixed reality videos.  Automatically enabled when externalcamera.cfg is added to the root of your projects (next to Assets or executable), and toggled by the presence of a third controller.

* Render models updated to load asynchronously.  Sends "render_model_loaded" event when finished.

* Added 'shader' property to render models to allow using alternate shaders. This also creates a dependency within the scene to ensure the shader is loaded in baked builds.

* Fix for render model components not respecting render model scale.

* SteamVR_Render.lockPhysicsUpdateRateToRenderFrequency now respects Time.timeScale.

* SteamVR_LoadLevel now hides overlays when finished to avoid persisting performance degredation.

* Added ability to launch external applications via SteamVR_LoadLevel.

* Added option to load levels non-asynchronously in SteamVR_LoadLevel since Unity crashes on some content when using asyn loading.

* SteamVR auto-disabled if initialization fails, to avoid continual retries.

* Updated SteamVR_ControllerManager to get controller indices from the runtime (via IVRSystem.GetTrackedDeviceIndexForControllerRole).

* SteamVR_ControllerManager now allows you to assign additional controllers to game objects.

* [CameraRig] prefab now listens for a third controller connection which will enable mixed reality recording mode in the game view.

* AudioListener is now transferred to a child of the eye camera called "ears" to allow controlling rotation independently when using speakers instead of headphones.

* Flare Layer is no longer transferred from eye camera to game view camera.


Changes for v1.0.7:

* Updated to SteamVR runtime v1448479831.

* Many enums updated to reflect latest SDK cleanup (v0.9.12).

* Various fixes to support color space changes in the SDK.

* Render models set the layer on their child components now to match their own.

* Added a bool 'Load Additive' to SteamVR_LoadLevel script to optionally load the level additively, as well as an optional 'Post Load Settle Time'.

* Fixed some issues with SteamVR_LoadLevel fading to a color with 'Show Grid' set to false.

* Fixed an issue with orienting the loading screen in the SteamVR_LoadLevel script when using 'Loading Screen Distance'.


Changes for v1.0.6:

* Updated to SteamVR runtime v1446847085.

* Added SteamVR_LevelLoad script to help smooth level transitions.

* Added 'Take Snapshot' button to SteamVR_Skybox to automate creation of cubemap assets.

* SteamVR_RenderModel now optionally creates subcomponents for buttons, etc. and optionally updates them dynamically to reflect pulling trigger, etc.

* Added SteamVR_TestIK scene to Extras.

* Added SteamVR.enabled which can be set to false to keep SteamVR.instance from initializing SteamVR.


Changes for v1.0.5:

* Updated to SteamVR runtime build #826021 (v.1445485596).

* Removed TrackedDevices from [CameraRig] prefab (these were only ever meant to be in the example scene.

* Added support for new native plugin interface.

* Enabled MSAA in OpenGL as that appears to be fixed in the latest version of Unity.

* Fix for upside-down rendering in OpenGL.

* Moved calls to IVRCompositor::WaitGetPoses and Submit to Unity's render thread.

* Couple fixes to prevent SteamVR from getting re-initialized when stopping the Editor preview.

* Fix for hitches caused by SteamVR_PlayArea when not running SteamVR.


Changes for v1.0.4:

* Updated to SteamVR runtime build #768489 (v.1441831863).

* Added SteamVR_Skybox for setting a cubemap in the compositor (useful for scene transitions).

* Fix for RenderModels disappearing across scene transitions, and disabling use of modelOverride at runtime.

* Added lockPhysicsUpdateRateToRenderFrequency to SteamVR_Render ([SteamVR] prefab) for apps that want to run their physics sim at a lower frequency.  Locked (true) by default.

* Made per-eye culling masks easier to use.  (See http://steamcommunity.com/app/250820/discussions/0/535152276589455019/)

* Exposed min/max curve distance settings for high quality overlay.  Note: High quality overlay not currently supported in Rift Direct Mode and falls back to normal (flat-only) overlay render path.

* Added 'valid' property to SteamVR_Controller.  This is useful for detecting the controller is plugged in before tracking comes online.


Changes for v1.0.3:

* Updated to SteamVR runtime build #710329 (v.1438035413).

* Added SteamVR_Controller.DeviceRelation.FarthestLeft/Right for GetDeviceIndex helper function.
Note: You can also use SteamVR.instance.hmd.GetSortedTrackedDeviceIndicesOfClass.

* Updated and fixed SteamVR_Controller.GetDeviceIndex to act more like people expect.

* Fix for SteamVR_Controller.angularVelocity (velocity reporting has also been fixed in the runtime).

* Renamed SteamVR_Controller.valid to hasTracking

* Removed SteamVR_Overlay visibility, systemOverlayVisible and activeSystemOverlay properties.

* Added collection of handy scripts to Assets/SteamVR/Extras: GazeTracker, IK (simple two-bone),
LaserPointer, Teleporter, TestThrow (with example scene) and TrackedController.

* Fix for hidden area mesh render order.

* Fix for render models not showing up after playing scene once in editor.

* Added controller manager left and right nodes to camera rig.  These are automatically disabled while the
dashboard is visible to avoid conflict with the dashboard rendering controllers.  If you are handling tracked
controllers using another method, you are encouraged to implement something similar using the input_focus event.

* OpenVR runtime events are now broadcast via the SteamVR_Utils.Event system.  The events can be found here:
https://github.com/ValveSoftware/openvr/blob/master/headers/openvr.h and are broadcast in Unity with their
prefix "VREvent_" stripped off.

* Added handling of dashboard visibility and quit events.

* Added SteamVR_Render.pauseGameWhenDashboardIsVisible (defaults to true).

* Allow Unity to buffer up frames for its companion window to avoid any latency introduction

* Lock physics update rate (Time.fixedDeltaTime) to match render frequency.

* SteamVR_Camera (i.e. 'eye' objects) are moved back to the 'head' location when not rendering.

* Simplified SteamVR_Camera Expand/Collapse functionality (now uses existing parent as origin if available).

* Added SteamVR_PlayArea component to visualize different size spaces to target.

* Exposed SteamVR_Overlay.curvedRange for the high-quality curved overlay render path.


Changes for v1.0.2:

* Updated to SteamVR runtime build #655277.

* Added check for new version and prompt to download.

* Moved remaining in-code shaders to separate shader assets.

* Switched RenderModels back to using Standard shader (to avoid having to manually add Unlit to the always load
assets list).

* RenderModels now provides a drop down list populated with available render models to preview.  This is useful
for displaying various controller models in Editor to line up attachments appropriately.

* Fix for [SteamVR] instance sometimes showing up twice in a scene between level loads and stomping existing
settings.

* Switched Overlay over to using new interface.  Please report any functional differences to the SteamVR forums.

* Added button in example escape menu [Status] to easily switch between Standing and Seated tracking space.

* Miscellaneous color space fixes due to changes in Unity 5.1 rendering.

* Added drawOverlay bool to GameView component to disable rendering the overlay texture automatically on top.

* Eye offsets now get updated at runtime to react to any dynanamic IPD changes.

* Added "hair-trigger" support to SteamVR_Controller.


Changes for v1.0.1:

* Updated to SteamVR runtime build #629708.

* Added accessors to SteamVR_Controller for working with input.

* Added TestController script for verifying controller functionality.

* Added CameraFlip to compensate for Unity's quirk of rendering upsidedown on Windows (was previously
corrected for in the compositor).

* Removed use of UNITY_5_0 defines as this was causing problems with newer versions of Unity 5.

* Shared render target size is now slightly larger to account for overlapping fovs.

* Fix for gamma issues with deferred rendering and hdr.

Note: MSAA is really important for rendering in VR, however, Unity does not support MSAA in deferred rendering.
It is therefore recommended that you use Unity's Forward rendering path.  Unity's Forward rendering path also
does not support MSAA when using HDR render buffers.  If you need to disable MSAA, you should at least attempt
to compensate with an AA post fx.  The MSAA settings for SteamVR's render buffers are controlled via Unity's
Quality settings (Edit > Project Settings > Quality > Anti Aliasing).


Upgrading from previous versions:

The easiest and safest thing to do is to delete your SteamVR folder, and any files and folders in your
Plugins directory called 'openvr_api', 'steam_api' or 'steam_unity' (and variants).  Additionally, verify there
are no SteamVR files found in Assets/Editor.  Then import the new unitypackage into your project.

This latest version has been greatly simplified.  SteamVR_CameraEye has been removed as well as the menu
option from SteamVR_Setup to 'Setup Selected Camera(s)'.  The SteamVR_Camera object is instead rendered twice
(once per eye) and the game view rendering handled in SteamVR_GameView.  SteamVR_Camera now has 'head' and
'origin' properties for accessing the associated Transforms, and 'offset' has been deprecated in favor of using
'head'.  By pressing the 'Expand' button below the SteamVR logo in SteamVR_Camera's Inspector, these objects are
automatically created.  This is useful for attaching objects appropriately, and removes the need for managing
separate FollowHead and FollowEyes arrays. Similarly, the RenderComponents list is no longer needed as the
SteamVR_Camera is itself used to render each eye.  And finally, the button below the SteamVR logo will change to
'Collapse' to restore the camera to its previous setup.

SteamVR_Camera's Overlay support has been broken out into a separate SteamVR_Overlay component.  This can be
added to any object in your scene.  If you wish to use it in some scenes, but not others, it is good practice
to add the component to each of your scenes and ensure its Texture is set to None in those that you do not wish
it rendered in.

The experimental binaural audio support has been removed as there are better plugins on the Unity Asset Store now,
and this was an incomplete and unsupported solution.


Files:

Assets/Plugins/openvr_api.cs - This direct wrapper for the native SteamVR SDK support mirrors SteamVR.h and  
is the only script required.  It exposes all functionality provided by SteamVR.  It is not recommended you make  
changes to this file.  It should be kept in sync with the associated openvr_api dll.

The remaining files found in Assets/SteamVR/Scripts are provided as a reference implementation, and to get you  
up and running quickly and easily.  You are encouraged to modify these to suit your project's unique needs,  
and provide feedback at http://steamcommunity.com/app/250820 or http://steamcommunity.com/app/358720/discussions

Assets/SteamVR/Scenes/example.unity - A sample scene demonstrating the functionality provided by this plugin.   
This also shows you how to set up a separate camera for rendering gui elements.


Details:

Note that these scripts are a work in progress. Many of these will change in future releases and we will not
necessarily be able to maintain compatibility with this version.

Assets/SteamVR/Scripts/SteamVR.cs - Handles initialization and shutdown of subsystems.  Use SteamVR.instance
to access.  This may return null if initialization fails for any reason.  Use SteamVR.active to determine if
VR has been initialized without attempting to initialized it in the process.

Assets/SteamVR/Scripts/SteamVR_Camera.cs - Adds VR support to your existing camera object.

To combat stretching incurred by distortion correction, we render scenes at a higher resolution off-screen.
Since all camera's in Unity are rendered sequentially, we share a single static render texture across each
eye camera.  SteamVR provides a recommended render target size as a minimum to account for distortion,
however, rendering to a higher resolution provides additional multisampling benefits at the associated
expense.  This can be controlled via SteamVR_Camera.sceneResolutionScale.

Note: Both GUILayer and FlareLayer are not compatible with SteamVR_Camera since they render in screen space
rather than world space. These are automatically moved the SteamVR_GameView object which itself is automatically
added to the SteamVR_Camera's parent 'head' object.  The AudioListener also gets transferred to the head in order
for audio to be properly spacialized.

Assets/SteamVR/Scripts/SteamVR_Overlay.cs - This component is provided to assist in rendering 2D content in VR.
The specified texture is composited into the scene on a virtual curved surface using a special render path for
increased fidelity.  See the [Status] prefab in the example scene for how to set this up.  Since it uses GUIText,
it should be dragged into the Hierarchy window rather than into the Scene window so it retains its default position
at the origin.

Assets/SteamVR/Scripts/SteamVR_TrackedObject.cs - Add this to any object that you want to use tracking.  The
hmd has one set up for it automatically.  For controllers, select the index of the object to map to.  In general
you should parent these objects to the camera's 'origin' object so they track in the same space.  However, if
that is inconvenient, you can specify the 'origin' in the TrackedObject itself.

Assets/SteamVR/Scripts/SteamVR_RenderModel.cs - Dynamically creates associated SteamVR provided models for tracked
objects.  See <SteamVR Runtime Path>/resources/rendermodels for the full list of overrides.

Assets/SteamVR/Scripts/SteamVR_Utils.cs - Various bits for working with the SteamVR API in Unity including a  
simple event system, a RigidTransform class for working with vector/quaternion pairs, matrix conversions, and  
other useful functions.


Prefabs:

[CameraRig] - This is the camera setup used by the example scene.  It is simply a default camera with the
SteamVR_Camera component added to it, and the Expand button clicked.  It also includes a full set of Tracked Devices
which will display and follow any connected tracked devices (e.g. controllers, base stations and cameras).

[Status] - The prefab is for demonstration purposes only.  It adds an escape menu to your scene.
Note: It uses the SteamVR_Overlay component, which is rather expensive rendering-wise.

[SteamVR] - This object controls some global settings for SteamVR, most notably Tracking Space.  Legacy projects
that want their viewed automatically centered on startup if not configured or to use the seated calibrated position
should switch Tracking Space to Seated.  This object is created automatically on startup if not added and defaults
to Standing Tracking Space.  It also provides the ability to set special masks for rendering each eye (in case you
want to do something differently per-eye) and some simple help text that demonstrates rendering only to the
companion window (which can be cleared or customized here).


GUILayer, GUIText, and GUITexture:

The recommended way for drawing 2D content is through SteamVR_Overlay.  There is an example of how to set this up
in the example scene.  GUIText and GUITexture use their Transform to determine where they are drawn, so these
objects will need to live near the origin.  You will need to set up a separate camera using a Target Texture.  To
keep it from rendering other elements of your scene, you should create a unique layer used by all of your gui
elements, and set the camera's Culling Mask to only draw those items.  Set its depth to -1 to ensure it gets
updated before composited into the final view.


OnGUI:

Assets/SteamVR/Scripts/SteamVR_Menu.cs demonstrates use of OnGUI with SteamVR_Camera's overlay texture.  The  
key is to set RenderTexture.active and restore it afterward.  Beware when also using a camera to render to the  
same texture as it may clear your content.


Camera layering:

One powerful feature of Unity is its ability to layer cameras to render scenes (e.g. drawing a skybox scene
with one camera, the rest of the environment with a second, and maybe a third for a 3D hud).  This is performed
by setting the latter cameras to only clear the depth buffer, and leveraging the cameras' cullingMask to control
which items get rendered per-camera, and depth to control order.


Camera scale:

Setting SteamVR_Camera's gameObject scale will result in the world appearing (inversely) larger or smaller.
This can be used to powerful effect, and is useful for allowing you to build skybox geometry at a sane scale
while still making it feel far away.  Similarly, it allows you to build geometry at scales the physics engine
and nav mesh generation prefers, while visually feeling much smaller or larger.  Of course, if you are building
geometry to real-world scale you should leave this at its default of 1,1,1.  Once a SteamVR_Camera has been
expanded, its 'origin' Transform should be scaled instead.


Camera masking:

By manually adding a GameObject with the SteamVR_Render component on it to your scene, you can specify a left
and right culling mask to use to control rendering per eye if necessary.


Events:

SteamVR fires off several events.  These can be handled by registering for them through
SteamVR_Events.<EventType>.Listen.  Be sure to remove your handler when no longer needed.
The best pattern is to Listen and Remove in OnEnable and OnDisable respectively.

Initializing - This event is sent when the hmd's tracking status changes to or from Unitialized.

Calibrating - This event is sent when starting or stopping calibration with the new state.

OutOfRange - This event is sent when losing or reacquiring absolute positional tracking.  This will 
never fire for the Rift DK1 since it does not have positional tracking.  For camera based trackers, this 
happens when the hmd exits and enters the camera's view.

DeviceConnected - This event is sent when devices are connected or disconnected.  The device index is passed
as the first argument, and the connected status (true / false) as the second argument.


Keybindings (if using the [Status] prefab):

Escape/Start - toggle menu
PageUp/PageDown - adjust scale
Home - reset scale
I - toggle frame stats on/off


Deploying on Steam:

If you are releasing your game on Steam (i.e. have a Steam ID and are calling Steam_Init through the  
Steamworks SDK), then you may want to check ISteamUtils::IsSteamRunningInVRMode() in order to determine if you  
should automatically launch into VR mode or not.


Known Issues:

* If Unity finds an Assets\Plugins\x86 folder, it will ignore all files in Assets\Plugins.  You will need to
either move openv_api.dll into the x86 subfolder, or move the dlls in the x86 folder up a level and delete
the x86 folder.


Troubleshooting:

* "Failed to connect to vrserver" - This often happens the first time you launch.  Often simply trying a second time
will clear this up.

* HmdError_Init_VRClientDLLNotFound - Make sure the SteamVR runtime is installed.  This can be found in Steam
under Tools.  Try uninstalling and reinstalling SteamVR.  Try deleting <user>/AppData/Local/OpenVR/openvrpaths.vrpath
and relaunching Steam to regenerate this file.

* HmdError_Init_HmdNotFound - SteamVR cannot detect your VR headset, ensure the USB cable is plugged in.
If that doesn't work, try deleting your Steam/config/steamvr.cfg.

* HmdError_Init_InterfaceNotFound - Make sure your SteamVR runtime is up to date.

* HmdError_IPC_ConnectFailed - SteamVR launches a separate process called vrserver.exe which directly talks
to the hardware.  Games communicate to vrserver through vrclient.dll over IPC.  This error is usually due
to the communication pipe between the two having closed.  Use task manager to verify there are no rogue apps
that got stuck trying to shut down.  Often it's just a matter of the connection timing out the first time
due to long load times.  Launching a second time usually resolves this.

* "Not using DXGI 1.1" - Older versions of Unity used DXGI 1.0 which doesn't support functionality the compositor
requires to operate properly.  To fix this, we've added a hook to Steam to force DXGI 1.1.  To enable this hook
set the environement variable ForceDXGICreateFactory1 = 1 and launch the Unity Editor or your standalone builds
via Steam by manually adding them using the "Add Game..." button found in the lower left of the Library tab.

* Core Parking often causes hitching.  The easiest way to disable core parking is to download the tool called
Core Parking Manager, slide the slider to 100% and click Apply.

