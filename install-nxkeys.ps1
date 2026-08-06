[CmdletBinding()]
# NXKeys installer menu revision: path-char-fix-v2
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
    [switch]$NoGlobalDuplication,
    [ValidateSet('Menu','Install','Audit','CleanConflicts','RepairCustomDirs','CleanInstall')]
    [string]$Mode = 'Menu',
    [switch]$Yes,
    [switch]$AutoCleanConflicts
)

<#
.SYNOPSIS
    Интерактивная установка, диагностика и безопасная очистка конфликтов NXKeys (rev. 2).

.DESCRIPTION
    Без параметров открывает меню. Старые установки NxHotkeys, конфликтующие custom_dirs,
    дубли CommandBridge, legacy toolbar и локальные NXOpen DLL не удаляются безвозвратно:
    они перемещаются в %LOCALAPPDATA%\NXKeys\conflict-backups\<дата>.

.EXAMPLE
    .\install-nxkeys-menu.ps1

.EXAMPLE
    .\install-nxkeys-menu.ps1 -Mode Audit

.EXAMPLE
    .\install-nxkeys-menu.ps1 -Mode CleanConflicts -Yes

.EXAMPLE
    .\install-nxkeys-menu.ps1 -Mode CleanInstall -Yes

.EXAMPLE
    .\install-nxkeys-menu.ps1 -Mode Install -AutoCleanConflicts -Yes -Clean
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

# Нормализуем кодировку консоли, чтобы русские сообщения не превращались в ?????.
try {
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    [Console]::InputEncoding = $utf8
    [Console]::OutputEncoding = $utf8
    $global:OutputEncoding = $utf8
    if ($env:OS -eq 'Windows_NT') { & chcp.com 65001 | Out-Null }
} catch {
    Write-Verbose "Не удалось переключить консоль в UTF-8: $_"
}

function Write-Step([string]$Text) { Write-Host "`n==> $Text" -ForegroundColor Cyan }

# Единый обработчик ошибок: показывает реальную строку вместо только сообщения CLR binder.
trap {
    $line = $_.InvocationInfo.ScriptLineNumber
    $position = $_.InvocationInfo.PositionMessage
    Write-Host "`n[ERROR] $($_.Exception.Message)" -ForegroundColor Red
    if ($line -gt 0) { Write-Host "Строка скрипта: $line" -ForegroundColor Yellow }
    if (-not [string]::IsNullOrWhiteSpace($position)) { Write-Host $position -ForegroundColor DarkYellow }
    if (-not [string]::IsNullOrWhiteSpace($_.ScriptStackTrace)) {
        Write-Host "Стек PowerShell:`n$($_.ScriptStackTrace)" -ForegroundColor DarkGray
    }
    exit 1
}

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

    $candidate = Join-Path $ScriptDir 'config\nx2512-v8-profile.json'
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "Профиль NXKeys v8 не найден: $candidate" }
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

function Get-AssemblyIdentity([string]$Path) {
    try {
        return [System.Reflection.AssemblyName]::GetAssemblyName($Path)
    } catch {
        return $null
    }
}

function Resolve-RuntimeAssembly([string]$AssemblyName, [string[]]$SearchRoots) {
    $fileName = "$AssemblyName.dll"

    # Порядок корней важен: сначала готовый dist, затем Release-выходы,
    # и только в конце резервный поиск по репозиторию.
    foreach ($root in $SearchRoots) {
        if ([string]::IsNullOrWhiteSpace($root) -or -not (Test-Path -LiteralPath $root)) { continue }

        if (Test-Path -LiteralPath $root -PathType Leaf) {
            $item = Get-Item -LiteralPath $root
            $identity = Get-AssemblyIdentity $item.FullName
            if ($item.Name.Equals($fileName, [StringComparison]::OrdinalIgnoreCase) -and
                $null -ne $identity -and
                $identity.Name.Equals($AssemblyName, [StringComparison]::OrdinalIgnoreCase)) {
                return $item.FullName
            }
            continue
        }

        $direct = Join-Path $root $fileName
        if (Test-Path -LiteralPath $direct -PathType Leaf) {
            $identity = Get-AssemblyIdentity $direct
            if ($null -ne $identity -and $identity.Name.Equals($AssemblyName, [StringComparison]::OrdinalIgnoreCase)) {
                return $direct
            }
        }

        foreach ($item in @(Get-ChildItem -LiteralPath $root -Filter $fileName -File -Recurse -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending)) {
            if ($item.FullName -match '[\\/](obj|ref|refint)[\\/]') { continue }
            $identity = Get-AssemblyIdentity $item.FullName
            if ($null -ne $identity -and $identity.Name.Equals($AssemblyName, [StringComparison]::OrdinalIgnoreCase)) {
                return $item.FullName
            }
        }
    }
    return ''
}

