[CmdletBinding()]
param(
    [string]$CatalogDir,
    [string]$NxRoot,
    [string]$OutputPath,
    [switch]$CompileOnly,
    [switch]$Clean,
    [switch]$NoBuild,
    [switch]$AllowRunningNX,
    [switch]$NoShortcut,
    [switch]$NoGlobalDuplication,
    [switch]$AllFrequencies
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root

function Resolve-OptionalDirectory([string]$Value, [string]$Description) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return '' }
    $expanded = [Environment]::ExpandEnvironmentVariables($Value)
    if (-not (Test-Path -LiteralPath $expanded -PathType Container)) {
        throw "$Description не найден: $expanded"
    }
    return (Resolve-Path -LiteralPath $expanded).Path
}

$node = Get-Command node -ErrorAction SilentlyContinue
if (-not $node) { throw 'Node.js 20+ не найден. Он требуется для компиляции главного профиля K3–K5.' }
$major = [int]((& $node.Source --version).TrimStart('v').Split('.')[0])
if ($major -lt 20) { throw "Требуется Node.js 20+, обнаружена версия $(& $node.Source --version)." }

$catalog = Resolve-OptionalDirectory -Value $CatalogDir -Description 'Каталог NX2512_Catalog_Studio'
if (-not [string]::IsNullOrWhiteSpace($catalog)) {
    $buttons = Join-Path $catalog '06_ui_commands_buttons.csv'
    if (-not (Test-Path -LiteralPath $buttons -PathType Leaf)) {
        throw "В каталоге отсутствует 06_ui_commands_buttons.csv: $catalog"
    }
} else {
    Write-Warning 'CatalogDir не задан. Профиль включит только команды с уже известными точными BUTTON ID; остальные K3–K5 останутся безопасно отключёнными.'
}

$defaultName = if ($AllFrequencies) { 'nx2512-pro-all.generated.json' } else { 'nx2512-pro-main.generated.json' }
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $Root (Join-Path 'config' $defaultName)
} else {
    $OutputPath = [Environment]::ExpandEnvironmentVariables($OutputPath)
    if (-not [System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath = Join-Path $Root $OutputPath }
}
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$reportName = if ($AllFrequencies) { 'all-frequency-resolution.md' } else { 'main-profile-resolution.md' }
$reportPath = Join-Path $Root (Join-Path 'docs\generated' $reportName)

Write-Host "`n==> Проверка каталога 1169 команд и главного scope K3–K5" -ForegroundColor Cyan
& $node.Source (Join-Path $Root 'scripts\validate-main-command-map.mjs')
if ($LASTEXITCODE -ne 0) { throw 'Карта команд не прошла структурную проверку.' }

$scopeLabel = if ($AllFrequencies) { 'K1–K5 (совместимый полный экспорт)' } else { 'K3–K5 (главный профиль, 885 намерений)' }
Write-Host "`n==> Компиляция профиля: $scopeLabel" -ForegroundColor Cyan
$compileArgs = @(
    (Join-Path $Root 'scripts\compile-main-command-map.mjs'),
    '--profile', (Join-Path $Root 'config\nx2512-pro-hybrid.json'),
    '--intents', (Join-Path $Root 'config\full-command-map'),
    '--probe', (Join-Path $Root 'docs\audit\runtime-command-probe-2026-07-28.json'),
    '--out', $OutputPath,
    '--report', $reportPath
)
if (-not [string]::IsNullOrWhiteSpace($catalog)) { $compileArgs += @('--catalog-dir', $catalog) }
if ($NoGlobalDuplication) { $compileArgs += '--no-global-duplication' }
if ($AllFrequencies) { $compileArgs += '--all-frequencies' }
& $node.Source @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Компиляция профиля завершилась ошибкой.' }

$generated = Get-Content -LiteralPath $OutputPath -Raw -Encoding UTF8 | ConvertFrom-Json
$allRows = @($generated.modules | ForEach-Object { $_.command_sets | ForEach-Object { $_.commands } })
$enabledRows = @($allRows | Where-Object { $_.enabled -ne $false })
$unresolvedRows = @($allRows | Where-Object { $_.fallback -like 'catalog:*' -and $_.enabled -eq $false })
$selectedIntents = [int]$generated.full_command_catalog.selected_intents
$frequencies = @($generated.full_command_catalog.selected_frequencies) -join ', '

Write-Host "  Профиль: $OutputPath" -ForegroundColor Green
Write-Host "  Частотный scope: $frequencies" -ForegroundColor Green
Write-Host "  Уникальных намерений: $selectedIntents" -ForegroundColor Green
Write-Host "  Исполняемых строк: $($enabledRows.Count) / $($allRows.Count)" -ForegroundColor Green
Write-Host "  Не разрешено для этой установки/лицензии: $($unresolvedRows.Count)" -ForegroundColor Yellow
Write-Host "  Отчёт: $reportPath" -ForegroundColor DarkGray

if ($CompileOnly) {
    Write-Host "`n[OK] Профиль скомпилирован без установки." -ForegroundColor Green
    exit 0
}

Write-Host "`n==> Установка профиля в managed runtime (compatibility filename nx2512-pro-hybrid.json)" -ForegroundColor Cyan
$installArgs = @('-ConfigPath', $OutputPath)
if (-not [string]::IsNullOrWhiteSpace($catalog)) { $installArgs += @('-CatalogDir', $catalog) }
if (-not [string]::IsNullOrWhiteSpace($NxRoot)) { $installArgs += @('-NxRoot', $NxRoot) }
if ($Clean) { $installArgs += '-Clean' }
if ($NoBuild) { $installArgs += '-NoBuild' }
if ($AllowRunningNX) { $installArgs += '-AllowRunningNX' }
if ($NoShortcut) { $installArgs += '-NoShortcut' }

& (Join-Path $Root 'install-nx-ribbon-buttons.ps1') @installArgs
if ($LASTEXITCODE -ne 0) { throw "Установка завершилась с кодом $LASTEXITCODE." }

Write-Host "`n[OK] Главный профиль NXKeys установлен." -ForegroundColor Green
