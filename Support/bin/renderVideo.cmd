@ECHO OFF

REM EXAMPLE USE FROM AN EXTERNAL BATCH FILE:
REM ---------------------------------------------------------------------------
REM @ECHO OFF
REM CALL "C:\Path\To\TiltBrush\Support\bin\renderVideo.cmd" ^
REM   "C:\Users\jcowles\Documents\Tilt Brush\Sketches\Untitled_163.tilt" ^
REM   "Red Swamp Melt_00.usda" ^
REM   "c:\Path\To\TiltBrush\TiltBrush.exe"
REM ---------------------------------------------------------------------------

SET sketchPath=%~f1
SET usdaPath=%~f2
SET exeName=%~nx3
SET exePath=%~f3

IF NOT "%ERRORLEVEL%" == "0" (
  ECHO ERROR: This batch file was not called with the expected arguments.
  GOTO Error
)

tasklist /FI "IMAGENAME eq %exeName%" 2>NUL | find /I /N "%exeName%">NUL
IF "%ERRORLEVEL%" == "0" (
  ECHO Tilt Brush is running, please exit Tilt Brush before rendering
  GOTO Error
)

ECHO Sketch: %sketchPath%
ECHO Video Path: %usdaPath%
ECHO.

ECHO What would you like to do?
ECHO.

ECHO 1) HD 30 FPS
ECHO 2) HD 60 FPS
ECHO 3) 4k (UHD-1) 30 FPS
ECHO 4) 4k (UHD-1) 60 FPS
ECHO 5) Omnidirectional Stereo 360, 4k x 4k 30 FPS
ECHO 6) Omnidirectional Stereo 360, 4k x 4k 30 FPS, no quick load
ECHO 7) [Fast,Low Quality] Omnidirectional Stereo 360, 1k x 1k 30 FPS
ECHO 8) [Fast,Low Quality] Omnidirectional Stereo 360, 1k x 1k 30 FPS, no quick load
ECHO.

SET /P selItem=Select an option: 
ECHO.

IF "%selItem%"=="" GOTO Error

IF "%selItem%"=="1" (
  SET res=1920
  SET resh=1080
  SET fps=30
  GOTO RunVideo
)
IF "%selItem%"=="2" (
  SET res=1920
  SET resh=1080
  SET fps=60
  GOTO RunVideo
)
IF "%selItem%"=="3" (
  SET res=3840
  SET resh=2160
  SET fps=30
  GOTO RunVideo
)
IF "%selItem%"=="4" (
  SET res=3840
  SET resh=2160
  SET fps=60
  GOTO RunVideo
)
IF "%selItem%"=="5" (
  ECHO Rendering 360 stereo omnidirecitonal stereo 4k x 4k 30fps 360 stereo
  "%exePath%" --renderCameraPath "%usdaPath%" --captureOds "%sketchPath%"
  GOTO End
)
IF "%selItem%"=="6" (
  ECHO Rendering 360 stereo omnidirecitonal stereo 4k x 4k 30fps 360 stereo, no quick load
  "%exePath%" --noQuickLoad --renderCameraPath "%usdaPath%" --captureOds "%sketchPath%"
  GOTO End
)
IF "%selItem%"=="7" (
  ECHO Rendering [Fast,Low Quality] 360 stereo omnidirecitonal stereo 1k x 1k 30fps 360 stereo
  "%exePath%" --preview --renderCameraPath "%usdaPath%" --captureOds "%sketchPath%"
  GOTO End
)
IF "%selItem%"=="8" (
  ECHO Rendering [Fast,Low Quality] 360 stereo omnidirecitonal stereo 1k x 1k 30fps 360 stereo, no quick load
  "%exePath%" --preview --noQuickLoad --renderCameraPath "%usdaPath%" --captureOds "%sketchPath%"
  GOTO End
)

ECHO Invalid selection
GOTO Error

:RunVideo
  ECHO Rendering %fps% FPS, %res% x %resh%
  "%exePath%" --renderCameraPath "%usdaPath%" --Video.OfflineFPS %fps% --Video.OfflineResolution %res% "%sketchPath%"
  GOTO End

:Run
  GOTO End

:Error
  ECHO Exiting due to error.

:End
ECHO.
PAUSE
