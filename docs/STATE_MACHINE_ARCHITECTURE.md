# Архитектура конечных автоматов NXKeys

## Назначение

Контекстный ввод NXKeys реализован четырьмя слоями:

1. `SequenceAutomaton` — DFA для prefix-free последовательностей.
2. `LeaderStateMachine` — HFSM пользовательского взаимодействия.
3. `ContextGuardEvaluator` — единая проверка контекста NX.
4. `LeaderBehaviorProfile` — декларативные guards, fallback и таймауты.

Keyboard hook не выполняет команды напрямую. Он помещает события в WinForms queue, поэтому переходы состояния выполняются последовательно.

## Состояния HFSM

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

Инварианты:

- dispatch возможен только из `Dispatching`;
- destructive-команда проходит через `AwaitingConfirmation`;
- `Esc`, потеря фокуса NX и остановка движка возвращают `Idle`;
- ошибка записи запроса освобождает keyboard capture;
- результат переводит HFSM в `Idle` или `Root` sticky-режима;
- смена модуля считается завершённой только после нового подтверждённого контекста.

## DFA последовательностей

При загрузке runtime schema 5 компилируется trie/DFA. Последовательность содержит:

```text
<внутренний префикс модуля> + <2–5 токенов пользовательского пути>
```

Пример:

```text
Пользователь: CapsLock → E → E → B
DFA:          M → E → E → B
Команда:      Modeling → Edit → Edge → Blend
```

Компилятор отклоняет:

- пустые пути;
- дубликаты после нормализации;
- команду, которая одновременно является терминалом и префиксом;
- конфликтующий alias;
- недостижимый терминал.

Одинаковый пользовательский путь допустим в разных модулях, потому что внутренний префикс различается.

## Связь с полной картой

Полная карта содержит 1169 намерений. `scripts/compile-full-command-map.mjs` заранее резервирует prefix-free пути внутри каждого модуля, а runtime `MnemonicPathGenerator` повторно нормализует профиль и защищает legacy-команды/aliases.

Восемь клавиш legacy-grid относятся только к быстрым primary aliases. DFA поддерживает многоуровневое дерево и не ограничивает модуль восемью командами.

## Декларативная policy

Файл:

```text
config/nx2512-state-machines.json
```

Policy задаёт:

- таймауты состояний;
- допустимые модули;
- interaction state;
- Work/Display Part;
- минимальный confidence;
- selection count и selected types;
- подтверждение;
- `on_unavailable`.

Пример актуального многоуровневого ключа:

```json
{
  "commands": {
    "MEEB": {
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
    }
  }
}
```

Policy может использовать `switch_module`, но локальный модуль не меняется заранее. HFSM ждёт новый `context.module_id` и revision.

## Контекст NX

Bridge публикует `NxContextSnapshot` protocol schema 3:

```text
schema_version
revision
status
application_id
module_id
module_label
selection_count
selection_state
selected_types
work_part_available
display_part_available
modal_dialog_active
active_command_id
context_confidence
updated_utc
last_request_id
last_result
last_message
```

`revision` изменяется при семантическом изменении контекста. Простое обновление heartbeat не должно создавать ложную ревизию.

## Проверки перед выполнением

Клиент проверяет свежесть контекста и policy. Bridge повторно проверяет:

- protocol schema;
- request expiry;
- `expected_context_revision`;
- `expected_selection_count`;
- `expected_application_id`;
- active module;
- modal dialog;
- destructive confirmation;
- наличие и чувствительность NX `BUTTON ID`.

`requires_selection` описывает ожидаемый workflow, но не всегда требует preselection. Жёсткий минимум задаётся policy или конкретной опасной операцией.

## Selection filters

```text
action = set_selection_filter
selection_filter = none | all | reset | edge | face | body |
                   component | curve | datum | feature | operation
```

Bridge применяет global `NXOpen.Select.FilterMember` и не вызывает `UG_SEL_*` как обычную кнопку меню.

## Надёжная очередь

```text
pending
   ↓ atomic claim
processing
   ↓ execute/reject
completed | failed
```

После выполнения создаётся result JSON. Если NX завершился во время `processing`, запрос переводится в `interrupted_unknown` и не повторяется автоматически. Повторный `request_id` не исполняется.

## Протокол

Общий source-файл:

```text
NXKeys.Protocol/NxProtocol.cs
```

Текущая версия IPC: `schema_version = 3`.

## Проверки CI

CI выполняет:

- компиляцию DFA/HFSM-инвариантов;
- deterministic replay;
- randomized transitions;
- запрет обхода confirmation;
- typed selection guards;
- `switch_module` fallback;
- expiry и at-most-once queue semantics;
- snake_case round-trip protocol schema 3;
- проверку 1169 intent paths и конфликтов префиксов;
- сборку HotkeyStudio, Control Center и CommandBridge contract.

Contract stubs подтверждают форму используемого API, но не заменяют интеграционный тест внутри лицензированной NX.
