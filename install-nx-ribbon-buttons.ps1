# PowerShell Script: Install Ribbon Buttons into Siemens NX 2512
# Creates a managed NXKeys package with "Toggle Leader Key" and
# "NXKeys Studio Settings" buttons for Siemens NX Ribbon Bar & Menubar.

[CmdletBinding()]
param(
    [switch]$Build,
    [switch]$Clean,
    [switch]$NoStopRunning,
    [switch]$AllowRunningNX,
    [string]$ConfigPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $ScriptDir

$HotkeyProjectDir = Join-Path $ScriptDir 'NX2512_HotkeyStudio'
$HotkeyBuildScript = Join-Path $HotkeyProjectDir 'build.ps1'
$HotkeyDist = Join-Path $HotkeyProjectDir 'dist'
$HotkeyRelease = Join-Path $HotkeyProjectDir 'bin\Release\net8.0-windows'
$BridgeProjectDir = Join-Path $ScriptDir 'NX2512_CommandBridge'
$BridgeBuildScript = Join-Path $BridgeProjectDir 'build.ps1'
$BridgeDist = Join-Path $BridgeProjectDir 'dist'
$BridgeRelease = Join-Path $BridgeProjectDir 'bin\Release\net8.0-windows'

function Write-Step([string]$Text) {
    Write-Host ""
    Write-Host $Text -ForegroundColor Yellow
}

function Resolve-HotkeyStudioExe {
    $candidates = @(
        (Join-Path $HotkeyDist 'NX2512_HotkeyStudio.exe'),
        (Join-Path $HotkeyRelease 'NX2512_HotkeyStudio.exe')
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    return $null
}

function Resolve-CommandBridgeDll {
    $candidates = @(
        (Join-Path $BridgeDist 'NX2512_CommandBridge.dll'),
        (Join-Path $BridgeRelease 'NX2512_CommandBridge.dll')
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    return $null
}

function Resolve-ConfigPath([string]$ExplicitPath) {
    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $candidates += $ExplicitPath
    }
    $candidates += @(
        (Join-Path $ScriptDir 'nx2512-pro-hybrid.json'),
        (Join-Path $HotkeyDist 'nx2512-pro-hybrid.json'),
        (Join-Path $ScriptDir 'nx2512-ergo-80.json')
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw 'No NXKeys config found. Expected nx2512-pro-hybrid.json or nx2512-ergo-80.json.'
}

function Stop-HotkeyStudioProcesses([string]$ManagedRootPath) {
    $processes = @(Get-Process -Name 'NX2512_HotkeyStudio' -ErrorAction SilentlyContinue)
    if ($processes.Count -eq 0) {
        return
    }

    Write-Host "  -> Stopping running NX2512_HotkeyStudio instances before update..." -ForegroundColor Yellow
    foreach ($proc in $processes) {
        $path = $null
        try { $path = $proc.MainModule.FileName } catch { }

        if ($path -and $path.StartsWith($ManagedRootPath, [StringComparison]::OrdinalIgnoreCase)) {
            try {
                if ($proc.MainWindowHandle -ne 0) {
                    [void]$proc.CloseMainWindow()
                    if ($proc.WaitForExit(3000)) { continue }
                }
                Stop-Process -Id $proc.Id -Force -ErrorAction Stop
                try { [void]$proc.WaitForExit(5000) } catch { }
            }
            catch {
                throw "Unable to stop running NX2512_HotkeyStudio process $($proc.Id). Close NXKeys Studio manually and rerun this script."
            }
        }
    }
}

function Copy-FileWithRetry([string]$Source, [string]$DestinationDir) {
    $lastError = $null
    for ($attempt = 1; $attempt -le 8; $attempt++) {
        try {
            Copy-Item -LiteralPath $Source -Destination $DestinationDir -Force -ErrorAction Stop
            return
        }
        catch {
            $lastError = $_
            Start-Sleep -Milliseconds (250 * $attempt)
        }
    }

    throw "Unable to copy '$Source' to '$DestinationDir' after waiting for file locks to clear. Last error: $($lastError.Exception.Message)"
}

function Get-SiemensNxProcesses {
    $processes = @(Get-Process -Name 'ugraf','run_nx','nx' -ErrorAction SilentlyContinue)
    foreach ($proc in $processes) {
        $path = $null
        $description = $null
        try { $path = $proc.MainModule.FileName } catch { }
        try { $description = $proc.Description } catch { }

        if ($proc.ProcessName -in @('ugraf', 'run_nx')) {
            $proc
            continue
        }

        if ($proc.ProcessName -eq 'nx' -and (
            ($path -and $path -match '\\Siemens\\|\\DesigncenterNX|\\NXBIN\\') -or
            ($description -and $description -match 'Siemens|Designcenter|NX')
        )) {
            $proc
        }
    }
}

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " Installing NXKeys Ribbon Buttons into Siemens NX 2512   " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path -LiteralPath $HotkeyProjectDir -PathType Container)) {
    throw "HotkeyStudio project folder not found: $HotkeyProjectDir"
}
if (-not (Test-Path -LiteralPath $BridgeProjectDir -PathType Container)) {
    throw "CommandBridge project folder not found: $BridgeProjectDir"
}

$ExePath = Resolve-HotkeyStudioExe
if ($Build -or -not $ExePath) {
    if (-not (Test-Path -LiteralPath $HotkeyBuildScript -PathType Leaf)) {
        throw "Build script not found: $HotkeyBuildScript"
    }

    Write-Step "[BUILD] Building NX2512_HotkeyStudio..."
    $buildArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $HotkeyBuildScript)
    if ($Clean) { $buildArgs += '-Clean' }
    & powershell @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "NX2512_HotkeyStudio build failed with exit code $LASTEXITCODE."
    }

    $ExePath = Resolve-HotkeyStudioExe
}

