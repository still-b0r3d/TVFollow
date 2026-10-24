@echo off
rem Builds TVFollow with the C# compiler that comes with Windows (.NET Framework 4).
rem Nothing to install. Run "build.cmd test" to build and run the tests too.
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Can't find the C# compiler that comes with Windows ^(.NET Framework 4^).
    exit /b 1
)

set "REFS=/r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll"
if not exist bin mkdir bin

"%CSC%" /nologo /optimize+ /debug- /target:winexe /win32icon:assets\tv.ico %REFS% /out:bin\TVFollow.exe src\*.cs
if errorlevel 1 exit /b 1

"%CSC%" /nologo /debug- /target:exe /main:TVFollow.Tests.TestMain %REFS% /out:bin\TVFollowTests.exe src\*.cs tests\*.cs
if errorlevel 1 exit /b 1

echo Built bin\TVFollow.exe

if /i "%~1"=="test" bin\TVFollowTests.exe