function Ensure-StagedRuntimeAssembly(
    [string]$AssemblyName,
    [string]$StagingRoot,
    [string[]]$SearchRoots
) {
    $destination = Join-Path $StagingRoot "$AssemblyName.dll"
    $existingIdentity = if (Test-Path -LiteralPath $destination -PathType Leaf) { Get-AssemblyIdentity $destination } else { $null }
    if ($null -ne $existingIdentity -and $existingIdentity.Name.Equals($AssemblyName, [StringComparison]::OrdinalIgnoreCase)) {
        return $destination
    }

    $source = Resolve-RuntimeAssembly -AssemblyName $AssemblyName -SearchRoots $SearchRoots
    if ([string]::IsNullOrWhiteSpace($source)) {
        throw "После сборки не найден обязательный runtime-компонент $AssemblyName.dll. Исправьте publish/build проекта NX2512_HotkeyStudio или добавьте ProjectReference с CopyLocal=true."
    }

    Copy-Item -LiteralPath $source -Destination $destination -Force
    Write-Host "  [+] Добавлена runtime-зависимость: $AssemblyName.dll" -ForegroundColor Green
    Write-Host "      Источник: $source" -ForegroundColor DarkGray
    return $destination
}

function Sync-InstalledRuntimeFile([string]$SourcePath, [string]$DestinationRoot) {
    if (-not (Test-Path -LiteralPath $SourcePath -PathType Leaf)) { throw "Runtime-файл отсутствует в staging: $SourcePath" }
    New-Item -ItemType Directory -Force -Path $DestinationRoot | Out-Null
    $destination = Join-Path $DestinationRoot (Split-Path -Leaf $SourcePath)

    $copyRequired = -not (Test-Path -LiteralPath $destination -PathType Leaf)
    if (-not $copyRequired) {
        $sourceHash = (Get-FileHash -LiteralPath $SourcePath -Algorithm SHA256).Hash
        $destinationHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        $copyRequired = -not $sourceHash.Equals($destinationHash, [StringComparison]::OrdinalIgnoreCase)
    }

    if ($copyRequired) {
        Copy-Item -LiteralPath $SourcePath -Destination $destination -Force
        Write-Warning "Package manifest не перенёс совместимую версию $(Split-Path -Leaf $SourcePath); файл восстановлен из staging перед health-check."
    }
    return $destination
}

function Invoke-NxKeysHealthCheck([string]$Executable, [string]$Config) {
    $healthOutput = @(& $Executable health --config $Config 2>&1)
    $healthExitCode = $LASTEXITCODE
    $healthOutput | ForEach-Object { Write-Host $_ }

    if ($healthExitCode -ne 0) {
        $logDir = Join-Path $env:LOCALAPPDATA 'NXKeys\logs'
        New-Item -ItemType Directory -Force -Path $logDir | Out-Null
        $logPath = Join-Path $logDir "install-health-$(Get-Date -Format 'yyyyMMdd_HHmmss').log"
        $healthOutput | Set-Content -LiteralPath $logPath -Encoding UTF8
        throw "Health-check завершился с кодом $healthExitCode. Полный вывод: $logPath"
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

    $managed = [System.IO.Path]::GetFullPath($ManagedCustomRoot).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar))
    $lines = @(Get-Content -LiteralPath $expanded -ErrorAction Stop)
    foreach ($line in $lines) {
        $trimmed = ([string]$line).Trim().Trim('"').TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar))
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

function Test-NxKeysAdministrator {
    try {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = New-Object Security.Principal.WindowsPrincipal($identity)
        return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    } catch {
        return $false
    }
}

function Confirm-NxKeysAction([string]$Prompt, [switch]$AssumeYes) {
    if ($AssumeYes) { return $true }
    $answer = Read-Host "$Prompt [y/N]"
    return $answer -match '^(?i:y|yes|д|да)$'
}

function Test-IsOldNxHotkeysPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    $expanded = [Environment]::ExpandEnvironmentVariables($Path.Trim().Trim('"'))
    return $expanded -match '(?i)(^|[\\/])NxHotkeys([\\/]|$)'
}

function Get-NxKeysKnownCustomDirsFiles {
    [string[]]$result = @()
    foreach ($target in @([EnvironmentVariableTarget]::Process, [EnvironmentVariableTarget]::User, [EnvironmentVariableTarget]::Machine)) {
        try {
            $value = [Environment]::GetEnvironmentVariable('UGII_CUSTOM_DIRECTORY_FILE', $target)
            if (-not [string]::IsNullOrWhiteSpace($value)) {
                $expanded = [Environment]::ExpandEnvironmentVariables($value.Trim().Trim('"'))
                if ($result -notcontains $expanded) { $result += $expanded }
            }
        } catch { }
    }

    foreach ($candidate in @(
        (Join-Path $env:LOCALAPPDATA 'Programs\NxHotkeys\2512\NxCustomization\custom_dirs.dat'),
        (Join-Path $env:LOCALAPPDATA 'NxHotkeys\2512\NxCustomization\custom_dirs.dat'),
        (Join-Path $env:LOCALAPPDATA 'NXKeys\custom_dirs.dat')
    )) {
        if ($result -notcontains $candidate) { $result += $candidate }
    }
    return $result
}

