[CmdletBinding()]
param(
    [string]$ConfigPath,
    [string]$CatalogDir,
    [string]$NxRoot,
    [string]$NxOpenDll,
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
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

function Write-Step([string]$Text) { Write-Host "`n==> $Text" -ForegroundColor Cyan }

function Assert-Node20 {
    $node = Get-Command node -ErrorAction SilentlyContinue
    if (-not $node) { throw 'Node.js 20+ не найден. Он требуется для главного профиля K3–K5.' }
    $major = [int]((& $node.Source --version).TrimStart('v').Split('.')[0])
    if ($major -lt 20) { throw "Требуется Node.js 20+, обнаружена версия $(& $node.Source --version)." }
    return $node.Source
}

function Resolve-Catalog([string]$Requested) {
    if ([string]::IsNullOrWhiteSpace($Requested)) { return '' }
    $expanded = [Environment]::ExpandEnvironmentVariables($Requested)
    if (-not (Test-Path -LiteralPath $expanded -PathType Container)) { throw "Каталог NX2512_Catalog_Studio не найден: $expanded" }
    $resolved = (Resolve-Path -LiteralPath $expanded).Path
    if (-not (Test-Path -LiteralPath (Join-Path $resolved '06_ui_commands_buttons.csv') -PathType Leaf)) {
        throw "В каталоге отсутствует 06_ui_commands_buttons.csv: $resolved"
    }
    return $resolved
}

function Resolve-OutputPath([string]$Requested) {
    if ([string]::IsNullOrWhiteSpace($Requested)) {
        return [System.IO.Path]::GetFullPath((Join-Path $ScriptDir 'config\nx2512-pro-main.generated.json'))
    }
    $expanded = [Environment]::ExpandEnvironmentVariables($Requested)
    if (-not [System.IO.Path]::IsPathRooted($expanded)) { $expanded = Join-Path $ScriptDir $expanded }
    return [System.IO.Path]::GetFullPath($expanded)
}

function Resolve-Config([string]$Requested, [string]$ResolvedCatalog, [string]$RequestedOutput, [switch]$DisableGlobalDuplication) {
    if (-not [string]::IsNullOrWhiteSpace($Requested)) {
        $candidate = [Environment]::ExpandEnvironmentVariables($Requested)
        if (-not [System.IO.Path]::IsPathRooted($candidate)) { $candidate = Join-Path $ScriptDir $candidate }
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "Профиль NXKeys не найден: $candidate" }
        return (Resolve-Path -LiteralPath $candidate).Path
    }

    $node = Assert-Node20
    $candidate = Resolve-OutputPath $RequestedOutput
    $report = Join-Path $ScriptDir 'docs\generated\main-profile-resolution.md'

    Write-Step 'Проверка каталога 1169 функций и единого пресета K3–K5'
    $validationOutput = & $node (Join-Path $ScriptDir 'scripts\validate-main-command-map.mjs') 2>&1
    $validationExit = $LASTEXITCODE
    $validationOutput | ForEach-Object { Write-Host $_ }
    if ($validationExit -ne 0) { throw 'Карта команд не прошла структурную проверку.' }

    $compileArgs = @(
        (Join-Path $ScriptDir 'scripts\compile-main-command-map.mjs'),
        '--profile', (Join-Path $ScriptDir 'config\nx2512-pro-hybrid.json'),
        '--intents', (Join-Path $ScriptDir 'config\full-command-map'),
        '--probe', (Join-Path $ScriptDir 'docs\audit\runtime-command-probe-2026-07-28.json'),
        '--out', $candidate,
        '--report', $report
    )
    if (-not [string]::IsNullOrWhiteSpace($ResolvedCatalog)) { $compileArgs += @('--catalog-dir', $ResolvedCatalog) }
    if ($DisableGlobalDuplication) { $compileArgs += '--no-global-duplication' }

    Write-Step 'Компиляция единого профиля K3–K5 (885 намерений)'
    $compileOutput = & $node @compileArgs 2>&1
    $compileExit = $LASTEXITCODE
    $compileOutput | ForEach-Object { Write-Host $_ }
    if ($compileExit -ne 0) { throw 'Не удалось скомпилировать профиль NXKeys.' }
    return (Resolve-Path -LiteralPath $candidate).Path
}

