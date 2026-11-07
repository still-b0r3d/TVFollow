@echo off
rem Builds TVFollow with the C# compiler that comes with Windows (.NET Framework 4).
rem Nothing to install. Run "build.cmd test" to build and run the tests too.
rem Double-clicked, the window stays open at the end so the result can be read.
rem Keep this file plain ASCII with CRLF line endings, or cmd misreads it.
setlocal
cd /d "%~dp0"
call :build %1
set "RC=%ERRORLEVEL%"
if "%~1"=="" echo %cmdcmdline% | "%WINDIR%\System32\find.exe" /i "%~nx0" >nul && pause
exit /b %RC%

:build
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Can't find the C# compiler that comes with Windows ^(.NET Framework 4^).
    exit /b 1
)

rem /codepage:65001 reads the source as UTF-8 (it has a few non-ASCII characters)
set "OPTS=/nologo /debug- /codepage:65001 /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.Runtime.Serialization.dll /r:System.Security.dll /r:System.Windows.Forms.dll /r:System.Xml.dll"
set "ICONS=/resource:assets\tv.ico,TVFollow.tv.ico /resource:assets\tv-paused.ico,TVFollow.tv-paused.ico /resource:assets\tv-on.ico,TVFollow.tv-on.ico /resource:assets\tv-off.ico,TVFollow.tv-off.ico /resource:assets\tv-pc.ico,TVFollow.tv-pc.ico"
rem the example app, file by file: TVFollow zips it when it's pushed. A test checks every file
rem in the folder is here.
set "EX=examples\tvfollow-example"
set "EXAMPLE=/resource:%EX%\manifest,example/manifest /resource:%EX%\source\main.brs,example/source/main.brs /resource:%EX%\components\MainScene.xml,example/components/MainScene.xml /resource:%EX%\components\MainScene.brs,example/components/MainScene.brs /resource:%EX%\images\icon_focus_fhd.png,example/images/icon_focus_fhd.png /resource:%EX%\images\icon_focus_hd.png,example/images/icon_focus_hd.png /resource:%EX%\images\splash_fhd.png,example/images/splash_fhd.png /resource:%EX%\images\splash_hd.png,example/images/splash_hd.png"
if not exist bin mkdir bin

"%CSC%" %OPTS% %ICONS% %EXAMPLE% /optimize+ /target:winexe /win32icon:assets\tv.ico /win32manifest:assets\TVFollow.manifest /out:bin\TVFollow.exe src\*.cs
if errorlevel 1 exit /b 1

"%CSC%" %OPTS% %ICONS% %EXAMPLE% /target:exe /win32manifest:assets\TVFollow.manifest /main:TVFollow.Tests.TestMain /out:bin\TVFollowTests.exe src\*.cs tests\*.cs
if errorlevel 1 exit /b 1

echo Built bin\TVFollow.exe
if /i "%~1"=="test" bin\TVFollowTests.exe
exit /b %ERRORLEVEL%
