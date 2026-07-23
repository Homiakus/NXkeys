# Архитектура NXKeys

## 1. Назначение системы

NXKeys разделяет редактирование профиля, генерацию файлов NX, выполнение UI-команд и исследование API. Такое разделение уменьшает риск изменения NX и позволяет проверять каждый слой отдельно.

## 2. Компоненты

```text
JSON-профиль
   │
   ├── Go CLI/TUI ── сканирование, план, MenuScript, backup/restore
   │
   ├── HotkeyStudio ── редактор, Leader HUD, deploy, health
   │        │
   │        └── файловая очередь Bridge
   │                 │
   │                 └── NX2512_CommandBridge.dll внутри NX
   │
   ├── Control Center ── контекстный обзор, настройки, API Explorer
   │
   └── Catalog Studio ── CSV-каталог UI/NXOpen/UFUN
```

### Go CLI/TUI

Пакеты:

- `internal/config` — загрузка, значения по умолчанию и валидация;
- `internal/discovery` — ограниченное сканирование файлов NX;
- `internal/nxmenu` — чтение MenuScript, каталог и генерация оверлея;
- `internal/engine` — планирование и применение;
- `internal/backup` — резервные копии и восстановление;
- `internal/tui` — интерфейс Bubble Tea/Lip Gloss.

### HotkeyStudio

WinForms-приложение выполняет:

- редактирование JSON-профиля;
- поиск и разрешение команд;
- управление Leader Hook/HUD;
- редактирование radial-планов;
- развёртывание;
- health-check;
- просмотр очередей Bridge;
- восстановление резервных копий.

### Adaptive Control Center

Control Center использует модели и сервисы HotkeyStudio через ссылку на проект. Он не дублирует полный редактор, а предоставляет компактный слой:

- обзор профиля и Bridge;
- контекстно ранжированный список Leader-команд;
- основные настройки Leader;
- запуск HotkeyStudio/Leader;
- поиск по CSV-каталогу API.

### Command Bridge

`NX2512_CommandBridge.dll` работает внутри процесса NX.

Основной поток:

```text
HotkeyStudio/Leader
  → pending/<request>.json
  → FileSystemWatcher или периодический опрос
  → GetButtonFromName(BUTTON_ID)
  → проверка Availability/Sensitivity
  → InvokeMenuButtonAction
  → completed или failed
  → context.json/status.json/log
```

Файловая очередь выбрана как простая диагностируемая граница между внешним интерфейсом и процессом NX.

### Catalog Studio

Catalog Studio инвентаризирует:

- UI `BUTTON ID`;
- сборки и пространства имён NXOpen;
- типы и члены;
- вероятные точки входа;
- UFUN-заголовки/функции;
- кандидатный crosswalk UI → API.

Между UI-кнопкой и API-методом часто нет однозначного соответствия. Поэтому crosswalk не используется как автоматическое доказательство эквивалентности.

## 3. Источник истины

```text
config/*.json
```

Производные артефакты:

- `.men`;
- `.tbr`;
- `.rtb`;
- `custom_dirs.dat`;
- launcher `.cmd`;
- `resolution-report.md`;
- `radial-menu-plan.*`;
- runtime-очереди и журналы.

Редактирование производного файла без изменения JSON будет потеряно при следующем применении.

## 4. Контекст Leader

Контекст содержит или может содержать:

```json
{
  "schema_version": 1,
  "revision": 0,
  "status": "running",
  "application_id": "UG_APP_MODELING",
  "module_id": "modeling",
  "module_label": "Modeling",
  "selection_count": -1,
  "work_part_available": true,
  "display_part_available": true,
  "modal_dialog_active": false,
  "updated_utc": "..."
}
```

`-1` для `selection_count` означает, что количество выбранных объектов неизвестно. Потребитель не должен интерпретировать неизвестное значение как гарантированное отсутствие выбора.

Текущий Bridge гарантированно публикует активное приложение, модуль, время и последний результат. Расширенные поля зависят от фактически установленной версии Bridge.

## 5. AdaptiveLeaderPolicy

Оценка команды состоит из:

- базового веса;
- бонуса совпадения модуля;
- бонуса общего слоя;
- частоты и давности использования;
- штрафа за разрушительность;
- блокировки при известном отсутствии требуемого выбора;
- блокировки при известном модальном диалоге;
- блокировки при известном отсутствии рабочей детали.

Control Center применяет политику для отображения. Интеграцию одной и той же политики непосредственно в Leader HUD следует рассматривать как отдельный этап развития, пока код HUD использует собственные индексы и правила видимости.

## 6. Протокол Bridge

Запрос схемы v2:

```json
{
  "schema_version": 2,
  "request_id": "...",
  "action": "execute_command",
  "command_id": "UG_...",
  "command_name": "...",
  "sequence": "M E",
  "module_id": "modeling",
  "target_application_id": "",
  "created_utc": "...",
  "expires_utc": "...",
  "source_process_id": 1234,
  "expected_context_revision": 0,
  "expected_selection_count": -1
}
```

Поля ожидания контекста позволяют развивать защиту от выполнения устаревшей команды. Потребитель Bridge должен сохранять обратную совместимость с запросами старой схемы.

## 7. Развёртывание

### managed-wrapper

Создаёт отдельное дерево и launcher, который задаёт `UGII_CUSTOM_DIRECTORY_FILE` только для запущенного процесса NX.

Преимущества:

- минимальное влияние на существующую настройку;
- простое удаление;
- предсказуемый набор файлов;
- удобное тестирование.

### existing-custom-dirs

Подключает NXKeys к существующему списку custom directories. Используется только при осознанной интеграции с корпоративной конфигурацией.

## 8. Версии MenuScript

Генераторы обязаны сохранять:

```text
.men       VERSION 139
.tbr/.rtb  VERSION 170
```

## 9. Состояние и журналы

```text
%LOCALAPPDATA%\NXKeys\
├─ backups
├─ bridge
├─ cache
├─ catalog
├─ logs
└─ leader-usage.json
```

Runtime-данные не должны попадать в Git.

## 10. Тестовые границы

Минимальный набор проверок:

- unit-тесты Go;
- `go vet`;
- сборка HotkeyStudio;
- сборка Control Center;
- сборка Bridge против целевых NXOpen DLL;
- JSON-валидация профилей;
- равенство распространяемых и встроенных профилей;
- тест генерации MenuScript;
- тест backup/restore;
- ручной runtime-тест внутри NX для каждого критичного `BUTTON ID`.

## 11. Направления развития

- единая схема JSON для Go и C#;
- полная проверка уникальности Leader-последовательностей;
- публикация расширенного контекста непосредственно Bridge;
- интеграция AdaptiveLeaderPolicy в HUD;
- версионированный SQLite-каталог API;
- строгие API-карточки с сигнатурами и примерами;
- телеметрия выполнения без содержимого пользовательских моделей;
- измеряемое workflow-покрытие по ролям пользователей.