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

function Write-Step([string]$Text) { Write-Host "`n==> $Text" -ForegroundColor Cyan }

function Resolve-Config([string]$Requested) {
    $candidate = if ([string]::IsNullOrWhiteSpace($Requested)) {
        Join-Path $ScriptDir 'config\nx2512-pro-hybrid.json'
    } else {
        [Environment]::ExpandEnvironmentVariables($Requested)
    }
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Канонический профиль NXKeys не найден: $candidate"
    }
    return (Resolve-Path -LiteralPath $candidate).Path
}

function Get-NxProcesses {
    $result = @()
    foreach ($name in @('ugraf', 'run_nx', 'nx')) {
        foreach ($process in @(Get-Process -Name $name -ErrorAction SilentlyContinue)) {
            $path = ''; $description = ''
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
    if (-not $dotnet) { throw '.NET 8 SDK не найден. Установите его вручную.' }
    if (-not (@(& $dotnet.Path --list-sdks) | Where-Object { $_ -match '^8\.' })) {
        throw '.NET 8 SDK не найден. Автоматическая установка зависимостей отключена.'
    }
    return $dotnet.Path
}

function Copy-DirectoryFiles([string]$Source, [string]$Destination) {
    if (-not (Test-Path -LiteralPath $Source -PathType Container)) { throw "Каталог артефактов не найден: $Source" }
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Get-ChildItem -LiteralPath $Source -File | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $Destination $_.Name) -Force
    }
}

$config = Resolve-Config $ConfigPath
$configJson = Get-Content -LiteralPath $config -Raw -Encoding UTF8 | ConvertFrom-Json
if ([int]$configJson.schema_version -ne 3) { throw 'Для установки требуется schema_version=3.' }
if ($configJson.leader_key.adaptive_module_mode -ne $true) { throw 'Для установки требуется adaptive_module_mode=true.' }

Write-Step 'Проверка 12 базовых сочетаний и адаптивных модулей'
& node (Join-Path $ScriptDir 'scripts\validate-command-tree.mjs')
if ($LASTEXITCODE -ne 0) { throw 'Канонический профиль не прошёл структурную проверку.' }

$managedRoot = [Environment]::ExpandEnvironmentVariables([string]$configJson.deployment.managed_root)
if ([string]::IsNullOrWhiteSpace($managedRoot)) { throw 'deployment.managed_root отсутствует в профиле.' }
$managedRoot = [System.IO.Path]::GetFullPath($managedRoot)

$nxProcesses = @(Get-NxProcesses)
if ($nxProcesses.Count -gt 0 -and -not $AllowRunningNX) {
    $details = ($nxProcesses | ForEach-Object { "$($_.ProcessName)[$($_.Id)]" }) -join ', '
    throw "Siemens NX запущен: $details. Закройте NX перед установкой."
}
if ($nxProcesses.Count -gt 0) { Write-Warning 'Загруженная Bridge DLL обновится только после перезапуска NX.' }

$dotnetExe = Assert-DotNet8
$hotkeyProject = Join-Path $ScriptDir 'NX2512_HotkeyStudio'
$bridgeProject = Join-Path $ScriptDir 'NX2512_CommandBridge'
$controlProject = Join-Path $ScriptDir 'NX2512_ControlCenter\NX2512_ControlCenter.csproj'
$hotkeyDist = Join-Path $hotkeyProject 'dist'
$bridgeDist = Join-Path $bridgeProject 'dist'
$controlDist = Join-Path $ScriptDir 'NX2512_ControlCenter\dist'

if (-not $NoBuild) {
    Write-Step 'Сборка адаптивного HotkeyStudio'
    $args = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $hotkeyProject 'build.ps1'))
    if ($Clean) { $args += '-Clean' }
    & powershell @args
    if ($LASTEXITCODE -ne 0) { throw "Сборка HotkeyStudio завершилась с кодом $LASTEXITCODE." }

    Write-Step 'Сборка CommandBridge против установленного NXOpen'
    $args = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $bridgeProject 'build.ps1'))
    if ($Clean) { $args += '-Clean' }
    if ($NxRoot) { $args += @('-NxRoot', $NxRoot) }
    if ($NxOpenDll) { $args += @('-NxOpenDll', $NxOpenDll) }
    & powershell @args
    if ($LASTEXITCODE -ne 0) { throw "Сборка CommandBridge завершилась с кодом $LASTEXITCODE." }

    Write-Step 'Публикация Adaptive Control Center'
    if (Test-Path -LiteralPath $controlDist) { Remove-Item -LiteralPath $controlDist -Recurse -Force }
    & $dotnetExe publish $controlProject -c Release -r win-x64 --self-contained false -p:Platform=x64 -o $controlDist --nologo
    if ($LASTEXITCODE -ne 0) { throw "Публикация Control Center завершилась с кодом $LASTEXITCODE." }
}

foreach ($path in @(
    (Join-Path $hotkeyDist 'NX2512_HotkeyStudio.exe'),
    (Join-Path $hotkeyDist 'nx2512-pro-hybrid.json'),
    (Join-Path $hotkeyDist 'nx2512-state-machines.json'),
    (Join-Path $bridgeDist 'NX2512_CommandBridge.dll'),
    (Join-Path $controlDist 'NX2512_ControlCenter.exe')
)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Обязательный артефакт не найден: $path" }
}

$staging = Join-Path (Join-Path $env:LOCALAPPDATA 'NXKeys\staging') ([Guid]::NewGuid().ToString('N'))
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
    Write-Step 'Проверка установленного package manifest'
    & $installedExe health --config $installedConfig
    if ($LASTEXITCODE -ne 0) { throw "Health-check завершился с кодом $LASTEXITCODE." }

    Write-Host "`nNXKeys Adaptive Modules установлен успешно." -ForegroundColor Green
    Write-Host "Managed root: $managedRoot"
    Write-Host "Запуск NX: $(Join-Path $managedRoot 'launch-nx2512-with-nxkeys.cmd')" -ForegroundColor Yellow
}
finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue }
}