function Resolve-NxKeysMaintenanceRoot([string]$RequestedConfig) {
    [string[]]$configCandidates = @()
    if (-not [string]::IsNullOrWhiteSpace($RequestedConfig)) {
        $candidate = [Environment]::ExpandEnvironmentVariables($RequestedConfig)
        if (-not [IO.Path]::IsPathRooted($candidate)) { $candidate = Join-Path $ScriptDir $candidate }
        $configCandidates += $candidate
    }
    foreach ($candidate in @(
        (Join-Path $ScriptDir 'config\nx2512-pro-main.generated.json'),
        (Join-Path $ScriptDir 'config\nx2512-v8-profile.json')
    )) {
        if ($configCandidates -notcontains $candidate) { $configCandidates += $candidate }
    }

    foreach ($candidate in $configCandidates) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
        try {
            $json = (Get-Content -LiteralPath $candidate -Raw -ErrorAction Stop) | ConvertFrom-Json
            $value = [string]$json.deployment.managed_root
            if (-not [string]::IsNullOrWhiteSpace($value)) {
                return [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($value))
            }
        } catch { }
    }

    $managedBase = Join-Path $env:LOCALAPPDATA 'NXKeys\managed'
    if (Test-Path -LiteralPath $managedBase -PathType Container) {
        $existing = @(Get-ChildItem -LiteralPath $managedBase -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like 'NX2512*' } |
            Sort-Object LastWriteTimeUtc -Descending)
        if ($existing.Count -gt 0) { return $existing[0].FullName }
    }
    return Join-Path $managedBase 'NX2512.6000'
}

function Get-NxKeysPreservedCustomRoots([string]$ManagedRoot) {
    [string[]]$result = @()
    foreach ($file in @(Get-NxKeysKnownCustomDirsFiles)) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { continue }
        foreach ($line in @(Get-Content -LiteralPath $file -ErrorAction SilentlyContinue)) {
            $value = ([string]$line).Trim().Trim('"').TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar))
            if ([string]::IsNullOrWhiteSpace($value) -or $value.StartsWith('#') -or $value.StartsWith('!')) { continue }
            $value = [Environment]::ExpandEnvironmentVariables($value)
            if (Test-IsOldNxHotkeysPath $value) { continue }
            try { $value = [IO.Path]::GetFullPath($value) } catch { continue }
            if ((Test-Path -LiteralPath $value -PathType Container) -and $result -notcontains $value) {
                $result += $value
            }
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($ManagedRoot)) {
        $managedCustom = Join-Path $ManagedRoot 'custom'
        if ($result -notcontains $managedCustom) { $result += $managedCustom }
    }
    return $result
}

function Set-NxKeysCanonicalCustomDirs([string]$ManagedRoot, [string[]]$PreservedRoots, [switch]$UpdateMachine) {
    $canonical = Join-Path $env:LOCALAPPDATA 'NXKeys\custom_dirs.dat'
    $canonicalDir = Split-Path -Parent $canonical
    New-Item -ItemType Directory -Force -Path $canonicalDir | Out-Null

    [string[]]$entries = @()
    foreach ($entry in @($PreservedRoots)) {
        if ([string]::IsNullOrWhiteSpace($entry) -or (Test-IsOldNxHotkeysPath $entry)) { continue }
        try { $full = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($entry)) } catch { continue }
        if ($entries -notcontains $full) { $entries += $full }
    }
    $managedCustom = Join-Path $ManagedRoot 'custom'
    if ($entries -notcontains $managedCustom) { $entries += $managedCustom }

    if (Test-Path -LiteralPath $canonical -PathType Leaf) {
        $backup = "$canonical.$(Get-Date -Format 'yyyyMMdd_HHmmss').bak"
        Copy-Item -LiteralPath $canonical -Destination $backup -Force
        Write-Host "  [B] Резервная копия custom_dirs.dat: $backup" -ForegroundColor DarkGray
    }
    [IO.File]::WriteAllLines($canonical, $entries, [Text.UTF8Encoding]::new($false))
    [Environment]::SetEnvironmentVariable('UGII_CUSTOM_DIRECTORY_FILE', $canonical, [EnvironmentVariableTarget]::User)
    $env:UGII_CUSTOM_DIRECTORY_FILE = $canonical
    Write-Host "  [+] Пользовательский UGII_CUSTOM_DIRECTORY_FILE: $canonical" -ForegroundColor Green

    $machineValue = [Environment]::GetEnvironmentVariable('UGII_CUSTOM_DIRECTORY_FILE', [EnvironmentVariableTarget]::Machine)
    if ($UpdateMachine -or (Test-IsOldNxHotkeysPath $machineValue)) {
        if (Test-NxKeysAdministrator) {
            [Environment]::SetEnvironmentVariable('UGII_CUSTOM_DIRECTORY_FILE', $canonical, [EnvironmentVariableTarget]::Machine)
            Write-Host "  [+] Системный UGII_CUSTOM_DIRECTORY_FILE также обновлён." -ForegroundColor Green
        } elseif (Test-IsOldNxHotkeysPath $machineValue) {
            Write-Warning 'Системный UGII_CUSTOM_DIRECTORY_FILE указывает на старый NxHotkeys, но для его изменения нужны права администратора. Пользовательское значение уже исправлено и имеет приоритет.'
        }
    }
    return $canonical
}

