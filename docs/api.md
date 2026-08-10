# NXKeys File IPC API — schema 4

NXKeys использует локальный файловый IPC между HotkeyStudio и Command Bridge. Это не HTTP API и не remote service.

Текущий protocol contract:

- schema: **4**;
- JSON UTF-8;
- root: `%LOCALAPPDATA%\NXKeys\bridge`;
- max request payload: 64 KiB;
- max pending files: 256;
- max admitted per Bridge poll: 8;
- max text field length: 1024 characters;
- default context freshness: 3 seconds;
- default request lifetime: 15 seconds.

Источник истины: `NXKeys.Protocol/NxProtocol.cs`. Security admission: `NXKeys.Protocol/NxBridgeSecurity.cs` и Command Bridge.

## Каталоги

```text
%LOCALAPPDATA%\NXKeys\bridge\
├── pending\
├── processing\
├── completed\
├── failed\
├── context.json
└── status.json
```

Lifecycle:

```text
pending → processing → completed | failed
```

Queue file — транспорт, а не authority.

## Supported actions

```text
execute_command
switch_module
set_selection_filter
probe_command
```

Неизвестный action отклоняется fail-closed.

## `NxCommandRequest`

Schema 4 добавляет authenticated envelope и anti-replay data.

Пример структуры:

```json
{
  "schema_version": 4,
  "request_id": "20260810-001",
  "action": "execute_command",
  "command_id": "UG_MODELING_EXTRUDED_FEATURE",
  "command_name": "Extrude",
  "sequence": "M C E",
  "module_id": "modeling",
  "target_application_id": "",
  "selection_filter": "",
  "created_utc": "2026-08-10T14:00:00Z",
  "expires_utc": "2026-08-10T14:00:15Z",
  "source_process_id": 1234,
  "expected_context_revision": 42,
  "expected_selection_count": 1,
  "expected_selection_fingerprint": "...",
  "expected_application_id": "UG_APP_MODELING",
  "session_id": "...",
  "client_instance_id": "...",
  "nonce": "...",
  "sequence_number": 101,
  "profile_digest": "...",
  "payload_hmac": "...",
  "destructive": false,
  "confirmation_accepted": false
}
```

Значение `sequence` — диагностическая/внутренняя последовательность. Пользовательский ввод может быть короче, потому что runtime module prefix добавляется автоматически.

## Поля security envelope

| Поле | Назначение |
|---|---|
| `session_id` | идентификатор shared launch session |
| `client_instance_id` | конкретный HotkeyStudio instance |
| `nonce` | защита от повторного request |
| `sequence_number` | монотонный anti-replay counter |
| `profile_digest` | связывает request с permission set активного profile |
| `payload_hmac` | HMAC-SHA-256 canonical payload |

Managed launcher создаёт случайный session secret и передаёт его только дочерним доверенным процессам через environment. Secret не записывается в queue.

## Validation shared model

`NxCommandRequest.Validate()` проверяет:

- exact schema 4;
- непустые `request_id` и `action`;
- supported action;
- field length limits;
- `command_id` для non-switch actions, кроме допустимой формы selection filter;
- target application/command для `switch_module`;
- filter/command для `set_selection_filter`;
- expiry;
- explicit confirmation для destructive request.

Bridge выполняет дополнительные security/runtime checks перед NX invocation.

## Bridge admission

Перед dispatch Bridge проверяет как минимум:

1. schema и expiry;
2. session id;
3. HMAC constant-time comparison;
4. source process/executable;
5. nonce/sequence anti-replay;
6. `profile_digest`;
7. profile permission для action + canonical command/module/application/filter;
8. destructive/confirmation policy;
9. fresh NX context;
10. expected application/revision/selection fingerprint.

Наличие правильно сформированного JSON без действующей session capability не является разрешением.

## Permission canonicalization

Permission layer должен использовать те же canonical IDs, что runtime.

Для Sheet Metal:

```text
UG_APP_SHEETMETAL → UG_APP_SBSM
UG_SHEET_METAL_*  → UG_SBSM_*
```

