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
    [switch]$NoGlobalDuplication
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Warning 'install-full-command-profile.ps1 оставлен для совместимости и собирает K1–K5. Для рабочего профиля используйте install-main-profile.ps1 (K3–K5).'
$argsToForward = @('-AllFrequencies')
if (-not [string]::IsNullOrWhiteSpace($CatalogDir)) { $argsToForward += @('-CatalogDir', $CatalogDir) }
if (-not [string]::IsNullOrWhiteSpace($NxRoot)) { $argsToForward += @('-NxRoot', $NxRoot) }
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) { $argsToForward += @('-OutputPath', $OutputPath) }
if ($CompileOnly) { $argsToForward += '-CompileOnly' }
if ($Clean) { $argsToForward += '-Clean' }
if ($NoBuild) { $argsToForward += '-NoBuild' }
if ($AllowRunningNX) { $argsToForward += '-AllowRunningNX' }
if ($NoShortcut) { $argsToForward += '-NoShortcut' }
if ($NoGlobalDuplication) { $argsToForward += '-NoGlobalDuplication' }

& (Join-Path $Root 'install-main-profile.ps1') @argsToForward
exit $LASTEXITCODE
