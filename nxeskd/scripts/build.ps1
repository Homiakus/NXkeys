<#
.SYNOPSIS
    Перенаправляющий скрипт для сборки NxEskd. Делегирует выполнение единому nxeskd.ps1.
#>
[CmdletBinding()]
param(
    [string]$NxRoot = $env:UGII_BASE_DIR,
    [int]$ExpectedNxRelease = 2512,
    [int]$ExpectedNxMaintenance = 0,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDirectory = '',
    [switch]$SkipNxVersionCheck,
    [switch]$SkipTests,
    [switch]$NoZip
)

$unifiedScript = Join-Path $PSScriptRoot 'nxeskd.ps1'
& $unifiedScript -Action Build @PSBoundParameters
