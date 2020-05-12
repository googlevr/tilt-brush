To enable support for importing and exporting fbx and obj files, you'll
need a set of C# wrappers around Autodesk's FBX sdk, and some support
functions implemented in C for speed.

1. Clone [Unity's com.autodesk.fbx package](https://github.com/Unity-Technologies/com.autodesk.fbx)
1. (optional) Follow the
   [build instructions](https://github.com/Unity-Technologies/com.autodesk.fbx/blob/master/README.md)
   to make sure you have all the other pieces you need (cmake, swig, pcre, ...)
   before the next step. NOTE: Although the instructions say to install VS 2015,
   the makefile expects VS 2017, so install that instead.
1. Copy `tilt_brush.i` into the `Source` directory of your `com.autodesk.fbx`
   repository and apply the patch `com.autodesk.fbx.patch`.
1. Follow the build instructions in `com.autodesk.fbx`. If successful, this
   will create a directory `build/install/com.autodesk.fbx`.
1. Add the package to your Tilt Brush project. The quickest way to do this
   is to copy or link the resulting directory `build/install/com.autodesk.fbx`
   into the `Packages` directory of your Tilt Brush repository.
1. Add `FBX_SUPPORTED` to Project Settings -> Player -> Scripting Define
   Symbols.
