[CmdletBinding()]
param(
    [string]$ConfigPath,
    [string]$NxRoot,
    [string]$NxOpenDll,
    [switch]$Clean,
    [switch]$NoBuild,
    [switch]$AllowRunningNX
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

function Write-Step([string]$Text) {
    Write-Host "`n==> $Text" -ForegroundColor Cyan
}

function Resolve-Config([string]$Requested) {
    $candidates = [System.Collections.Generic.List[string]]::new()
    if (-not [string]::IsNullOrWhiteSpace($Requested)) { [void]$candidates.Add($Requested) }
    [void]$candidates.Add((Join-Path $ScriptDir 'config\nx2512-pro-hybrid.json'))
    [void]$candidates.Add((Join-Path $ScriptDir 'internal\defaults\nx2512-pro-hybrid.json'))
    [void]$candidates.Add((Join-Path $ScriptDir 'config\nx2512-ergo-80.json'))

    foreach ($candidate in $candidates) {
        $expanded = [Environment]::ExpandEnvironmentVariables($candidate)
        if (Test-Path -LiteralPath $expanded -PathType Leaf) {
            return (Resolve-Path -LiteralPath $expanded).Path
        }
    }
    throw 'Не найден JSON-профиль NXKeys. Передайте -ConfigPath.'
}

function Get-NxProcesses {
    $result = @()
    foreach ($name in @('ugraf', 'run_nx', 'nx')) {
        foreach ($process in @(Get-Process -Name $name -ErrorAction SilentlyContinue)) {
            $path = ''
            $description = ''
            try { $path = $process.MainModule.FileName } catch { }
            try { $description = $process.MainModule.FileVersionInfo.FileDescription } catch { }
            $evidence = ($path + ' ' + $description).ToLowerInvariant()
            if ($name -in @('ugraf', 'run_nx') -or $evidence.Contains('siemens') -or $evidence.Contains('designcenter') -or $evidence.Contains('\nxbin\')) {
                $result += $process
            }
        }
    }
    return $result
}

function Assert-DotNet8 {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) { throw '.NET 8 SDK не найден. Установите его вручную перед сборкой.' }
    $sdks = @(& $dotnet.Path --list-sdks)
    if (-not ($sdks | Where-Object { $_ -match '^8\.' })) {
        throw '.NET 8 SDK не найден. Автоматическая установка зависимостей отключена по соображениям безопасности.'
    }
    return $dotnet.Path
}

function Copy-DirectoryFiles([string]$Source, [string]$Destination) {
    if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
        throw "Каталог артефактов не найден: $Source"
    }
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Get-ChildItem -LiteralPath $Source -File | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $Destination $_.Name) -Force
    }
}

$config = Resolve-Config $ConfigPath
$configJson = Get-Content -LiteralPath $config -Raw -Encoding UTF8 | ConvertFrom-Json
$managedRootRaw = [string]$configJson.deployment.managed_root
$managedRoot = [Environment]::ExpandEnvironmentVariables($managedRootRaw)
if ([string]::IsNullOrWhiteSpace($managedRoot)) { throw 'deployment.managed_root отсутствует в профиле.' }
$managedRoot = [System.IO.Path]::GetFullPath($managedRoot)

$nxProcesses = @(Get-NxProcesses)
if ($nxProcesses.Count -gt 0 -and -not $AllowRunningNX) {
    $details = ($nxProcesses | ForEach-Object { "$($_.ProcessName)[$($_.Id)]" }) -join ', '
    throw "Siemens NX запущен: $details. Закройте NX перед установкой."
}
if ($nxProcesses.Count -gt 0) {
    Write-Warning 'Установка при запущенном NX разрешена явно. Загруженная Bridge DLL обновится только после перезапуска NX.'
}

$dotnetExe = Assert-DotNet8
$hotkeyProject = Join-Path $ScriptDir 'NX2512_HotkeyStudio'
$bridgeProject = Join-Path $ScriptDir 'NX2512_CommandBridge'
$controlProject = Join-Path $ScriptDir 'NX2512_ControlCenter\NX2512_ControlCenter.csproj'
$hotkeyDist = Join-Path $hotkeyProject 'dist'
$bridgeDist = Join-Path $bridgeProject 'dist'
$controlDist = Join-Path $ScriptDir 'NX2512_ControlCenter\dist'

