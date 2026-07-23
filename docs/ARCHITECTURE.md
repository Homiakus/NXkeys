# Архитектура NXKeys

## Цель

NXKeys предоставляет один устойчивый способ вызова профессиональных команд Siemens NX 2512: базовые системные сочетания остаются прямыми, а все остальные операции выбираются из набора активного модуля.

## Поток данных

```text
Siemens NX
  └─ NX2512_CommandBridge
       ├─ context.json
       ├─ pending/
       ├─ processing/
       ├─ completed/
       └─ failed/

context.json
  → AdaptiveModuleResolver
  → активный ModuleConfig
  → восемь LeaderSequenceItem
  → SequenceAutomaton
  → LeaderStateMachine
  → ContextGuardEvaluator
  → Command Bridge
  → NX BUTTON ID
```

## Почему карта не дублируется

`modules[].command_sets[].commands` — единственный источник профессиональных команд. `LeaderKeyConfig.RebuildFromModules()` строит последовательности во время загрузки.

Это предотвращает:

- расхождение модульной карты и Leader;
- разные guards у одной команды;
- забытые изменения в отдельном списке;
- конфликты между прямым ускорителем и модульным вызовом.

## Адаптивное разрешение модуля

`AdaptiveModuleResolver` использует:

1. `module_id` Bridge;
2. `module_label`;
3. известное сопоставление `application_id`;
4. список `nx_application_ids` профиля.

При устаревшем контексте набор не выбирается. При смене приложения открытый HUD перестраивается.

## DFA

Внутренний алфавит состоит из:

- одного префикса модуля;
- одной клавиши позиции.

Пример:

```text
Modeling + X → MX → Edge Blend
Sketch   + X → SX → Trim
Drafting + X → DX → Update Views
```

Префикс вводит движок, а не пользователь. Это сохраняет однозначный DFA и сокращает физический ввод до одной клавиши после Leader.

## HFSM

```text
Idle
  → Root
  → Prefix
  → Search
  → AwaitingConfirmation
  → Dispatching
  → AwaitingResult
  → Idle | Prefix(sticky) | Failed
```

`SwitchingModule` используется только при явном `Tab`/`Shift+Tab`.

Keyboard hook не исполняет бизнес-логику. Он помещает события в очередь WinForms; все переходы выполняются последовательно UI event loop.

## Guards

До dispatch проверяются:

- свежесть Bridge;
- активный модуль;
- interaction state;
- modal dialog;
- work/display part;
- достоверность контекста;
- количество выбранных объектов;
- `types_any` и `types_all`;
- confirmation.

Bridge повторяет критические проверки по ожидаемой ревизии, количеству выбора и приложению.

## Очередь

```text
pending → processing → completed | failed
```

Захват запроса выполняется атомарным перемещением. После прерывания NX запрос получает `interrupted_unknown` и не запускается повторно.

## Deployment

```text
plan
→ staging
→ SHA-256 validation
→ backup
→ atomic commit
→ package manifest
→ post-install verification
→ rollback on failure
```

Пакет не изменяет системные файлы Siemens, не подменяет `PATH` или `UGII_USER_DIR` и подключается через отдельный `UGII_CUSTOM_DIRECTORY_FILE`.

## Границы компонентов

| Компонент | Ответственность |
|---|---|
| HotkeyStudio | профиль, UI, Leader runtime, CLI и deployment |
| AdaptiveModuleResolver | выбор модуля по фактическому контексту |
| StateMachines | чистые переходы DFA/HFSM и guards |
| Protocol | общие snake_case DTO |
| CommandBridge | контекст, очередь и вызов NX UI command |
| ControlCenter | наблюдение, покрытие и диагностика |
| CatalogStudio | построение каталога UI/API |

## Инварианты

- ровно 12 прямых сочетаний;
- профессиональная команда принадлежит модулю;
- каждый модуль содержит восемь уникальных слотов;
- набор определяется контекстом NX;
- команда другого модуля невидима и не исполняется;
- destructive-команда не минует подтверждение;
- неизвестно завершившийся запрос не повторяется;
- deployment изменяет только управляемые файлы.