function Get-NxKeysConflictInventory([string]$ManagedRoot) {
    [object[]]$items = @()
    $oldRoots = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\NxHotkeys'),
        (Join-Path $env:LOCALAPPDATA 'NxHotkeys'),
        (Join-Path $env:APPDATA 'NxHotkeys')
    ) | Select-Object -Unique

    foreach ($root in $oldRoots) {
        if (Test-Path -LiteralPath $root) {
            $items += [pscustomobject]@{ Severity='Критично'; Type='Старая установка'; Path=$root; Detail='Может загружать NxHotkeys.Connector.dll параллельно с NXKeys.' }
        }
    }

    foreach ($proc in @(Get-Process -ErrorAction SilentlyContinue)) {
        $processPath = ''
        try { $processPath = $proc.MainModule.FileName } catch { }
        if ($proc.ProcessName -like 'NxHotkeys*' -or (Test-IsOldNxHotkeysPath $processPath)) {
            $items += [pscustomobject]@{ Severity='Критично'; Type='Старый процесс'; Path="$($proc.ProcessName)[$($proc.Id)]"; Detail="Запущен из: $processPath" }
        }
    }

    foreach ($target in @([EnvironmentVariableTarget]::Process, [EnvironmentVariableTarget]::User, [EnvironmentVariableTarget]::Machine)) {
        try {
            $value = [Environment]::GetEnvironmentVariable('UGII_CUSTOM_DIRECTORY_FILE', $target)
            if (Test-IsOldNxHotkeysPath $value) {
                $items += [pscustomobject]@{ Severity='Критично'; Type='Переменная NX'; Path=$value; Detail="UGII_CUSTOM_DIRECTORY_FILE[$target] указывает внутрь старой установки." }
            }
        } catch { }
    }

    foreach ($file in @(Get-NxKeysKnownCustomDirsFiles)) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { continue }
        if (Test-IsOldNxHotkeysPath $file) {
            $items += [pscustomobject]@{ Severity='Критично'; Type='Старый custom_dirs'; Path=$file; Detail='Файл конфигурации принадлежит прежней установке NxHotkeys.' }
        }
        foreach ($line in @(Get-Content -LiteralPath $file -ErrorAction SilentlyContinue)) {
            $entry = ([string]$line).Trim().Trim('"')
            if (Test-IsOldNxHotkeysPath $entry) {
                $items += [pscustomobject]@{ Severity='Критично'; Type='Ссылка custom_dirs'; Path=$file; Detail="Старая запись: $entry" }
            }
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($ManagedRoot)) {
        $startup = Join-Path $ManagedRoot 'custom\startup'
        $application = Join-Path $ManagedRoot 'custom\application'
        $startupBridge = Join-Path $startup 'NX2512_CommandBridge.dll'
        $applicationBridge = Join-Path $application 'NX2512_CommandBridge.dll'
        if ((Test-Path -LiteralPath $startupBridge -PathType Leaf) -and (Test-Path -LiteralPath $applicationBridge -PathType Leaf)) {
            $items += [pscustomobject]@{ Severity='Критично'; Type='Двойная загрузка Bridge'; Path=$startupBridge; Detail='CommandBridge присутствует одновременно в startup и application.' }
        }

        $legacyToolbar = Join-Path $startup 'nxkeys_toolbar.tbr'
        if (Test-Path -LiteralPath $legacyToolbar -PathType Leaf) {
            $items += [pscustomobject]@{ Severity='Ошибка'; Type='Legacy toolbar'; Path=$legacyToolbar; Detail='В NX 2512 вызывает ошибку TBR buffer size 15.' }
        }

        foreach ($oldName in @('NxHotkeys.Connector.dll','NxHotkeys.Shared.dll','nxhotkeys.men')) {
            foreach ($found in @(Get-ChildItem -LiteralPath (Join-Path $ManagedRoot 'custom') -Filter $oldName -File -Recurse -ErrorAction SilentlyContinue)) {
                $items += [pscustomobject]@{ Severity='Критично'; Type='Старый runtime'; Path=$found.FullName; Detail='Артефакт прежнего NxHotkeys внутри нового managed root.' }
            }
        }

        foreach ($found in @(Get-ChildItem -LiteralPath $ManagedRoot -Filter 'NXOpen*.dll' -File -Recurse -ErrorAction SilentlyContinue)) {
            $items += [pscustomobject]@{ Severity='Критично'; Type='Локальная NXOpen DLL'; Path=$found.FullName; Detail='Может перекрыть NXOpen DLL из установленного Designcenter NX.' }
        }
    }
    return $items
}

function Show-NxKeysConflictInventory([string]$ManagedRoot) {
    Write-Step 'Аудит конфликтов NXKeys'
    Write-Host "Managed root: $ManagedRoot" -ForegroundColor DarkGray
    $items = @(Get-NxKeysConflictInventory $ManagedRoot)
    if ($items.Count -eq 0) {
        Write-Host '[OK] Конфликты старых установок и двойной загрузки не обнаружены.' -ForegroundColor Green
        return $items
    }
    $items | Sort-Object Severity, Type, Path | Format-Table Severity, Type, Path, Detail -Wrap -AutoSize | Out-Host
    Write-Warning "Обнаружено конфликтов: $($items.Count)"
    return $items
}

function Move-NxKeysConflictItem([string]$Path, [string]$BackupRoot, [string]$Category) {
    if (-not (Test-Path -LiteralPath $Path)) { return '' }
    $categoryRoot = Join-Path $BackupRoot $Category
    New-Item -ItemType Directory -Force -Path $categoryRoot | Out-Null
    $leaf = Split-Path -Leaf $Path
    if ([string]::IsNullOrWhiteSpace($leaf)) { $leaf = 'item' }
    $destination = Join-Path $categoryRoot $leaf
    if (Test-Path -LiteralPath $destination) { $destination = "$destination.$([Guid]::NewGuid().ToString('N').Substring(0,8))" }
    Move-Item -LiteralPath $Path -Destination $destination -Force
    Write-Host "  [>] В резерв: $Path" -ForegroundColor Yellow
    return $destination
}

