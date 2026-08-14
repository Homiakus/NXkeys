@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-nxkeys.ps1" %*
exit /b %ERRORLEVEL%
