# NXKeys Adaptive Control Center

`NX2512_ControlCenter` — русскоязычный центр обзора и настройки NXKeys для Siemens NX 2512.

Он дополняет `NX2512_HotkeyStudio`, но не заменяет полный редактор профиля и механизм развёртывания.

## Возможности

- загрузка JSON-профиля NXKeys;
- отображение версии NX и количества Leader-последовательностей;
- расчёт доли включённых последовательностей с непустым `BUTTON ID`;
- отображение состояния NX Command Bridge: `ONLINE`, `STALE` или `OFFLINE`;
- просмотр активного модуля, последнего результата Bridge и доступных полей контекста;
- контекстное ранжирование Leader-команд;
- объяснение причин временной недоступности команды;
- изменение trigger и таймаутов Leader;
- включение перехвата только при активном окне NX;
- запуск HotkeyStudio и фонового Leader Engine;
- поиск по экспортам Catalog Studio;
- поддержка простых русских запросов.

## Вкладки

### Обзор

Показывает:

- путь к профилю;
- целевую версию NX;
- число включённых последовательностей;
- количество точных `BUTTON ID`;
- активный модуль;
- количество выбранных объектов, когда Bridge публикует это поле;
- наличие рабочей детали, когда Bridge публикует это поле;
- последний результат и сообщение Bridge.

### Adaptive Leader

Список содержит:

- последовательность;
- модуль;
- имя команды;
- `BUTTON ID`;
- контекстное состояние.

Ранжирование выполняет `AdaptiveLeaderPolicy`. Оно учитывает модуль, общие слои, требование выбора, модальное окно, рабочую деталь, разрушительность и локальную историю использования.

В текущей версии Control Center **просматривает и ранжирует** команды. Непосредственное выполнение последовательностей выполняют Leader Engine HotkeyStudio и Command Bridge.

### NX API Explorer

Загружаются файлы:

```text
04_nxopen_members.csv
05_nxopen_entry_points.csv
06_ui_commands_buttons.csv
07_ufun_functions.csv
08_ui_command_api_candidates.csv
```

Примеры запросов:

```text
Как через NXOpen создать выдавливание?
поиск API для отверстия
selection manager
edge blend builder
UG_FILE_SAVE_PART
```

Русские слова расширяются небольшим встроенным словарём. Поиск выполняется локально по токенам и возвращает до 200 кандидатов.

> Результат API Explorer является поисковой подсказкой. Он не доказывает эквивалентность UI-команды конкретному NXOpen/UFUN-вызову.

### Настройки

Поддерживаются:

- `trigger_key`;
- `first_key_timeout_ms`;
- `next_key_timeout_ms`;
- `hook_only_when_nx_active`;
- путь к API-каталогу.

После сохранения изменяется указанный JSON-профиль.

## Сборка

```powershell
dotnet build .\NX2512_ControlCenter\NX2512_ControlCenter.csproj `
  -c Release `
  -p:Platform=x64
```

Публикация:

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

Путь по умолчанию вычисляется относительно исполняемого файла:

```text
config\nx2512-pro-hybrid.json
```

Поэтому для предсказуемого запуска рекомендуется всегда передавать `--config` явно.

## Подключение API-каталога

### Параметр

```powershell
.\dist\control-center\NX2512_ControlCenter.exe `
  --config .\config\nx2512-pro-hybrid.json `
  --catalog "D:\NX2512_Full_Function_API_Catalog_YYYYMMDD_HHMMSS"
```

### Переменная окружения

```powershell
$env:NXKEYS_CATALOG_DIR = "D:\NX2512_Full_Function_API_Catalog_YYYYMMDD_HHMMSS"
```

### Автоматическое обнаружение

Control Center ищет последний подходящий каталог внутри:

```text
%LOCALAPPDATA%\NXKeys\catalog
```

Каталог считается подходящим, если содержит `04_nxopen_members.csv` или `06_ui_commands_buttons.csv`.

## Связь с HotkeyStudio

Для кнопок запуска Control Center ищет:

```text
NX2512_HotkeyStudio.exe
```

рядом со своим исполняемым файлом либо на ожидаемом соседнем уровне. Глобальный поиск по Windows не выполняется.

Рекомендуемая структура ручной установки:

```text
NXKeys-ControlCenter\
├─ NX2512_ControlCenter.exe
├─ NX2512_HotkeyStudio.exe
├─ зависимости обоих приложений
└─ config\
   └─ nx2512-pro-hybrid.json
```

## Метрики покрытия

Текущая карточка покрытия вычисляет:

```text
включённые Leader-последовательности с непустым BUTTON ID
────────────────────────────────────────────────────────── × 100%
все включённые Leader-последовательности
```

Эта метрика не проверяет:

- наличие кнопки в фактической установке NX;
- доступность лицензии;
- чувствительность команды;
- успешное runtime-выполнение;
- охват всего пользовательского workflow.

Для строгой оценки дополнительно нужны каталог целевой установки и результаты выполнения Bridge.

## Контекст Bridge

Control Center считает контекст свежим, если `updated_utc` моложе примерно 10 секунд.

`selection_count: -1` или отсутствие поля означает неизвестный выбор, а не ноль выбранных объектов.

Клиентская модель поддерживает расширенные поля, однако фактически доступные данные зависят от установленной версии `NX2512_CommandBridge.dll`.

## Локальная статистика Leader

`AdaptiveLeaderPolicy` поддерживает статистику использования в:

```text
%LOCALAPPDATA%\NXKeys\leader-usage.json
```

Файл является локальным runtime-состоянием и не должен добавляться в Git.

## Ограничения

- интерфейс рассчитан на Windows x64;
- минимальный размер окна — 760×560;
- поиск API не анализирует семантику кода;
- Control Center не редактирует полное дерево последовательностей;
- Control Center не применяет MenuScript;
- Control Center не заменяет health-check и backup/restore HotkeyStudio;
- прямое выполнение выбранной строки списка в текущем интерфейсе отсутствует.

Общая документация: [../docs/README.md](../docs/README.md).