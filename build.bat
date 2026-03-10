@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"

set "MODE=%~1"
if "%MODE%"=="" set "MODE=all"

set "VERSION_FILE=version.txt"
if not exist "%VERSION_FILE%" (
  >"%VERSION_FILE%" echo 0.1.0
)

for /f "usebackq delims=" %%V in ("%VERSION_FILE%") do set "APP_VERSION=%%V"
if "%APP_VERSION%"=="" set "APP_VERSION=0.1.0"

echo ==========================================
echo MirrorDeck Build Pipeline
echo Mode: %MODE%
echo Current version: %APP_VERSION%
echo ==========================================

if /I "%~2"=="--bump" (
  call :BumpPatch "%APP_VERSION%"
  set "APP_VERSION=!NEW_VERSION!"
  >"%VERSION_FILE%" echo !APP_VERSION!
  echo Version bumped to !APP_VERSION!
)

where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: dotnet SDK not found.
  exit /b 1
)

set "DIST_DIR=%cd%\dist"
set "PUBLISH_DIR=%DIST_DIR%\portable\MirrorDeck"
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if not exist "%DIST_DIR%" mkdir "%DIST_DIR%"

echo Publishing MirrorDeck.WinUI...
dotnet publish "MirrorDeck.WinUI\MirrorDeck.WinUI.csproj" -c Release -r win-x64 --self-contained true -o "%PUBLISH_DIR%"
if errorlevel 1 exit /b 1

copy /y "%VERSION_FILE%" "%PUBLISH_DIR%\version.txt" >nul

if /I "%MODE%"=="portable" goto :Done

if /I "%MODE%"=="installer" goto :BuildInstaller
if /I "%MODE%"=="all" goto :BuildInstaller
if /I "%MODE%"=="msix" goto :BuildMsix
if /I "%MODE%"=="package" goto :BuildMsix

echo ERROR: Unknown mode "%MODE%". Use portable^|installer^|msix^|all
exit /b 1

:BuildInstaller
echo Building Inno Setup installer...
set "ISCC="
for /f "delims=" %%I in ('where iscc.exe 2^>nul') do (
  if not defined ISCC set "ISCC=%%~fI"
)
if not defined ISCC if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "C:\Program Files\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"
if not defined ISCC (
  echo ERROR: ISCC.exe not found. Install Inno Setup 6.
  exit /b 1
)

"%ISCC%" "/DMyAppVersion=%APP_VERSION%" "/O%DIST_DIR%" "script.iss"
if errorlevel 1 exit /b 1

if /I "%MODE%"=="installer" goto :Done
if /I "%MODE%"=="all" goto :BuildMsix
goto :Done

:BuildMsix
echo Building MSIX package project...
if not exist "MirrorDeck.Package\MirrorDeck.Package.wapproj" (
  echo WARNING: MirrorDeck.Package.wapproj not found. Skipping MSIX build.
  goto :Done
)

for /f "delims=" %%S in ('dotnet --version') do set "DOTNET_SDK_VER=%%S"
if not exist "C:\Program Files\dotnet\sdk\!DOTNET_SDK_VER!\Microsoft\DesktopBridge\Microsoft.DesktopBridge.props" (
  echo WARNING: DesktopBridge build tools not found. Install Visual Studio Packaging workload to build MSIX.
  goto :Done
)

dotnet build "MirrorDeck.Package\MirrorDeck.Package.wapproj" -c Release /p:AppxBundle=Never /p:UapAppxPackageBuildMode=SideloadOnly /p:AppxPackageDir="%DIST_DIR%\msix\\"
if errorlevel 1 exit /b 1

goto :Done

:BumpPatch
set "IN=%~1"
for /f "tokens=1,2,3 delims=." %%a in ("%IN%") do (
  set "MAJOR=%%a"
  set "MINOR=%%b"
  set "PATCH=%%c"
)
if "%MAJOR%"=="" set "MAJOR=0"
if "%MINOR%"=="" set "MINOR=1"
if "%PATCH%"=="" set "PATCH=0"
set /a PATCH=PATCH+1
set "NEW_VERSION=%MAJOR%.%MINOR%.%PATCH%"
goto :eof

:Done
echo.
echo Build complete for version %APP_VERSION%.
echo Dist root: %DIST_DIR%
if exist "%DIST_DIR%\portable\MirrorDeck\MirrorDeck.exe" echo Portable: dist\portable\MirrorDeck\MirrorDeck.exe
set "FOUND_INSTALLER="
for %%F in ("%DIST_DIR%\MirrorDeck-Setup-%APP_VERSION%*.exe") do (
  echo Installer: %%~nxF
  set "FOUND_INSTALLER=1"
)
if not defined FOUND_INSTALLER (
  for %%F in ("%DIST_DIR%\MirrorDeck-Setup-v%APP_VERSION%*.exe") do echo Installer: %%~nxF
)
echo.
exit /b 0
