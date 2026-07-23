# Архитектура конечных автоматов NXKeys

## Назначение

Контекстные сочетания NXKeys реализованы тремя согласованными слоями:

1. `SequenceAutomaton` — детерминированный автомат распознавания клавишных последовательностей.
2. `LeaderStateMachine` — иерархический автомат пользовательского взаимодействия.
3. `ContextGuardEvaluator` — единая проверка контекста Siemens NX перед dispatch.

Windows keyboard hook не выполняет команды и не изменяет бизнес-состояние напрямую. Он только формирует события, которые последовательно обрабатываются WinForms event loop.

## Состояния Leader

```text
Idle
Root
Prefix
Search
AwaitingConfirmation
Dispatching
AwaitingResult
SwitchingModule
Failed
```

Ключевые инварианты:

- выполнение команды возможно только из `Dispatching`;
- destructive-команда проходит через `AwaitingConfirmation`;
- смена модуля считается завершённой только после нового `context.json` с целевым `module_id`;
- `Esc`, потеря фокуса NX и остановка движка всегда приводят в `Idle`;
- non-sticky тайм-аут всегда освобождает keyboard capture;
- завершение запроса переводит автомат в `Idle` или `Root` для sticky-режима.

## DFA последовательностей

При запуске профиль компилируется в trie/DFA. Компилятор отклоняет:

- пустые последовательности;
- дубликаты после нормализации;
- узлы, которые одновременно являются командой и префиксом более длинной команды;
- команды без достижимого терминального состояния.

Каждый ввод имеет один однозначный переход. Поиск команд является отдельным состоянием HFSM и не изменяет DFA.

## Контекст NX

Bridge публикует `NxContextSnapshot`:

```text
revision
application_id
module_id
selection_count
selection_state
selected_types
work_part_available
display_part_available
modal_dialog_active
context_confidence
updated_utc
```

`revision` изменяется только при семантическом изменении контекста. Обновление времени само по себе не увеличивает revision.

Перед постановкой команды в очередь клиент проверяет свежесть контекста. Перед фактическим выполнением Bridge повторно проверяет:

- срок действия запроса;
- `expected_context_revision`;
- `expected_selection_count`;
- `expected_application_id`;
- отсутствие модального диалога;
- соответствие модуля;
- наличие и чувствительность NX `BUTTON ID`.

## Надёжная очередь

```text
pending
   ↓ atomic claim
processing
   ↓ execute/reject
completed | failed
```

Файл сначала атомарно перемещается из `pending` в `processing`. После результата создаётся `<request_id>.result.json`, затем архивируется исходный запрос.

Если NX аварийно завершился во время `processing`, запрос не воспроизводится автоматически. При следующем запуске он получает результат `interrupted_unknown` и переносится в `failed`. Это обеспечивает at-most-once поведение для потенциально разрушительных команд.

Повторный `request_id` не исполняется: Bridge использует существующий результат.

## Общий протокол

Файл `NXKeys.Protocol/NxProtocol.cs` подключается как shared source в HotkeyStudio и CommandBridge. Это обеспечивает единственную модель snake_case JSON без дополнительной runtime-DLL внутри NX.

Текущая версия протокола: `schema_version = 3`.

## Проверки CI

CI выполняет:

- компиляцию и запуск DFA/HFSM-инвариантов;
- проверку snake_case round-trip;
- проверку expiry;
- проверку destructive confirmation;
- сборку HotkeyStudio и Control Center;
- компиляцию CommandBridge против минимального NXOpen contract stub;
- статическую проверку отсутствия старых неявных флагов;
- проверку queue/context/deployment-инвариантов.

Contract stubs используются только в CI. Поставляемый Bridge по-прежнему собирается против DLL конкретной установленной версии Siemens NX 2512.

## Границы проверки

Contract build подтверждает компилируемость и ожидаемую форму используемого NXOpen API, но не заменяет интеграционный тест внутри реального NX. Перед эксплуатацией destructive-команд необходимо проверить Bridge на целевой сборке NX, роли и лицензии.