function Get-NxProcesses {
    $result = @()
    foreach ($name in @('ugraf', 'run_nx', 'nx')) {
        foreach ($process in @(Get-Process -Name $name -ErrorAction SilentlyContinue)) {
            $path = ''; $description = ''
            try { $path = $process.MainModule.FileName } catch { }
            try { $description = $process.MainModule.FileVersionInfo.FileDescription } catch { }
            $evidence = ($path + ' ' + $description).ToLowerInvariant()
            if ($name -in @('ugraf', 'run_nx') -or $evidence.Contains('siemens') -or $evidence.Contains('designcenter') -or $evidence.Contains('\nxbin\')) {
                $result += $process
            }
        }
    }
    return $result
}

function Assert-DotNet8 {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) { throw '.NET 8 SDK не найден. Установите его вручную.' }
    if (-not (@(& $dotnet.Path --list-sdks) | Where-Object { $_ -match '^8\.' })) {
        throw '.NET 8 SDK не найден. Автоматическая установка зависимостей отключена.'
    }
    return $dotnet.Path
}

function Copy-DirectoryFiles([string]$Source, [string]$Destination) {
    if (-not (Test-Path -LiteralPath $Source -PathType Container)) { throw "Каталог артефактов не найден: $Source" }
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Get-ChildItem -LiteralPath $Source -File | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $Destination $_.Name) -Force
    }
}

function New-WindowsShortcut([string]$ShortcutPath, [string]$TargetPath, [string]$WorkingDirectory, [string]$Description) {
    try {
        $wshShell = New-Object -ComObject WScript.Shell
        $shortcut = $wshShell.CreateShortcut($ShortcutPath)
        $shortcut.TargetPath = $TargetPath
        $shortcut.WorkingDirectory = $WorkingDirectory
        $shortcut.Description = $Description
        $shortcut.Save()
        Write-Host "  [+] Ярлык создан: $ShortcutPath" -ForegroundColor Green
    } catch {
        Write-Warning "Не удалось создать ярлык $ShortcutPath`: $_"
    }
}

