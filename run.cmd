@echo off
setlocal
set "ROOT=%~dp0"
pushd "%ROOT%" || exit /b 1
if not exist "nx2512-pro-hybrid.json" copy "config\nx2512-pro-hybrid.json" "nx2512-pro-hybrid.json" >nul
if exist "dist\nxkeys-windows-amd64.exe" (
  "dist\nxkeys-windows-amd64.exe" --config "nx2512-pro-hybrid.json"
) else (
  go run .\cmd\nxkeys --config "nx2512-pro-hybrid.json"
)
set "CODE=%ERRORLEVEL%"
popd
exit /b %CODE%
