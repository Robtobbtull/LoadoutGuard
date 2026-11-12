@echo off
rem Builds LoadoutGuard with the C# compiler that ships with Windows (.NET Framework 4.x).
rem Nothing to install. Output: bin\LoadoutGuard.exe
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo C# compiler not found. It comes with .NET Framework 4.x, which is part of Windows 10 and 11.
  exit /b 1
)

if not exist bin mkdir bin

"%CSC%" /nologo /target:winexe /optimize+ /out:bin\LoadoutGuard.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll src\*.cs || exit /b 1
"%CSC%" /nologo /target:exe /out:bin\SelfTest.exe /r:System.Drawing.dll src\LogTail.cs src\Detector.cs src\Settings.cs src\ClickArea.cs src\Theme.cs src\Updater.cs tests\SelfTest.cs || exit /b 1
rem Smart App Control (Windows 11) often blocks a freshly built exe on its first run, then allows it.
bin\SelfTest.exe 2>nul || (ping -n 6 127.0.0.1 >nul & bin\SelfTest.exe) || exit /b 1

echo Build OK: bin\LoadoutGuard.exe