function Get-NxKeysRelativePath([string]$BasePath, [string]$TargetPath) {
    $base = [IO.Path]::GetFullPath($BasePath).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)) + [IO.Path]::DirectorySeparatorChar
    $target = [IO.Path]::GetFullPath($TargetPath)
    $baseUri = New-Object Uri($base)
    $targetUri = New-Object Uri($target)
    $relative = [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString())
    return $relative.Replace('/', [IO.Path]::DirectorySeparatorChar)
}

function Update-NxKeysManifestAfterCleanup([string]$ManagedRoot, [string[]]$RemovedPaths, [string]$BackupRoot) {
    $manifestPath = Join-Path $ManagedRoot 'package-manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or @($RemovedPaths).Count -eq 0) { return }
    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $filesProperty = if ($manifest.PSObject.Properties.Name -contains 'Files') { 'Files' } elseif ($manifest.PSObject.Properties.Name -contains 'files') { 'files' } else { '' }
        if ([string]::IsNullOrWhiteSpace($filesProperty)) { return }

        $removedRelative = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($path in $RemovedPaths) {
            try { [void]$removedRelative.Add((Get-NxKeysRelativePath -BasePath $ManagedRoot -TargetPath $path)) } catch { }
        }
        $kept = @()
        foreach ($entry in @($manifest.$filesProperty)) {
            $relative = if ($entry.PSObject.Properties.Name -contains 'RelativePath') { [string]$entry.RelativePath } else { [string]$entry.relative_path }
            $relative = $relative.Replace('/', [IO.Path]::DirectorySeparatorChar)
            if (-not $removedRelative.Contains($relative)) { $kept += $entry }
        }
        $manifest.$filesProperty = $kept
        $manifestBackup = Join-Path $BackupRoot 'package-manifest.before-cleanup.json'
        Copy-Item -LiteralPath $manifestPath -Destination $manifestBackup -Force
        [IO.File]::WriteAllText($manifestPath, (($manifest | ConvertTo-Json -Depth 30) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
        Write-Host '  [+] package-manifest.json согласован с очищенной структурой.' -ForegroundColor Green
    } catch {
        Write-Warning "Не удалось обновить package-manifest.json после очистки: $_"
    }
}

function Remove-NxKeysInternalLayoutConflicts([string]$ManagedRoot, [string]$BackupRoot) {
    [string[]]$removed = @()
    if (-not (Test-Path -LiteralPath $ManagedRoot -PathType Container)) { return @() }
    $startup = Join-Path $ManagedRoot 'custom\startup'
    $application = Join-Path $ManagedRoot 'custom\application'

    foreach ($name in @(
        'NX2512_CommandBridge.dll',
        'NX2512_CommandBridge.deps.json',
        'NX2512_CommandBridge.runtimeconfig.json',
        'NX2512_CommandBridge.pdb',
        'NXKeys.BridgeCore.dll',
        'NXKeys.Protocol.dll'
    )) {
        $source = Join-Path $startup $name
        $canonical = Join-Path $application $name
        if ((Test-Path -LiteralPath $source -PathType Leaf) -and (Test-Path -LiteralPath $canonical -PathType Leaf)) {
            [void](Move-NxKeysConflictItem -Path $source -BackupRoot $BackupRoot -Category 'duplicate-startup-runtime')
            $removed += $source
        }
    }

    $legacyToolbar = Join-Path $startup 'nxkeys_toolbar.tbr'
    if (Test-Path -LiteralPath $legacyToolbar -PathType Leaf) {
        [void](Move-NxKeysConflictItem -Path $legacyToolbar -BackupRoot $BackupRoot -Category 'legacy-toolbar')
        $removed += $legacyToolbar
    }

    foreach ($found in @(Get-ChildItem -LiteralPath $ManagedRoot -Filter 'NXOpen*.dll' -File -Recurse -ErrorAction SilentlyContinue)) {
        [void](Move-NxKeysConflictItem -Path $found.FullName -BackupRoot $BackupRoot -Category 'local-nxopen')
        $removed += $found.FullName
    }

    Update-NxKeysManifestAfterCleanup -ManagedRoot $ManagedRoot -RemovedPaths $removed -BackupRoot $BackupRoot
    return $removed
}

function Invoke-NxKeysConflictCleanup([string]$ManagedRoot, [switch]$AssumeYes) {
    $nxProcesses = @(Get-NxProcesses)
    if ($nxProcesses.Count -gt 0) {
        $details = ($nxProcesses | ForEach-Object { "$($_.ProcessName)[$($_.Id)]" }) -join ', '
        throw "Для безопасной очистки полностью закройте Siemens NX: $details. Параметр -AllowRunningNX на операцию очистки не распространяется."
    }

    $inventory = @(Show-NxKeysConflictInventory $ManagedRoot)
    if ($inventory.Count -eq 0) { return $true }
    if (-not (Confirm-NxKeysAction 'Создать резервную копию и очистить найденные конфликты?' -AssumeYes:$AssumeYes)) {
        Write-Host 'Очистка отменена.' -ForegroundColor Yellow
        return $false
    }

    $normalizedManagedRoot = [IO.Path]::GetFullPath($ManagedRoot).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)) + [IO.Path]::DirectorySeparatorChar
    foreach ($proc in @(Get-Process -ErrorAction SilentlyContinue)) {
        $processPath = ''
        try { $processPath = $proc.MainModule.FileName } catch { }
        $isManagedProcess = -not [string]::IsNullOrWhiteSpace($processPath) -and
            $processPath.StartsWith($normalizedManagedRoot, [StringComparison]::OrdinalIgnoreCase)
        $isLegacyProcess = $proc.ProcessName -like 'NxHotkeys*' -or (Test-IsOldNxHotkeysPath $processPath)
        $isCurrentProcess = $proc.ProcessName -in @('NX2512_HotkeyStudio','NX2512_ControlCenter')
        if ($isManagedProcess -or $isLegacyProcess -or $isCurrentProcess) {
            try {
                Write-Host "  [-] Остановка процесса $($proc.ProcessName) [$($proc.Id)]" -ForegroundColor Yellow
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            } catch { }
        }
    }
    Start-Sleep -Milliseconds 300

    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $backupRoot = Join-Path $env:LOCALAPPDATA "NXKeys\conflict-backups\$timestamp"
    New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
    $preservedRoots = @(Get-NxKeysPreservedCustomRoots $ManagedRoot)
    [string[]]$actions = @()

    foreach ($customDirs in @(Get-NxKeysKnownCustomDirsFiles)) {
        if (-not (Test-Path -LiteralPath $customDirs -PathType Leaf) -or (Test-IsOldNxHotkeysPath $customDirs)) { continue }
        $lines = @(Get-Content -LiteralPath $customDirs -ErrorAction SilentlyContinue)
        $filtered = @($lines | Where-Object { -not (Test-IsOldNxHotkeysPath ([string]$_)) })
        if ($filtered.Count -ne $lines.Count) {
            $backup = Join-Path $backupRoot ("custom-dirs-" + ([IO.Path]::GetFileName($customDirs)) + '.bak')
            Copy-Item -LiteralPath $customDirs -Destination $backup -Force
            [IO.File]::WriteAllLines($customDirs, [string[]]$filtered, [Text.UTF8Encoding]::new($false))
            $actions += "Удалены старые записи из $customDirs"
        }
    }

    foreach ($root in @(
        (Join-Path $env:LOCALAPPDATA 'Programs\NxHotkeys'),
        (Join-Path $env:LOCALAPPDATA 'NxHotkeys'),
        (Join-Path $env:APPDATA 'NxHotkeys')
    ) | Select-Object -Unique) {
        if (Test-Path -LiteralPath $root) {
            [void](Move-NxKeysConflictItem -Path $root -BackupRoot $backupRoot -Category 'old-installations')
            $actions += "Архивирована старая установка $root"
        }
    }

    if (Test-Path -LiteralPath (Join-Path $ManagedRoot 'custom') -PathType Container) {
        foreach ($oldName in @('NxHotkeys.Connector.dll','NxHotkeys.Shared.dll','nxhotkeys.men')) {
            foreach ($found in @(Get-ChildItem -LiteralPath (Join-Path $ManagedRoot 'custom') -Filter $oldName -File -Recurse -ErrorAction SilentlyContinue)) {
                [void](Move-NxKeysConflictItem -Path $found.FullName -BackupRoot $backupRoot -Category 'old-runtime-in-managed-root')
                $actions += "Архивирован старый runtime $($found.FullName)"
            }
        }
    }

    $layoutRemoved = @(Remove-NxKeysInternalLayoutConflicts -ManagedRoot $ManagedRoot -BackupRoot $backupRoot)
    foreach ($path in $layoutRemoved) { $actions += "Устранён внутренний конфликт $path" }
    $canonical = Set-NxKeysCanonicalCustomDirs -ManagedRoot $ManagedRoot -PreservedRoots $preservedRoots
    $actions += "UGII_CUSTOM_DIRECTORY_FILE переключён на $canonical"

    $reportPath = Join-Path $backupRoot 'cleanup-report.txt'
    $report = @(
        "NXKeys conflict cleanup",
        "Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
        "Managed root: $ManagedRoot",
        "Canonical custom dirs: $canonical",
        '',
        'Actions:'
    ) + @($actions | ForEach-Object { "- $_" })
    [IO.File]::WriteAllLines($reportPath, [string[]]$report, [Text.UTF8Encoding]::new($false))

    Write-Host "`n[OK] Очистка завершена. Резервная копия: $backupRoot" -ForegroundColor Green
    Write-Host "Отчёт: $reportPath" -ForegroundColor Green
    [void](Show-NxKeysConflictInventory $ManagedRoot)
    return $true
}

