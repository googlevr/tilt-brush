Using Universal Scene Description in Tilt Brush
------------------------------------------------

1) Compile USD using a method such as this:

Source: https://github.com/PixarAnimationStudios/USD/
Build scripts: https://github.com/vfxpro99/usd-build-club

2) Combine the /bin and /lib directories, pruning them down
to what we need at runtime.

3) Copy C++ DLLs into Assets/ThirdParty/Usd/Plugins/x86_64/...

4) Copy USD {share, plugin} directories into Support/ThirdParty/Usd/...

5) The C# bindings and Unity utilities are generated using Swig via:
https://github.com/jcowles/UsdBindings

6) Copy C# DLLs into Assets/ThirdParty/Usd

7) Profit.


Concerning plugin discovery:
----------------------------
All of USD is plugin based, even the core types and file formats. As a result, these plugins must 
be discovered at runtime. To avoid dlopen'ing all plugins in the universe, plugins are required to
include a plugInfo.json file summarizing the plugins contained and their DLL path. The location of
these files can be customized:

1. using pxr.PlugRegistry.RegisterPlugins()
2. using the environment variable PXR_PLUGINPATH_NAME

Windows 10 Anniversary update changed in such a way the the CRT now caches environment variables
before the application can set them, which means on that OS, setting PXR_PLUGINPATH_NAME will not
work. As a result, Tilt Brush uses option #1.