function Add-ManagedRootToExistingCustomDirs([string]$ExistingCustomDirsFile, [string]$ManagedCustomRoot) {
    if ([string]::IsNullOrWhiteSpace($ExistingCustomDirsFile) -or [string]::IsNullOrWhiteSpace($ManagedCustomRoot)) { return }
    $expanded = [Environment]::ExpandEnvironmentVariables($ExistingCustomDirsFile.Trim('"'))
    if (-not (Test-Path -LiteralPath $expanded -PathType Leaf)) { return }

    $managed = [System.IO.Path]::GetFullPath($ManagedCustomRoot).TrimEnd('\', '/')
    $lines = @(Get-Content -LiteralPath $expanded -ErrorAction Stop)
    foreach ($line in $lines) {
        $trimmed = ([string]$line).Trim().Trim('"').TrimEnd('\', '/')
        if ($trimmed.Equals($managed, [StringComparison]::OrdinalIgnoreCase)) {
            Write-Host "  [=] Existing UGII_CUSTOM_DIRECTORY_FILE already includes NXKeys: $expanded" -ForegroundColor DarkGray
            return
        }
    }

    $backup = "$expanded.nxkeys.$(Get-Date -Format 'yyyyMMdd_HHmmss').bak"
    Copy-Item -LiteralPath $expanded -Destination $backup -Force
    Add-Content -LiteralPath $expanded -Value $managed -Encoding UTF8
    Write-Host "  [+] Добавлен NXKeys custom root в существующий custom_dirs.dat: $expanded" -ForegroundColor Green
    Write-Host "      Backup: $backup" -ForegroundColor DarkGray
}

function Repair-UserUgiiCustomDirs([string]$ManagedRoot) {
    $managedCustomRoot = Join-Path $ManagedRoot 'custom'
    foreach ($target in @([EnvironmentVariableTarget]::User, [EnvironmentVariableTarget]::Machine)) {
        $value = [Environment]::GetEnvironmentVariable('UGII_CUSTOM_DIRECTORY_FILE', $target)
        if ([string]::IsNullOrWhiteSpace($value)) { continue }
        Add-ManagedRootToExistingCustomDirs -ExistingCustomDirsFile $value -ManagedCustomRoot $managedCustomRoot
    }
}

$catalog = Resolve-Catalog $CatalogDir
$config = Resolve-Config -Requested $ConfigPath -ResolvedCatalog $catalog -RequestedOutput $OutputPath -DisableGlobalDuplication:$NoGlobalDuplication
$configJson = (Get-Content -LiteralPath $config) -join "`n" | ConvertFrom-Json
$schemaVersion = [int]$configJson.schema_version
if ($schemaVersion -lt 3 -or $schemaVersion -gt 6) { throw 'Для установки требуется schema_version от 3 до 6.' }
if ($configJson.leader_key.adaptive_module_mode -ne $true) { throw 'Для установки требуется adaptive_module_mode=true.' }
if (-not (($configJson.PSObject.Properties.Name -contains 'full_command_catalog') -and $null -ne $configJson.full_command_catalog)) {
    throw 'Единый установщик принимает только generated profile с full_command_catalog.'
}
$selectedFrequencies = @($configJson.full_command_catalog.selected_frequencies)
$scope = $selectedFrequencies -join ', '
$selected = [int]$configJson.full_command_catalog.selected_intents
if ($selected -ne 885 -or ($selectedFrequencies -join '|') -ne 'K3|K4|K5') {
    throw "Единый установщик принимает только главный пресет K3–K5 на 885 намерений. Обнаружено: $scope; намерений: $selected."
}
Write-Host "Единый профиль: $scope; намерений: $selected" -ForegroundColor Green

Write-Step 'Проверка 12 базовых сочетаний, 14 модулей и покрытия K3–K5'
& node (Join-Path $ScriptDir 'scripts\validate-command-tree.mjs')
if ($LASTEXITCODE -ne 0) { throw 'Главный профиль не прошёл структурную проверку.' }

if ($CompileOnly) {
    Write-Host "`n[OK] Единый профиль K3–K5 скомпилирован без установки: $config" -ForegroundColor Green
    exit 0
}

$managedRoot = [Environment]::ExpandEnvironmentVariables([string]$configJson.deployment.managed_root)
if ([string]::IsNullOrWhiteSpace($managedRoot)) { throw 'deployment.managed_root отсутствует в профиле.' }
$managedRoot = [System.IO.Path]::GetFullPath($managedRoot)

function Stop-NxKeysProcesses([string]$TargetRoot) {
    $processNames = @('NX2512_HotkeyStudio', 'NX2512_ControlCenter')
    foreach ($name in $processNames) {
        foreach ($proc in @(Get-Process -Name $name -ErrorAction SilentlyContinue)) {
            try {
                Write-Host "  [-] Остановка процесса $($proc.ProcessName) [$($proc.Id)]..." -ForegroundColor Yellow
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            } catch { }
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($TargetRoot) -and (Test-Path -LiteralPath $TargetRoot)) {
        $normalizedRoot = [System.IO.Path]::GetFullPath($TargetRoot).TrimEnd('\', '/') + '\'
        foreach ($proc in Get-Process -ErrorAction SilentlyContinue) {
            try {
                $mainModule = $proc.MainModule.FileName
                if ($mainModule -and $mainModule.StartsWith($normalizedRoot, [StringComparison]::OrdinalIgnoreCase)) {
                    Write-Host "  [-] Остановка процесса из managed root: $($proc.ProcessName) [$($proc.Id)]..." -ForegroundColor Yellow
                    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
                }
            } catch { }
        }
    }
    Start-Sleep -Milliseconds 300
}

$nxProcesses = @(Get-NxProcesses)
if ($nxProcesses.Count -gt 0 -and -not $AllowRunningNX) {
    $details = ($nxProcesses | ForEach-Object { "$($_.ProcessName)[$($_.Id)]" }) -join ', '
    throw "Siemens NX запущен: $details. Закройте NX перед установкой."
}
if ($nxProcesses.Count -gt 0) { Write-Warning 'Загруженная Bridge DLL обновится только после перезапуска NX.' }

Write-Step 'Остановка работающих процессов NXKeys'
Stop-NxKeysProcesses $managedRoot

if ($Clean -and (Test-Path -LiteralPath $managedRoot)) {
    Write-Step "Очистка предыдущих файлов пакета в $managedRoot"
    Get-ChildItem -LiteralPath $managedRoot | ForEach-Object {
        try { Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue } catch { }
    }
}

$dotnetExe = Assert-DotNet8
$hotkeyProject = Join-Path $ScriptDir 'NX2512_HotkeyStudio'
$bridgeProject = Join-Path $ScriptDir 'NX2512_CommandBridge'
$controlProject = Join-Path $ScriptDir 'NX2512_ControlCenter\NX2512_ControlCenter.csproj'
$hotkeyDist = Join-Path $hotkeyProject 'dist'
$bridgeDist = Join-Path $bridgeProject 'dist'
$controlDist = Join-Path $ScriptDir 'NX2512_ControlCenter\dist'

if (-not $NoBuild) {
    $psExe = if (Get-Command pwsh -ErrorAction SilentlyContinue) { (Get-Command pwsh).Source } else { 'powershell' }
    Write-Step 'Сборка адаптивного HotkeyStudio'
    $args = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $hotkeyProject 'build.ps1'), '-ProfilePath', $config)
    if (-not [string]::IsNullOrWhiteSpace($catalog)) { $args += @('-CatalogDir', $catalog) }
    if ($Clean) { $args += '-Clean' }
    & $psExe @args
    if ($LASTEXITCODE -ne 0) { throw "Сборка HotkeyStudio завершилась с кодом $LASTEXITCODE." }

    Write-Step 'Сборка CommandBridge против установленного NXOpen'
    $args = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $bridgeProject 'build.ps1'))
    if ($Clean) { $args += '-Clean' }
    if ($NxRoot) { $args += @('-NxRoot', $NxRoot) }
    if ($NxOpenDll) { $args += @('-NxOpenDll', $NxOpenDll) }
    & $psExe @args
    if ($LASTEXITCODE -ne 0) { throw "Сборка CommandBridge завершилась с кодом $LASTEXITCODE." }

    Write-Step 'Публикация Adaptive Control Center'
    if (Test-Path -LiteralPath $controlDist) { Remove-Item -LiteralPath $controlDist -Recurse -Force }
    & $dotnetExe publish $controlProject -c Release -r win-x64 --self-contained false -p:Platform=x64 -o $controlDist --nologo
    if ($LASTEXITCODE -ne 0) { throw "Публикация Control Center завершилась с кодом $LASTEXITCODE." }
}

