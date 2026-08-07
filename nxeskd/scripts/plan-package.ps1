<#
.SYNOPSIS
    Перенаправляющий скрипт для валидации пакета NxEskd. Делегирует выполнение единому nxeskd.ps1.
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$ManifestPath,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$ReportPath = ''
)

$unifiedScript = Join-Path $PSScriptRoot 'nxeskd.ps1'
& $unifiedScript -Action PlanPackage @PSBoundParameters