function Repair-NxKeysCustomDirsOnly([string]$ManagedRoot, [switch]$AssumeYes) {
    if (-not (Confirm-NxKeysAction 'Пересоздать безопасный custom_dirs.dat и обновить переменную NX?' -AssumeYes:$AssumeYes)) { return }
    $preserved = @(Get-NxKeysPreservedCustomRoots $ManagedRoot)
    [void](Set-NxKeysCanonicalCustomDirs -ManagedRoot $ManagedRoot -PreservedRoots $preserved)
    Write-Host '[OK] Конфигурация загрузки кастомизаций NX восстановлена.' -ForegroundColor Green
}

function Show-NxKeysInstallerMenu([string]$ManagedRoot) {
    while ($true) {
        Clear-Host
        Write-Host '============================================================' -ForegroundColor DarkCyan
        Write-Host ' NXKeys — установка, диагностика и очистка конфликтов' -ForegroundColor Cyan
        Write-Host '============================================================' -ForegroundColor DarkCyan
        Write-Host " Managed root: $ManagedRoot" -ForegroundColor DarkGray
        Write-Host ''
        Write-Host '  [1] Установить или обновить NXKeys'
        Write-Host '  [2] Проверить конфликты без изменений'
        Write-Host '  [3] Очистить старый NxHotkeys и внутренние конфликты'
        Write-Host '  [4] Восстановить UGII_CUSTOM_DIRECTORY_FILE'
        Write-Host '  [5] Полная очистка конфликтов и чистая переустановка'
        Write-Host '  [0] Выход'
        Write-Host ''
        $choice = Read-Host 'Выберите действие'
        switch ($choice.Trim()) {
            '1' {
                $conflicts = @(Get-NxKeysConflictInventory $ManagedRoot)
                if ($conflicts.Count -gt 0) {
                    Write-Warning "Перед установкой обнаружено конфликтов: $($conflicts.Count)"
                    if (Confirm-NxKeysAction 'Очистить их перед установкой?') {
                        $cleaned = Invoke-NxKeysConflictCleanup -ManagedRoot $ManagedRoot -AssumeYes
                        if (-not $cleaned) { continue }
                    }
                }
                return 'Install'
            }
            '2' { [void](Show-NxKeysConflictInventory $ManagedRoot); Read-Host 'Нажмите Enter для возврата в меню' | Out-Null }
            '3' { [void](Invoke-NxKeysConflictCleanup -ManagedRoot $ManagedRoot); Read-Host 'Нажмите Enter для возврата в меню' | Out-Null }
            '4' { Repair-NxKeysCustomDirsOnly -ManagedRoot $ManagedRoot; Read-Host 'Нажмите Enter для возврата в меню' | Out-Null }
            '5' { return 'CleanInstall' }
            '0' { return 'Exit' }
            default { Write-Warning 'Неизвестный пункт меню.'; Start-Sleep -Seconds 1 }
        }
    }
}