if (-not $ExePath) {
    throw 'NX2512_HotkeyStudio.exe was not found after build.'
}

$BridgeDllPath = Resolve-CommandBridgeDll
if ($Build -or -not $BridgeDllPath) {
    if (-not (Test-Path -LiteralPath $BridgeBuildScript -PathType Leaf)) {
        throw "Bridge build script not found: $BridgeBuildScript"
    }

    Write-Step "[BUILD] Building NX2512_CommandBridge..."
    $bridgeBuildArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $BridgeBuildScript)
    if ($Clean) { $bridgeBuildArgs += '-Clean' }
    & powershell @bridgeBuildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "NX2512_CommandBridge build failed with exit code $LASTEXITCODE."
    }

    $BridgeDllPath = Resolve-CommandBridgeDll
}

if (-not $BridgeDllPath) {
    throw 'NX2512_CommandBridge.dll was not found after build.'
}

$ArtifactSource = if (Test-Path -LiteralPath $HotkeyDist -PathType Container) { $HotkeyDist } else { $HotkeyRelease }
if (-not (Test-Path -LiteralPath $ArtifactSource -PathType Container)) {
    throw "Runtime artifact folder not found: $ArtifactSource"
}

$BridgeArtifactSource = if (Test-Path -LiteralPath $BridgeDist -PathType Container) { $BridgeDist } else { $BridgeRelease }
if (-not (Test-Path -LiteralPath $BridgeArtifactSource -PathType Container)) {
    throw "CommandBridge artifact folder not found: $BridgeArtifactSource"
}

Write-Step "[1/5] Copying NXKeys Studio application binaries to managed folder..."
$LocalAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$ManagedRoot = Join-Path $LocalAppData 'NXKeys\managed\NX2512.6000'
$ManagedCustomDir = Join-Path $ManagedRoot 'custom'
$ManagedApplicationDir = Join-Path $ManagedCustomDir 'application'
$ManagedStartupDir = Join-Path $ManagedCustomDir 'startup'

