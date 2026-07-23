# Архитектура конечных автоматов NXKeys

## Назначение

Контекстные сочетания NXKeys реализованы четырьмя согласованными слоями:

1. `SequenceAutomaton` — детерминированный автомат распознавания клавишных последовательностей.
2. `LeaderStateMachine` — иерархический автомат пользовательского взаимодействия.
3. `ContextGuardEvaluator` — единая проверка контекста Siemens NX перед dispatch.
4. `LeaderBehaviorProfile` — декларативные guards, fallback-действия и таймауты состояний.

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
- ошибка записи запроса немедленно завершает `Dispatching` и освобождает автомат;
- завершение запроса переводит автомат в `Idle` или `Root` для sticky-режима.

## DFA последовательностей

При запуске профиль компилируется в trie/DFA. Компилятор отклоняет:

- пустые последовательности;
- дубликаты после нормализации;
- узлы, которые одновременно являются командой и префиксом более длинной команды;
- команды без достижимого терминального состояния.

Каждый ввод имеет один однозначный переход. Поиск команд является отдельным состоянием HFSM и не изменяет DFA.

## Декларативная policy

Файл:

```text
config/nx2512-state-machines.json
```

автоматически включается в `dotnet publish` и устанавливаемый managed-пакет. Альтернативный путь можно задать через:

```text
NXKEYS_STATE_MACHINE_CONFIG
```

Policy содержит отдельные таймауты:

```json
{
  "timeouts": {
    "root_ms": 4000,
    "prefix_ms": 2500,
    "search_ms": 5000,
    "confirmation_ms": 10000,
    "result_ms": 20000,
    "module_switch_ms": 8000
  }
}
```

Для каждой нормализованной последовательности можно определить:

- допустимые модули;
- допустимые состояния Bridge;
- допустимое состояние взаимодействия: `idle`, `modal_dialog`, `command_active`;
- минимальную достоверность контекста;
- необходимость work/display part;
- минимальное и максимальное число выбранных объектов;
- `types_any` и `types_all` для выбранных NXOpen-типов;
- обязательное подтверждение;
- формальное действие `on_unavailable`.

Пример:

```json
{
  "commands": {
    "MB": {
      "guards": {
        "modules": ["modeling"],
        "require_work_part": true,
        "minimum_context_confidence": 60,
        "selection": {
          "minimum": 1,
          "types_any": ["Edge"]
        }
      },
      "on_unavailable": {
        "action": "show_reason",
        "message": "Выберите одно или несколько рёбер"
      }
    },
    "DB": {
      "guards": {
        "modules": ["drafting"]
      },
      "on_unavailable": {
        "action": "switch_module",
        "target_module": "drafting",
        "retry_once": true
      }
    }
  }
}
```

`switch_module` не меняет локальный модуль немедленно. HFSM переходит в `SwitchingModule` и продолжает команду только после подтверждённого `context.module_id`.

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
active_command_id
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
- deterministic replay одного event log;
- randomized-проверку 20 000 переходов;
- проверку запрета обхода confirmation;
- проверку типизированных selection guards;
- проверку fallback `switch_module`;
- проверку загрузки декларативных таймаутов;
- проверку snake_case round-trip;
- проверку expiry;
- проверку destructive confirmation;
- сборку HotkeyStudio и Control Center;
- проверку наличия policy JSON в publish output;
- компиляцию CommandBridge против минимального NXOpen contract stub;
- статическую проверку отсутствия старых неявных флагов;
- проверку queue/context/deployment-инвариантов.

Contract stubs используются только в CI. Поставляемый Bridge по-прежнему собирается против DLL конкретной установленной версии Siemens NX 2512.

## Границы проверки

Contract build подтверждает компилируемость и ожидаемую форму используемого NXOpen API, но не заменяет интеграционный тест внутри реального NX. Перед эксплуатацией destructive-команд необходимо проверить Bridge на целевой сборке NX, роли и лицензии.