if (-not $NoBuild) {
    Write-Step 'Сборка NX2512_HotkeyStudio'
    $hotkeyArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $hotkeyProject 'build.ps1'))
    if ($Clean) { $hotkeyArgs += '-Clean' }
    & powershell @hotkeyArgs
    if ($LASTEXITCODE -ne 0) { throw "Сборка HotkeyStudio завершилась с кодом $LASTEXITCODE." }

    Write-Step 'Сборка NX2512_CommandBridge против установленного NXOpen'
    $bridgeArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $bridgeProject 'build.ps1'))
    if ($Clean) { $bridgeArgs += '-Clean' }
    if ($NxRoot) { $bridgeArgs += @('-NxRoot', $NxRoot) }
    if ($NxOpenDll) { $bridgeArgs += @('-NxOpenDll', $NxOpenDll) }
    & powershell @bridgeArgs
    if ($LASTEXITCODE -ne 0) { throw "Сборка CommandBridge завершилась с кодом $LASTEXITCODE." }

    Write-Step 'Публикация NX2512_ControlCenter'
    if (Test-Path -LiteralPath $controlDist) { Remove-Item -LiteralPath $controlDist -Recurse -Force }
    & $dotnetExe publish $controlProject -c Release -r win-x64 --self-contained false -p:Platform=x64 -o $controlDist --nologo
    if ($LASTEXITCODE -ne 0) { throw "Публикация Control Center завершилась с кодом $LASTEXITCODE." }
}

$required = @(
    (Join-Path $hotkeyDist 'NX2512_HotkeyStudio.exe'),
    (Join-Path $bridgeDist 'NX2512_CommandBridge.dll'),
    (Join-Path $controlDist 'NX2512_ControlCenter.exe')
)
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Обязательный артефакт не найден: $path" }
}

$stagingParent = Join-Path $env:LOCALAPPDATA 'NXKeys\staging'
$staging = Join-Path $stagingParent ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $staging | Out-Null

try {
    Write-Step 'Формирование чистого staging-набора'
    Copy-DirectoryFiles $hotkeyDist $staging
    Copy-DirectoryFiles $bridgeDist (Join-Path $staging 'bridge')
    Copy-DirectoryFiles $controlDist (Join-Path $staging 'control-center')
    Copy-Item -LiteralPath $config -Destination (Join-Path $staging 'nx2512-pro-hybrid.json') -Force

    $stagedExe = Join-Path $staging 'NX2512_HotkeyStudio.exe'
    $stagedConfig = Join-Path $staging 'nx2512-pro-hybrid.json'

    Write-Step "Транзакционная установка в $managedRoot"
    $applyArgs = @('apply', '--config', $stagedConfig, '--yes')
    if ($AllowRunningNX) { $applyArgs += '--allow-running-nx' }
    & $stagedExe @applyArgs
    if ($LASTEXITCODE -ne 0) { throw "C# deployment завершился с кодом $LASTEXITCODE." }

    $installedExe = Join-Path $managedRoot 'NX2512_HotkeyStudio.exe'
    $installedConfig = Join-Path $managedRoot 'nx2512-pro-hybrid.json'
    if (-not (Test-Path -LiteralPath $installedExe -PathType Leaf)) { throw "После установки отсутствует $installedExe" }

    Write-Step 'Проверка установленного SHA-256 манифеста'
    & $installedExe health --config $installedConfig
    if ($LASTEXITCODE -ne 0) { throw "Health-check завершился с кодом $LASTEXITCODE." }

    Write-Host "`nNXKeys установлен успешно." -ForegroundColor Green
    Write-Host "Managed root: $managedRoot"
    Write-Host "Запуск NX: $(Join-Path $managedRoot 'launch-nx2512-with-nxkeys.cmd')" -ForegroundColor Yellow
}
finally {
    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
    }
}
