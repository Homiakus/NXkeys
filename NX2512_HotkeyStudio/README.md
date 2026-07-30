# NX2512_HotkeyStudio

## Назначение

HotkeyStudio — основной desktop/runtime-компонент NXKeys. Он загружает profile schema 3–6, мигрирует его к schema 6, строит Leader index из модулей, показывает HUD, обрабатывает keyboard input, формирует IPC requests и предоставляет CLI/deployment функции.

Target: `.NET 8`, `net8.0-windows`, Windows Forms, x64.

## Ответственность

- single-instance desktop/tray process;
- keyboard hook и UI event queue;
- adaptive module resolution;
- prefix-free sequence automaton;
- state-machine orchestration и confirmation;
- command search;
- IPC client;
- profile validation/migration;
- MenuScript/overlay generation;
- scan, deployment, backup, restore и health CLI;
- launcher integration.

HotkeyStudio не доказывает, что command ID доступен в текущей лицензии NX. Последнюю runtime-проверку выполняет Command Bridge/NX.

## Основные каталоги

| Путь | Назначение |
|---|---|
| `Program.cs` | desktop/CLI entry point и single-instance lifecycle |
| `Models/` | profile schema, defaults, validation и mnemonic generator |
| `Services/` | runtime, scanner, resolver, deployment, backup, IPC client |
| `UI/` | WinForms editors, HUD и окна |
| `build.ps1` | generation/validation/publish distribution |
| `.csproj` | net8.0-windows/x64 и shared source links |

Shared files `NXKeys.Protocol/NxProtocol.cs` и `NXKeys.StateMachines/*.cs` подключаются как linked compile items.

## Сборка без NX

Из корня репозитория:

```powershell
dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj `
  -c Release -p:Platform=x64 --nologo
```

NXOpen references подключаются условно, если задан `NXOpenDir` и DLL существуют.

## Distribution build

Перед вызовом `build.ps1` должен существовать Bridge artifact:

```text
NX2512_CommandBridge\dist\NX2512_CommandBridge.dll
```

Затем:

```powershell
.\NX2512_HotkeyStudio\build.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -Clean
```

Скрипт:

1. компилирует main K3–K5 profile, если `-ProfilePath` не задан;
2. запускает profile validators;
3. публикует framework-dependent `win-x64`;
4. копирует runtime profile и state-machine policy;
5. добавляет external operation icons;
6. добавляет Bridge в `custom\application`;
7. проверяет required artifacts.

Output:

```text
NX2512_HotkeyStudio\dist
```

## Запуск из исходников

Bootstrap profile:

```powershell
dotnet run --project .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -- `
  --config .\config\nx2512-pro-hybrid.json
```

Main generated profile:

```powershell
dotnet run --project .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -- `
  --config .\config\nx2512-pro-main.generated.json
```

## Desktop lifecycle

- используется global single-instance mutex;
- второй запуск сигнализирует существующему process через named events;
- background/tray flags не создают второй keyboard hook;
- Leader запускается, если `leader_key.enabled=true`;
- runtime log пишется в `%LOCALAPPDATA%\NXKeys\logs\leader-key.log`.

## CLI

Поддерживаются:

```text
validate, scan, catalog, plan, apply, launch, leader,
backups, restore, bridge-status, health, icons, export-icons
```

Полный справочник: [`docs/CLI.md`](../docs/CLI.md).

## Profile loading

`Config.Load`:

1. читает UTF-8 JSON;
2. допускает comments и trailing commas;
3. разворачивает environment paths;
4. применяет defaults/migration;
5. нормализует command fields;
6. запускает `MnemonicPathGenerator.Apply`;
7. перестраивает Leader sequences;
8. валидирует profile.

Current schema — 6, minimum supported — 3.

## Изменение модели

При изменении schema обновите:

- `ConfigRuntimeV5.cs`;
- соответствующий `*TypesV5.cs`;
- defaults и validation;
- installer accepted range;
- CI source checks;
- configuration docs и examples;
- tests migration/round-trip.

Не оставляйте error messages со старым номером schema.

## Tests

```powershell
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-main-command-map.mjs

dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -c Release -p:Platform=x64
```

UI и global keyboard hook дополнительно требуют ручной проверки на Windows.

## Known limitations

- WinForms UI не является cross-platform;
- текущие runtime strings в `Program.cs` местами всё ещё упоминают schema v4 — это технический долг;
- model fields `path_locked/path_source` присутствуют, но отдельный end-to-end user override workflow не подтверждён;
- HotkeyStudio build без NX не подтверждает Bridge runtime;
- автоматическая команда `--help` не реализована в подтверждённом CLI switch.
