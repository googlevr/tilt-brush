# Tilt Brush README

Tilt Brush is licensed under Apache 2.0. It is not an officially supported
Google product. See the [LICENSE](LICENSE) file for more details.

## Trademarks

The Tilt Brush trademark and logo (“Tilt Brush Trademarks”) are trademarks of
Google, and are treated separately from the copyright or patent license grants
contained in the Apache-licensed Tilt Brush repositories on GitHub. Any use of
the Tilt Brush Trademarks other than those permitted in these guidelines must be
approved in advance.

For more information, read the
[Tilt Brush Brand Guidelines](BRAND_GUIDELINES.md).

## Building the application

Get the Tilt Brush open-source application running on your own devices.

### Prerequisites

*   [Unity 2018.4.11f1](unityhub://2018.4.11f1/7098af2f11ea)
*   [SteamVR](https://store.steampowered.com/app/250820/SteamVR/)
*   [Python 2.7.0](https://www.python.org/download/releases/2.7/) (Optional —
    needed only if you wish to run the scripts in the `Support/bin` directory)

### Changing the application name  {:#changing-the-application-name}

_Tilt Brush_ is a Google trademark. If you intend to publish a cloned version of
the application, you are required to choose a different name to distinguish it
from the official version. Before building the application, go into `App.cs` and
the Player settings to change the company and application names to your own.

Please see the [Tilt Brush Brand Guidelines](BRAND_GUIDELINES.md) for more details.

### Running the application in the Unity editor

Follow these steps when running the application for the first time:

1.  Start Unity.
1.  Go to **File** > **Open Scene**. \
1.  Select `/Assets/Scenes/Main.unity`. Unity should automatically prompt you to
    import **TextMesh Pro**.
1.  Choose **Import TMP Essentials**. \
    You can also do this through **Window** > **TextMesh Pro** > **Import TMP
    Essential Resources**.
1.  Press **Play**.

These steps have been tested with Release 1.0.54.

### Building the application from the Unity editor

Although it's possible to build Tilt Brush using the standard Unity build tools,
we recommend using a build script to ensure the application builds with the
correct settings. To run this script, go to **Tilt** > **Build** > **Do Build**,
or build from the Tilt Brush build window by navigating to **Tilt** >
**Build** > **Build Window**.

Note: The application may take a while to build the first time.

### Building the application from the Windows command line

Use the `build` script in the `Support/bin` directory to specify the target
platform and the build options you wish to enable. Run `build —help` to see the
various build options.

### Additional features

You should be able to get the basic version of Tilt Brush up and running very
quickly. The following features will take a little more time.

*   [Google service API support](#google-service-api-support)
*   [Enabling native Oculus support](#enabling-native-oculus-support)
*   [Sketchfab support](#sketchfab-support)
*   [*.fbx file support](#fbx-file-support)
*   [Camera path support](#camera-path-support)
*   [Video recording for the camera](#video-support)
*   [Offline rendering support](#offline-rendering-support)
*   [GIF support for the camera](#gif-support)

**Note:** Uploading to Poly has been removed completely and cannot be added back
in, because it uses an internal Google API. Download from Poly can still be
enabled.

## Systems that were replaced or removed when open-sourcing Tilt Brush

Some systems in Tilt Brush were removed or replaced with alternatives due to
open-source licensing issues. These are:

*   **Sonic Ether Natural Bloom**. The official Tilt Brush app uses a version
    purchased from the Asset Store; the open-source version uses
    [Sonic Ether's slightly modified open-source version](https://github.com/sonicether/SE-Natural-Bloom-Dirty-Lens).
*   **FXAA**. The official Tilt Brush app uses a modified version of the FXAA
    that Unity previously released with the standard assets on earlier versions
    of Unity - FXAA3 Console. This has been replaced with
    [FXAA by jintiao](https://github.com/jintiao/FXAA).
*   **Vignette and Chromatic Aberration**. The official Tilt Brush app uses
    modified versions of the Vignette and Chromatic Aberration effects that came
    with the standard assets in earlier versions of Unity. These have been
    replaced with a modified version of
    [KinoVignette by Keijiro](https://github.com/keijiro/KinoVignette).
*   **Tilt Shift**. The official Tilt Brush app uses modified versions of the
    Tilt Shift effect that came with the standard assets in earlier versions of
    Unity. These have been replaced with a modified version of
    [Tilt shift by underscorediscovery](https://gist.github.com/underscorediscovery/10324388).

## Google service API support

Set up Google API support to access Google services in the app.

### Enabling Google service APIs

Follow these steps when enabling Google service APIs:

1.  Create a new project in the
    [Google Cloud Console](https://console.developers.google.com/).
1.  Enable the following APIs and services:

    *   **YouTube Data API v3** — for uploading videos to YouTube
    *   **Poly API** — for accessing the Poly model library
    *   **Google Drive API** — for backup to Google Drive
    *   **People API** — for username and profile picture

Note: The name of your application on the developer console should match the
name you've given the app in `App.kGoogleServicesAppName` in `App.cs`.

### Creating a Google API key

Follow these steps when creating a Google API key:

1.  Go to the Credentials page from the Google Cloud Console.
1.  Click **Create Credential** and select **API key** from the drop-down menu.

### Google OAuth consent screen information

The OAuth consent screen asks users for permission to access their Google
account. You should be able to configure it from the Credentials screen.

Follow these steps when configuring the OAuth consent screen:

1.  Fill in the name and logo of your app, as well as the scope of the user data
    that the app will access.
1.  Add the following paths to the list of scopes:

    *   Google Drive API `../auth/drive.appdata`
    *   Google Drive API `../auth/drive.file`

### Creating an OAuth credential

The credential identifies the application to the Google servers. Follow these
steps to create an OAuth credential:

1.  Create a new credential on the Credentials screen.
1.  Select **OAuth**, and then select **Other**. Take note of the client ID and
    client secret values that are created for you. Keep the client secret a
    secret!

### Storing the Google API Key and credential data

Follow these steps to store the Google API Key and credential data:

1.  There is an asset in the `Assets/` directory called `Secrets` that contains
    a `Secrets` field. Add a new item to this field.
2.  Select `Google` as the service. Paste in the API key, client ID, and client
    secret that were generated earlier.

## Enabling native Oculus support

Note: _Tilt Brush_ is a Google trademark. If you intend to publish a cloned
version of the application, you are required to
[choose a different name](#changing-the-application-name) to distinguish it from
the official version.

Tilt Brush targets SteamVR instead of Oculus by default. Follow these steps to
enable native Oculus support:

1.  Enable the Oculus desktop package in the Package Manager.
1.  Install the
    [Oculus Unity Integration](https://developer.oculus.com/downloads/package/unity-integration-archive/1.41.0/).

**Note:** The above link goes to version 1.41, which the official version of
Tilt Brush was built with. Later versions have not been tested. You only need to
include the `Platform` and `VR` subdirectories when you import.

1.  If you see a dialog about upgrading the Unity Oculus plugin, click
    **Accept**.
1.  In the **Standalone** tab of the Player settings, go to **Other Settings**
    and define `OCULUS_SUPPORTED` in **Scripting Define Symbols**. Add it to the
    end of the list and use a semicolon to separate it from the previous define
    commands.

### Building your app for Oculus Quest

Follow these steps to build your app for Oculus Quest:

1.  Define `OCULUS_SUPPORTED` in **Scripting Define Symbols** in the **Android**
    tab of **Player Settings**, under **Other Settings**. Add it to the end of
    the list and use a semicolon to separate it from the previous define
    command.
1.  Set up your machine for
    [Oculus Quest Development](https://developer.oculus.com/documentation/unity/book-unity-gsg/?device=QUEST).
1.  Make sure the following are set in Unity:
    *   **Tilt** > **Build** > **SDK: OVR**
    *   **Tilt** > **Build** > **Platform: Android**
    *   **Tilt** > **Build** > **Runtime: Mono**
1.  Navigate to **Tilt** > **Build** > **Do Build**.
1.  Find the generated executable. It will most likely be somewhere under
    `Builds/OculusMobile_Release_TiltBrush_FromGui/`.
1.  Run `adb install com.google.tiltbrush.apk`.

### Publishing to Oculus stores

Follow these steps to publish to Oculus stores:

1.  Get an application ID from Oculus. The desktop and quest versions of each
    application need separate IDs.
1.  Add these IDs to the `Secrets` file. Both `Oculus` and `OculusMobile` should
    have their own entries.
1.  Put the app IDs in the `Client ID` field for each.

## Tilt Brush intro sketch

The Tilt Brush intro sketch uses some slightly modified shaders to produce the
animating-in effect while the sketch fades in. For faster loading, the intro
sketch is turned into a `*.prefab` file beforehand. Only the shaders used in the
intro sketch have been converted to work with the introduction.

*   The current intro sketches are located in `Support/Sketches/Intro`. There
    are two versions, one for PC and one for mobile.
*   The `*.prefab` files are located in `Assets/Prefabs/Intro`.
*   The materials and shaders used in the intro are located in
    `Assets/Materials/IntroMaterials`.
*   The `Assets/PlatformConfigPC` and `Assets/PlatformConfigMobile` files
    reference the `*.prefab` files that will be used in the intro.

### Creating an intro sketch

Follow these steps to replace or alter the intro sketch:

1.  Make sure the sketch of your choice is already loaded. Run Tilt Brush in the
    Unity Editor.
1.  Select **Tilt** > **Convert To Intro Materials** in the main Unity menu.
    This converts the materials in the sketch to the intro versions. \
    You will get warnings in the console for any materials it could not convert,
    as well as a summary of how many materials it converted.
1.  Navigate the hierarchy. Under the **Main** scene, open `SceneParent/Main
    Canvas`. Select any of the `Batch_...` objects to check whether they have
    the intro materials set.
1.  Move any objects that do not start with `Batch_` out from under the **Main
    Canvas** node.
1.  Select the **Main Canvas** node and run the **Tilt** > **Save Game Object As
    Prefab** menu command. \
    The scene will be saved as a `*.prefab` file called `gameobject_to_prefab`.
    under the `Assets/TestData` folder.
1.  Move the game object into the `Assets/Prefabs/Intro` folder.
1.  Update the references in `Assets/PlatformConfigPC` and
    `Assets/PlatformConfigMobile` to point to your new prefab file.

### Creating an intro sketch for mobile applications

You may want to have a pared-down version of the intro sketch for the mobile
version of the app. Stroke simplification is located in the **Settings** menu
inside Tilt Brush.

## Sketchfab support

Follow these steps to enable Sketchfab support:

1.  [Contact Sketchfab](https://sketchfab.com/developers/oauth) for a client ID
    and secret before you can upload to their service.
1.  Add the client ID and secret to the `Secrets` file.
1.  Set the service as **Sketchfab**. Leave the API key blank.

## FBX file support

You will need to build C# wrappers for the Autodesk FBX (the Autodesk filebox
format) SDK in order to import or export FBX and OBJ files in the app. See
[Support/fbx/README.md](Support/fbx/README.md) for details.

## Camera path support

Follow these steps to enable camera path support:

1.  Enable video support.
1.  Uncomment the code in `CameraPathCaptureRig.RecordPath()`.

## Video support

To get video support you will need to put an ffmpeg.exe binary in to 
`/Support/ThirdParty/ffmpeg/bin`. We have created a script to build one for
you - it temporarily requires around 2GB of space to build but will clear up
after itself.

Follow these steps to get video support:

1. Find '/Support/ThirdParty/ffmpeg/BuildFfmpeg.ps1', right-click on it in explorer and select
   'Run with Powershell'. It will take some time to build.
2. In Unity, modify `/Assets/PlatformConfigPC` and add 'Video' to 'Enabled Multicam Styles'.
   'Snapshot' should always be enabled.

### Video support bug fix

If you add video support, you may encounter a bug where the "Looking for audio"
and "Play some music on your computer" text will disappear if the controller is
angled too far. Fix this by doing the following:

1.  In Unity, find the `/Assets/TextMesh Pro/Resources/Shaders/TMP_SDF.shader`
    file.
1.  Duplicate it and rename this file `TMP_SDF-WriteDepth.shader`.
1.  Open the new file in a code or text editor and make the following changes to
    it:
    1.  Change the name from `TextMeshPro/Distance Field` to
        `TextMeshPro/Distance Field Depth`.
    1.  Change `Zwrite Off` to `Zwrite On`.
1.  In Unity, select `/Assets/Fonts/Oswald-Light SDF.asset`.
1.  Under `Atlas & Material`, double click `Oswald-Light SDF Material`.
1.  At the top, change the name for `Shader` from `TextMeshPro/Distance Field`
    to `TextMeshPro/Distance Field Depth`.

## Offline rendering support

When the user records a video from a saved sketch in Tilt Brush, a `.bat` file
is generated next to the `.mp4` for offline rendering support. This `.bat` file
requires the path to the executable of Tilt Brush. The code for writing out this
path to the file has been removed.

Follow these steps to restore the path:

1.  Open the file `Assets/Scripts/Rendering/VideoRecorderUtils.cs` in a code or
    text editor.
1.  Look for the function `CreateOfflineRenderBatchFile` near the bottom of the
    file.
1.  In the function, find the comments on how to modify the string to point to
    the executable path.
1.  Update the string to point to the correct path.

## GIF support

GIF support was removed for licensing reasons. To get GIF support, integrate
your own system by following these steps:

1.  Add the encoding code in around lines 129 - 148 of `Assets/Scripts/Gif
    Creation/GifEncodeTask.cs`.
1.  Modify `Assets/PlatformConfigPC` and `Assets/PlatformConfigMobile`. Add
    **Auto GIF** and/or **Time GIF** to **Enabled Multicam Styles**.

The released PC build had the following settings:

*   Snapshot
*   Auto GIF
*   Time GIF
*   Video

The released Quest build had the following settings:

*   Snapshot
*   Time GIF

## Experimental mode

Experimental mode is where features live before they are ready to be released in
a production build. This mode enables the experimental brushes and experimental
panel while disabling the intro sequence.

**New features and brushes that you find in experimental mode may not work as
expected.** Sketches that use experimental features and brushes won't work on
Poly or Sketchfab, and may break if loaded into production versions of Tilt
Brush.

### Turning on experimental mode

Follow these steps to turn on experimental mode:

1.  Find the Config object in the main scene by going to **App** > **Config**.
1.  Turn on the **Is Experimental** flag.

The Tilt Brush build system will then set up the experimental flag as needed
when you make a build.

### Making your code experimental

Code in experimental mode is usually surrounded by the following block:

```
# if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)

    if (Config.IsExperimental) {
      // Experimental code goes here
    }

# endif
```

In the editor, all you need to enable experimental mode is to turn on the
experimental flag. The `EXPERIMENTAL_ENABLED` scripting definition needs to be
set, or the code will not be compiled into the build at all. This prevents
unfinished features from being accessed by people who hack or decompile the
executable.

### Experimental brushes

Experimental brushes and environments are located in the `Assets/Resources/X`
folder. They are not included in non-experimental builds.