$nxProcesses = @(Get-SiemensNxProcesses)
if ($nxProcesses.Count -gt 0) {
    $details = ($nxProcesses | ForEach-Object { "$($_.ProcessName)[$($_.Id)] $($_.Path)" }) -join '; '
    if (-not $AllowRunningNX) {
        throw "Siemens NX is running. Close NX before installing NXKeys. Detected: $details"
    }
    Write-Warning "Siemens NX is running. Managed files will be updated, but NX must be restarted or menus reloaded to see new startup/application MenuScript. Detected: $details"
}

New-Item -ItemType Directory -Force -Path $ManagedRoot | Out-Null
New-Item -ItemType Directory -Force -Path $ManagedApplicationDir | Out-Null
New-Item -ItemType Directory -Force -Path $ManagedStartupDir | Out-Null
if (-not $NoStopRunning) {
    Stop-HotkeyStudioProcesses $ManagedRoot
}

Get-ChildItem -LiteralPath $ArtifactSource -File | ForEach-Object {
    Copy-FileWithRetry $_.FullName $ManagedRoot
}

Write-Host "  -> Application copied from: $ArtifactSource" -ForegroundColor Green
Write-Host "  -> Application copied to:   $ManagedRoot" -ForegroundColor Green

Write-Step "[2/5] Copying NXOpen direct command bridge..."
Get-ChildItem -LiteralPath $BridgeArtifactSource -File | ForEach-Object {
    Copy-FileWithRetry $_.FullName $ManagedApplicationDir
    Copy-FileWithRetry $_.FullName $ManagedStartupDir
}
Write-Host "  -> Bridge copied from: $BridgeArtifactSource" -ForegroundColor Green
Write-Host "  -> Bridge copied to:   $ManagedApplicationDir" -ForegroundColor Green
Write-Host "  -> Bridge autoload to: $ManagedStartupDir" -ForegroundColor Green

Write-Step "[3/5] Generating MenuScript overlay, Ribbon Bar (.rtb), and launchers..."
$ResolvedConfigPath = Resolve-ConfigPath $ConfigPath
Write-Host "  -> Config: $ResolvedConfigPath" -ForegroundColor Gray

$applyArgs = @('apply', '--config', $ResolvedConfigPath, '--yes')
if ($AllowRunningNX) { $applyArgs += '--allow-running-nx' }
& $ExePath @applyArgs
if ($LASTEXITCODE -ne 0) {
    throw "NX2512_HotkeyStudio apply failed with exit code $LASTEXITCODE."
}

Write-Step "[4/5] Leaving Siemens installation and profile files untouched"
Write-Host "  -> Managed custom directory: $ManagedCustomDir" -ForegroundColor Green
Write-Host "  -> Activate it through the generated launch-nx2512-with-nxkeys.cmd wrapper." -ForegroundColor Green

Write-Step "[5/5] Skipped direct custom_dirs.dat/profile patching by design"
Write-Host "  -> To patch an existing custom_dirs.dat, set deployment.mode to existing-custom-dirs and use the app apply flow." -ForegroundColor Gray

$WrapperPath = Join-Path $ManagedRoot 'launch-nx2512-with-nxkeys.cmd'

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host " SUCCESS: NXKeys Studio managed package is ready!       " -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host ""
Write-Host "When NX is launched through the generated wrapper, the custom directory provides:" -ForegroundColor White
Write-Host "  1. 'NXKeys Direct Command Bridge' - Loads in-process NX command executor" -ForegroundColor Cyan
Write-Host "  2. 'Enable Leader Key'            - Starts key interception service (CapsLock)" -ForegroundColor Cyan
Write-Host "  3. 'NXKeys Studio Settings'       - Opens Hotkey Studio settings window" -ForegroundColor Cyan
Write-Host ""
Write-Host "Launch Siemens NX via '$WrapperPath'." -ForegroundColor Yellow
