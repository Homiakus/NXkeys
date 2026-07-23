[CmdletBinding()]
param(
    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = Join-Path $ProjectDir 'NX2512_HotkeyStudio.csproj'
$DistDir = Join-Path $ProjectDir 'dist'
$BuildDir = Join-Path $ProjectDir 'bin'
$ObjDir = Join-Path $ProjectDir 'obj'
$RepoRoot = Split-Path -Parent $ProjectDir

function Assert-DotNet8 {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) { throw '.NET 8 SDK не найден.' }
    $sdks = @(& $dotnet.Path --list-sdks)
    if (-not ($sdks | Where-Object { $_ -match '^8\.' })) { throw 'Для сборки требуется .NET 8 SDK.' }
    return $dotnet.Path
}

$dotnetExe = Assert-DotNet8
if ($Clean) {
    Remove-Item -LiteralPath $BuildDir, $ObjDir -Recurse -Force -ErrorAction SilentlyContinue
}
Remove-Item -LiteralPath $DistDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null

Write-Host '==> Публикация NX2512_HotkeyStudio .NET 8 win-x64' -ForegroundColor Cyan
& $dotnetExe publish $ProjectFile -c Release -r win-x64 --self-contained false -p:Platform=x64 -o $DistDir --nologo
if ($LASTEXITCODE -ne 0) { throw "Сборка завершилась с кодом $LASTEXITCODE." }

$configCandidates = @(
    (Join-Path $RepoRoot 'config\nx2512-pro-hybrid.json'),
    (Join-Path $RepoRoot 'internal\defaults\nx2512-pro-hybrid.json'),
    (Join-Path $RepoRoot 'config\nx2512-ergo-80.json')
)
$configSource = $configCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $configSource) { throw 'Канонический профиль NXKeys не найден.' }
Copy-Item -LiteralPath $configSource -Destination (Join-Path $DistDir 'nx2512-pro-hybrid.json') -Force

$required = @('NX2512_HotkeyStudio.exe', 'NX2512_HotkeyStudio.dll', 'NX2512_HotkeyStudio.deps.json', 'NX2512_HotkeyStudio.runtimeconfig.json')
foreach ($name in $required) {
    $path = Join-Path $DistDir $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "После публикации отсутствует $path" }
}

Write-Host "SUCCESS: $DistDir" -ForegroundColor Green
