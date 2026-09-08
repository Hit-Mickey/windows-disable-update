@echo off
rem ============================================================
rem  WinUpdatePauser build script
rem  Requires: .NET SDK 5.0+ (builds the net48 target)
rem ============================================================
setlocal
cd /d "%~dp0"

set "DOTNET="

rem --- Candidate 1: dotnet on PATH (only if it actually has an SDK; a
rem     runtime-only install has dotnet.exe but "--list-sdks" prints nothing)
for /f "delims=" %%i in ('dotnet --list-sdks 2^>nul') do if not defined DOTNET set "DOTNET=dotnet"

rem --- Candidate 2: per-user install (default location of dotnet-install.ps1)
if not defined DOTNET if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" (
    for /f "delims=" %%i in ('call "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" --list-sdks 2^>nul') do if not defined DOTNET set "DOTNET=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
)

if not defined DOTNET (
    echo [ERROR] No .NET SDK found. Install one first:
    echo   winget install Microsoft.DotNet.SDK.10
    exit /b 1
)

echo Using dotnet: %DOTNET%
echo Building Release...
"%DOTNET%" build "src\WinUpdatePauser\WinUpdatePauser.csproj" -c Release || (
    echo [ERROR] Build failed.
    exit /b 1
)

echo.
echo ============================================================
echo  Build succeeded.
echo  Output: %~dp0src\WinUpdatePauser\bin\Release\net48\WUPause.exe
echo ============================================================
exit /b 0
