[CmdletBinding()]
param(
    [string]$NxRoot,
    [string]$NxOpenDll,
    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = Join-Path $ProjectDir 'NX2512_CommandBridge.csproj'
$DistDir = Join-Path $ProjectDir 'dist'
$BuildDir = Join-Path $ProjectDir 'bin'
$ObjDir = Join-Path $ProjectDir 'obj'

function Write-Step([string]$Text) {
    Write-Host "`n==> $Text" -ForegroundColor Cyan
}

function Add-ExistingPath {
    param(
        [System.Collections.Generic.List[string]]$List,
        [string]$Path
    )
    if ([string]::IsNullOrWhiteSpace($Path)) { return }
    $expanded = [Environment]::ExpandEnvironmentVariables($Path.Trim('"'))
    if (Test-Path -LiteralPath $expanded) {
        $full = (Resolve-Path -LiteralPath $expanded).Path
        if (-not $List.Contains($full)) {
            [void]$List.Add($full)
        }
    }
}

function Get-NxSearchRoots {
    $roots = [System.Collections.Generic.List[string]]::new()

    Add-ExistingPath $roots $NxRoot
    Add-ExistingPath $roots $env:UGII_BASE_DIR
    Add-ExistingPath $roots $env:UGII_ROOT_DIR
    Add-ExistingPath $roots $env:UGOPEN

    foreach ($pf in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if (-not $pf) { continue }
        Add-ExistingPath $roots (Join-Path $pf 'Siemens')
        Add-ExistingPath $roots (Join-Path $pf 'Siemens\DesigncenterNX2512')
        Add-ExistingPath $roots (Join-Path $pf 'Siemens\NX2512')
    }

    return $roots
}

function Find-NxOpenDll {
    if ($NxOpenDll) {
        if (-not (Test-Path -LiteralPath $NxOpenDll -PathType Leaf)) {
            throw "Specified NXOpen.dll was not found: $NxOpenDll"
        }
        return (Resolve-Path -LiteralPath $NxOpenDll).Path
    }

    $relativeCandidates = @(
        'NXBIN\managed\NXOpen.dll',
        'UGII\managed\NXOpen.dll',
        'managed\NXOpen.dll',
        'NXOpen.dll'
    )

    foreach ($root in Get-NxSearchRoots) {
        foreach ($relative in $relativeCandidates) {
            $candidate = Join-Path $root $relative
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                return (Resolve-Path -LiteralPath $candidate).Path
            }
        }
    }

    throw "NXOpen.dll was not found. Pass -NxOpenDll `"C:\...\NXOpen.dll`"."
}

function Find-SigningResource([string]$NxOpenPath) {
    $start = Split-Path -Parent $NxOpenPath
    $cursor = Get-Item -LiteralPath $start
    for ($i = 0; $i -lt 5 -and $cursor; $i++) {
        $candidate = Join-Path $cursor.FullName 'NXSigningResource.res'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
        $candidate = Join-Path $cursor.FullName 'UGOPEN\NXSigningResource.res'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
        $cursor = $cursor.Parent
    }

    return $null
}

if ($Clean) {
    Write-Step 'Clearing previous build directories'
    Remove-Item -LiteralPath $BuildDir, $ObjDir, $DistDir -Recurse -Force -ErrorAction SilentlyContinue
}

$dotnetExe = (Get-Command dotnet -ErrorAction SilentlyContinue).Path
if (-not $dotnetExe) {
    $localDotnet = Join-Path $env:LocalAppData 'Microsoft\dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $localDotnet) {
        $dotnetExe = $localDotnet
    }
}
if (-not $dotnetExe) {
    throw 'dotnet CLI is not installed or not found in PATH.'
}

Write-Step 'Finding NXOpen.dll'
$resolvedNxOpen = Find-NxOpenDll
$nxOpenDir = Split-Path -Parent $resolvedNxOpen
Write-Host "NXOpen.dll: $resolvedNxOpen" -ForegroundColor Green

$properties = @("-p:NXOpenDir=$nxOpenDir")
$signingResource = Find-SigningResource $resolvedNxOpen
if ($signingResource) {
    $properties += "-p:NXSigningResource=$signingResource"
    Write-Host "NXSigningResource: $signingResource" -ForegroundColor Green
}

Write-Step 'Building NX2512_CommandBridge .NET 8 x64'
& $dotnetExe build $ProjectFile --configuration Release --nologo @properties
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
$binOutDir = Join-Path $ProjectDir 'bin\Release\net8.0-windows'
Get-ChildItem -LiteralPath $binOutDir -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $DistDir -Force
}

$distDll = Join-Path $DistDir 'NX2512_CommandBridge.dll'
if (-not (Test-Path -LiteralPath $distDll -PathType Leaf)) {
    throw "Build completed, but DLL was not found: $distDll"
}

$hash = (Get-FileHash -LiteralPath $distDll -Algorithm SHA256).Hash
Write-Host ""
Write-Host "SUCCESS: NX2512_CommandBridge build completed." -ForegroundColor Green
Write-Host "DLL: $distDll"
Write-Host "SHA256: $hash"
