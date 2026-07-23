# NXKeys для Siemens NX 2512

NXKeys — набор инструментов для безопасной настройки горячих клавиш, последовательного Leader-меню, радиальных меню и выполнения UI-команд в Siemens NX 2512 под Windows.

Проект объединяет несколько интерфейсов, использующих общий JSON-профиль:

| Компонент | Назначение |
|---|---|
| `NX2512_ControlCenter` | единый русскоязычный центр управления, просмотр адаптивного Leader-меню, метрик покрытия, состояния NX Bridge и поиск по NXOpen/UFUN-каталогу |
| `NX2512_HotkeyStudio` | основной редактор профиля, горячих клавиш, Leader-последовательностей, радиальных меню, развёртывания и резервных копий |
| `NX2512_CommandBridge` | библиотека NXOpen, загружаемая внутрь NX и выполняющая проверенные `BUTTON ID` в контексте текущего приложения |
| `NX2512_Catalog_Studio` | формирование каталога UI-команд, типов и членов NXOpen, точек входа и функций UFUN |
| `nxkeys` | Go CLI/TUI для сканирования NX, разрешения команд, планирования изменений, применения MenuScript и восстановления резервных копий |

Основной профиль проекта: **NX Pro Hybrid 2512.6000**.

> NXKeys не является продуктом Siemens и не заменяет штатную настройку NX. Доступность конкретной команды зависит от установленной лицензии, роли, локализации, приложения NX и активного контекста.

## Содержание

