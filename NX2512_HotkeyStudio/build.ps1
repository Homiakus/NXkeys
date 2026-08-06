[CmdletBinding()]
param(
    [switch]$Clean,
    [string]$ProfilePath,
    [string]$CatalogDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ProjectDir
$ProjectFile = Join-Path $ProjectDir 'NX2512_HotkeyStudio.csproj'
$DistDir = Join-Path $ProjectDir 'dist'
$BuildDir = Join-Path $ProjectDir 'bin'
$ObjDir = Join-Path $ProjectDir 'obj'
# $PolicySource removed: state machines not used in schema_version 8
$BridgeDistDir = Join-Path $RepoRoot 'NX2512_CommandBridge\dist'
$OperationIconsSource = Join-Path $RepoRoot 'assets\nx-operation-icons'

function Assert-DotNet8 {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) { throw '.NET 8 SDK не найден.' }
    $sdks = @(& $dotnet.Path --list-sdks)
    if (-not ($sdks | Where-Object { $_ -match '^8\.' })) { throw 'Для сборки требуется .NET 8 SDK.' }
    return $dotnet.Path
}

# Assert-Node20 removed: v8 profile does not require Node.js compilation

function Resolve-Profile([string]$Requested) {
    if (-not [string]::IsNullOrWhiteSpace($Requested)) {
        $expanded = [Environment]::ExpandEnvironmentVariables($Requested)
        if (-not [System.IO.Path]::IsPathRooted($expanded)) { $expanded = Join-Path $RepoRoot $expanded }
        if (-not (Test-Path -LiteralPath $expanded -PathType Leaf)) { throw "Профиль не найден: $expanded" }
        return (Resolve-Path -LiteralPath $expanded).Path
    }

    $candidate = Join-Path $RepoRoot 'config\nx2512-v8-profile.json'
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "Профиль v8 не найден: $candidate" }
    return (Resolve-Path -LiteralPath $candidate).Path
}

$dotnetExe = Assert-DotNet8
$ProfileSource = Resolve-Profile $ProfilePath
if ($Clean) { Remove-Item -LiteralPath $BuildDir, $ObjDir -Recurse -Force -ErrorAction SilentlyContinue }
Remove-Item -LiteralPath $DistDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null

# Для v8-профиля state machines и Node.js валидации не требуются.
# Если нужно вернуть валидации для schema < 8, оберните их в:
#   $json = Get-Content $ProfileSource -Raw | ConvertFrom-Json
#   if ([int]$json.schema_version -lt 8) { ... }

Write-Host '==> Публикация NX2512_HotkeyStudio .NET 8 win-x64' -ForegroundColor Cyan
& $dotnetExe publish $ProjectFile -c Release -r win-x64 --self-contained false -p:Platform=x64 -o $DistDir --nologo
if ($LASTEXITCODE -ne 0) { throw "Сборка завершилась с кодом $LASTEXITCODE." }

# Runtime profile is now hardcoded in ConfigRuntimeV5.BuildHardcodedModules().
# JSON is no longer needed at runtime.

if (Test-Path -LiteralPath $OperationIconsSource -PathType Container) {
    $assetsTarget = Join-Path $DistDir 'assets'
    Remove-Item -LiteralPath $assetsTarget -Recurse -Force -ErrorAction SilentlyContinue
    $operationIconsTarget = Join-Path $assetsTarget 'nx-operation-icons'
    New-Item -ItemType Directory -Force -Path $operationIconsTarget | Out-Null
    Get-ChildItem -LiteralPath $OperationIconsSource -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $operationIconsTarget -Recurse -Force
    }
}

# CommandBridge is a separate NXOpen project. The root installer builds it after HotkeyStudio
# and stages it from NX2512_CommandBridge\dist. If it has already been built, include it in
# this dist as a convenience for standalone packaging, but do not fail HotkeyStudio publication.
$bridgeDll = Join-Path $BridgeDistDir 'NX2512_CommandBridge.dll'
$bridgePackaged = $false
if (Test-Path -LiteralPath $bridgeDll -PathType Leaf) {
    $bridgeTarget = Join-Path $DistDir 'custom\application'
    New-Item -ItemType Directory -Force -Path $bridgeTarget | Out-Null
    Get-ChildItem -LiteralPath $BridgeDistDir -File | Where-Object {
        $_.Name -like 'NX2512_CommandBridge*'
    } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $bridgeTarget $_.Name) -Force
    }
    $bridgePackaged = $true
}

$required = @(
    'NX2512_HotkeyStudio.exe',
    'NX2512_HotkeyStudio.dll',
    'NX2512_HotkeyStudio.deps.json',
    'NX2512_HotkeyStudio.runtimeconfig.json'
)
foreach ($name in $required) {
    $path = Join-Path $DistDir $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "После публикации отсутствует $path" }
}

if (-not $bridgePackaged) {
    Write-Warning 'NX2512_CommandBridge.dll ещё не собран. Это допустимо: install-nxkeys.ps1 соберёт Bridge следующим этапом и добавит его в staging-пакет.'
}

Write-Host "SUCCESS: $DistDir" -ForegroundColor Green
Write-Host "Runtime profile: hardcoded in BuildHardcodedModules()" -ForegroundColor Green
