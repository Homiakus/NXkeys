# NXKeys

[![CI](https://github.com/Homiakus/NXkeys/actions/workflows/ci.yml/badge.svg)](https://github.com/Homiakus/NXkeys/actions/workflows/ci.yml)
[![Main K3–K5 Profile](https://github.com/Homiakus/NXkeys/actions/workflows/full-command-map.yml/badge.svg)](https://github.com/Homiakus/NXkeys/actions/workflows/full-command-map.yml)
[![Mnemonic Command Language](https://github.com/Homiakus/NXkeys/actions/workflows/mnemonic-command-language.yml/badge.svg)](https://github.com/Homiakus/NXkeys/actions/workflows/mnemonic-command-language.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/)

NXKeys — сторонний контекстный клавиатурный слой для Siemens NX / Designcenter NX 2512 под Windows x64. Пользователь вызывает команды через мнемонические последовательности вместо большого числа глобальных сочетаний:

```text
CapsLock → действие → объект → команда → вариант
```

Примеры:

```text
CapsLock → C → E    Create → Extrude
CapsLock → S → F    Select → Face
CapsLock → S → A    Select All
CapsLock → G → D    Go → Drafting
```

Интерактивная карта команд публикуется через GitHub Pages: <https://homiakus.github.io/NXkeys/>.

> NXKeys не является продуктом Siemens. Доступность конкретной команды зависит от установленной сборки NX, лицензий, роли, локализации и корпоративных MenuScript-расширений.

## Возможности

- главный профиль из **885 команд-намерений K3–K5**;
- исходный каталог из **1169 намерений K1–K5** в 32 разделах;
- 14 контекстных модулей NX;
- prefix-free мнемонические пути длиной 2–5 токенов;
- универсальные фильтры выбора `SB`, `SF`, `SE`, `ST`, `SC`, `SU`, `SD`, `SR`, `SA`, `SN`;
- явные переходы между приложениями через `G*`;
- HUD, поиск, WinForms-редактор профиля и системный tray;
- файловый IPC между HotkeyStudio и NXOpen Command Bridge;
- проверка контекста, подтверждение опасных операций и at-most-once обработка запросов;
- транзакционная установка, резервные копии, manifest, health-check и rollback;
- Catalog Studio для экспорта фактических `BUTTON ID` целевой установки NX;
- Control Center для диагностики профиля и Bridge.

## Состав репозитория

| Компонент | Назначение |
|---|---|
| `NX2512_HotkeyStudio/` | desktop UI, HUD, Leader runtime, CLI, генерация MenuScript и deployment |
| `NX2512_CommandBridge/` | NXOpen-библиотека, выполняемая внутри процесса NX |
| `NX2512_ControlCenter/` | диагностика профиля, Bridge и покрытия команд |
| `NX2512_Catalog_Studio/` | экспорт UI-команд и NXOpen-каталога целевой установки |
| `NXKeys.Protocol/` | общий JSON-контракт IPC schema 3 |
| `NXKeys.StateMachines/` | DFA/HFSM и контекстные guards |
| `NXKeys.StateMachines.Tests/` | исполняемый набор инвариантных и randomized-тестов |
| `config/full-command-map/` | версионируемый каталог 1169 намерений |
| `config/nx2512-pro-hybrid.json` | bootstrap-профиль и источник safety/deployment-настроек |
| `scripts/` | компиляция, валидация и аудит профилей |
| `install-nxkeys.ps1` | поддерживаемая точка компиляции и установки |

Подробная карта компонентов находится в [архитектурной документации](docs/ARCHITECTURE.md).

## Требования

Для проверки кода без установленного NX:

- Windows, Linux или macOS для Node-валидаторов;
- .NET SDK 8;
- Node.js 20 или новее;
- PowerShell 5.1 или PowerShell 7 для Windows-скриптов.

Для сборки и установки полного пакета:

- Windows 10/11 x64;
- Siemens NX или Designcenter NX 2512;
- `NXOpen.dll` и `NXOpenUI.dll` из целевой установки;
- права записи в `%LOCALAPPDATA%\NXKeys`;
- экспорт Catalog Studio с `06_ui_commands_buttons.csv` — рекомендуется для production-профиля.

В проекте нет `package.json` и обязательного шага `npm install`: Node-скрипты используют стандартную библиотеку Node.js.

## Быстрый старт для разработчика

Рабочая директория для следующих команд — корень репозитория.

```powershell
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs
node .\scripts\audit-command-sequences.mjs

dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -c Release -p:Platform=x64
dotnet build .\NX2512_ControlCenter\NX2512_ControlCenter.csproj -c Release -p:Platform=x64
```

Command Bridge не собирается обычной командой без `NXOpenDir`. Используйте его `build.ps1` либо контрактные stubs, как это делает CI. Подробности: [DEVELOPMENT.md](DEVELOPMENT.md).

## Компиляция профиля

Рекомендуемый способ:

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -CompileOnly
```

Результаты:

```text
config/nx2512-pro-main.generated.json
docs/generated/main-profile-resolution.md
```

`install-nxkeys.ps1` принимает только главный scope `K3|K4|K5` с 885 уникальными намерениями.

### Источники истины

| Данные | Источник истины |
|---|---|
| состав и частоты K1–K5 | `config/full-command-map/` |
| safety, deployment и bootstrap IDs | `config/nx2512-pro-hybrid.json` |
| правила универсальных путей | `scripts/sequence-policy.mjs` |
| runtime-нормализация путей | `NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs` |
| IPC-поля и таймауты | `NXKeys.Protocol/NxProtocol.cs` |
| контекстные guards | `config/nx2512-state-machines.json` и `NXKeys.StateMachines/` |
| фактические NX IDs | `06_ui_commands_buttons.csv` целевой установки |

Checked-in generated-файлы и `docs/audit/*` являются результатами конкретного запуска. После изменения компилятора или sequence policy их нужно пересоздать; они не имеют приоритета над исходным кодом.

## Установка

Закройте NX и запустите из корня репозитория:

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Для Designcenter NX:

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -NxRoot "C:\Program Files\Siemens\DesigncenterNX2512" `
  -Clean
```

Установщик компилирует профиль, собирает компоненты, формирует staging-набор, выполняет транзакционное развёртывание и health-check. Полная инструкция: [docs/INSTALLATION.md](docs/INSTALLATION.md).

## Запуск установленного пакета

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\launch-nx2512-with-nxkeys.cmd
```

Проверка установки:

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
& "$root\NX2512_HotkeyStudio.exe" validate --config "$root\nx2512-pro-hybrid.json"
& "$root\NX2512_HotkeyStudio.exe" health --config "$root\nx2512-pro-hybrid.json"
& "$root\NX2512_HotkeyStudio.exe" bridge-status --config "$root\nx2512-pro-hybrid.json"
```

## Безопасность выполнения

Перед отправкой команды проверяются свежесть контекста, приложение и модуль, Work/Display Part, modal state, selection, точный `BUTTON ID` и confirmation policy. `ambiguous` и `unresolved` команды остаются видимыми, но отключаются. Запрос, прерванный после захвата Bridge, получает `interrupted_unknown` и автоматически не повторяется.

NXKeys не должен использоваться как единственная мера защиты производственных данных. Новые и destructive-команды необходимо проверять на копии детали в целевой лицензированной NX.

## Документация

- [Оглавление документации](docs/README.md)
- [Аудит и карта кодовой базы](docs/DOCUMENTATION_AUDIT.md)
- [Локальная разработка](DEVELOPMENT.md)
- [Внесение изменений](CONTRIBUTING.md)
- [Архитектура](docs/ARCHITECTURE.md)
- [Установка и обновление](docs/INSTALLATION.md)
- [Конфигурация](docs/CONFIGURATION.md)
- [CLI](docs/CLI.md)
- [IPC API](docs/api.md)
- [Мнемонический язык](docs/MNEMONIC_COMMAND_LANGUAGE.md)
- [Язык намерений Sketch](docs/SKETCH_INTENT_LANGUAGE.md)
- [Конечные автоматы](docs/STATE_MACHINE_ARCHITECTURE.md)
- [Модель безопасности](docs/SAFETY_MODEL.md)
- [Эксплуатация](docs/OPERATIONS.md)
- [Диагностика](docs/TROUBLESHOOTING.md)
- [Архитектурные решения](docs/adr/README.md)
- [История изменений](CHANGELOG.md)
- [Сообщение об уязвимостях](SECURITY.md)

## Участие в разработке

Изменения profile schema, sequence policy, IPC или deployment должны сопровождаться обновлением документации и соответствующих валидаторов. Порядок работы и обязательные проверки описаны в [CONTRIBUTING.md](CONTRIBUTING.md).

## Лицензия

[MIT](LICENSE)
