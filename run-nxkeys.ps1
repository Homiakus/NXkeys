[CmdletBinding()]
param(
    [string]$Profile,
    [switch]$Daemon,
    [switch]$Minimized,
    [switch]$Apply,
    [switch]$Verify,
    [switch]$Repair,
    [switch]$Help,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ExtraArgs
)

if ($Help) {
    Write-Host @"
NXKeys Launcher -- Adaptive Mnemonic Command System for Siemens NX 2512

Usage:
  .\run-nxkeys.cmd [options]

Options:
  --daemon / --minimized  Launch in background tray mode
  --apply                 Apply profile and deploy ribbon/overlay to NX
  --verify                Validate profile integrity and state machine invariants
  --repair                Restore canonical profile and redeploy to NX
  --profile <path>        Path to custom profile JSON
  --help                  Show this help message
"@
    exit 0
}

$RootDir = $PSScriptRoot
$ExePath = Join-Path $RootDir "NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe"
if (-not (Test-Path $ExePath)) {
    $BinPath = Join-Path $RootDir "NX2512_HotkeyStudio\bin\Release\net8.0-windows\NX2512_HotkeyStudio.exe"
    if (Test-Path $BinPath) {
        $ExePath = $BinPath
    } else {
        $DebugPath = Join-Path $RootDir "NX2512_HotkeyStudio\bin\Debug\net8.0-windows\NX2512_HotkeyStudio.exe"
        if (Test-Path $DebugPath) {
            $ExePath = $DebugPath
        } else {
            Write-Host "[NXKeys] Building application..." -ForegroundColor Cyan
            & dotnet build (Join-Path $RootDir "NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj") -c Release --nologo -v q
            if ($LASTEXITCODE -ne 0) {
                Write-Error "[NXKeys] Build failed."
                exit 1
            }
            $ExePath = Join-Path $RootDir "NX2512_HotkeyStudio\bin\Release\net8.0-windows\NX2512_HotkeyStudio.exe"
        }
    }
}

$Arguments = @()
if ($Profile) { $Arguments += @("--config", $Profile) }
if ($Daemon -or $Minimized) { $Arguments += "--daemon" }
if ($Apply) { $Arguments += "--apply" }
if ($Verify) { $Arguments += "--verify" }
if ($Repair) { $Arguments += "--repair" }
if ($ExtraArgs) { $Arguments += $ExtraArgs }

if ($Apply -or $Verify -or $Repair) {
    & $ExePath $Arguments
    exit $LASTEXITCODE
} else {
    Start-Process -FilePath $ExePath -ArgumentList $Arguments
    exit 0
}
