# Локальная разработка NXKeys

## Поддерживаемая среда

| Инструмент | Требование | Где используется |
|---|---|---|
| .NET SDK | 8.x | все C#-проекты и тесты |
| Node.js | 20+ | profile compiler, validators, audits |
| PowerShell | 5.1 или 7 | build/install scripts |
| Windows x64 | обязательно для WinForms runtime и production deployment | HotkeyStudio, Control Center, NX integration |
| Siemens NX / Designcenter NX | 2512 | production build и интеграционные проверки Bridge/Catalog Studio |

Node-скрипты не требуют `npm install`: в репозитории нет `package.json` и внешних npm-зависимостей.

## Подготовка

Рабочая директория — корень репозитория.

```powershell
dotnet --info
node --version
$PSVersionTable.PSVersion
```

Ожидается наличие .NET 8 SDK и Node.js 20 или новее.

## Что можно проверить без Siemens NX

### Profile validators

```powershell
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs
node .\scripts\audit-command-sequences.mjs
```

Назначение:

| Команда | Проверяет |
|---|---|
| `validate-main-command-map.mjs` | 1169 source intents, K3–K5 scope 885, generation contract и документационные инварианты |
| `validate-command-tree.mjs` | bootstrap, 12 basic shortcuts, 14 modules, paths, aliases, support commands и runtime model |
| `validate-full-command-map.mjs` | полный каталог K1–K5 и отсутствие конфликтов |
| `audit-command-sequences.mjs` | статистику путей и generated audit в `docs/audit/` |

`audit-command-sequences.mjs` изменяет generated-файлы. После запуска проверьте diff.

### State-machine tests

```powershell
dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
```

Это executable test runner, а не xUnit/NUnit-проект. Он включает protocol invariants, declarative policy tests, deterministic replay и randomized transitions.

### HotkeyStudio

```powershell
dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj `
  -c Release -p:Platform=x64 --nologo
```

Проект условно подключает NXOpen references только когда задан `NXOpenDir`, поэтому базовая сборка возможна без установленного NX.

### Control Center

```powershell
dotnet build .\NX2512_ControlCenter\NX2512_ControlCenter.csproj `
  -c Release -p:Platform=x64 --nologo
```

Control Center имеет `ProjectReference` на HotkeyStudio.

### Command Bridge contract build

Production Bridge требует proprietary NXOpen DLL. Для проверки формы используемого API CI сначала собирает contract stubs:

```powershell
$contract = (Resolve-Path .\artifacts\nxopen-contract -ErrorAction SilentlyContinue)
```

Локально проще воспроизвести шаги CI:

```powershell
New-Item -ItemType Directory -Force .\artifacts\nxopen-contract | Out-Null

dotnet build .\NX2512_CommandBridge.Tests\NXOpenUI\NXOpenUI.csproj `
  -c Release -o .\artifacts\nxopen-contract --nologo

$nxOpenDir = (Resolve-Path .\artifacts\nxopen-contract).Path

dotnet build .\NX2512_CommandBridge\NX2512_CommandBridge.csproj `
  -c Release -p:Platform=x64 -p:NXOpenDir="$nxOpenDir" --nologo
```

Contract build подтверждает компиляцию против используемой формы NXOpen API, но не заменяет загрузку DLL в реальный NX.

## Сборка с Siemens NX 2512

### Command Bridge

```powershell
.\NX2512_CommandBridge\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Альтернатива с точным DLL:

```powershell
.\NX2512_CommandBridge\build.ps1 `
  -NxOpenDll "C:\Program Files\Siemens\NX2512\NXBIN\managed\NXOpen.dll" `
  -Clean
```

Скрипт требует `NXOpenUI.dll` рядом с `NXOpen.dll` и по умолчанию отклоняет путь, не подтверждающий версию `2512`.

`-AllowVersionMismatch` разрешён только после ручной проверки совместимости.

### Catalog Studio

```powershell
.\NX2512_Catalog_Studio\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Для подписи, если это требуется организацией:

