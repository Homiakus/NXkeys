[CmdletBinding()]
param(
    [string]$NxRoot,
    [string]$NxOpenDll,
    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = Join-Path $ProjectDir 'NX2512_HotkeyStudio.csproj'
$DistDir = Join-Path $ProjectDir 'dist'
$BuildDir = Join-Path $ProjectDir 'bin'
$ObjDir = Join-Path $ProjectDir 'obj'

function Write-Step([string]$Text) {
    Write-Host "`n==> $Text" -ForegroundColor Cyan
}

if ($Clean) {
    Write-Step 'Clearing previous build directories'
    Remove-Item -LiteralPath $BuildDir, $ObjDir, $DistDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Step 'Building NX2512_HotkeyStudio .NET 8 x64'

$dotnetExe = (Get-Command dotnet -ErrorAction SilentlyContinue).Path
if (-not $dotnetExe) {
    $localDotnet = Join-Path $env:LocalAppData 'Microsoft\dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $localDotnet) {
        $dotnetExe = $localDotnet
    }
}

if (-not $dotnetExe) {
    throw "dotnet CLI is not installed or not found in PATH."
}

& $dotnetExe build $ProjectFile --configuration Release --nologo

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

$binOutDir = Join-Path $ProjectDir 'bin\Release\net8.0-windows'
if (Test-Path -LiteralPath $binOutDir) {
    Get-ChildItem -LiteralPath $binOutDir -File | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $DistDir -Force
    }
    Write-Host "Copied all runtime artifacts to dist/" -ForegroundColor Green
}

# Copy default config template
$configSource = Join-Path (Split-Path -Parent $ProjectDir) 'nx2512-pro-hybrid.json'
if (Test-Path -LiteralPath $configSource) {
    Copy-Item -LiteralPath $configSource -Destination (Join-Path $DistDir 'nx2512-pro-hybrid.json') -Force
    Write-Host "Copied default profile nx2512-pro-hybrid.json to dist/" -ForegroundColor Yellow
}

Write-Host "`nSUCCESS: NX2512_HotkeyStudio build completed successfully." -ForegroundColor Green
