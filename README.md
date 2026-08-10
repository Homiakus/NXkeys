# NXKeys

[![CI](https://github.com/Homiakus/NXkeys/actions/workflows/ci.yml/badge.svg)](https://github.com/Homiakus/NXkeys/actions/workflows/ci.yml)
[![Main K3–K5 Profile](https://github.com/Homiakus/NXkeys/actions/workflows/full-command-map.yml/badge.svg)](https://github.com/Homiakus/NXkeys/actions/workflows/full-command-map.yml)
[![Mnemonic Command Language](https://github.com/Homiakus/NXkeys/actions/workflows/mnemonic-command-language.yml/badge.svg)](https://github.com/Homiakus/NXkeys/actions/workflows/mnemonic-command-language.yml)
[![Sketch Intent Grammar](https://github.com/Homiakus/NXkeys/actions/workflows/sketch-intent.yml/badge.svg)](https://github.com/Homiakus/NXkeys/actions/workflows/sketch-intent.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/)

NXKeys — контекстный клавиатурный слой для Siemens NX / Designcenter NX 2512 под Windows x64. Текущий runtime — **v8**: активное приложение NX определяется автоматически, а пользователь вводит только смысловой путь внутри текущего контекста.

> NXKeys не является продуктом Siemens. Доступность конкретной команды зависит от установленной сборки NX, лицензий, роли, локализации и MenuScript-расширений.

## Текущий контракт

| Область | Значение |
|---|---|
| profile schema | **8** |
| IPC schema | **4** |
| sequence policy | **v8** |
| default profile | `config/nx2512-v8-profile.json` |
| no-profile fallback | hardcoded v8 configuration |
| target | Siemens NX / Designcenter NX 2512, Windows x64, .NET 8 |

Подробности и правила приоритета: **[docs/RUNTIME_V8.md](docs/RUNTIME_V8.md)**.

## Как выглядит работа

Пользователь не вводит module prefix — он добавляется runtime автоматически.

### Sketch v8

В активном Sketch частые команды максимально короткие:

```text
CapsLock → L            Line
CapsLock → R            Rectangle
CapsLock → C            Circle
CapsLock → T            Trim
CapsLock → K → C        Coincident
CapsLock → D → Q        Rapid Dimension
CapsLock → C → V → …    варианты построения
```

Полная грамматика: [docs/SKETCH_INTENT_LANGUAGE.md](docs/SKETCH_INTENT_LANGUAGE.md).

### Modeling: `M = Manage`

```text
CapsLock → M → L → S    Layer Settings
```

`M` — смысловой корень Manage. Внутренний префикс Modeling скрыт от пользователя.

### Быстрый Selection Intent

Во время активного NX collector цифры работают как способ распространения выбора:

```text
0  Reset
1  Single
2  Connected / Chain
3  Tangent
4  Inferred Path / Region Boundary
```

Цифры не являются безусловными глобальными hotkeys: handler активируется только в NX и только когда есть подходящий collector или seed selection. Подробности: [docs/SELECTION_INTENT.md](docs/SELECTION_INTENT.md).

## Что изменилось в v8 runtime

- физический CapsLock защёлкивается до отпускания клавиши, поэтому autorepeat не должен создавать несколько Leader-событий;
- `secondary_aliases` реально участвуют в runtime routing;
- workspace-local keys не превращаются автоматически в root commands;
- Modeling использует `M` как Manage subtree;
- Sketch constraints доступны через `K → …` в активном Sketch;
- Sheet Metal нормализован на реальные NX2512 IDs `UG_APP_SBSM` и `UG_SBSM_*`;
- Selection Intent `0…4` работает внутри процесса NX;
- IPC schema 4 использует authenticated request envelope, profile permissions и anti-replay checks;
- при отсутствии JSON HotkeyStudio способен построить и проверить hardcoded v8 fallback.

## Состав репозитория

| Компонент | Назначение |
|---|---|
| `NX2512_HotkeyStudio/` | desktop/tray runtime, Leader, HUD, profile loading, CLI, deployment client |
| `NX2512_CommandBridge/` | NXOpen runtime внутри NX, context, authenticated queue, command dispatch, Selection Intent |
| `NXKeys.Protocol/` | IPC schema 4, security envelope и permission model |
| `NXKeys.StateMachines/` | DFA/HFSM, guards и lifecycle |
| `NX2512_ControlCenter/` | диагностика runtime/profile/Bridge |
| `NX2512_Catalog_Studio/` | экспорт фактических NX UI commands/API candidates |
| `config/nx2512-v8-profile.json` | основной versioned v8 runtime profile |
| `config/full-command-map/` | полный K1–K5 intent catalog для анализа/coverage/generation |
| `scripts/` | validators, audit и legacy/main-profile generation pipeline |
| `install-nxkeys.ps1` | установка, диагностика, очистка конфликтов и recovery custom dirs |
| `docs/` | canonical, generated и historical documentation |

K1–K5 / generated K3–K5 pipeline сохраняется для трассировки, coverage и экспериментов с распределением команд. **Installer по умолчанию выбирает v8 profile**, а не generated K3–K5 profile.

### Термины legacy validators

Несколько legacy/catalog validators всё ещё проверяют текстовые маркеры прежней архитектуры. Они сохранены здесь **только для однозначной исторической трассировки**, а не как описание current runtime:

- `14 контекстных модулей` — legacy normalized module grid;
- `CapsLock → действие → объект` — прежняя концептуальная формула adaptive input, **не буквальная грамматика v8**;
- `K3–K5` / `885` — scope старого generated main profile;
- `config/nx2512-pro-main.generated.json` — generated compatibility artifact;
- термин `главный профиль` в старом pipeline означает generated K3–K5 profile, а **не** текущий default `config/nx2512-v8-profile.json`.

Эти маркеры существуют, чтобы старый analytical pipeline оставался проверяемым, не подменяя current v8 contract.

## Требования

Для разработки без установленного NX:

- .NET SDK 8;
- Node.js 20+;
- PowerShell 5.1 или 7;
- Windows для WinForms/runtime-сценариев; Node/C# validators частично запускаются и в других ОС.

Для production build/install:

- Windows 10/11 x64;
- Siemens NX / Designcenter NX 2512;
- `NXOpen.dll` и `NXOpenUI.dll` целевой установки;
- права записи в `%LOCALAPPDATA%\NXKeys`;
- актуальный Catalog Studio export рекомендуется для проверки реальных IDs.

Node-скрипты используют стандартную библиотеку: обязательного `npm install` нет.

## Быстрый старт разработчика

Из корня репозитория:

```powershell
node .\scripts\validate-documentation.mjs
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs

dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
dotnet run --project .\NX2512_HotkeyStudio.Tests\NX2512_HotkeyStudio.Tests.csproj -c Release

dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -c Release -p:Platform=x64 --nologo
dotnet build .\NX2512_ControlCenter\NX2512_ControlCenter.csproj -c Release -p:Platform=x64 --nologo
```

Command Bridge production build требует NXOpen. CI дополнительно компилирует его против contract stubs.

Полный developer guide: [DEVELOPMENT.md](DEVELOPMENT.md).

## Установка

Без параметров installer открывает интерактивное меню:

```powershell
.\install-nxkeys.ps1
```

Типичная чистая установка:

```powershell
.\install-nxkeys.ps1 `
  -Mode CleanInstall `
  -Yes `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Если `-ConfigPath` не задан, используется:

```text
config\nx2512-v8-profile.json
```

Доступны maintenance modes `Audit`, `CleanConflicts`, `RepairCustomDirs`, `CleanInstall` и обычный `Install`.

Полная инструкция: [docs/INSTALLATION.md](docs/INSTALLATION.md).

## Проверка установленной системы

Актуальный installer устанавливает profile под именем:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\nx2512-v8-profile.json
```

Проверка:

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
$studio = "$root\NX2512_HotkeyStudio.exe"
$config = "$root\nx2512-v8-profile.json"

& $studio validate --config $config
& $studio health --config $config
```

Затем **запустите NX через managed launcher** и проверьте:

```powershell
& $studio bridge-status --config $config
```

Authenticated IPC session создаётся managed launch workflow. Независимый запуск NX и HotkeyStudio не эквивалентен штатному launcher-сценарию.

## Безопасность

Перед dispatch Bridge повторно проверяет authenticated session, HMAC, anti-replay state, profile permission, fresh NX context, application/module, selection fingerprint и destructive confirmation policy.

Файловая очередь — транспорт, а не источник полномочий. Unknown/stale/unsigned requests должны отклоняться.

NXKeys не заменяет резервное копирование и инженерную проверку. Новые/destructive команды тестируйте на копии детали в целевой лицензированной NX.

Подробнее: [docs/SAFETY_MODEL.md](docs/SAFETY_MODEL.md) и [docs/api.md](docs/api.md).

## Документация

- [Канонический v8 runtime](docs/RUNTIME_V8.md)
- [Подробная шпаргалка](docs/CHEATSHEET.md)
- [Selection Intent 0…4](docs/SELECTION_INTENT.md)
- [Sketch v8](docs/SKETCH_INTENT_LANGUAGE.md)
- [Оглавление](docs/README.md)
- [Установка](docs/INSTALLATION.md)
- [Конфигурация](docs/CONFIGURATION.md)
- [Архитектура](docs/ARCHITECTURE.md)
- [CLI](docs/CLI.md)
- [IPC API](docs/api.md)
- [State machines](docs/STATE_MACHINE_ARCHITECTURE.md)
- [Безопасность](docs/SAFETY_MODEL.md)
- [Эксплуатация](docs/OPERATIONS.md)
- [Диагностика](docs/TROUBLESHOOTING.md)
- [Аудит актуальности](docs/DOCUMENTATION_AUDIT.md)
- [Разработка](DEVELOPMENT.md)
- [Внесение изменений](CONTRIBUTING.md)
- [История изменений](CHANGELOG.md)
- [Security reporting](SECURITY.md)

Интерактивная карта команд публикуется через GitHub Pages: <https://homiakus.github.io/NXkeys/>.

## Важное ограничение живой NX

CI проверяет contract/API shape, но не может доказать интерактивное поведение конкретной NX 2512 workstation. После обновления Bridge **полностью закройте NX**, запустите заново через NXKeys launcher и проверьте реальные collectors/dialogs.

В частности, результат `DialogTester.InvokeMenuButtonAction(...)` для некоторых интерактивных команд требует живого NX-теста и не должен считаться доказанным только по contract build.

## Лицензия

[MIT](LICENSE)