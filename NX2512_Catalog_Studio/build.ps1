[CmdletBinding()]
param(
    [string]$NxRoot,
    [string]$NxOpenDll,
    [switch]$Sign,
    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = Join-Path $ProjectDir 'NX2512_CatalogStudio.csproj'
$DistDir = Join-Path $ProjectDir 'dist'
$BuildDir = Join-Path $ProjectDir 'bin'
$ObjDir = Join-Path $ProjectDir 'obj'

function Write-Step([string]$Text) {
    Write-Host "`n==> $Text" -ForegroundColor Cyan
}

function Add-ExistingPath {
    param(
        [System.Collections.Generic.List[string]]$List,
        [string]$Path
    )
    if ([string]::IsNullOrWhiteSpace($Path)) { return }
    $expanded = [Environment]::ExpandEnvironmentVariables($Path.Trim('"'))
    if (Test-Path -LiteralPath $expanded) {
        $full = (Resolve-Path -LiteralPath $expanded).Path
        if (-not $List.Contains($full)) {
            [void]$List.Add($full)
        }
    }
}

function Get-NxSearchRoots {
    $roots = [System.Collections.Generic.List[string]]::new()

    Add-ExistingPath $roots $NxRoot
    Add-ExistingPath $roots $env:UGII_BASE_DIR
    Add-ExistingPath $roots $env:UGII_ROOT_DIR
    Add-ExistingPath $roots $env:UGOPEN

    $programFiles = @(
        $env:ProgramFiles,
        ${env:ProgramFiles(x86)}
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    foreach ($pf in $programFiles) {
        foreach ($candidate in @(
            (Join-Path $pf 'Siemens'),
            (Join-Path $pf 'Siemens\NX2512'),
            (Join-Path $pf 'Siemens\NX 2512'),
            (Join-Path $pf 'Siemens\DesigncenterNX2512')
        )) {
            Add-ExistingPath $roots $candidate
        }
    }

    return $roots
}

function Find-NxOpenDll {
    if ($NxOpenDll) {
        if (-not (Test-Path -LiteralPath $NxOpenDll -PathType Leaf)) {
            throw "Указанный NXOpen.dll не найден: $NxOpenDll"
        }
        return (Resolve-Path -LiteralPath $NxOpenDll).Path
    }

    $roots = Get-NxSearchRoots
    $relativeCandidates = @(
        'NXBIN\managed\NXOpen.dll',
        'UGII\managed\NXOpen.dll',
        'managed\NXOpen.dll',
        'NXOpen.dll'
    )

    foreach ($root in $roots) {
        foreach ($relative in $relativeCandidates) {
            $candidate = Join-Path $root $relative
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                return (Resolve-Path -LiteralPath $candidate).Path
            }
        }
    }

    Write-Step 'Быстрый поиск NXOpen.dll в каталогах Siemens'
    foreach ($root in $roots) {
        try {
            $match = Get-ChildItem -LiteralPath $root -Filter NXOpen.dll -File -Recurse `
                -ErrorAction SilentlyContinue |
                Where-Object {
                    $_.FullName -match '\\(managed|NXBIN|UGII)\\'
                } |
                Select-Object -First 1

            if ($match) {
                return $match.FullName
            }
        }
        catch {
            Write-Warning "Не удалось просканировать $root : $($_.Exception.Message)"
        }
    }

    throw "NXOpen.dll не найден автоматически.`n`nЗапустите так:`n  .\build.ps1 -NxOpenDll `"C:\...\NXOpen.dll`"`n`nТипичные расположения:`n  C:\Program Files\Siemens\NX2512\NXBIN\managed\NXOpen.dll`n  C:\Program Files\Siemens\NX2512\UGII\managed\NXOpen.dll"
}

function Find-FileNearNx {
    param(
        [string]$NxOpenPath,
        [string[]]$Names
    )

    $start = Split-Path -Parent $NxOpenPath
    $roots = [System.Collections.Generic.List[string]]::new()
    Add-ExistingPath $roots $start

    $cursor = Get-Item -LiteralPath $start
    for ($i = 0; $i -lt 5 -and $cursor; $i++) {
        Add-ExistingPath $roots $cursor.FullName
        $cursor = $cursor.Parent
    }

    foreach ($root in $roots) {
        foreach ($name in $Names) {
            $directCandidates = @(
                (Join-Path $root $name),
                (Join-Path $root "UGOPEN\$name"),
                (Join-Path $root "UGII\$name"),
                (Join-Path $root "NXBIN\$name")
            )
            foreach ($candidate in $directCandidates) {
                if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                    return (Resolve-Path -LiteralPath $candidate).Path
                }
            }
        }
    }

    foreach ($root in $roots) {
        foreach ($name in $Names) {
            $found = Get-ChildItem -LiteralPath $root -Filter $name -File -Recurse `
                -ErrorAction SilentlyContinue |
                Select-Object -First 1
            if ($found) { return $found.FullName }
        }
    }

    return $null
}

function Install-DotNet8Sdk {
    Write-Step 'Автоматическая установка .NET 8 SDK'

    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if ($winget) {
        Write-Host "Попытка установки через winget (Microsoft.DotNet.SDK.8)..." -ForegroundColor Cyan
        try {
            & winget install Microsoft.DotNet.SDK.8 --source winget --accept-source-agreements --accept-package-agreements --silent --disable-interactivity
            $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")

            $checkDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
            if ($checkDotnet) {
                $checkSdks = & dotnet --list-sdks
                if ($checkSdks) {
                    Write-Host ".NET 8 SDK успешно установлен через winget." -ForegroundColor Green
                    return
                }
            }
        }
        catch {
            Write-Warning "winget завершился с ошибкой: $($_.Exception.Message)"
        }
    }

    Write-Host "Загрузка и запуск официально инсталляционного скрипта Microsoft (dotnet-install.ps1)..." -ForegroundColor Cyan
    $installScript = Join-Path $env:TEMP 'dotnet-install.ps1'
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installScript -UseBasicParsing
    & powershell -NoProfile -ExecutionPolicy Bypass -File $installScript -Channel 8.0 -Quality GA

    $dotnetLocal = Join-Path $env:LocalAppData 'Microsoft\dotnet'
    if (Test-Path -LiteralPath $dotnetLocal) {
        $env:PATH = "$dotnetLocal;C:\Program Files\dotnet;" + $env:PATH
    }
}

function Assert-DotNet8 {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) {
        $sdks = & dotnet --list-sdks
        if ($sdks | Where-Object { $_ -match '^[89]\.' }) {
            return
        }
        if ($sdks | Where-Object { $_ -match '^\d+\.' }) {
            return
        }
    }

    $vsSdk = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Sdks\Microsoft.NET.Sdk\Sdk"
    if (Test-Path -LiteralPath $vsSdk) {
        return
    }

    Write-Warning "Отсутствует .NET SDK. Выполняется автоматическое скачивание и установка..."
    Install-DotNet8Sdk

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) {
        $sdks = & dotnet --list-sdks
        if ($sdks) {
            return
        }
    }

    $dotnetLocalExe = Join-Path $env:LocalAppData 'Microsoft\dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $dotnetLocalExe) {
        return
    }

    throw "Не удалось автоматически установить .NET 8 SDK.`n`nПожалуйста, установите .NET 8 SDK x64 вручную по ссылке:`n  https://dotnet.microsoft.com/download/dotnet/8.0"
}

if ($Clean) {
    Write-Step 'Очистка предыдущей сборки'
    Remove-Item -LiteralPath $BuildDir, $ObjDir, $DistDir -Recurse -Force `
        -ErrorAction SilentlyContinue
}

Assert-DotNet8

Write-Step 'Поиск NXOpen.dll'
$resolvedNxOpen = Find-NxOpenDll
$nxOpenDir = Split-Path -Parent $resolvedNxOpen
Write-Host "NXOpen.dll: $resolvedNxOpen" -ForegroundColor Green

$assemblyName = $null
try {
    $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($resolvedNxOpen)
    Write-Host "Версия NXOpen: $($assemblyName.Version)"
}
catch {
    Write-Warning "Не удалось прочитать версию NXOpen.dll: $($_.Exception.Message)"
}

$signingResource = Find-FileNearNx `
    -NxOpenPath $resolvedNxOpen `
    -Names @('NXSigningResource.res')

$signTool = Find-FileNearNx `
    -NxOpenPath $resolvedNxOpen `
    -Names @('SignDotNet.exe')

if ($signingResource) {
    Write-Host "NXSigningResource: $signingResource"
}
else {
    Write-Host "NXSigningResource.res не найден — сборка будет без signing resource." `
        -ForegroundColor Yellow
}

Write-Step 'Сборка Class Library .NET 8'
$properties = @(
    "-p:NXOpenDir=$nxOpenDir"
)
if ($signingResource) {
    $properties += "-p:NXSigningResource=$signingResource"
}

$dotnetExe = (Get-Command dotnet -ErrorAction SilentlyContinue).Path
if (-not $dotnetExe) {
    $localDotnet = Join-Path $env:LocalAppData 'Microsoft\dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $localDotnet) {
        $dotnetExe = $localDotnet
    }
}

$msbuildPath = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

if ($dotnetExe) {
    & $dotnetExe build $ProjectFile --configuration Release --nologo @properties
}

if (($LASTEXITCODE -ne 0 -or -not $dotnetExe) -and (Test-Path -LiteralPath $msbuildPath)) {
    Write-Host "Переключение на MSBuild VS 2022..." -ForegroundColor Yellow
    & $msbuildPath $ProjectFile /p:Configuration=Release @properties
}

if ($LASTEXITCODE -ne 0) {
    throw "Сборка завершилась с кодом $LASTEXITCODE."
}

$builtDll = Join-Path $ProjectDir `
    'bin\Release\net8.0-windows\NX2512_CatalogStudio.dll'

if (-not (Test-Path -LiteralPath $builtDll -PathType Leaf)) {
    throw "Сборка завершилась, но DLL не найдена: $builtDll"
}

New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
$distDll = Join-Path $DistDir 'NX2512_CatalogStudio.dll'
Copy-Item -LiteralPath $builtDll -Destination $distDll -Force

if ($Sign) {
    Write-Step 'Подпись DLL через SignDotNet'
    if (-not $signTool) {
        throw "Запрошена подпись, но SignDotNet.exe не найден."
    }
    if (-not $signingResource) {
        throw "Запрошена подпись, но NXSigningResource.res не найден."
    }

    & $signTool $distDll
    if ($LASTEXITCODE -ne 0) {
        throw "SignDotNet завершился с кодом $LASTEXITCODE.`nДля подписи может потребоваться NX Open .NET Author license.`nDLL без подписи осталась в:`n  $builtDll"
    }
}

$hash = (Get-FileHash -LiteralPath $distDll -Algorithm SHA256).Hash
$info = [ordered]@{
    built_at = (Get-Date).ToString('o')
    nxopen_dll = $resolvedNxOpen
    nxopen_version = if ($assemblyName) { $assemblyName.Version.ToString() } else { $null }
    output_dll = $distDll
    signed_requested = [bool]$Sign
    sign_tool = $signTool
    sha256 = $hash
}
$info | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $DistDir 'build-info.json') -Encoding UTF8

Write-Host ""
Write-Host "ГОТОВО" -ForegroundColor Green
Write-Host "DLL: $distDll"
Write-Host "SHA256: $hash"
Write-Host ""
Write-Host "Запуск в NX 2512:" -ForegroundColor Cyan
Write-Host "  File -> Execute -> NX Open... (Ctrl+U)"
Write-Host "  Тип файла: Динамически загружаемые библиотеки (*.dll)"
Write-Host "  Выберите: $distDll"
