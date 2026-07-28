# NXKeys Adaptive Control Center

`NX2512_ControlCenter` — центр обзора, поиска и диагностики NXKeys для Siemens NX 2512.

Он дополняет HotkeyStudio, но не заменяет компилятор полной карты, deployment engine и редактор JSON-профиля.

## Поддерживаемые профили

Control Center может открыть:

```text
config/nx2512-pro-hybrid.json          базовый профиль
config/nx2512-pro-full.generated.json  полный профиль конкретной установки
```

Полный профиль создаётся из 1169 намерений и локального каталога NX. Число включённых команд зависит от разрешённых `BUTTON ID`.

## Возможности

- загрузка source profile schema 3–4 с runtime migration в schema 5;
- отображение версии NX, модулей и Leader-последовательностей;
- обзор команд с точным `BUTTON ID`;
- отображение Bridge: `ONLINE`, `STALE`, `OFFLINE`;
- активный модуль, selection, Work Part и последний результат;
- контекстное ранжирование команд;
- объяснение недоступности по guards;
- изменение trigger и таймаутов Leader;
- запуск HotkeyStudio и фонового Leader Engine;
- локальный поиск по Catalog Studio;
- русские поисковые запросы.

## Вкладка «Обзор»

Показывает:

- путь к активному профилю;
- имя и версию NX;
- число модулей и последовательностей;
- количество строк с непустым `BUTTON ID`;
- активный module/application;
- selection count и selected types;
- Work/Display Part;
- состояние и последний результат Bridge.

Для полного профиля число строк может быть значительно больше 1169 из-за curated-команд и опционального дублирования глобальных намерений во все модули.

## Вкладка «Adaptive Leader»

Список содержит:

- путь;
- module scope;
- имя команды;
- точный `BUTTON ID`;
- состояние контекста;
- destructive/selection признаки.

`AdaptiveLeaderPolicy` учитывает активный модуль, общие слои, selection, modal dialog, Work Part, destructive flag и локальную историю.

Control Center просматривает и ранжирует команды. Выполнение выполняют Leader Engine и CommandBridge.

## Статусы полной карты

Если профиль содержит метаданные компилятора, учитывайте:

- `existing` — ID известен из базового профиля;
- `resolved` — ID найден в локальном каталоге;
- `ambiguous` — команда отключена;
- `unresolved` — команда отключена.

Control Center не должен интерпретировать непустое имя как доказательство исполняемости. Критерий — `enabled`, точный `command.id`, runtime guards и проверка Bridge.

## NX API Explorer

Поддерживаемые файлы Catalog Studio:

```text
04_nxopen_members.csv
05_nxopen_entry_points.csv
06_ui_commands_buttons.csv
07_ufun_functions.csv
08_ui_command_api_candidates.csv
```

Примеры:

```text
Как через NXOpen создать выдавливание?
поиск API для отверстия
selection manager
edge blend builder
UG_FILE_SAVE_PART
```

Результат — поисковая подсказка, а не доказательство эквивалентности UI-команды и API-вызова.

## Настройки

Поддерживаются:

- `trigger_key`;
- `first_key_timeout_ms`;
- `next_key_timeout_ms`;
- `hook_only_when_nx_active`;
- путь к API-каталогу.

После сохранения изменяется указанный профиль. Перед редактированием сгенерированного полного профиля сохраните его копию: повторная компиляция может перезаписать файл.

## Сборка

```powershell
dotnet build .\NX2512_ControlCenter\NX2512_ControlCenter.csproj `
  -c Release `
  -p:Platform=x64
```

```powershell
dotnet publish .\NX2512_ControlCenter\NX2512_ControlCenter.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o .\dist\control-center
```

Требуется .NET 8 Desktop Runtime.

## Запуск

```powershell
.\dist\control-center\NX2512_ControlCenter.exe `
  --config .\config\nx2512-pro-hybrid.json
```

Полный профиль:

```powershell
.\dist\control-center\NX2512_ControlCenter.exe `
  --config .\config\nx2512-pro-full.generated.json `
  --catalog "D:\NX2512_Catalog_Output"
```

Переменная окружения каталога:

```powershell
$env:NXKEYS_CATALOG_DIR = "D:\NX2512_Catalog_Output"
```

Auto-discovery проверяет `%LOCALAPPDATA%\NXKeys\catalog` и ищет каталог с `04_nxopen_members.csv` или `06_ui_commands_buttons.csv`.

## Связь с HotkeyStudio

Control Center ищет `NX2512_HotkeyStudio.exe` рядом со своей папкой и в родительском managed root.

Стандартная структура:

```text
managed-root\
├─ NX2512_HotkeyStudio.exe
├─ nx2512-pro-hybrid.json
└─ control-center\
   └─ NX2512_ControlCenter.exe
```

## Метрика покрытия

Базовая карточка вычисляет долю включённых последовательностей с непустым ID. Она не проверяет:

- наличие кнопки в фактической NX;
- лицензию;
- чувствительность команды;
- успешное runtime-выполнение;
- семантическую правильность auto-resolution;
- полноту пользовательского workflow.

Для строгой оценки используйте:

```text
docs/generated/full-command-resolution.md
runtime probe
completed/failed Bridge results
```

## Контекст Bridge

Protocol schema 3 использует:

```text
revision, status, application_id, module_id, module_label,
selection_count, selection_state, selected_types,
work_part_available, display_part_available,
modal_dialog_active, active_command_id,
context_confidence, updated_utc, last_result
```

`selection_count = -1` означает неизвестное значение.

## Локальная статистика

```text
%LOCALAPPDATA%\NXKeys\leader-usage.json
```

Это runtime-состояние, его не следует добавлять в Git.

## Ограничения

- Windows x64;
- Control Center не применяет MenuScript;
- не редактирует полное дерево путей как специализированный IDE;
- не заменяет full-map compiler;
- не заменяет health, backup/restore и runtime probe;
- API Explorer не выполняет семантический анализ кода;
- production-готовность команды подтверждается только в целевой NX.

Общая документация: [../docs/README.md](../docs/README.md).
