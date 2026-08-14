<#
.SYNOPSIS
    Единый мультифункциональный скрипт управления NxEskd: сборка, проверка версий NX 2512,
    установка, удаление, unit- и smoke-тесты, валидация пакета и исправление кодировок.
.DESCRIPTION
    Объединяет весь функционал build.ps1, plan-package.ps1, fix-encodings.ps1 и nxeskd.ps1
    в один универсальный PowerShell скрипт с интерактивным меню.
.PARAMETER Action
    Действие: Menu (по умолчанию), AutoSetup, Build, Install, Uninstall, Test, PlanPackage, FixEncodings.
.EXAMPLE
    .\nxeskd.ps1                      # Интерактивное консольное меню
    .\nxeskd.ps1 -Action AutoSetup    # Полная сборка, установка и проверка в 1 клик
    .\nxeskd.ps1 -Action Build        # Полная сборка с валидацией версий NXOpen и тестами
    .\nxeskd.ps1 -Action Install      # Регистрация плагина в Siemens NX
    .\nxeskd.ps1 -Action Uninstall    # Безопасное удаление из Siemens NX
    .\nxeskd.ps1 -Action Test         # Запуск Unit- и Smoke-тестов
    .\nxeskd.ps1 -Action PlanPackage  # Проверка манифеста через PackagePlanner
    .\nxeskd.ps1 -Action FixEncodings # Конвертация меню и скриптов в UTF-8 BOM
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [ValidateSet('Menu', 'Build', 'Install', 'Uninstall', 'Test', 'PlanPackage', 'FixEncodings', 'AutoSetup')]
    [string]$Action = 'Menu',

    # ─── Build Options ──────────────────────────────────────────────
    [string]$NxRoot = $env:UGII_BASE_DIR,
    [int]$ExpectedNxRelease = 2512,
    [int]$ExpectedNxMaintenance = 0,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDirectory = '',
    [switch]$SkipNxVersionCheck,
    [switch]$SkipTests,
    [switch]$NoZip,

    # ─── Install Options ─────────────────────────────────────────────
    [string]$PackagePath = '',
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'NxEskdGenerator\install'),
    [switch]$Force,

    # ─── Uninstall Options ───────────────────────────────────────────
    [switch]$KeepProfiles,
    [switch]$RemoveProfiles,

    # ─── Test & Profile Options ──────────────────────────────────────
    [Alias('Profile')]
    [string]$ProfilePath = '',

    # ─── PlanPackage Options ─────────────────────────────────────────
    [string]$ManifestPath = '',
    [string]$ReportPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectRoot = Split-Path -Parent $PSScriptRoot

# UTF-8 без BOM — совместимо с PowerShell 5 и 7
$script:Utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Set-Utf8NoBomContent([string]$Path, [string]$Value) {
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [IO.File]::WriteAllText($Path, $Value, $script:Utf8NoBom)
}