```powershell
.\NX2512_Catalog_Studio\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Sign `
  -Clean
```

`-Sign` требует найденных рядом `SignDotNet.exe` и `NXSigningResource.res`.

### HotkeyStudio distribution

Сначала соберите Command Bridge, затем:

```powershell
.\NX2512_HotkeyStudio\build.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -Clean
```

Скрипт компилирует main K3–K5 profile и требует Bridge artifact в `NX2512_CommandBridge\dist`.

## Генерация профиля

Рекомендуемый вход:

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -CompileOnly
```

Прямой вызов компилятора:

```powershell
node .\scripts\compile-main-command-map.mjs `
  --profile .\config\nx2512-pro-hybrid.json `
  --intents .\config\full-command-map `
  --probe .\docs\audit\runtime-command-probe-2026-07-28.json `
  --catalog-dir "D:\NX2512_Catalog_Output" `
  --out .\config\nx2512-pro-main.generated.json `
  --report .\docs\generated\main-profile-resolution.md
```

Не редактируйте generated profile и resolution report вручную.

## Запуск desktop-приложений из исходников

### HotkeyStudio

```powershell
dotnet run --project .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -- `
  --config .\config\nx2512-pro-hybrid.json
```

Это запускает bootstrap source profile. Для проверки main profile сначала скомпилируйте его и передайте `--config .\config\nx2512-pro-main.generated.json`.

### Control Center

```powershell
dotnet run --project .\NX2512_ControlCenter\NX2512_ControlCenter.csproj -- `
  --config .\config\nx2512-pro-main.generated.json `
  --catalog "D:\NX2512_Catalog_Output"
```

## Полный локальный pre-commit набор

```powershell
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs
node .\scripts\audit-command-sequences.mjs

dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -c Release -p:Platform=x64 --nologo
dotnet build .\NX2512_ControlCenter\NX2512_ControlCenter.csproj -c Release -p:Platform=x64 --nologo
```

При изменении Bridge дополнительно выполните contract build. При изменении NXOpen integration — production build и runtime-проверку внутри NX.

## Generated-файлы

После изменения profile compiler, sequence policy или full command map могут измениться:

```text
config/nx2512-pro-main.generated.json
docs/generated/main-profile-resolution.md
docs/audit/command-sequence-audit.md
docs/audit/command-sequence-audit.json
docs/command-tree.html
```

В commit должны попадать только результаты, созданные текущим кодом. Не обновляйте timestamps отдельно от содержимого.

## Конфигурация разработки

Подтверждённые необязательные переменные:

```powershell
$env:NXKEYS_CATALOG_DIR = "D:\NX2512_Catalog_Output"
$env:UGII_BASE_DIR = "C:\Program Files\Siemens\NX2512"
```

`UGII_ROOT_DIR` и `UGOPEN` также используются как hints поиска NXOpen. Не добавляйте machine-specific absolute paths в versioned JSON.

## Сброс локальных build artifacts

Безопасно удалить только build outputs репозитория:

```powershell
Get-ChildItem -Recurse -Directory -Include bin,obj,dist | Remove-Item -Recurse -Force
Remove-Item .\artifacts -Recurse -Force -ErrorAction SilentlyContinue
```

Команда не удаляет managed installation и backups в `%LOCALAPPDATA%\NXKeys`.

Для сброса установленного пакета не удаляйте managed root вручную. Используйте backup/restore workflow либо повторную установку с `-Clean` после сохранения нужных backup manifests.

## Известные ограничения

- GUI и production deployment не тестируются полноценно на non-Windows системах.
- Contract stubs не доказывают runtime-совместимость с конкретной NX build.
- `BUTTON ID` и module availability необходимо проверять на целевой лицензии и роли.
- Source sequence policy и checked-in generated artifacts могут расходиться до запуска генераторов; validators и diff должны обнаруживать такое состояние.
