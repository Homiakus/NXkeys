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
$PolicySource = Join-Path $RepoRoot 'config\nx2512-state-machines.json'
$BridgeDistDir = Join-Path $RepoRoot 'NX2512_CommandBridge\dist'
$OperationIconsSource = Join-Path $RepoRoot 'assets\nx-operation-icons'

function Assert-DotNet8 {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) { throw '.NET 8 SDK не найден.' }
    $sdks = @(& $dotnet.Path --list-sdks)
    if (-not ($sdks | Where-Object { $_ -match '^8\.' })) { throw 'Для сборки требуется .NET 8 SDK.' }
    return $dotnet.Path
}

function Assert-Node20 {
    $node = Get-Command node -ErrorAction SilentlyContinue
    if (-not $node) { throw 'Node.js 20+ не найден. Он требуется для компиляции главного профиля K3–K5.' }
    $major = [int]((& $node.Source --version).TrimStart('v').Split('.')[0])
    if ($major -lt 20) { throw "Требуется Node.js 20+, обнаружена версия $(& $node.Source --version)." }
    return $node.Source
}

function Resolve-Profile([string]$Requested) {
    if (-not [string]::IsNullOrWhiteSpace($Requested)) {
        $expanded = [Environment]::ExpandEnvironmentVariables($Requested)
        if (-not [System.IO.Path]::IsPathRooted($expanded)) { $expanded = Join-Path $RepoRoot $expanded }
        if (-not (Test-Path -LiteralPath $expanded -PathType Leaf)) { throw "Профиль не найден: $expanded" }
        return (Resolve-Path -LiteralPath $expanded).Path
    }

    $node = Assert-Node20
    $outputName = 'nx2512-pro-main.generated.json'
    $reportName = 'main-profile-resolution.md'
    $output = Join-Path $RepoRoot (Join-Path 'config' $outputName)
    $report = Join-Path $RepoRoot (Join-Path 'docs\generated' $reportName)
    $compileArgs = @(
        (Join-Path $RepoRoot 'scripts\compile-main-command-map.mjs'),
        '--profile', (Join-Path $RepoRoot 'config\nx2512-pro-hybrid.json'),
        '--intents', (Join-Path $RepoRoot 'config\full-command-map'),
        '--probe', (Join-Path $RepoRoot 'docs\audit\runtime-command-probe-2026-07-28.json'),
        '--out', $output,
        '--report', $report
    )
    $catalog = if (-not [string]::IsNullOrWhiteSpace($CatalogDir)) { $CatalogDir } else { $env:NXKEYS_CATALOG_DIR }
    if (-not [string]::IsNullOrWhiteSpace($catalog)) { $compileArgs += @('--catalog-dir', $catalog) }

    Write-Host '==> Компиляция единого профиля K3–K5 (885 намерений)' -ForegroundColor Cyan
    $compileOutput = & $node @compileArgs 2>&1
    $compileExit = $LASTEXITCODE
    $compileOutput | ForEach-Object { Write-Host $_ }
    if ($compileExit -ne 0) { throw 'Компиляция профиля завершилась ошибкой.' }
    return (Resolve-Path -LiteralPath $output).Path
}

$dotnetExe = Assert-DotNet8
$ProfileSource = Resolve-Profile $ProfilePath
if ($Clean) { Remove-Item -LiteralPath $BuildDir, $ObjDir -Recurse -Force -ErrorAction SilentlyContinue }
Remove-Item -LiteralPath $DistDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null

if (-not (Test-Path -LiteralPath $PolicySource -PathType Leaf)) { throw "Policy не найдена: $PolicySource" }

Write-Host '==> Проверка bootstrap, главного профиля и документации' -ForegroundColor Cyan
& node (Join-Path $RepoRoot 'scripts\validate-command-tree.mjs')
if ($LASTEXITCODE -ne 0) { throw 'Bootstrap-профиль не прошёл проверку.' }
& node (Join-Path $RepoRoot 'scripts\validate-main-command-map.mjs')
if ($LASTEXITCODE -ne 0) { throw 'Главный профиль K3–K5 не прошёл проверку.' }

Write-Host '==> Публикация NX2512_HotkeyStudio .NET 8 win-x64' -ForegroundColor Cyan
& $dotnetExe publish $ProjectFile -c Release -r win-x64 --self-contained false -p:Platform=x64 -o $DistDir --nologo
if ($LASTEXITCODE -ne 0) { throw "Сборка завершилась с кодом $LASTEXITCODE." }

# Runtime filename is preserved for backwards compatibility. Its content is the selected main/generated profile.
Copy-Item -LiteralPath $ProfileSource -Destination (Join-Path $DistDir 'nx2512-pro-hybrid.json') -Force
Copy-Item -LiteralPath $PolicySource -Destination (Join-Path $DistDir 'nx2512-state-machines.json') -Force
if (Test-Path -LiteralPath $OperationIconsSource -PathType Container) {
    $assetsTarget = Join-Path $DistDir 'assets'
    Remove-Item -LiteralPath $assetsTarget -Recurse -Force -ErrorAction SilentlyContinue
    $operationIconsTarget = Join-Path $assetsTarget 'nx-operation-icons'
    New-Item -ItemType Directory -Force -Path $operationIconsTarget | Out-Null
    Get-ChildItem -LiteralPath $OperationIconsSource -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $operationIconsTarget -Recurse -Force
    }
}
if (Test-Path -LiteralPath (Join-Path $BridgeDistDir 'NX2512_CommandBridge.dll') -PathType Leaf) {
    $bridgeTarget = Join-Path $DistDir 'custom\application'
    New-Item -ItemType Directory -Force -Path $bridgeTarget | Out-Null
    Get-ChildItem -LiteralPath $BridgeDistDir -File | Where-Object {
        $_.Name -like 'NX2512_CommandBridge*'
    } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $bridgeTarget $_.Name) -Force
    }
}

$required = @(
    'NX2512_HotkeyStudio.exe',
    'NX2512_HotkeyStudio.dll',
    'NX2512_HotkeyStudio.deps.json',
    'NX2512_HotkeyStudio.runtimeconfig.json',
    'nx2512-pro-hybrid.json',
    'nx2512-state-machines.json',
    'custom\application\NX2512_CommandBridge.dll'
)
foreach ($name in $required) {
    $path = Join-Path $DistDir $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "После публикации отсутствует $path" }
}

Write-Host "SUCCESS: $DistDir" -ForegroundColor Green
Write-Host "Runtime profile source: $ProfileSource" -ForegroundColor Green