function Write-JsonNoBom([string]$Path, [object]$Value) {
    $directory = Split-Path -Parent $Path
    if (-not (Test-Path $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    $json = $Value | ConvertTo-Json -Depth 12
    [IO.File]::WriteAllText($Path, $json, $script:Utf8NoBom)
}

# ═════════════════════════════════════════════════════════════════════
#  Общие утилиты
# ═════════════════════════════════════════════════════════════════════

function Get-NormalizedPath([string]$Path) {
    return [IO.Path]::GetFullPath($Path).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar
    )
}

function Test-IsSameOrNestedPath([string]$Left, [string]$Right) {
    $leftPath  = Get-NormalizedPath $Left
    $rightPath = Get-NormalizedPath $Right
    if ($leftPath.Equals($rightPath, [StringComparison]::OrdinalIgnoreCase)) { return $true }
    $leftPrefix  = $leftPath  + [IO.Path]::DirectorySeparatorChar
    $rightPrefix = $rightPath + [IO.Path]::DirectorySeparatorChar
    return $leftPath.StartsWith($rightPrefix, [StringComparison]::OrdinalIgnoreCase) -or
           $rightPath.StartsWith($leftPrefix, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-PathInside([string]$Root, [string]$Candidate, [string]$Description) {
    $rootPath      = (Get-NormalizedPath $Root) + [IO.Path]::DirectorySeparatorChar
    $candidatePath = [IO.Path]::GetFullPath($Candidate)
    if (-not $candidatePath.StartsWith($rootPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description выходит за пределы каталога пакета: $candidatePath"
    }
    return $candidatePath
}

function Write-AtomicLines([string]$Path, [string[]]$Lines) {
    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $temp = Join-Path $directory ('.' + [IO.Path]::GetFileName($Path) + '.' +
            [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [IO.File]::WriteAllLines($temp, $Lines, $script:Utf8NoBom)
        Move-Item $temp $Path -Force
    }
    finally {
        if (Test-Path $temp) { Remove-Item $temp -Force -ErrorAction SilentlyContinue }
    }
}

function Assert-LastExitCode([string]$Operation) {
    if ($LASTEXITCODE -ne 0) { throw "$Operation завершилась с кодом $LASTEXITCODE." }
}

function Write-Banner([string]$Text) {
    $line = '─' * 60
    Write-Host ''
    Write-Host $line -ForegroundColor DarkCyan
    Write-Host "  $Text" -ForegroundColor Cyan
    Write-Host $line -ForegroundColor DarkCyan
    Write-Host ''
}

function Write-Success([string]$Text) {
    Write-Host "  ✓ $Text" -ForegroundColor Green
}

function Write-Info([string]$Text) {
    Write-Host "  ℹ $Text" -ForegroundColor DarkGray
}

# ═════════════════════════════════════════════════════════════════════
#  1. FIX ENCODINGS (UTF-8 with BOM)
# ═════════════════════════════════════════════════════════════════════

function Invoke-FixEncodings {
    Write-Banner 'ИСПРАВЛЕНИЕ КОДИРОВОК (UTF-8 WITH BOM)'
    $utf8Bom = New-Object System.Text.UTF8Encoding($true)

    # 1. Ribbon Definition
    $rtbPath = Join-Path $ProjectRoot 'application\profiles\Default\rbn_nxeskd.rtb'
    if (Test-Path $rtbPath) {
        $rtbContent = [IO.File]::ReadAllText((Resolve-Path $rtbPath).Path)
        [IO.File]::WriteAllText((Resolve-Path $rtbPath).Path, $rtbContent, $utf8Bom)
        Write-Success 'Обновлен файл ленты: rbn_nxeskd.rtb'
    }

    # 2. MenuScript Overlay
    $menPath = Join-Path $ProjectRoot 'startup\nx_eskd.men'
    if (Test-Path $menPath) {
        $menContent = [IO.File]::ReadAllText((Resolve-Path $menPath).Path)
        [IO.File]::WriteAllText((Resolve-Path $menPath).Path, $menContent, $utf8Bom)
        Write-Success 'Обновлен файл меню: nx_eskd.men'
    }

    # 3. PowerShell Scripts
    $scriptsDir = Join-Path $ProjectRoot 'scripts'
    if (Test-Path $scriptsDir) {
        Get-ChildItem -Path $scriptsDir -Filter '*.ps1' | ForEach-Object {
            $content = [IO.File]::ReadAllText($_.FullName)
            [IO.File]::WriteAllText($_.FullName, $content, $utf8Bom)
            Write-Success "Переведен в UTF-8 BOM: $($_.Name)"
        }
    }
}

# ═════════════════════════════════════════════════════════════════════
#  2. PLAN PACKAGE (NxEskd.PackagePlanner)
# ═════════════════════════════════════════════════════════════════════

function Invoke-PlanPackage {
    Write-Banner 'ВАЛИДАЦИЯ ПАКЕТА (NxEskd.PackagePlanner)'

    $targetManifest = $ManifestPath
    if ([string]::IsNullOrWhiteSpace($targetManifest)) {
        $distManifest = Join-Path $ProjectRoot 'dist\NxEskd\manifest.sha256.json'
        if (Test-Path $distManifest -PathType Leaf) {
            $targetManifest = $distManifest
        }
        else {
            $sourceManifest = Join-Path $ProjectRoot 'source-manifest.sha256.json'
            if (Test-Path $sourceManifest -PathType Leaf) {
                $targetManifest = $sourceManifest
            }
            else {
                throw 'Не указан -ManifestPath и не найден манифест по умолчанию (dist\NxEskd\manifest.sha256.json).'
            }
        }
    }

    $manifestPath = [IO.Path]::GetFullPath($targetManifest)
    if (-not (Test-Path $manifestPath -PathType Leaf)) {
        throw "Манифест комплекта не найден: $manifestPath"
    }

    $packagedPlanner = Join-Path $ProjectRoot 'tools\package-planner\NxEskd.PackagePlanner.dll'
    $distPlanner = Join-Path $ProjectRoot 'dist\NxEskd\tools\package-planner\NxEskd.PackagePlanner.dll'
    $sourceProject = Join-Path $ProjectRoot 'tools\NxEskd.PackagePlanner\NxEskd.PackagePlanner.csproj'

    $plannerDll = if (Test-Path $distPlanner -PathType Leaf) { $distPlanner }
                 elseif (Test-Path $packagedPlanner -PathType Leaf) { $packagedPlanner }
                 else { $null }

    Write-Info "Манифест: $manifestPath"
    $temporary = Join-Path ([IO.Path]::GetTempPath()) ("nx-eskd-package-plan-" + [Guid]::NewGuid().ToString('N') + '.json')
    try {
        if ($null -ne $plannerDll) {
            & dotnet $plannerDll $manifestPath | Set-Content -Path $temporary -Encoding utf8
        }
        elseif (Test-Path $sourceProject -PathType Leaf) {
            & dotnet run --project $sourceProject --configuration $Configuration -- $manifestPath | Set-Content -Path $temporary -Encoding utf8
        }
        else {
            throw 'Не найден NxEskd.PackagePlanner ни в пакете, ни в исходном дереве.'
        }
        $exitCode = $LASTEXITCODE

        $json = Get-Content $temporary -Raw
        if ([string]::IsNullOrWhiteSpace($ReportPath)) {
            $json | Write-Output
        }
        else {
            $target = [IO.Path]::GetFullPath($ReportPath)
            $directory = Split-Path -Parent $target
            if (-not (Test-Path $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
            [IO.File]::WriteAllText($target, $json, $script:Utf8NoBom)
            Write-Success "Отчёт комплекта сохранён: $target"
        }

        if ($exitCode -ne 0) {
            throw "План комплекта содержит блокирующие ошибки. Код NxEskd.PackagePlanner: $exitCode"
        }
        Write-Success 'Манифест успешно проверен.'
    }
    finally {
        if (Test-Path $temporary) { Remove-Item $temporary -Force -ErrorAction SilentlyContinue }
    }
}

# ═════════════════════════════════════════════════════════════════════
#  3. TEST (Unit + Smoke Tests)
# ═════════════════════════════════════════════════════════════════════

function Invoke-Test {
    Write-Banner 'ТЕСТИРОВАНИЕ NxEskd'

    Write-Info 'Запуск Unit-тестов (NxEskd.Core.Tests)...'
    & dotnet test (Join-Path $ProjectRoot 'tests\NxEskd.Core.Tests\NxEskd.Core.Tests.csproj') `
        --configuration $Configuration `
        --nologo
    Assert-LastExitCode 'Unit tests'
    Write-Success 'Unit-тесты успешно пройдены.'

    if ([string]::IsNullOrWhiteSpace($ProfilePath)) {
        $script:ProfilePath = Join-Path $ProjectRoot 'config\active-profile.example.json'
    }

    Write-Info "Запуск Smoke-тестов с профилем: $ProfilePath..."
    & dotnet run --project (Join-Path $ProjectRoot 'tests\NxEskd.SmokeTests\NxEskd.SmokeTests.csproj') `
        --configuration $Configuration `
        -- $ProfilePath
    Assert-LastExitCode 'Smoke tests'
    Write-Success 'Smoke-тесты успешно пройдены.'
}

# ═════════════════════════════════════════════════════════════════════
#  4. BUILD (Complete Build + Assembly Verification + Packaging)
# ═════════════════════════════════════════════════════════════════════

function Get-AssemblyEvidence([string]$Path) {
    $file = Get-Item $Path
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($file.FullName)
    $assemblyName = [Reflection.AssemblyName]::GetAssemblyName($file.FullName)
    $tokenBytes = $assemblyName.GetPublicKeyToken()
    $token = if ($null -eq $tokenBytes -or $tokenBytes.Length -eq 0) { '' }
    else { -join ($tokenBytes | ForEach-Object { $_.ToString('x2') }) }
    [PSCustomObject]@{
        name            = $file.Name
        path            = $file.FullName
        size            = $file.Length
        fileVersion     = $version.FileVersion
        productVersion  = $version.ProductVersion
        assemblyVersion = $assemblyName.Version.ToString()
        publicKeyToken  = $token
        sha256          = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function Assert-VersionEvidence($Evidence, [int]$Release, [int]$Maintenance) {
    $combined = @(
        $Evidence.fileVersion,
        $Evidence.productVersion,
        $Evidence.assemblyVersion
    ) -join ' '
    if ($combined -notmatch "(?<!\d)$Release(?!\d)") {
        throw "$($Evidence.name): в версиях не найден ожидаемый release $Release. Получено: $combined"
    }
    if ($Maintenance -gt 0 -and $combined -notmatch "(?<!\d)$Maintenance(?!\d)") {
        throw "$($Evidence.name): в FileVersion/ProductVersion не найден maintenance $Maintenance. Получено: $combined"
    }
}

function Invoke-Build {
    Write-Banner 'СБОРКА И УПАКОВКА NxEskd'

    if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
        $script:OutputDirectory = Join-Path $ProjectRoot 'dist'
    }
    $script:OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)

    # ── Resolve NX managed ──
    function Resolve-NxManagedDir([string]$Root) {
        $roots = @()
        if (-not [string]::IsNullOrWhiteSpace($Root))              { $roots += [IO.Path]::GetFullPath($Root) }
        if (-not [string]::IsNullOrWhiteSpace($env:UGII_BASE_DIR)) { $roots += [IO.Path]::GetFullPath($env:UGII_BASE_DIR) }

        $searchDirs = @(
            'C:\Program Files\Siemens\DesigncenterNX2512',
            'C:\Program Files\Siemens\NX2512',
            'C:\Program Files\Siemens\DesigncenterNX*',
            'C:\Program Files\Siemens\NX*'
        )
        foreach ($dirPattern in $searchDirs) {
            Get-Item $dirPattern -ErrorAction SilentlyContinue | ForEach-Object { $roots += $_.FullName }
        }

        $candidates = foreach ($candidateRoot in ($roots | Select-Object -Unique)) {
            Join-Path $candidateRoot 'NXBIN\managed_core'
            Join-Path $candidateRoot 'UGII\managed_core'
            Join-Path $candidateRoot 'managed_core'
            Join-Path $candidateRoot 'NXBIN\managed'
            Join-Path $candidateRoot 'UGII\managed'
            Join-Path $candidateRoot 'managed'
        }
        foreach ($candidate in $candidates) {
            if ((Test-Path (Join-Path $candidate 'NXOpen.dll') -PathType Leaf) -and
                (Test-Path (Join-Path $candidate 'NXOpen.UF.dll') -PathType Leaf)) {
                return (Resolve-Path $candidate).Path
            }
        }
        throw 'Не найдены NXOpen.dll и NXOpen.UF.dll. Передайте -NxRoot с каталогом установленной NX 2512.'
    }

    # ── Check .NET SDK ──
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) { throw 'Не установлен .NET SDK 8.' }
    $dotnetVersion = (& dotnet --version).Trim()
    if (-not $dotnetVersion.StartsWith('8.')) { throw "Требуется .NET SDK 8; обнаружен $dotnetVersion." }

    $managedDir = Resolve-NxManagedDir $NxRoot
    $nxOpen     = Get-AssemblyEvidence (Join-Path $managedDir 'NXOpen.dll')
    $nxUf       = Get-AssemblyEvidence (Join-Path $managedDir 'NXOpen.UF.dll')

    Write-Info "NX Managed Dir: $managedDir"
    Write-Info "NXOpen.dll:     $($nxOpen.fileVersion)"
    Write-Info "NXOpen.UF.dll:  $($nxUf.fileVersion)"
    Write-Info ".NET SDK:       $dotnetVersion"

    if (-not $SkipNxVersionCheck) {
        Assert-VersionEvidence $nxOpen $ExpectedNxRelease $ExpectedNxMaintenance
        Assert-VersionEvidence $nxUf $ExpectedNxRelease $ExpectedNxMaintenance
        if ([string]::IsNullOrWhiteSpace($nxOpen.publicKeyToken) -and [string]::IsNullOrWhiteSpace($nxUf.publicKeyToken)) {
            Write-Info 'Сборки NX Open верифицированы по версии (Public Key Token отсутствует).'
        }
        elseif (-not [string]::IsNullOrWhiteSpace($nxOpen.publicKeyToken) -and -not [string]::IsNullOrWhiteSpace($nxUf.publicKeyToken) -and -not $nxOpen.publicKeyToken.Equals($nxUf.publicKeyToken, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Public Key Token для NXOpen и NXOpen.UF различается: $($nxOpen.publicKeyToken) / $($nxUf.publicKeyToken)"
        }
        else {
            Write-Success 'Версии и Public Key Token сборок NX Open верифицированы.'
        }
    }

    # ── Unit & Smoke Tests ──
    if (-not $SkipTests) {
        Invoke-Test
    }

    # ── Clean check: очистить bin/obj если NxOpenManagedDir изменился ──
    $prevBuildInfo = Join-Path $OutputDirectory 'NxEskd\build-info.json'
    $needsClean    = $false
    $cleanReason   = ''

    if (Test-Path $prevBuildInfo -PathType Leaf) {
        try {
            $prev = Get-Content $prevBuildInfo -Raw | ConvertFrom-Json
            $prevDir = [string]$prev.nxManagedDirectory
            if (-not $prevDir.Equals($managedDir, [StringComparison]::OrdinalIgnoreCase)) {
                $needsClean  = $true
                $cleanReason = "NxOpenManagedDir изменился:`n    было: $prevDir`n    стало: $managedDir"
            }
            elseif ($prevDir -notmatch 'managed_core' -and $managedDir -match 'managed_core') {
                $needsClean  = $true
                $cleanReason = "Обнаружен переход с managed → managed_core."
            }
        }
        catch {
            Write-Info "Не удалось прочитать предыдущий build-info.json: $_"
        }
    }

    if ($Force) {
        $needsClean  = $true
        $cleanReason = "Принудительная очистка (-Force)."
    }

    if ($needsClean) {
        Write-Host ''
        Write-Host "  ⚠  ОЧИСТКА bin/obj: $cleanReason" -ForegroundColor Yellow
        Write-Info 'Выполняется dotnet clean...'
        & dotnet clean (Join-Path $ProjectRoot 'NxEskdDrawingAutomation.sln') `
            --configuration $Configuration `
            --nologo
        Assert-LastExitCode 'dotnet clean'
        Write-Success 'Очистка выполнена.'
    }
    else {
        Write-Info "NxOpenManagedDir не изменился ($managedDir) — очистка не нужна."
    }

    # ── Solution Build ──
    Write-Info 'Сборка C# решения...'
    & dotnet build (Join-Path $ProjectRoot 'NxEskdDrawingAutomation.sln') `
        --configuration $Configuration `
        --nologo `
        -p:NxOpenManagedDir="$managedDir"
    Assert-LastExitCode 'Сборка решения'
    Write-Success 'Сборка решения успешно завершена.'

    # ── Package Staging ──
    $package = Join-Path $OutputDirectory 'NxEskd'
    $staging = Join-Path $OutputDirectory ('.NxEskd.staging.' + [Guid]::NewGuid().ToString('N'))
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }

    $dirs = @('application', 'bin', 'config', 'startup', 'docs', 'scripts', 'templates', 'reports', 'tools')
    foreach ($dir in $dirs) { New-Item -ItemType Directory -Path (Join-Path $staging $dir) -Force | Out-Null }

    $runtimeOut = Join-Path $ProjectRoot "src\NxEskd.NxRuntime\bin\$Configuration\net8.0-windows"
    Copy-Item (Join-Path $runtimeOut '*') (Join-Path $staging 'application') -Recurse -Force

    if (Test-Path (Join-Path $ProjectRoot 'src\NxEskd.Commands')) {
        foreach ($command in @('CommandCenter', 'Generate', 'Update', 'Validate', 'Preview', 'Inventory')) {
            $commandOut = Join-Path $ProjectRoot "src\NxEskd.Commands\$command\bin\$Configuration\net8.0-windows"
            if (Test-Path $commandOut) {
                Copy-Item (Join-Path $commandOut 'NxEskd.*') (Join-Path $staging 'application') -Force
            }
        }
    }
    if (Test-Path (Join-Path $ProjectRoot 'application')) {
        Copy-Item (Join-Path $ProjectRoot 'application\*') (Join-Path $staging 'application') -Recurse -Force
    }

    $configOut = Join-Path $ProjectRoot "src\NxEskd.Configurator\bin\$Configuration\net8.0-windows"
    Copy-Item (Join-Path $configOut '*') (Join-Path $staging 'bin') -Recurse -Force
    Copy-Item (Join-Path $ProjectRoot 'config\*')    (Join-Path $staging 'config')    -Recurse -Force
    Copy-Item (Join-Path $ProjectRoot 'startup\*')   (Join-Path $staging 'startup')   -Recurse -Force
    Copy-Item (Join-Path $ProjectRoot 'docs\*')      (Join-Path $staging 'docs')      -Recurse -Force
    Copy-Item (Join-Path $ProjectRoot 'templates\*') (Join-Path $staging 'templates') -Recurse -Force
    Copy-Item (Join-Path $ProjectRoot 'scripts\*')   (Join-Path $staging 'scripts')   -Recurse -Force
    Copy-Item (Join-Path $ProjectRoot 'README.md')   $staging -Force
    Copy-Item (Join-Path $ProjectRoot 'LICENSE')     $staging -Force
    Copy-Item (Join-Path $ProjectRoot 'config\active-profile.example.json') `
              (Join-Path $staging 'config\active-profile.json') -Force

    # ── Stage PackagePlanner tool ──
    $plannerOutput = Join-Path $ProjectRoot "tools\NxEskd.PackagePlanner\bin\$Configuration\net8.0"
    if (Test-Path (Join-Path $plannerOutput 'NxEskd.PackagePlanner.dll') -PathType Leaf) {
        $packagedPlanner = Join-Path $staging 'tools\package-planner'
        New-Item -ItemType Directory -Path $packagedPlanner -Force | Out-Null
        Copy-Item (Join-Path $plannerOutput '*') $packagedPlanner -Recurse -Force
    }

    # ── Write build-info.json ──
    $buildInfo = [ordered]@{
        schemaVersion         = 1
        createdUtc            = [DateTimeOffset]::UtcNow.ToString('O')
        verified              = -not [bool]$SkipNxVersionCheck
        expectedNxRelease     = $ExpectedNxRelease
        expectedNxMaintenance = $ExpectedNxMaintenance
        configuration         = $Configuration
        platformTarget        = 'x64'
        dotnetSdk             = $dotnetVersion
        nxRoot                = [IO.Path]::GetFullPath($NxRoot)
        nxManagedDirectory    = $managedDir
        assemblies            = @($nxOpen, $nxUf)
    }
    Write-JsonNoBom (Join-Path $staging 'build-info.json') $buildInfo

    # ── Verify no forbidden Siemens assemblies ──
    $forbidden = Get-ChildItem $staging -Recurse -File | Where-Object {
        $_.Name -in @('NXOpen.dll', 'NXOpen.UF.dll', 'NXOpenUI.dll', 'NXOpen.Utilities.dll')
    }
    if ($forbidden) {
        throw 'Пакет содержит запрещённые закрытые сборки Siemens: ' + (($forbidden.FullName) -join ', ')
    }

    # ── Generate manifest.sha256.json ──
    $manifest = Get-ChildItem $staging -Recurse -File |
        Where-Object { $_.Name -ne 'manifest.sha256.json' } |
        Sort-Object FullName |
        ForEach-Object {
            [PSCustomObject]@{
                path   = $_.FullName.Substring($staging.Length + 1)
                sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                size   = $_.Length
            }
        }
    Write-JsonNoBom (Join-Path $staging 'manifest.sha256.json') $manifest

    if (Test-Path $package) { Remove-Item $package -Recurse -Force }
    Move-Item $staging $package

    # ── Create Zip archive ──
    $versionFile = Join-Path $ProjectRoot 'src\NxEskd.NxRuntime\BuildInfo.cs'
    $versionMatch = Select-String -Path $versionFile -Pattern 'Version\s*=\s*"([^"]+)"' | Select-Object -First 1
    $version = if ($versionMatch) { $versionMatch.Matches[0].Groups[1].Value } else { '0.0.0' }

    if (-not $NoZip) {
        $zipPath = Join-Path $OutputDirectory "NxEskd-NX2512-v$version.zip"
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
        Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zipPath -CompressionLevel Optimal
        $zipHash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
        [IO.File]::WriteAllText(
            $zipPath + '.sha256',
            "$zipHash  $([IO.Path]::GetFileName($zipPath))`r`n",
            [Text.Encoding]::ASCII)
        Write-Success "ZIP-архив пакета: $zipPath"
    }

    Write-Success "Сформирован каталог пакета: $package"
}

# ═════════════════════════════════════════════════════════════════════
#  5. INSTALL
# ═════════════════════════════════════════════════════════════════════

function Invoke-Install {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    Write-Banner 'УСТАНОВКА И РЕГИСТРАЦИЯ NxEskd В NX'

    if ([string]::IsNullOrWhiteSpace($PackagePath)) {
        $distPkg = Join-Path $ProjectRoot 'dist\NxEskd'
        if (Test-Path (Join-Path $distPkg 'startup\nx_eskd.men') -PathType Leaf) {
            $script:PackagePath = $distPkg
            Write-Info "Пакет найден: $distPkg"
        }
        elseif (Test-Path (Join-Path $ProjectRoot 'startup\nx_eskd.men') -PathType Leaf) {
            $script:PackagePath = $ProjectRoot
        }
        else {
            throw @"
Не удалось определить каталог пакета.
  — Выполните сборку (Build) или передайте -PackagePath явно.
  — Ожидается: dist\NxEskd или корень распакованного пакета.
"@
        }
    }

    $resolvedPackage = Get-NormalizedPath (Resolve-Path $PackagePath).Path
    $resolvedInstall = Get-NormalizedPath $InstallRoot

    if (Test-IsSameOrNestedPath $resolvedPackage $resolvedInstall) {
        throw 'PackagePath и InstallRoot не должны совпадать или быть вложены друг в друга.'
    }
    if (-not (Test-Path (Join-Path $resolvedPackage 'startup\nx_eskd.men') -PathType Leaf)) {
        throw "Каталог не похож на пакет NxEskd: $resolvedPackage"
    }

    $stateRoot = Join-Path $env:LOCALAPPDATA 'NxEskdGenerator'
    $lockPath  = Join-Path $stateRoot 'install.lock'
    New-Item -ItemType Directory -Path $stateRoot -Force | Out-Null
    $lock = [IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None')
    $staging            = $null
    $previousInstallTemp = $null
    $committed          = $false
    $previousNxRoot     = [Environment]::GetEnvironmentVariable('NX_ESKD_ROOT', 'User')
    $previousCustomFile = [Environment]::GetEnvironmentVariable('UGII_CUSTOM_DIRECTORY_FILE', 'User')
    $customFileBackup   = $null

    try {
        # ── Manifest verification ──
        $manifestPath = Join-Path $resolvedPackage 'manifest.sha256.json'
        if (Test-Path $manifestPath -PathType Leaf) {
            Write-Info 'Проверка целостности manifest.sha256.json...'
            $entries = Get-Content $manifestPath -Raw | ConvertFrom-Json
            if ($entries.Count -eq 0) { throw 'Manifest пакета пуст.' }
            foreach ($entry in $entries) {
                if ([string]::IsNullOrWhiteSpace([string]$entry.path) -or
                    [IO.Path]::IsPathRooted([string]$entry.path)) {
                    throw "Недопустимый путь в manifest: $($entry.path)"
                }
                $file = Assert-PathInside $resolvedPackage `
                    (Join-Path $resolvedPackage ([string]$entry.path)) 'Путь manifest'
                if (-not (Test-Path $file -PathType Leaf)) {
                    throw "В пакете отсутствует файл $($entry.path)."
                }
                if ($null -ne $entry.size -and (Get-Item $file).Length -ne [long]$entry.size) {
                    throw "Размер файла не соответствует manifest: $($entry.path)."
                }
                if ((Get-FileHash $file -Algorithm SHA256).Hash -ne [string]$entry.sha256) {
                    throw "Нарушена контрольная сумма $($entry.path)."
                }
            }
            Write-Success 'Целостность манифеста подтверждена.'
        }
        elseif (-not $Force) {
            throw @"
В пакете отсутствует manifest.sha256.json.
  — Выполните сборку (Build), чтобы пакет содержал manifest.
  — Или используйте -Force для диагностической установки без проверки целостности.
"@
        }
        else {
            Write-Host '  ⚠ Установка без manifest (-Force).' -ForegroundColor Yellow
        }

        $stamp     = Get-Date -Format 'yyyyMMdd-HHmmss'
        $backupRoot = Join-Path $stateRoot "backups\$stamp"
        $staging   = Join-Path $stateRoot ('.install-staging-' + [Guid]::NewGuid().ToString('N'))
        $previousInstallTemp = Join-Path $stateRoot ('.previous-install-' + [Guid]::NewGuid().ToString('N'))

        New-Item -ItemType Directory -Path $staging -Force | Out-Null
        Copy-Item (Join-Path $resolvedPackage '*') $staging -Recurse -Force

        if (-not (Test-Path (Join-Path $staging 'startup\nx_eskd.men') -PathType Leaf)) {
            throw 'Staging-пакет поврежден: отсутствует startup\nx_eskd.men.'
        }

        if (-not $PSCmdlet.ShouldProcess($resolvedInstall, 'Установить или обновить NxEskd')) { return }

        New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
        @{
            previousNxEskdRoot           = $previousNxRoot
            previousCustomDirectoryFile  = $previousCustomFile
            installRoot                  = $resolvedInstall
            installedAt                  = (Get-Date).ToString('O')
        } | ConvertTo-Json | ForEach-Object {
            Set-Utf8NoBomContent (Join-Path $backupRoot 'install-state.json') $_
        }

        if (Test-Path $resolvedInstall) {
            Move-Item $resolvedInstall $previousInstallTemp
        }

        try {
            Move-Item $staging $resolvedInstall
            $staging = $null

            $userProfileDir = Join-Path $stateRoot 'profiles'
            New-Item -ItemType Directory -Path $userProfileDir -Force | Out-Null
            $activeProfile = Join-Path $userProfileDir 'active-profile.json'
            if (-not (Test-Path $activeProfile)) {
                Copy-Item (Join-Path $resolvedInstall 'config\active-profile.example.json') $activeProfile
            }

            [Environment]::SetEnvironmentVariable('NX_ESKD_ROOT', $resolvedInstall, 'User')
            $env:NX_ESKD_ROOT = $resolvedInstall

            $customFile = $previousCustomFile
            if ([string]::IsNullOrWhiteSpace($customFile)) {
                $customFile = Join-Path $stateRoot 'custom_dirs.dat'
                [Environment]::SetEnvironmentVariable('UGII_CUSTOM_DIRECTORY_FILE', $customFile, 'User')
            }
            $customFile = [IO.Path]::GetFullPath($customFile)
            New-Item -ItemType Directory -Path (Split-Path -Parent $customFile) -Force | Out-Null
            if (Test-Path $customFile) {
                $customFileBackup = Join-Path $backupRoot 'custom_dirs.dat.bak'
                Copy-Item $customFile $customFileBackup -Force
            }

            $lines = if (Test-Path $customFile) { @(Get-Content $customFile) } else { @() }
            $normalizedInstall = Get-NormalizedPath $resolvedInstall
            $filtered = @($lines | Where-Object {
                if ([string]::IsNullOrWhiteSpace($_)) { return $false }
                try { -not (Get-NormalizedPath $_).Equals($normalizedInstall, [StringComparison]::OrdinalIgnoreCase) }
                catch { $true }
            })
            $filtered += $resolvedInstall
            Write-AtomicLines $customFile $filtered

            if (Test-Path $previousInstallTemp) {
                Move-Item $previousInstallTemp (Join-Path $backupRoot 'previous-install')
                $previousInstallTemp = $null
            }

            $committed = $true
            Write-Success "Установлено: $resolvedInstall"
            Write-Info   "Активный профиль: $activeProfile"
            Write-Info   "UGII_CUSTOM_DIRECTORY_FILE: $customFile"
            Write-Info   "Резервная копия: $backupRoot"
            Write-Host ''
            Write-Host '  Перезапустите Siemens NX 2512.' -ForegroundColor Yellow
        }
        catch {
            if (Test-Path $resolvedInstall) {
                Remove-Item $resolvedInstall -Recurse -Force -ErrorAction SilentlyContinue
            }
            if ($previousInstallTemp -and (Test-Path $previousInstallTemp)) {
                Move-Item $previousInstallTemp $resolvedInstall -Force
                $previousInstallTemp = $null
            }
            [Environment]::SetEnvironmentVariable('NX_ESKD_ROOT', $previousNxRoot, 'User')
            [Environment]::SetEnvironmentVariable('UGII_CUSTOM_DIRECTORY_FILE', $previousCustomFile, 'User')
            if ($customFileBackup -and $previousCustomFile) {
                Copy-Item $customFileBackup $previousCustomFile -Force -ErrorAction SilentlyContinue
            }
            throw
        }
    }
    finally {
        if ($staging -and (Test-Path $staging)) {
            Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
        }
        if (-not $committed -and $previousInstallTemp -and (Test-Path $previousInstallTemp)) {
            if (-not (Test-Path $resolvedInstall)) {
                Move-Item $previousInstallTemp $resolvedInstall -Force -ErrorAction SilentlyContinue
            }
        }
        $lock.Dispose()
    }
}

# ═════════════════════════════════════════════════════════════════════
#  6. UNINSTALL
# ═════════════════════════════════════════════════════════════════════

function Invoke-Uninstall {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    Write-Banner 'УДАЛЕНИЕ NxEskd'

    $resolvedInstall = Get-NormalizedPath $InstallRoot
    $stateRoot = Join-Path $env:LOCALAPPDATA 'NxEskdGenerator'
    $lockPath  = Join-Path $stateRoot 'install.lock'
    New-Item -ItemType Directory -Path $stateRoot -Force | Out-Null
    $lock = [IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None')

    try {
        if (-not $PSCmdlet.ShouldProcess($resolvedInstall, 'Удалить NxEskd')) { return }

        $backupRoot = Join-Path $stateRoot ('backups\uninstall-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
        New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null

        $previousState = Get-ChildItem (Join-Path $stateRoot 'backups') `
            -Filter install-state.json -Recurse -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc -Descending |
            ForEach-Object {
                try { Get-Content $_.FullName -Raw | ConvertFrom-Json } catch { $null }
            } |
            Where-Object {
                $_ -and $_.installRoot -and
                (Get-NormalizedPath $_.installRoot).Equals($resolvedInstall, [StringComparison]::OrdinalIgnoreCase)
            } |
            Select-Object -First 1

        $customFile = [Environment]::GetEnvironmentVariable('UGII_CUSTOM_DIRECTORY_FILE', 'User')
        if (-not [string]::IsNullOrWhiteSpace($customFile) -and (Test-Path $customFile -PathType Leaf)) {
            Copy-Item $customFile (Join-Path $backupRoot 'custom_dirs.dat.bak') -Force
            $remaining = @()
            foreach ($line in @(Get-Content $customFile)) {
                if ([string]::IsNullOrWhiteSpace($line)) { continue }
                $isInstall = $false
                try {
                    $isInstall = (Get-NormalizedPath $line).Equals(
                        $resolvedInstall, [StringComparison]::OrdinalIgnoreCase)
                } catch { }
                if (-not $isInstall) { $remaining += $line }
            }
            Write-AtomicLines $customFile $remaining
        }

        if (Test-Path $resolvedInstall) {
            Move-Item $resolvedInstall (Join-Path $backupRoot 'removed-install')
        }

        if ($previousState) {
            [Environment]::SetEnvironmentVariable('NX_ESKD_ROOT',
                [string]$previousState.previousNxEskdRoot, 'User')
            [Environment]::SetEnvironmentVariable('UGII_CUSTOM_DIRECTORY_FILE',
                [string]$previousState.previousCustomDirectoryFile, 'User')
        }
        else {
            $currentNxRoot = [Environment]::GetEnvironmentVariable('NX_ESKD_ROOT', 'User')
            if ($currentNxRoot -and
                (Get-NormalizedPath $currentNxRoot).Equals($resolvedInstall, [StringComparison]::OrdinalIgnoreCase)) {
                [Environment]::SetEnvironmentVariable('NX_ESKD_ROOT', $null, 'User')
            }
        }

        $profiles = Join-Path $stateRoot 'profiles'
        $deleteProfiles = $RemoveProfiles -and -not $KeepProfiles
        if ($deleteProfiles -and (Test-Path $profiles)) {
            Copy-Item $profiles (Join-Path $backupRoot 'profiles') -Recurse -Force
            Remove-Item $profiles -Recurse -Force
        }

        Write-Success 'NxEskd успешно удален.'
        Write-Info   "Резервная копия сохранена в: $backupRoot"
        if (-not $deleteProfiles) {
            Write-Info 'Профили сохранены. Для удаления используйте -RemoveProfiles.'
        }
        Write-Host ''
        Write-Host '  Перезапустите Siemens NX.' -ForegroundColor Yellow
    }
    finally {
        $lock.Dispose()
    }
}

# ═════════════════════════════════════════════════════════════════════
#  7. AUTO-SETUP (Полная автонастройка)
# ═════════════════════════════════════════════════════════════════════

function Invoke-AutoSetup {
    Write-Banner 'ПОЛНАЯ АВТОНАСТРОЙКА NxEskd И NX (В 1 клик)'

    Write-Info 'Шаг 1/4: Исправление кодировок меню и скриптов...'
    Invoke-FixEncodings

    Write-Info 'Шаг 2/4: Сборка плагина, проверка версий и тесты...'
    Invoke-Build

    Write-Info 'Шаг 3/4: Установка и регистрация в окружении NX...'
    $script:PackagePath = Join-Path $ProjectRoot 'dist\NxEskd'
    Invoke-Install

    Write-Banner 'ПРОВЕРКА И ДИАГНОСТИКА ОКРУЖЕНИЯ NX'

    $nxEskdRoot = [Environment]::GetEnvironmentVariable('NX_ESKD_ROOT', 'User')
    $customFile = [Environment]::GetEnvironmentVariable('UGII_CUSTOM_DIRECTORY_FILE', 'User')

    if (-not [string]::IsNullOrWhiteSpace($nxEskdRoot) -and (Test-Path $nxEskdRoot)) {
        Write-Success "NX_ESKD_ROOT активна: $nxEskdRoot"
    } else {
        Write-Host "  ❌ Переменная NX_ESKD_ROOT не настроена!" -ForegroundColor Red
    }

    $menFile = Join-Path $nxEskdRoot 'startup\nx_eskd.men'
    if (Test-Path $menFile -PathType Leaf) {
        Write-Success "Файл меню зарегистрирован: $menFile"
    } else {
        Write-Host "  ❌ Файл меню отсутствует: $menFile" -ForegroundColor Red
    }

    if (-not [string]::IsNullOrWhiteSpace($customFile) -and (Test-Path $customFile -PathType Leaf)) {
        Write-Success "UGII_CUSTOM_DIRECTORY_FILE зарегистрирован: $customFile"
        Write-Host ''
        Write-Host '  Активные каталоги в custom_dirs.dat:' -ForegroundColor DarkCyan
        Get-Content $customFile | ForEach-Object {
            if (-not [string]::IsNullOrWhiteSpace($_) -and -not $_.StartsWith('#')) {
                Write-Host "    • $_" -ForegroundColor Cyan
            }
        }
    } else {
        Write-Host "  ❌ UGII_CUSTOM_DIRECTORY_FILE не настроен!" -ForegroundColor Red
    }

    Write-Host ''
    Write-Host '  ════════════════════════════════════════════════════' -ForegroundColor Green
    Write-Host '   ✓ Настройка завершена! Перезапустите Siemens NX.   ' -ForegroundColor Green
    Write-Host '  ════════════════════════════════════════════════════' -ForegroundColor Green
    Write-Host ''
}

# ═════════════════════════════════════════════════════════════════════
#  МЕНЮ
# ═════════════════════════════════════════════════════════════════════

function Show-Menu {
    $width = 58
    $border = '═' * $width

    Write-Host ''
    Write-Host "  ╔$border╗" -ForegroundColor DarkCyan
    Write-Host "  ║                                                          ║" -ForegroundColor DarkCyan
    Write-Host "  ║         N x E s k d   М е н е д ж е р                  ║" -ForegroundColor Cyan
    Write-Host "  ║                                                          ║" -ForegroundColor DarkCyan
    Write-Host "  ╠$border╣" -ForegroundColor DarkCyan
    Write-Host "  ║                                                          ║" -ForegroundColor DarkCyan
    Write-Host "  ║   [1]  Полная автонастройка (Сборка + Установка + NX)     ║" -ForegroundColor Green
    Write-Host "  ║   [2]  Сборка          (build + check assemblies + zip)  ║" -ForegroundColor DarkCyan
    Write-Host "  ║   [3]  Установка       (install / update)                ║" -ForegroundColor DarkCyan
    Write-Host "  ║   [4]  Удаление        (uninstall)                       ║" -ForegroundColor DarkCyan
    Write-Host "  ║   [5]  Тесты           (unit + smoke tests)              ║" -ForegroundColor DarkCyan
    Write-Host "  ║   [6]  Проверка пакета (plan-package / PackagePlanner)   ║" -ForegroundColor DarkCyan
    Write-Host "  ║   [7]  Кодировки       (fix-encodings UTF-8 BOM)         ║" -ForegroundColor DarkCyan
    Write-Host "  ║                                                          ║" -ForegroundColor DarkCyan
    Write-Host "  ║   [0]  Выход                                             ║" -ForegroundColor DarkCyan
    Write-Host "  ║                                                          ║" -ForegroundColor DarkCyan
    Write-Host "  ╚$border╝" -ForegroundColor DarkCyan
    Write-Host ''

    $choice = Read-Host '  Выберите действие'

    switch ($choice) {
        '1' { Invoke-AutoSetup }
        '2' { Invoke-Build }
        '3' { Invoke-Install }
        '4' { Invoke-Uninstall }
        '5' { Invoke-Test }
        '6' { Invoke-PlanPackage }
        '7' { Invoke-FixEncodings }
        '0' { Write-Host '  До свидания!' -ForegroundColor DarkGray; return }
        default {
            Write-Host '  Неверный выбор.' -ForegroundColor Red
            Show-Menu
        }
    }
}

# ═════════════════════════════════════════════════════════════════════
#  ТОЧКА ВХОДА
# ═════════════════════════════════════════════════════════════════════

switch ($Action) {
    'AutoSetup'   { Invoke-AutoSetup }
    'Build'       { Invoke-Build }
    'Install'     { Invoke-Install }
    'Uninstall'   { Invoke-Uninstall }
    'Test'        { Invoke-Test }
    'PlanPackage' { Invoke-PlanPackage }
    'FixEncodings'{ Invoke-FixEncodings }
    'Menu'        { Show-Menu }
}
