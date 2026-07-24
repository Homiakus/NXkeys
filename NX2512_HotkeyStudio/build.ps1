[CmdletBinding()]
param([switch]$Clean)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ProjectDir
$ProjectFile = Join-Path $ProjectDir 'NX2512_HotkeyStudio.csproj'
$DistDir = Join-Path $ProjectDir 'dist'
$BuildDir = Join-Path $ProjectDir 'bin'
$ObjDir = Join-Path $ProjectDir 'obj'
$ProfileSource = Join-Path $RepoRoot 'config\nx2512-pro-hybrid.json'
$PolicySource = Join-Path $RepoRoot 'config\nx2512-state-machines.json'

function Assert-DotNet8 {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) { throw '.NET 8 SDK не найден.' }
    $sdks = @(& $dotnet.Path --list-sdks)
    if (-not ($sdks | Where-Object { $_ -match '^8\.' })) { throw 'Для сборки требуется .NET 8 SDK.' }
    return $dotnet.Path
}

$dotnetExe = Assert-DotNet8
if ($Clean) { Remove-Item -LiteralPath $BuildDir, $ObjDir -Recurse -Force -ErrorAction SilentlyContinue }
Remove-Item -LiteralPath $DistDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null

if (-not (Test-Path -LiteralPath $ProfileSource -PathType Leaf)) { throw "Канонический профиль не найден: $ProfileSource" }
if (-not (Test-Path -LiteralPath $PolicySource -PathType Leaf)) { throw "Policy не найдена: $PolicySource" }

Write-Host '==> Проверка адаптивного профиля' -ForegroundColor Cyan
& node (Join-Path $RepoRoot 'scripts\validate-command-tree.mjs')
if ($LASTEXITCODE -ne 0) { throw 'Адаптивный профиль не прошёл проверку.' }

Write-Host '==> Публикация NX2512_HotkeyStudio .NET 8 win-x64' -ForegroundColor Cyan
& $dotnetExe publish $ProjectFile -c Release -r win-x64 --self-contained false -p:Platform=x64 -o $DistDir --nologo
if ($LASTEXITCODE -ne 0) { throw "Сборка завершилась с кодом $LASTEXITCODE." }

Copy-Item -LiteralPath $ProfileSource -Destination (Join-Path $DistDir 'nx2512-pro-hybrid.json') -Force
Copy-Item -LiteralPath $PolicySource -Destination (Join-Path $DistDir 'nx2512-state-machines.json') -Force

$required = @(
    'NX2512_HotkeyStudio.exe',
    'NX2512_HotkeyStudio.dll',
    'NX2512_HotkeyStudio.deps.json',
    'NX2512_HotkeyStudio.runtimeconfig.json',
    'nx2512-pro-hybrid.json',
    'nx2512-state-machines.json'
)
foreach ($name in $required) {
    $path = Join-Path $DistDir $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "После публикации отсутствует $path" }
}

Write-Host "SUCCESS: $DistDir" -ForegroundColor Green
