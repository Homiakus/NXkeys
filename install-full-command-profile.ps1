[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CatalogDir,

    [string]$NxRoot,
    [string]$OutputPath,
    [switch]$CompileOnly,
    [switch]$Clean,
    [switch]$NoBuild,
    [switch]$AllowRunningNX,
    [switch]$NoShortcut,
    [switch]$NoGlobalDuplication
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root

function Resolve-RequiredPath([string]$Value, [string]$Description, [switch]$Directory) {
    if ([string]::IsNullOrWhiteSpace($Value)) { throw "$Description не задан." }
    $expanded = [Environment]::ExpandEnvironmentVariables($Value)
    $type = if ($Directory) { 'Container' } else { 'Leaf' }
    if (-not (Test-Path -LiteralPath $expanded -PathType $type)) {
        throw "$Description не найден: $expanded"
    }
    return (Resolve-Path -LiteralPath $expanded).Path
}

$node = Get-Command node -ErrorAction SilentlyContinue
if (-not $node) { throw 'Node.js 20+ не найден. Он требуется для компилятора полного каталога.' }
$major = [int]((& $node.Source --version).TrimStart('v').Split('.')[0])
if ($major -lt 20) { throw "Требуется Node.js 20+, обнаружена версия $(& $node.Source --version)." }

$catalog = Resolve-RequiredPath -Value $CatalogDir -Description 'Каталог NX2512_Catalog_Studio' -Directory
$buttons = Join-Path $catalog '06_ui_commands_buttons.csv'
if (-not (Test-Path -LiteralPath $buttons -PathType Leaf)) {
    throw "В каталоге отсутствует 06_ui_commands_buttons.csv: $catalog"
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $Root 'config\nx2512-pro-full.generated.json'
} else {
    $OutputPath = [Environment]::ExpandEnvironmentVariables($OutputPath)
    if (-not [System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath = Join-Path $Root $OutputPath }
}
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$reportPath = Join-Path $Root 'docs\generated\full-command-resolution.md'

Write-Host "`n==> Проверка карты 1169 команд" -ForegroundColor Cyan
& $node.Source (Join-Path $Root 'scripts\validate-full-command-map.mjs')
if ($LASTEXITCODE -ne 0) { throw 'Полная карта команд не прошла структурную проверку.' }

Write-Host "`n==> Разрешение названий в точные BUTTON ID установленной NX" -ForegroundColor Cyan
$compileArgs = @(
    (Join-Path $Root 'scripts\compile-full-command-map.mjs'),
    '--profile', (Join-Path $Root 'config\nx2512-pro-hybrid.json'),
    '--intents', (Join-Path $Root 'config\full-command-map'),
    '--catalog-dir', $catalog,
    '--probe', (Join-Path $Root 'docs\audit\runtime-command-probe-2026-07-28.json'),
    '--out', $OutputPath,
    '--report', $reportPath
)
if ($NoGlobalDuplication) { $compileArgs += '--no-global-duplication' }
& $node.Source @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Компиляция полного профиля завершилась ошибкой.' }

$generated = Get-Content -LiteralPath $OutputPath -Raw -Encoding UTF8 | ConvertFrom-Json
$allRows = @($generated.modules | ForEach-Object { $_.command_sets | ForEach-Object { $_.commands } })
$enabledRows = @($allRows | Where-Object { $_.enabled -ne $false })
$unresolvedRows = @($allRows | Where-Object { $_.fallback -like 'catalog:*' -and $_.enabled -eq $false })

Write-Host "  Профиль: $OutputPath" -ForegroundColor Green
Write-Host "  Исполняемых команд: $($enabledRows.Count) / $($allRows.Count)" -ForegroundColor Green
Write-Host "  Не разрешено для этой установки/лицензии: $($unresolvedRows.Count)" -ForegroundColor Yellow
Write-Host "  Отчёт: $reportPath" -ForegroundColor DarkGray

if ($CompileOnly) {
    Write-Host "`n[OK] Полный профиль скомпилирован без установки." -ForegroundColor Green
    exit 0
}

Write-Host "`n==> Установка скомпилированного профиля" -ForegroundColor Cyan
$installArgs = @('-ConfigPath', $OutputPath)
if (-not [string]::IsNullOrWhiteSpace($NxRoot)) { $installArgs += @('-NxRoot', $NxRoot) }
if ($Clean) { $installArgs += '-Clean' }
if ($NoBuild) { $installArgs += '-NoBuild' }
if ($AllowRunningNX) { $installArgs += '-AllowRunningNX' }
if ($NoShortcut) { $installArgs += '-NoShortcut' }

& (Join-Path $Root 'install-nx-ribbon-buttons.ps1') @installArgs
if ($LASTEXITCODE -ne 0) { throw "Установка завершилась с кодом $LASTEXITCODE." }

Write-Host "`n[OK] Полный профиль NXKeys установлен." -ForegroundColor Green
