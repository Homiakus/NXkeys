[CmdletBinding()]
param(
    [ValidateSet('amd64', 'arm64')]
    [string]$Arch = 'amd64',
    [switch]$Race
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Dist = Join-Path $Root 'dist'
New-Item -ItemType Directory -Force -Path $Dist | Out-Null

Push-Location $Root
try {
    go mod download
    go test ./...
    $env:CGO_ENABLED = if ($Race) { '1' } else { '0' }
    $env:GOOS = 'windows'
    $env:GOARCH = $Arch
    $raceArg = if ($Race) { '-race' } else { '' }
    $output = Join-Path $Dist "nxkeys-windows-$Arch.exe"
    if ($raceArg) {
        go build $raceArg -trimpath -ldflags '-s -w' -o $output ./cmd/nxkeys
    } else {
        go build -trimpath -ldflags '-s -w' -o $output ./cmd/nxkeys
    }
    Copy-Item -Force (Join-Path $Root 'config\nx2512-pro-hybrid.json') (Join-Path $Dist 'nx2512-pro-hybrid.json')
    Write-Host "Built $output"
}
finally {
    Pop-Location
}
