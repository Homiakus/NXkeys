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
$BridgeDistDir = Join-Path $RepoRoot 'NX2512_CommandBridge\dist'
$OperationIconsSource = Join-Path $RepoRoot 'assets\nx-operation-icons'

function Assert-DotNet8 {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) { throw '.NET 8 SDK not found.' }
    $sdks = @(& $dotnet.Path --list-sdks)
    if (-not ($sdks | Where-Object { $_ -match '^8\.' })) { throw '.NET 8 SDK is required for build.' }
    return $dotnet.Path
}

function Resolve-Profile([string]$Requested) {
    if (-not [string]::IsNullOrWhiteSpace($Requested)) {
        $expanded = [Environment]::ExpandEnvironmentVariables($Requested)
        if (-not [System.IO.Path]::IsPathRooted($expanded)) { $expanded = Join-Path $RepoRoot $expanded }
        if (-not (Test-Path -LiteralPath $expanded -PathType Leaf)) { throw "Profile not found: $expanded" }
        return (Resolve-Path -LiteralPath $expanded).Path
    }

    $candidate = Join-Path $RepoRoot 'config\nx2512-v8-profile.json'
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "Profile v8 not found: $candidate" }
    return (Resolve-Path -LiteralPath $candidate).Path
}

$dotnetExe = Assert-DotNet8
$ProfileSource = Resolve-Profile $ProfilePath
if ($Clean) { Remove-Item -LiteralPath $BuildDir, $ObjDir -Recurse -Force -ErrorAction SilentlyContinue }
Remove-Item -LiteralPath $DistDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null

Write-Host '==> Publishing NX2512_HotkeyStudio .NET 8 win-x64' -ForegroundColor Cyan
& $dotnetExe publish $ProjectFile -c Release -r win-x64 --self-contained false -p:Platform=x64 -o $DistDir --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

Write-Host '==> Generating Runtime-Driven documentation FULL_COMMAND_MAP.md' -ForegroundColor Cyan
$docMapPath = Join-Path $RepoRoot 'FULL_COMMAND_MAP.md'
$exePath = Join-Path $DistDir 'NX2512_HotkeyStudio.exe'
if (Test-Path -LiteralPath $exePath -PathType Leaf) {
    & $exePath doc-map --out $docMapPath
}

# Copy canonical profile to dist/config for runtime reference
$distConfigDir = Join-Path $DistDir 'config'
New-Item -ItemType Directory -Force -Path $distConfigDir | Out-Null
Copy-Item -LiteralPath $ProfileSource -Destination (Join-Path $distConfigDir 'nx2512-v8-profile.json') -Force

if (Test-Path -LiteralPath $OperationIconsSource -PathType Container) {
    $assetsTarget = Join-Path $DistDir 'assets'
    Remove-Item -LiteralPath $assetsTarget -Recurse -Force -ErrorAction SilentlyContinue
    $operationIconsTarget = Join-Path $assetsTarget 'nx-operation-icons'
    New-Item -ItemType Directory -Force -Path $operationIconsTarget | Out-Null
    Get-ChildItem -LiteralPath $OperationIconsSource -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $operationIconsTarget -Recurse -Force
    }
}

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
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing published artifact: $path" }
}

if (-not $bridgePackaged) {
    Write-Warning 'NX2512_CommandBridge.dll is not yet built. Root installer will build and stage Bridge.'
}

Write-Host "SUCCESS: $DistDir" -ForegroundColor Green
