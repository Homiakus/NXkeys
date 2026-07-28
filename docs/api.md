# NXKeys IPC API and File Queue

**Protocol schema:** 3  
**Encoding:** JSON UTF-8  
**IPC root:** `%LOCALAPPDATA%\NXKeys\bridge`

Источник истины: `NXKeys.Protocol/NxProtocol.cs`.

## Каталоги

```text
%LOCALAPPDATA%\NXKeys\bridge\
├── pending\       запросы HotkeyStudio
├── processing\    атомарно захваченные запросы
├── completed\     успешно завершённые запросы и результаты
├── failed\        отклонённые, ошибочные и interrupted_unknown
├── context.json   текущий контекст NX
└── status.json    heartbeat и диагностический статус Bridge
```

## `NxCommandRequest`

Пример обычной команды:

```json
{
  "schema_version": 3,
  "request_id": "20260728-001",
  "action": "execute_command",
  "command_id": "UG_MODELING_EXTRUDED_FEATURE",
  "command_name": "Extrude",
  "sequence": "M C F E",
  "module_id": "modeling",
  "target_application_id": "",
  "selection_filter": "",
  "created_utc": "2026-07-28T19:30:00Z",
  "expires_utc": "2026-07-28T19:30:15Z",
  "source_process_id": 1234,
  "expected_context_revision": 42,
  "expected_selection_count": -1,
  "expected_application_id": "UG_APP_MODELING",
  "destructive": false,
  "confirmation_accepted": false
}
```

### Поля запроса

| Поле | Тип | Назначение |
|---|---|---|
| `schema_version` | integer | Должно быть `3`. |
| `request_id` | string | Уникальный идентификатор запроса. |
| `action` | string | `execute_command`, `set_selection_filter`, `switch_module` или диагностическое действие Bridge. |
| `command_id` | string | Точный NX `BUTTON ID`; обязателен кроме допустимого `switch_module`. |
| `command_name` | string | Читаемое имя. |
| `sequence` | string | Нормализованная внутренняя последовательность, включая module prefix. |
| `module_id` | string | Ожидаемый runtime module. |
| `target_application_id` | string | Целевое приложение для `switch_module`. |
| `selection_filter` | string | Тип global selection filter. |
| `created_utc` | ISO-8601 string | Время создания. |
| `expires_utc` | ISO-8601 string | Срок действия; default lifetime в коде — 15 секунд. |
| `source_process_id` | integer | PID процесса-источника. |
| `expected_context_revision` | integer | Ожидаемая revision контекста. |
| `expected_selection_count` | integer | Ожидаемое число выбранных объектов; `-1` означает «не проверять/неизвестно». |
| `expected_application_id` | string | Ожидаемое активное приложение NX. |
| `destructive` | boolean | Потенциально разрушительная операция. |
| `confirmation_accepted` | boolean | Явное подтверждение пользователя. |

## Действия

### `execute_command`

Bridge проверяет контекст и вызывает точный `command_id` через NX MenuBar/DialogTester. Если задан `selection_filter`, фильтр применяется перед вызовом команды.

### `set_selection_filter`

```json
{
  "schema_version": 3,
  "request_id": "20260728-filter-edge",
  "action": "set_selection_filter",
  "command_id": "UG_SEL_EDGE_PRIORITY",
  "command_name": "Edge",
  "sequence": "F S E",
  "module_id": "selection_object",
  "selection_filter": "edge",
  "created_utc": "2026-07-28T19:30:00Z",
  "expires_utc": "2026-07-28T19:30:15Z",
  "expected_context_revision": 42,
  "expected_selection_count": -1,
  "destructive": false,
  "confirmation_accepted": false
}
```

Допустимые значения:

```text
none, all, reset, edge, face, body, component,
curve, datum, feature, operation
```

`command_id` сохраняется для трассировки. Bridge не обязан запускать `UG_SEL_*` как menu button: он применяет `NXOpen.Select.FilterMember`.

### `switch_module`

Для смены приложения требуется `target_application_id` либо подходящий `command_id`. HFSM завершает switch только после нового контекста с подтверждённым приложением/модулем.

## Валидация запроса

`NxCommandRequest.Validate()` проверяет:

- protocol schema 3;
- непустой `request_id`;
- непустой `action`;
- обязательный `command_id` для обычного действия;
- target application для switch;
- filter или command ID для `set_selection_filter`;
- expiry;
- явное подтверждение destructive-запроса.

## `NxContextSnapshot`

```json
{
  "schema_version": 3,
  "revision": 42,
  "status": "running",
  "application_id": "UG_APP_MODELING",
  "module_id": "modeling",
  "module_label": "Modeling",
  "selection_count": 2,
  "selection_state": "known",
  "selected_types": ["Edge"],
  "work_part_available": true,
  "display_part_available": true,
  "modal_dialog_active": false,
  "active_command_id": "",
  "context_confidence": 100,
  "updated_utc": "2026-07-28T19:30:00Z",
  "last_request_id": "20260728-001",
  "last_result": "executed",
  "last_message": "OK"
}
```

### Семантика

- `selection_count = -1` — число неизвестно, не ноль;
- `selection_state` описывает достоверность выбора;
- `revision` меняется при семантическом изменении;
- `updated_utc` используется для freshness;
- default freshness в protocol — 3 секунды;
- UI-компоненты могут использовать более мягкий порог отображения, но dispatch обязан соблюдать policy.

## `NxCommandResult`

```json
{
  "schema_version": 3,
  "request_id": "20260728-001",
  "status": "executed",
  "message": "OK",
  "context_revision": 42,
  "completed_utc": "2026-07-28T19:30:01Z"
}
```

Успешными считаются статусы `executed` и `completed`. Ошибки, reject и `interrupted_unknown` сопровождаются диагностическим `message`.

## Queue semantics

1. HotkeyStudio атомарно создаёт запрос в `pending`.
2. Bridge перемещает его в `processing`.
3. После выполнения создаётся result JSON.
4. Запрос архивируется в `completed` или `failed`.
5. Повторный `request_id` не выполняется повторно.
6. Незавершённый запрос после сбоя NX получает `interrupted_unknown`.

Это обеспечивает at-most-once поведение для потенциально разрушительных действий.

## Совместимость с полной картой

Поля `frequency`, `catalog_refs`, `resolution_status` и `resolution_candidates` относятся к profile layer и в IPC не передаются. Bridge получает только точный исполняемый `command_id`, action, context expectations и safety flags.
