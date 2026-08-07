<#
.SYNOPSIS
    Перенаправляющий скрипт для исправления кодировок NxEskd. Делегирует выполнение единому nxeskd.ps1.
#>
[CmdletBinding()]
param()

$unifiedScript = Join-Path $PSScriptRoot 'nxeskd.ps1'
& $unifiedScript -Action FixEncodings @PSBoundParameters
