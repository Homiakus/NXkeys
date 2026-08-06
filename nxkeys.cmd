@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

:menu
cls
echo ============================================================
echo               NXKeys Adaptive Leader v8
echo ============================================================
echo.
echo   [1] Build ^& Launch (dev)
echo   [2] Launch (pre-built dist)
echo   [3] Launch (background/tray)
echo   [4] Validate profile
echo   [5] Scan NX installation
echo   [6] Export operation icons
echo   [7] Bridge status
echo   [8] Health check
echo   [9] Build only (no launch)
echo   [0] Exit
echo.
echo ============================================================
set /p choice="Choose [0-9]: "

if "%choice%"=="1" goto build_and_launch
if "%choice%"=="2" goto launch
if "%choice%"=="3" goto launch_tray
if "%choice%"=="4" goto validate
if "%choice%"=="5" goto scan
if "%choice%"=="6" goto export_icons
if "%choice%"=="7" goto bridge_status
if "%choice%"=="8" goto health
if "%choice%"=="9" goto build_only
if "%choice%"=="0" goto exit

echo Invalid choice.
timeout /t 1 >nul
goto menu

:build_and_launch
echo.
echo ==^> Building NX2512_HotkeyStudio...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0NX2512_HotkeyStudio\build.ps1"
if %ERRORLEVEL% neq 0 (
    echo BUILD FAILED (code %ERRORLEVEL%).
    pause
    goto menu
)
echo Build OK. Launching...
"%~dp0NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe"
goto menu

:launch
echo.
echo ==^> Launching NXKeys...
if not exist "%~dp0NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe" (
    echo ERROR: dist\NX2512_HotkeyStudio.exe not found. Build first [1].
    pause
    goto menu
)
start "" "%~dp0NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe"
echo Launched.
timeout /t 1 >nul
goto menu

:launch_tray
echo.
echo ==^> Launching NXKeys in background/tray...
if not exist "%~dp0NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe" (
    echo ERROR: dist\NX2512_HotkeyStudio.exe not found. Build first [1].
    pause
    goto menu
)
start "" "%~dp0NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe" --tray --gui
echo Launched in tray.
timeout /t 1 >nul
goto menu

:validate
echo.
echo ==^> Validating profile...
if not exist "%~dp0NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe" (
    echo ERROR: dist not found. Build first [1].
    pause
    goto menu
)
"%~dp0NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe" validate
pause
goto menu

:scan
echo.
echo ==^> Scanning NX installation...
if not exist "%~dp0NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe" (
    echo ERROR: dist not found. Build first [1].
    pause
    goto menu
)
"%~dp0NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe" scan
pause
goto menu

:export_icons
echo.
echo ==^> Exporting operation icons...
if not exist "%~dp0NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe" (
    echo ERROR: dist not found. Build first [1].
    pause
    goto menu
)
"%~dp0NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe" export-icons
pause
goto menu

:bridge_status
echo.
echo ==^> Bridge status...
if not exist "%~dp0NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe" (
    echo ERROR: dist not found. Build first [1].
    pause
    goto menu
)
"%~dp0NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe" bridge-status
pause
goto menu

:health
echo.
echo ==^> Health check...
if not exist "%~dp0NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe" (
    echo ERROR: dist not found. Build first [1].
    pause
    goto menu
)
"%~dp0NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe" health
pause
goto menu

:build_only
echo.
echo ==^> Building NX2512_HotkeyStudio...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0NX2512_HotkeyStudio\build.ps1"
if %ERRORLEVEL% neq 0 (
    echo BUILD FAILED (code %ERRORLEVEL%).
) else (
    echo Build OK.
)
pause
goto menu

:exit
echo.
echo Goodbye.
exit /b 0