foreach ($path in @(
    (Join-Path $hotkeyDist 'NX2512_HotkeyStudio.exe'),
    (Join-Path $hotkeyDist 'nx2512-pro-hybrid.json'),
    (Join-Path $hotkeyDist 'nx2512-state-machines.json'),
    (Join-Path $bridgeDist 'NX2512_CommandBridge.dll'),
    (Join-Path $controlDist 'NX2512_ControlCenter.exe')
)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Обязательный артефакт не найден: $path" }
}

$staging = Join-Path (Join-Path $env:LOCALAPPDATA 'NXKeys\staging') ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $staging | Out-Null
try {
    Write-Step 'Формирование чистого staging-набора'
    Copy-DirectoryFiles $hotkeyDist $staging
    Copy-DirectoryFiles $bridgeDist (Join-Path $staging 'bridge')
    Copy-DirectoryFiles $controlDist (Join-Path $staging 'control-center')
    Copy-Item -LiteralPath $config -Destination (Join-Path $staging 'nx2512-pro-hybrid.json') -Force

    $stagedExe = Join-Path $staging 'NX2512_HotkeyStudio.exe'
    $stagedConfig = Join-Path $staging 'nx2512-pro-hybrid.json'
    Write-Step "Транзакционная установка в $managedRoot"
    $applyArgs = @('apply', '--config', $stagedConfig, '--yes')
    if ($AllowRunningNX) { $applyArgs += '--allow-running-nx' }
    & $stagedExe @applyArgs
    if ($LASTEXITCODE -ne 0) { throw "C# deployment завершился с кодом $LASTEXITCODE." }

    $installedExe = Join-Path $managedRoot 'NX2512_HotkeyStudio.exe'
    $installedConfig = Join-Path $managedRoot 'nx2512-pro-hybrid.json'
    Write-Step 'Проверка установленного package manifest'
    & $installedExe health --config $installedConfig
    if ($LASTEXITCODE -ne 0) { throw "Health-check завершился с кодом $LASTEXITCODE." }

    if (-not $NoShortcut) {
        Write-Step 'Создание ярлыков на Рабочем столе и в Главном меню'
        $launcherCmd = Join-Path $managedRoot 'launch-nx2512-with-nxkeys.cmd'
        $controlCenterExe = Join-Path $managedRoot 'control-center\NX2512_ControlCenter.exe'

        $desktopDir = [Environment]::GetFolderPath('Desktop')
        if (Test-Path -LiteralPath $desktopDir) {
            $desktopShortcut = Join-Path $desktopDir 'Siemens NX 2512 (NXKeys).lnk'
            New-WindowsShortcut -ShortcutPath $desktopShortcut -TargetPath $launcherCmd -WorkingDirectory $managedRoot -Description 'Запуск Siemens NX 2512 с главным профилем NXKeys K3–K5'
        }

        $startMenuPrograms = [Environment]::GetFolderPath('Programs')
        if (Test-Path -LiteralPath $startMenuPrograms) {
            $nxkeysFolder = Join-Path $startMenuPrograms 'NXKeys'
            if (-not (Test-Path -LiteralPath $nxkeysFolder)) { New-Item -ItemType Directory -Force -Path $nxkeysFolder | Out-Null }
            $startMenuShortcut = Join-Path $nxkeysFolder 'Siemens NX 2512 (NXKeys).lnk'
            New-WindowsShortcut -ShortcutPath $startMenuShortcut -TargetPath $launcherCmd -WorkingDirectory $managedRoot -Description 'Запуск Siemens NX 2512 с главным профилем NXKeys K3–K5'

            if (Test-Path -LiteralPath $controlCenterExe) {
                $controlShortcut = Join-Path $nxkeysFolder 'NXKeys Control Center.lnk'
                New-WindowsShortcut -ShortcutPath $controlShortcut -TargetPath $controlCenterExe -WorkingDirectory (Split-Path $controlCenterExe) -Description 'Панель управления и мониторинга NXKeys'
            }
        }
    }

    Write-Step 'Согласование UGII_CUSTOM_DIRECTORY_FILE со старой кастомизацией'
    Repair-UserUgiiCustomDirs $managedRoot

    Write-Host "`nNXKeys Main K3–K5 Profile установлен успешно." -ForegroundColor Green
    Write-Host "Managed root: $managedRoot"
    Write-Host "Главный профиль K3–K5 установлен как: $(Join-Path $managedRoot 'nx2512-pro-hybrid.json')" -ForegroundColor Green
    Write-Host "Запуск NX: $(Join-Path $managedRoot 'launch-nx2512-with-nxkeys.cmd')" -ForegroundColor Yellow
}
finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue }
}
