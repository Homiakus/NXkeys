# NXKeys Adaptive Control Center

`NX2512_ControlCenter` показывает состояние NXKeys, активный модуль, команды, доступность IDs и Bridge diagnostics.

## Главный профиль

Control Center должен открывать главный K3–K5 profile:

```text
config/nx2512-pro-main.generated.json
```

В установленном managed-пакете тот же профиль хранится под compatibility filename:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\nx2512-pro-hybrid.json
```

Главный scope содержит **885 уникальных намерений**: K5 — 69, K4 — 371, K3 — 445. K1–K2 не входят в стандартный runtime.

## Возможности

- загрузка JSON-профиля;
- отображение `selected_frequencies` и `selected_intents`;
- просмотр 14 модулей и Leader-последовательностей;
- вычисление числа enabled-команд с ID;
- отображение `existing`, `resolved`, `ambiguous`, `unresolved`;
- состояние Bridge: ONLINE, STALE, OFFLINE;
- активный модуль, selection, Work Part и последний результат;
- поиск по имени, ID, aliases, пути и каталогу;
- запуск HotkeyStudio и Leader Engine;
- локальная статистика использования.

## Запуск из репозитория

```powershell
dotnet run --project .\NX2512_ControlCenter\NX2512_ControlCenter.csproj -- `
  --config .\config\nx2512-pro-main.generated.json `
  --catalog "D:\NX2512_Catalog_Output"
```

## Запуск установленной версии

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
& "$root\control-center\NX2512_ControlCenter.exe" `
  --config "$root\nx2512-pro-hybrid.json"
```

## Метрики

Различайте:

1. **Coverage scope:** 885 уникальных K3–K5 `catalog_refs`.
2. **Resolved coverage:** доля `existing + resolved` среди 885.
3. **Executable module rows:** enabled-строки с ID; число может быть больше уникальных intents из-за global duplication.
4. **Runtime verified:** команды, реально проверенные в целевой NX.

`ambiguous` и `unresolved` не должны считаться исполняемыми.

## NX API Explorer

Использует `04_nxopen_members.csv`, `05_nxopen_entry_points.csv`, `06_ui_commands_buttons.csv`, `07_ufun_functions.csv` и `08_ui_command_api_candidates.csv`. Результаты Explorer являются кандидатами и не доказывают эквивалентность UI-команды конкретному API.

## Контекст Bridge

Control Center показывает application/module, selection, Work/Display Part, modal dialog, confidence, age и последний результат. `selection_count: -1` означает неизвестное состояние, а не нулевой выбор.

## Сборка

```powershell
dotnet publish .\NX2512_ControlCenter\NX2512_ControlCenter.csproj `
  -c Release -r win-x64 --self-contained false -p:Platform=x64 `
  -o .\NX2512_ControlCenter\dist
```

Control Center не заменяет Catalog Studio, health-check и runtime-тест в лицензированной NX.