$maintenanceRoot = Resolve-NxKeysMaintenanceRoot $ConfigPath
if (-not $PSBoundParameters.ContainsKey('Mode') -and
    ($CompileOnly -or $Clean -or $NoBuild -or $AllowRunningNX -or $NoShortcut -or $NoGlobalDuplication -or
     -not [string]::IsNullOrWhiteSpace($ConfigPath) -or -not [string]::IsNullOrWhiteSpace($CatalogDir) -or
     -not [string]::IsNullOrWhiteSpace($NxRoot) -or -not [string]::IsNullOrWhiteSpace($NxOpenDll) -or
     -not [string]::IsNullOrWhiteSpace($OutputPath) -or $AutoCleanConflicts)) {
    $Mode = 'Install'
}
if ($Mode -eq 'Menu') {
    $inputRedirected = $false
    try { $inputRedirected = [Console]::IsInputRedirected } catch { }
    if ($inputRedirected -or -not [Environment]::UserInteractive) {
        Write-Warning 'Интерактивный ввод недоступен; выполняется обычная установка. Для обслуживания используйте -Mode Audit или -Mode CleanConflicts.'
        $Mode = 'Install'
    } else {
        $Mode = Show-NxKeysInstallerMenu $maintenanceRoot
    }
}

switch ($Mode) {
    'Exit' { Write-Host 'Выход без изменений.'; exit 0 }
    'Audit' { [void](Show-NxKeysConflictInventory $maintenanceRoot); exit 0 }
    'CleanConflicts' { [void](Invoke-NxKeysConflictCleanup -ManagedRoot $maintenanceRoot -AssumeYes:$Yes); exit 0 }
    'RepairCustomDirs' { Repair-NxKeysCustomDirsOnly -ManagedRoot $maintenanceRoot -AssumeYes:$Yes; exit 0 }
    'CleanInstall' {
        $cleanupCompleted = Invoke-NxKeysConflictCleanup -ManagedRoot $maintenanceRoot -AssumeYes:$Yes
        if (-not $cleanupCompleted) { Write-Host 'Переустановка отменена.' -ForegroundColor Yellow; exit 1 }
        $Clean = $true
        $Mode = 'Install'
    }
    'Install' {
        if ($AutoCleanConflicts) {
            $cleanupCompleted = Invoke-NxKeysConflictCleanup -ManagedRoot $maintenanceRoot -AssumeYes:$Yes
            if (-not $cleanupCompleted) { Write-Host 'Установка отменена.' -ForegroundColor Yellow; exit 1 }
        }
    }
}

$catalog = Resolve-Catalog $CatalogDir
$config = Resolve-Config -Requested $ConfigPath -ResolvedCatalog $catalog -RequestedOutput $OutputPath -DisableGlobalDuplication:$NoGlobalDuplication
$configJson = (Get-Content -LiteralPath $config) -join "`n" | ConvertFrom-Json
$schemaVersion = [int]$configJson.schema_version
if ($schemaVersion -lt 3 -or $schemaVersion -gt 8) { throw 'Для установки требуется schema_version от 3 до 8.' }

