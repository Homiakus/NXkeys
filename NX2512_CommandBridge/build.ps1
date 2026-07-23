[CmdletBinding()]
param(
    [string]$NxRoot,
    [string]$NxOpenDll,
    [string]$ExpectedNxVersion = '2512',
    [switch]$AllowVersionMismatch,
    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = Join-Path $ProjectDir 'NX2512_CommandBridge.csproj'
$DistDir = Join-Path $ProjectDir 'dist'
$BuildDir = Join-Path $ProjectDir 'bin'
$ObjDir = Join-Path $ProjectDir 'obj'

function Assert-DotNet8 {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) { throw '.NET 8 SDK не найден.' }
    $sdks = @(& $dotnet.Path --list-sdks)
    if (-not ($sdks | Where-Object { $_ -match '^8\.' })) { throw 'Для сборки требуется .NET 8 SDK.' }
    return $dotnet.Path
}

function Add-Candidate([System.Collections.Generic.List[string]]$List, [string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return }
    $expanded = [Environment]::ExpandEnvironmentVariables($Path.Trim('"'))
    if (Test-Path -LiteralPath $expanded -PathType Leaf) {
        $full = (Resolve-Path -LiteralPath $expanded).Path
        if (-not $List.Contains($full)) { [void]$List.Add($full) }
    }
}

function Add-RootCandidates([System.Collections.Generic.List[string]]$List, [string]$Root) {
    if ([string]::IsNullOrWhiteSpace($Root)) { return }
    $expanded = [Environment]::ExpandEnvironmentVariables($Root.Trim('"'))
    foreach ($relative in @('NXBIN\managed\NXOpen.dll', 'UGII\managed\NXOpen.dll', 'managed\NXOpen.dll', 'NXOpen.dll')) {
        Add-Candidate $List (Join-Path $expanded $relative)
    }
}

function Resolve-NxOpen {
    $candidates = [System.Collections.Generic.List[string]]::new()
    Add-Candidate $candidates $NxOpenDll
    Add-RootCandidates $candidates $NxRoot
    Add-RootCandidates $candidates $env:UGII_BASE_DIR
    Add-RootCandidates $candidates $env:UGII_ROOT_DIR
    Add-RootCandidates $candidates $env:UGOPEN

    foreach ($pf in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if (-not $pf) { continue }
        Add-RootCandidates $candidates (Join-Path $pf 'Siemens\DesigncenterNX2512')
        Add-RootCandidates $candidates (Join-Path $pf 'Siemens\NX2512')
        Add-RootCandidates $candidates (Join-Path $pf 'Siemens\NX 2512')
    }

    if ($candidates.Count -eq 0) {
        throw 'NXOpen.dll не найден. Передайте -NxOpenDll или -NxRoot.'
    }

    $preferred = $candidates | Where-Object { $_ -match [Regex]::Escape($ExpectedNxVersion) } | Select-Object -First 1
    if ($preferred) { return $preferred }
    if (-not $AllowVersionMismatch) {
        throw "Найден NXOpen.dll, но путь не подтверждает целевую версию NX $ExpectedNxVersion. Используйте точный -NxOpenDll или -AllowVersionMismatch после ручной проверки. Кандидаты: $($candidates -join '; ')"
    }
    return $candidates[0]
}

$dotnetExe = Assert-DotNet8
$resolvedNxOpen = Resolve-NxOpen
$nxOpenDir = Split-Path -Parent $resolvedNxOpen
$nxOpenUi = Join-Path $nxOpenDir 'NXOpenUI.dll'
if (-not (Test-Path -LiteralPath $nxOpenUi -PathType Leaf)) { throw "NXOpenUI.dll не найден рядом с NXOpen.dll: $nxOpenDir" }

$assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($resolvedNxOpen)
Write-Host "NXOpen.dll: $resolvedNxOpen" -ForegroundColor Green
Write-Host "NXOpen assembly version: $($assemblyName.Version)" -ForegroundColor Green

$signingResource = $null
$cursor = Get-Item -LiteralPath $nxOpenDir
for ($i = 0; $i -lt 6 -and $cursor; $i++) {
    foreach ($candidate in @(
        (Join-Path $cursor.FullName 'NXSigningResource.res'),
        (Join-Path $cursor.FullName 'UGOPEN\NXSigningResource.res')
    )) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { $signingResource = (Resolve-Path -LiteralPath $candidate).Path; break }
    }
    if ($signingResource) { break }
    $cursor = $cursor.Parent
}

if ($Clean) { Remove-Item -LiteralPath $BuildDir, $ObjDir -Recurse -Force -ErrorAction SilentlyContinue }
Remove-Item -LiteralPath $DistDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null

$properties = @("-p:NXOpenDir=$nxOpenDir", '-p:Platform=x64')
if ($signingResource) { $properties += "-p:NXSigningResource=$signingResource" }

Write-Host '==> Сборка NX2512_CommandBridge .NET 8 x64' -ForegroundColor Cyan
& $dotnetExe build $ProjectFile -c Release --nologo @properties
if ($LASTEXITCODE -ne 0) { throw "Сборка завершилась с кодом $LASTEXITCODE." }

$output = Join-Path $ProjectDir 'bin\Release\net8.0-windows'
Get-ChildItem -LiteralPath $output -File | Where-Object {
    $_.Name -like 'NX2512_CommandBridge*'
} | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $DistDir $_.Name) -Force
}

$dll = Join-Path $DistDir 'NX2512_CommandBridge.dll'
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw "После сборки отсутствует $dll" }
$hash = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "SUCCESS: $dll" -ForegroundColor Green
Write-Host "SHA256: $hash"