- [Текущий статус](#текущий-статус)
- [Возможности](#возможности)
- [Быстрый запуск](#быстрый-запуск)
- [Сборка](#сборка)
- [Установка в NX](#установка-в-nx)
- [Control Center](#control-center)
- [HotkeyStudio](#hotkeystudio)
- [Leader-меню](#leader-меню)
- [NX Command Bridge](#nx-command-bridge)
- [NX API Explorer](#nx-api-explorer)
- [Профили и покрытие 80%](#профили-и-покрытие-80)
- [Безопасность и резервные копии](#безопасность-и-резервные-копии)
- [Структура каталогов](#структура-каталогов)
- [Команды CLI](#команды-cli)
- [Проверка и диагностика](#проверка-и-диагностика)
- [Документация](#документация)
- [Разработка](#разработка)

## Текущий статус

На текущем этапе реализованы:

- JSON-профили схемы v1 и v2;
- модульные наборы команд для основных приложений NX;
- генерация безопасных MenuScript-оверлеев;
- редактор HotkeyStudio;
- глобальный Leader Key с HUD;
- файловый NX Command Bridge;
- Adaptive Control Center;
- поиск по экспортам NXOpen, UFUN и UI-каталога;
- резервное копирование и контролируемое восстановление;
- Go CLI/TUI;
- CI-сборка Go, HotkeyStudio и Control Center на `windows-latest`.

Важные ограничения текущей реализации:

1. **80% — целевой охват рабочего процесса, а не 80% всех команд NX.** NX содержит тысячи специализированных команд, поэтому покрытие оценивается по типовым операциям конструктора.
2. Control Center контекстно ранжирует команды и объясняет ограничения. Само выполнение по Leader-последовательности остаётся задачей HotkeyStudio и Command Bridge.
3. Текущий Bridge надёжно передаёт активное приложение и модуль. Расширенные поля выбора объектов и состояния рабочей детали поддерживаются клиентской моделью, но могут отображаться как неизвестные, пока соответствующие данные не записаны загруженной версией Bridge.
4. Поиск UI-команды → NXOpen/UFUN является **кандидатным сопоставлением**. Он не доказывает, что конкретный API-вызов полностью эквивалентен нажатию кнопки NX.
5. Радиальные меню автоматически развёртываются только через заранее экспортированную и проверенную роль `.mtx`. Внутренний бинарный формат роли не изменяется.

## Возможности

### Управление горячими клавишами

- привязка сочетания к точному `BUTTON ID`;
- поиск команды по имени и псевдонимам;
- обнаружение слабых и неоднозначных совпадений;
- генерация `.men`-оверлея без изменения файлов установки Siemens;
- отчёт разрешения команд до применения.

### Адаптивное Leader-меню

- последовательности вида `CapsLock → M → E`;
- группировка по модулям NX;
- контекстное ранжирование в Control Center;
- блокировка или понижение приоритета опасных действий;
- подтверждение разрушительных команд клавишей `Enter`;
- поиск по командам через `Space`;
- возврат через `Backspace`, отмена через `Esc`;
- локальная статистика использования в `%LOCALAPPDATA%\NXKeys\leader-usage.json`.

### Развёртывание и восстановление

- предварительный план без записи;
- режим `dry-run`;
- атомарная запись через временный файл;
- SHA-256-манифест резервной копии;
- проверка хеша перед восстановлением;
- отдельный управляемый каталог NXKeys;
- возможность подключения через существующий `custom_dirs.dat`.

### Каталог команд и API

Catalog Studio формирует CSV-экспорты:

- `04_nxopen_members.csv`;
- `05_nxopen_entry_points.csv`;
- `06_ui_commands_buttons.csv`;
- `07_ufun_functions.csv`;
- `08_ui_command_api_candidates.csv`.

Control Center загружает эти файлы и позволяет искать команды по русским и английским словам.

## Требования

- Windows 10 или Windows 11 x64;
- Siemens NX / Designcenter NX 2512;
- .NET 8 SDK для сборки C#-компонентов;
- Go 1.25+ для Go CLI/TUI;
- доступ к `NXOpen.dll`, `NXOpen.UF.dll` и `NXOpenUI.dll` для сборки Command Bridge;
- закрытый NX при полном обновлении установленного Bridge, потому что NX может удерживать DLL заблокированной.

## Быстрый запуск

### 1. Собрать HotkeyStudio

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\NX2512_HotkeyStudio\build.ps1
```

### 2. Установить управляемый пакет NXKeys

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-nx-ribbon-buttons.ps1
```

### 3. Запустить NX через созданную обёртку

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\launch-nx2512-with-nxkeys.cmd
```

Обёртка запускает фоновый Leader Engine и затем NX с приватным `UGII_CUSTOM_DIRECTORY_FILE`.

### 4. При необходимости собрать Control Center

```powershell
dotnet publish .\NX2512_ControlCenter\NX2512_ControlCenter.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o .\dist\control-center
```

Запуск:

```powershell
.\dist\control-center\NX2512_ControlCenter.exe `
  --config .\config\nx2512-pro-hybrid.json
```

Control Center пока публикуется отдельно и автоматически не копируется установщиком управляемого NX-пакета.

## Сборка

### Go CLI/TUI

```powershell
.\scripts\build.ps1
```

Результат создаётся в `dist\`.

### HotkeyStudio

```powershell
dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -c Release -p:Platform=x64
```

### Adaptive Control Center

```powershell
dotnet build .\NX2512_ControlCenter\NX2512_ControlCenter.csproj -c Release -p:Platform=x64
```

### NX Command Bridge

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\NX2512_CommandBridge\build.ps1
```

Bridge требует корректно найденный каталог NXOpen целевой установки NX 2512.

## Установка в NX

Рекомендуемый режим — `managed-wrapper`. Он создаёт приватный каталог кастомизации и активирует его только для NX, запущенного через сгенерированный `.cmd`.

Ключевое правило версий MenuScript:

| Тип файла | Версия |
|---|---:|
| `.men` | `VERSION 139` |
| `.tbr` / `.rtb` | `VERSION 170` |

NXKeys генерирует версии по типу файла. Смешивание этих значений вызывает ошибки парсера NX.

Подробная инструкция: [docs/INSTALLATION.md](docs/INSTALLATION.md).

## Control Center

`NX2512_ControlCenter` — дополнительный русскоязычный интерфейс для контроля текущей конфигурации.

Вкладки:

- **Обзор** — профиль, версия NX, количество Leader-последовательностей, доля команд с `BUTTON ID`, состояние и свежесть Bridge;
- **Adaptive Leader** — ранжированный список команд текущего модуля и причины временной недоступности;
- **NX API Explorer** — поиск по CSV-каталогу NXOpen/UFUN/UI;
- **Настройки** — trigger, таймауты Leader, ограничение перехвата активным NX и путь к API-каталогу.

Control Center может:

- сохранить изменённый JSON-профиль;
- запустить HotkeyStudio в GUI-режиме;
- запустить фоновый Leader Engine;
- автоматически найти последний каталог в `%LOCALAPPDATA%\NXKeys\catalog`;
- использовать путь из `NXKEYS_CATALOG_DIR` или параметра `--catalog`.

Он не заменяет полный редактор профиля HotkeyStudio.

## HotkeyStudio

Основные разделы HotkeyStudio:

- **Обзор** — профиль, процессы NX, состояние Bridge и нерешённые команды;
- **Команды** — клавиатурные привязки и поиск по каталогу;
- **Leader Key** — запуск/остановка hook, HUD и управление последовательностями;
- **Radials** — редактор восьми направлений;
- **NX / Bridge** — сканирование, сгенерированные файлы и очереди Bridge;
- **Deploy** — план, dry-run и применение;
- **Backups / Profile** — сохранение профиля и восстановление резервных копий.

Прямой запуск:

```powershell
& "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\NX2512_HotkeyStudio.exe" `
  --gui `
  --config ".\config\nx2512-pro-hybrid.json"
```

## Leader-меню

По умолчанию используется `CapsLock`, но в профиле можно выбрать другой trigger.

Управление HUD:

| Клавиша | Действие |
|---|---|
| `Tab` / `Shift+Tab` | следующий или предыдущий модуль при ручном выборе |
| `Space` | поиск по командам |
| `Backspace` | удалить символ или вернуться на уровень выше |
| `Esc` | закрыть Leader HUD |
| `Enter` | подтвердить опасную команду |

Контекстная логика учитывает:

- активный модуль;
- общие слои `selection_object`, `inspect_view`, `reuse`;
- требование выбранного объекта, когда Bridge передаёт этот признак;
- наличие модального диалога, когда Bridge передаёт этот признак;
- разрушительность и необходимость подтверждения;
- локальную историю использования в Adaptive Control Center.

## NX Command Bridge

`NX2512_CommandBridge.dll` загружается внутрь NX и обрабатывает JSON-запросы из:

```text
%LOCALAPPDATA%\NXKeys\bridge\pending
```

Результаты перемещаются в:

```text
%LOCALAPPDATA%\NXKeys\bridge\completed
%LOCALAPPDATA%\NXKeys\bridge\failed
```

Текущий протокол запроса содержит:

- `schema_version`;
- уникальный `request_id`;
- действие `execute_command` или `switch_module`;
- `command_id` и имя команды;
- последовательность Leader;
- модуль и целевое приложение;
- время создания и истечения запроса;
- PID источника;
- ожидаемую ревизию контекста и количество выбранных объектов.

Bridge проверяет, что кнопка существует, доступна и чувствительна, затем вызывает её через NX UI. Ошибка контекста записывается в `failed`, а не маскируется.

## NX API Explorer

Запуск с явным каталогом:

```powershell
.\dist\control-center\NX2512_ControlCenter.exe `
  --config .\config\nx2512-pro-hybrid.json `
  --catalog "D:\NX2512_Full_Function_API_Catalog_YYYYMMDD_HHMMSS"
```

Или:

```powershell
$env:NXKEYS_CATALOG_DIR = "D:\NX2512_Full_Function_API_Catalog_YYYYMMDD_HHMMSS"
```

Примеры запросов:

```text
Как через NXOpen создать выдавливание?
поиск функции UFUN для тела
selection manager
edge blend builder
UG_FILE_SAVE_PART
```

Поиск основан на токенах и небольшом русско-английском словаре. Это локальный поисковый интерфейс, а не генератор готового безопасного NXOpen-кода.

## Профили и покрытие 80%

Источником истины является JSON. Сгенерированные `.men`, `.tbr`, `.rtb` и отчёты вручную не редактируются — следующее применение заменит их.

Основные профили:

- `config/nx2512-pro-hybrid.json`;
- `config/nx2512-ergo-80.json`;
- встроенные копии в `internal/defaults/`.

Показатель покрытия нужно разделять:

| Метрика | Что показывает |
|---|---|
| Workflow coverage | доля типовых операций, доступных через штатные клавиши, Leader, radial или поиск |
| Leader coverage | доля запланированных Leader-команд в профиле |
| Verified BUTTON ID | доля включённых команд с непустым точным `BUTTON ID` |
| Runtime availability | команды, реально принятые NX в текущем модуле и контексте |
| API mapping coverage | команды, для которых найден кандидат NXOpen/UFUN |

Control Center сейчас отображает долю включённых Leader-последовательностей с непустым `BUTTON ID`. Это полезная проверка профиля, но не доказательство успешного выполнения внутри NX.

Подробная раскладка: [NX_2512_Ergonomic_80_горячие_клавиши_и_радиальные_меню.md](NX_2512_Ergonomic_80_горячие_клавиши_и_радиальные_меню.md).

## Безопасность и резервные копии

NXKeys:

- не перезаписывает файлы установки Siemens;
- не декодирует и не модифицирует внутренности `.mtx`;
- исключает неоднозначные команды из оверлея;
- формирует план до записи;
- создаёт резервную копию перед первой записью;
- использует атомарную замену при включённом `atomic_writes`;
- проверяет хеш перед восстановлением;
- требует закрыть NX для полного обновления заблокированной DLL.

Подробно: [docs/SAFETY_MODEL.md](docs/SAFETY_MODEL.md).

## Структура каталогов

Управляемая установка:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\
├─ NX2512_HotkeyStudio.exe
├─ NX2512_HotkeyStudio.dll
├─ nx2512-pro-hybrid.json
├─ launch-nx2512-with-nxkeys.cmd
├─ custom_dirs.dat
├─ custom\
│  ├─ application\
│  │  ├─ NX2512_CommandBridge.dll
│  │  └─ nxkeys_command_bridge.men
│  └─ startup\
│     ├─ NX2512_CommandBridge.dll
│     ├─ nxkeys_generated.men
│     ├─ nxkeys_ribbon.rtb
│     ├─ nxkeys_toolbar.tbr
│     ├─ launch-hotkeystudio-daemon.cmd
│     └─ launch-hotkeystudio-gui.cmd
├─ resolution-report.md
├─ radial-menu-plan.md
└─ radial-menu-plan.json
```

Состояние выполнения:

```text
%LOCALAPPDATA%\NXKeys\
├─ backups\
├─ bridge\
│  ├─ pending\
│  ├─ completed\
│  ├─ failed\
│  ├─ context.json
│  └─ status.json
├─ cache\
├─ catalog\
├─ logs\
└─ leader-usage.json
```

## Команды CLI

HotkeyStudio поддерживает операционные команды:

```powershell
$studio = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\NX2512_HotkeyStudio.exe"

& $studio validate --config .\config\nx2512-pro-hybrid.json
& $studio scan --config .\config\nx2512-pro-hybrid.json
& $studio catalog --config .\config\nx2512-pro-hybrid.json
& $studio plan --config .\config\nx2512-pro-hybrid.json
& $studio apply --config .\config\nx2512-pro-hybrid.json --yes
& $studio health --config .\config\nx2512-pro-hybrid.json
& $studio bridge-status
& $studio backups --config .\config\nx2512-pro-hybrid.json
& $studio restore --config .\config\nx2512-pro-hybrid.json
& $studio leader --config .\config\nx2512-pro-hybrid.json
```

Восстановление по конкретному манифесту:

```powershell
& $studio restore `
  --config .\config\nx2512-pro-hybrid.json `
  --manifest "$env:LOCALAPPDATA\NXKeys\backups\YYYYMMDD_HHMMSS.mmm\manifest.json"
```

## Проверка и диагностика

Рекомендуемая проверка:

```powershell
go test ./...
go vet ./...
dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -c Release -p:Platform=x64
dotnet build .\NX2512_ControlCenter\NX2512_ControlCenter.csproj -c Release -p:Platform=x64
```

Проверка установленного профиля:

```powershell
$studio = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\NX2512_HotkeyStudio.exe"
& $studio validate --config .\config\nx2512-pro-hybrid.json
& $studio plan --config .\config\nx2512-pro-hybrid.json
& $studio health --config .\config\nx2512-pro-hybrid.json
```

Нормальные показатели MenuScript:

```text
MenuScript versions OK: yes
Invalid NXKeys VERSION files: 0
Managed package OK: yes
```

Когда NX закрыт, `Bridge loaded: no` является нормальным состоянием.

Подробная диагностика: [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).

## Документация

Начальная страница: [docs/README.md](docs/README.md).

| Документ | Назначение |
|---|---|
| [Установка](docs/INSTALLATION.md) | сборка, установка, обновление и удаление |
| [Конфигурация](docs/CONFIGURATION.md) | поля JSON, модули, Leader, radial и deployment |
| [Архитектура](docs/ARCHITECTURE.md) | компоненты и потоки данных |
| [Модель безопасности](docs/SAFETY_MODEL.md) | инварианты, угрозы и границы |
| [Диагностика](docs/TROUBLESHOOTING.md) | типовые ошибки и порядок проверки |
| [Спецификация NX Pro Hybrid](docs/NX_PRO_HYBRID_SOURCE_SPEC.md) | источник принципов профиля и покрытия |
| [Control Center](NX2512_ControlCenter/README.md) | запуск и ограничения адаптивного интерфейса |
| [Роли NX](roles/README.md) | безопасная работа с экспортированными `.mtx` |

## Структура проекта

```text
cmd/nxkeys/                    точка входа Go CLI/TUI
internal/config/               конфигурация, значения по умолчанию и валидация
internal/discovery/            ограниченное сканирование установки и профилей NX
internal/nxmenu/               парсер MenuScript и генератор оверлея
internal/backup/               резервное копирование и восстановление
internal/engine/               планирование и применение
internal/tui/                  интерфейс Bubble Tea / Lip Gloss
NX2512_HotkeyStudio/           редактор, Leader HUD и службы развёртывания
NX2512_ControlCenter/          адаптивный центр управления и API Explorer
NX2512_CommandBridge/          внутрипроцессный NXOpen Bridge
NX2512_Catalog_Studio/         формирование каталога NXOpen/UFUN/UI
config/                        распространяемые шаблоны профилей
internal/defaults/             встроенные копии профилей Go
roles/                         проверенные экспортированные роли `.mtx`
docs/                          русская документация
scripts/                       сценарии сборки
```

## Разработка

В репозиторий следует добавлять:

- исходный код;
- шаблоны из `config/` и `internal/defaults/`;
- документацию;
- сценарии сборки и установки;
- только намеренно распространяемые роли из `roles/`.

Не следует добавлять:

- `bin/`, `obj/`, `dist/`;
- локальные рабочие профили пользователя;
- содержимое `%LOCALAPPDATA%\NXKeys`;
- резервные копии, очереди Bridge и журналы;
- скомпилированные EXE, DLL, PDB, `runtimeconfig` и `deps`.

## Лицензия

MIT.