if ($schemaVersion -lt 8) {
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
} else {
    Write-Host "Профиль v8: обнаружено $($configJson.operations.Count) контрактов операций." -ForegroundColor Green
}

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
        $normalizedRoot = [System.IO.Path]::GetFullPath($TargetRoot).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)) + '\'
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
    Copy-Item -LiteralPath $config -Destination (Join-Path $staging 'nx2512-v8-profile.json') -Force

    # NXKeys.Protocol загружается лениво командой health, поэтому обычный запуск apply
    # может пройти даже при ошибочном publish-наборе. Добавляем DLL в staging заранее.
    $protocolSearchRoots = @(
        $hotkeyDist,
        (Join-Path $hotkeyProject 'bin\Release'),
        (Join-Path $ScriptDir 'NXKeys.Protocol\bin\Release'),
        (Join-Path $ScriptDir 'src\NXKeys.Protocol\bin\Release'),
        $ScriptDir
    )
    $stagedProtocol = Ensure-StagedRuntimeAssembly -AssemblyName 'NXKeys.Protocol' -StagingRoot $staging -SearchRoots $protocolSearchRoots

    $stagedExe = Join-Path $staging 'NX2512_HotkeyStudio.exe'
    $stagedConfig = Join-Path $staging 'nx2512-v8-profile.json'

    # Deploy Bridge DLLs (NXOpen plugin) directly to the managed application
    # directory BEFORE calling C# apply.  The apply command deploys MenuScript
    # files; Bridge binaries are deployed here so we can handle locked-DLL
    # errors with a clear message rather than a cryptic C# exception.
    $managedAppDir = Join-Path $managedRoot 'custom\application'
    New-Item -ItemType Directory -Force -Path $managedAppDir | Out-Null

    Write-Step 'Деплой Bridge DLL в managed application'
    $bridgeStaging = Join-Path $staging 'bridge'
    $bridgeDeployed = $true
    if (Test-Path -LiteralPath $bridgeStaging -PathType Container) {
        Get-ChildItem -LiteralPath $bridgeStaging -File | ForEach-Object {
            $dest = Join-Path $managedAppDir $_.Name
            try {
                Copy-Item -LiteralPath $_.FullName -Destination $dest -Force -ErrorAction Stop
                Write-Host "  OK: $($_.Name)"
            } catch [System.IO.IOException] {
                $bridgeDeployed = $false
                Write-Warning "Bridge DLL заблокирован (NX запущен?): $($_.Name)"
            }
        }
    }
    if (-not $bridgeDeployed) {
        Write-Warning 'Не все Bridge DLL скопированы. Закрой NX и повтори установку.'
    }

    # Also copy Protocol.dll to managed root (runtime dependency of HotkeyStudio).
    Copy-Item -LiteralPath $stagedProtocol -Destination (Join-Path $managedRoot (Split-Path $stagedProtocol -Leaf)) -Force -ErrorAction SilentlyContinue

    Write-Step "Транзакционная установка в $managedRoot"
    $applyArgs = @('apply', '--config', $stagedConfig, '--yes')
    if ($AllowRunningNX) { $applyArgs += '--allow-running-nx' }
    & $stagedExe @applyArgs
    if ($LASTEXITCODE -ne 0) { throw "C# deployment завершился с кодом $LASTEXITCODE." }

    $installedExe = Join-Path $managedRoot 'NX2512_HotkeyStudio.exe'
    $installedConfig = Join-Path $managedRoot 'nx2512-v8-profile.json'

    # Защита от дефекта package manifest: runtime-зависимость должна лежать рядом с EXE,
    # иначе CLR не сможет загрузить NXKeys.Protocol при выполнении команды health.
    Sync-InstalledRuntimeFile -SourcePath $stagedProtocol -DestinationRoot $managedRoot | Out-Null

    Write-Step 'Устранение двойной загрузки Bridge и legacy toolbar'
    if (@(Get-NxProcesses).Count -eq 0) {
        $postInstallBackup = Join-Path $env:LOCALAPPDATA ("NXKeys\conflict-backups\post-install-" + (Get-Date -Format 'yyyyMMdd_HHmmss'))
        New-Item -ItemType Directory -Force -Path $postInstallBackup | Out-Null
        [void](Remove-NxKeysInternalLayoutConflicts -ManagedRoot $managedRoot -BackupRoot $postInstallBackup)
    } else {
        Write-Warning 'NX продолжает работать: нормализация загруженных DLL отложена. После закрытия NX запустите -Mode CleanConflicts.'
    }

    Write-Step 'Проверка установленного package manifest'
    Invoke-NxKeysHealthCheck -Executable $installedExe -Config $installedConfig

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

    Write-Step 'Нормализация UGII_CUSTOM_DIRECTORY_FILE'
    $preservedCustomRoots = @(Get-NxKeysPreservedCustomRoots $managedRoot)
    [void](Set-NxKeysCanonicalCustomDirs -ManagedRoot $managedRoot -PreservedRoots $preservedCustomRoots)

    Write-Host "`nNXKeys Main K3–K5 Profile установлен успешно." -ForegroundColor Green
    Write-Host "Managed root: $managedRoot"
    Write-Host "Главный профиль K3–K5 установлен как: $(Join-Path $managedRoot 'nx2512-v8-profile.json')" -ForegroundColor Green
    Write-Host "Запуск NX: $(Join-Path $managedRoot 'launch-nx2512-with-nxkeys.cmd')" -ForegroundColor Yellow
}
finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue }
}
