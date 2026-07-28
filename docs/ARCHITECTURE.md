# Архитектура NXKeys

## Цель

Главная конфигурация NXKeys предоставляет доступ ко всем **885 командам-намерениям K3–K5** через контекстный мнемонический язык, сохраняя 12 прямых системных shortcuts и безопасный Command Bridge.

## Слои данных

```text
Полный каталог 1169 K1–K5
        ↓ фильтр K3–K5
885 намерений главного scope
        ↓ resolver + каталог установленной NX
existing / resolved / ambiguous / unresolved
        ↓
main generated profile
        ↓ runtime migration schema 5
DFA / HFSM / HUD / Command Bridge
```

Bootstrap `config/nx2512-pro-hybrid.json` задаёт проверенную архитектурную основу. Компилятор переносит её safety-параметры в главный generated profile, добавляет K3–K5 и удаляет команды, не связанные с выбранным scope.

## Поток исполнения

```text
Keyboard hook
  → UI event queue
  → active NX context
  → AdaptiveModuleResolver
  → prefix-free SequenceAutomaton
  → LeaderStateMachine
  → ContextGuardEvaluator
  → atomic IPC request
  → NX2512_CommandBridge
  → NX BUTTON ID / selection filter
  → result
```

## Компоненты

| Компонент | Ответственность |
|---|---|
| `config/full-command-map/` | все 1169 исходных намерений и частоты K1–K5 |
| `compile-main-command-map.mjs` | выбор K3–K5, разрешение IDs, построение путей и отчёта |
| `nx2512-pro-main.generated.json` | главный профиль 885 намерений конкретной установки |
| HotkeyStudio | загрузка профиля, HUD, Leader runtime, CLI и deployment |
| AdaptiveModuleResolver | выбор одного из 14 модулей по контексту NX |
| StateMachines | DFA, HFSM, guards, подтверждение и timeouts |
| Protocol | IPC schema 3 |
| CommandBridge | контекст, очередь, global selection filters и UI command invocation |
| Control Center | обзор scope, статусов и доступности |
| Catalog Studio | экспорт реальных UI IDs и API crosswalk |

## Модули и пути

Пользователь вводит 2–5 токенов после `CapsLock`. Внутренний module prefix добавляется движком. Пути уникальны и prefix-free внутри активного модуля.

Количество строк в generated profile может превышать 885 из-за дублирования глобальных команд по модулям. Архитектурная метрика покрытия — 885 уникальных `catalog_refs`, а не число сериализованных строк.

## Разрешение IDs

Resolver объединяет:

1. точные IDs bootstrap;
2. `06_ui_commands_buttons.csv`;
3. runtime probe;
4. названия и synonyms;
5. module-aware scoring.

Только `existing` и надёжные `resolved` становятся исполняемыми. `ambiguous` и `unresolved` сохраняются в дереве для поиска и отчётности, но отключены.

## Надёжность IPC

```text
pending → processing → completed | failed
```

Request атомарно захватывается Bridge. После возможного прерывания он получает `interrupted_unknown` и не повторяется автоматически. Это защищает от повторного выполнения destructive-команд.

## Deployment

```text
compile main K3–K5
→ validate
→ build
→ staging
→ SHA-256
→ backup
→ atomic commit
→ package-manifest.json
→ health-check
→ rollback on failure
```

Launcher задаёт отдельный `UGII_CUSTOM_DIRECTORY_FILE` и не подменяет глобальные `PATH` или `UGII_USER_DIR`.

## Инварианты CI

- 1169 исходных намерений и точное распределение K1–K5;
- главный scope — ровно 885 K3–K5;
- отсутствие K1–K2 в main;
- 14 модулей и 12 direct shortcuts;
- prefix-free paths и aliases;
- enabled command всегда имеет ID;
- runtime schema 5 и protocol schema 3;
- DFA/HFSM и destructive confirmation;
- сборка HotkeyStudio, Control Center и Bridge contract.