V8 availability applications также используются для создания switch permissions, чтобы profile runtime и Bridge allowlist не расходились.

## `execute_command`

Обычный action требует `command_id`. Bridge проверяет availability/sensitivity/context и вызывает точную NX UI command.

Важно: для интерактивных UI commands return value `DialogTester.InvokeMenuButtonAction(...)` не заменяет live-NX verification. Contract build подтверждает API shape, но не семантику конкретного dialog/collector.

## `switch_module`

Требует `target_application_id` либо подходящий `command_id`.

Успех module switch подтверждается **новым свежим context**, а не только записью request.

Для Sheet Metal canonical target — `UG_APP_SBSM`.

## `set_selection_filter`

Это type selection layer, например:

```text
body
face
edge
feature
component
curve
datum
reset
all
none
operation
```

Leader paths `S → …` относятся к фильтру типа объекта. Они не заменяют Selection Intent `0…4`, который работает отдельно внутри Bridge/NX process.

## `probe_command`

Используется для безопасной проверки/диагностики command availability согласно runtime policy. Это supported action и проходит тот же authenticated admission path.

## `NxContextSnapshot`

Schema 4 context содержит:

```text
revision
status
application_id
module_id/module_label
selection_count/state/types/fingerprint
work_part_available
display_part_available
modal_dialog_active
active_command_id
context_confidence
updated_utc
last_request_id/last_result/last_message
security_status
security_session_id
security_profile_digest
```

`selection_count = -1` означает unknown, а не zero.

`revision` меняется по semantic fingerprint, включающему selection fingerprint и security state. Обычный heartbeat без semantic change не должен искусственно менять revision.

## `NxCommandResult`

Пример:

```json
{
  "schema_version": 4,
  "request_id": "20260810-001",
  "status": "executed",
  "message": "OK",
  "context_revision": 43,
  "completed_utc": "2026-08-10T14:00:01Z"
}
```

Shared model считает успешными только statuses:

```text
executed
completed
```

`interrupted_unknown` не является успехом и запрещает автоматический retry без проверки фактического NX state.

## Atomic publication

Writer должен:

1. сериализовать полный request во временный файл;
2. flush/close;
3. атомарно переместить его в `pending`;
4. не изменять после публикации;
5. использовать новый request id/nonce для нового пользовательского действия.

Partial write под final filename недопустим.

## Managed launch requirement

Authenticated schema 4 предполагает shared session. Штатный путь:

```text
launch-nx2512-with-nxkeys.cmd
```

Независимый запуск NX и HotkeyStudio может оставить Bridge в `authentication_required`; commands в такой session должны отклоняться.

## Recovery semantics

Bridge атомарно claims request в `processing`. Если процесс завершился после claim, невозможно гарантировать, произошёл ли side effect в NX.

Поэтому recovery использует at-most-once policy:

```text
processing after interruption → failed/interrupted_unknown
```

Автоматический повтор неизвестного результата запрещён.

## JSON format

Protocol JSON reader:

- case-insensitive properties;
- trailing commas запрещены;
- comments запрещены.

Это строже, чем profile JSON reader HotkeyStudio.

## Ошибки интегратора

| Ошибка | Результат |
|---|---|
| schema 3 request в current Bridge | reject |
| unknown action | reject |
| expired request | reject |
| HMAC/session mismatch | reject |
| повтор nonce/sequence | reject |
| profile digest/permission mismatch | reject |
| stale context/revision | reject |
| changed selection fingerprint | reject |
| wrong app/module | reject |
| destructive без confirmation | reject |
| ручной retry `interrupted_unknown` без проверки | риск duplicate side effect |

## Совместимость

Изменение protocol поля требует согласованного изменения:

1. `NxProtocol.cs`;
2. HotkeyStudio writer;
3. Bridge reader/admission;
4. HMAC canonicalization;
5. tests;
6. recovery semantics;
7. этого документа и [SAFETY_MODEL.md](SAFETY_MODEL.md).

Не создавайте сторонний writer, копируя только JSON example: без session secret, canonical signing, anti-replay и profile permission такой writer не является совместимым client.
