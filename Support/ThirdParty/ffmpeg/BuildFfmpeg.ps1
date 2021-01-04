# Copyright 2020 Google Inc.
#
# FFMPEG building script for use with TiltBrush
# Installs a temporary copy of Cygwin in order to do the compilation. After that, passes
# on control to a Cygwin script to download and build x264 and ffmpeg.

# Set up directories
$buildDir = "build_ffmpeg"
$cygwinRoot = "$(pwd)\${buildDir}\cygwin"
$cygwinDownload = "$(pwd)\${buildDir}\cygwin_download"

New-Item -Name "${buildDir}" -ItemType "directory" -Force
New-Item -Path "${buildDir}" -Name "cygwin_download" -ItemType "directory" -Force

# Download the Cygwin installer 
Invoke-WebRequest -Uri https://cygwin.com/setup-x86_64.exe -OutFile "${buildDir}\setup-x86_64.exe"

# Packages needed for the build
$packages = "git wget tar gawk autoconf automake binutils cmake doxygen git libtool make gcc-core " +
	"mingw64-x86_64-SDL2 mingw64-x86_64-binutils mingw64-x86_64-fribidi mingw64-x86_64-gcc-core " +
	"mingw64-x86_64-gcc-g++ mingw64-x86_64-headers mingw64-x86_64-runtime mingw64-x86_64-win-iconv " +
	"mingw64-x86_64-windows-default-manifest mingw64-x86_64-zlib nasm pkg-config subversion texinfo yasm"
$packages = [string]::Join(" ", ($packages.Split(" ") | % { "--packages " +$_ }))

Write-Output "${packages} --site https://mirrors.kernel.org/sourceware/cygwin --root ${cygwinRoot} --local-package-dir ${cygwinDownload} --quiet-mode --no-admin --no-desktop --no-replaceonreboot --no-shortcuts --no-startmenu"
$setupProcess = Start-Process -PassThru -FilePath "${buildDir}\setup-x86_64.exe" -ArgumentList "--packages '${packages}' --site https://mirrors.kernel.org/sourceware/cygwin --root ${cygwinRoot} --local-package-dir ${cygwinDownload} --quiet-mode --no-admin --no-desktop --no-replaceonreboot --no-shortcuts --no-startmenu"
$setupProcess.WaitForExit() 

Write-Output "Building FFMPEG."  

Copy-Item "BuildFfmpeg.sh" -Destination "${buildDir}\cygwin"

$buildProcess = Start-Process -PassThru -FilePath "${buildDir}\cygwin\bin\bash.exe" -ArgumentList '--login -c "/BuildFfmpeg.sh"' -NoNewWindow
$buildProcess.WaitForExit()

New-Item -Name "bin" -ItemType "directory" -Force
New-Item -Name "licenses" -ItemType "directory" -Force
Copy-Item "${buildDir}\cygwin\usr\local\bin\ffmpeg.exe" -Destination "bin"
Copy-Item "${buildDir}\cygwin\build_ffmpeg\ffmpeg\COPYING.GPLv2" -Destination "licenses\ffmpeg_COPYING"
Copy-Item "${buildDir}\cygwin\build_ffmpeg\ffmpeg\LICENSE.md" -Destination "licenses\ffmpeg_LICENSE.md"
Copy-Item "${buildDir}\cygwin\build_ffmpeg\x264\COPYING" -Destination "licenses\x264_COPYING"
Remove-Item -Force -Recurse -Path "${buildDir}"

Write-Output "DONE" 
