# NXKeys v8 — канонический runtime-контракт

Этот документ фиксирует **текущее исполняемое поведение ветки `main`**. Если старый аудит, generated-отчёт или инструкция противоречит этому документу, приоритет имеют код, валидаторы и этот контракт.

## Текущие версии

| Контракт | Текущая версия | Источник истины |
|---|---:|---|
| profile schema | **8** | `NX2512_HotkeyStudio/Models/ConfigRuntimeV5.cs` |
| минимальная читаемая profile schema | **3** | `ConfigRuntimeV5.cs` |
| IPC schema | **4** | `NXKeys.Protocol/NxProtocol.cs` |
| sequence policy | **8** | `scripts/sequence-policy.mjs` |
| основной runtime profile | `config/nx2512-v8-profile.json` | `install-nxkeys.ps1`, `Program.ResolveConfigPath` |
| fallback без JSON | hardcoded v8 profile | `Config.Load` |

`config/full-command-map/`, K1–K5 и generated K3–K5 profile остаются полезным каталогом, инструментом анализа, трассировки и генерации. Они **не являются профилем, который installer выбирает по умолчанию**.

## Как выбирается профиль

`install-nxkeys.ps1` без `-ConfigPath` использует:

```text
config/nx2512-v8-profile.json
```

HotkeyStudio при запуске без `--config` ищет в первую очередь `nx2512-v8-profile.json`, затем compatibility-файл `nx2512-pro-hybrid.json`. Если профиль не найден, `Config.Load` создаёт hardcoded v8 configuration и валидирует её.

Для воспроизводимой production-установки рекомендуется всегда устанавливать и запускать managed v8 profile, а fallback считать аварийной защитой от отсутствующего JSON.

## Модель ввода

NXKeys автоматически определяет активное приложение NX и добавляет внутренний module prefix. **Пользователь префикс модуля не вводит.**

Общая форма:

```text
CapsLock → путь внутри активного контекста
```

Путь может быть однотокенным или многотокенным. Нельзя применять старое правило «все команды имеют 2–5 токенов» к v8: частые Sketch-команды являются однотокенными.

### CapsLock

Физическое нажатие CapsLock защёлкивается до `key-up`: keyboard autorepeat не должен превращать одно удержание в несколько виртуальных нажатий Leader.

### Modeling: `M = Manage`

В Modeling `M` используется как смысловой корень **Manage**. Пример:

```text
CapsLock → M → L → S    Layer Settings
```

Внутренняя DFA-последовательность содержит ещё скрытый префикс Modeling; пользователь его не вводит.

`workspace_key` не должен автоматически становиться командой корневого уровня. Пока отдельное workspace-state не представлено в runtime, workspace-local key исключается из корневой DFA, чтобы не создавать terminal/prefix collision с ветвями вроде `M → …`.

### Sketch v8

В активном Sketch частые операции вызываются одной клавишей после Leader:

```text
CapsLock → L            Line
CapsLock → R            Rectangle
CapsLock → C            Circle
CapsLock → A            Arc
CapsLock → T            Trim
CapsLock → E            Extend
```

Семейства, которым нужен дополнительный смысловой уровень:

```text
CapsLock → D → Q        Rapid Dimension
CapsLock → K → C        Coincident
CapsLock → K → H        Horizontal
CapsLock → K → V        Vertical
CapsLock → K → T        Tangent
CapsLock → C → V → …    Construction variants
CapsLock → J → …        Projection / derived geometry
CapsLock → U → …        Sketch utilities
```

Полная таблица: [SKETCH_INTENT_LANGUAGE.md](SKETCH_INTENT_LANGUAGE.md).

## Selection Intent: клавиши `0…4`

Внутри NX process Command Bridge устанавливает low-level handler для быстрого изменения способа выбора геометрии.

| Клавиша | Режим |
|---:|---|
| `0` | Reset — обычный выбор NX, снять intent toggles |
| `1` | Single — одиночный объект |
| `2` | Connected / Chain — связанная цепочка |
| `3` | Tangent — касательно связанная геометрия |
| `4` | Inferred Path / Region Boundary — путь/граница области |

Цифра перехватывается **только**, когда NX находится на переднем плане и есть активный native collector либо уже выбран seed-объект. Ctrl/Alt/Win, текстовые поля и обычный цифровой ввод не должны перехватываться.

Для `3` используются native tangent toggles NX (`UI_CURVE_FINDER_TANGENT`, `UI_FACE_FINDER_TANGENT`) и, при наличии seed, NXOpen rule expansion. Для `2`/`4` используются native chain/path/boundary controls и, где возможно, `ScRuleFactory` через compatibility reflection.

Подробности и сценарии: [SELECTION_INTENT.md](SELECTION_INTENT.md).

## Sheet Metal

Каноническое приложение для переключения — `UG_APP_SBSM`. Реальные feature command IDs NX 2512 используют префикс `UG_SBSM_*`.

Runtime сохраняет compatibility с историческими/синтетическими именами:

```text
UG_APP_SHEETMETAL → UG_APP_SBSM
UG_SHEET_METAL_*  → UG_SBSM_*
```

Это нормализация совместимости, а не рекомендация добавлять старые IDs в новые профили.

## `secondary_aliases`

`paths.secondary_aliases` в schema v8 — реальные runtime routing aliases. Они десериализуются и разворачиваются в операции до построения Leader sequences.

Пример формы:

```json
{
  "paths": {
    "leader": ["W", "L", "S"],
    "secondary_aliases": ["M->L->S"]
  }
}
```

Alias обязан сохранять prefix-free инварианты своего runtime-модуля.

## IPC schema 4 и аутентификация

File IPC остаётся локальным транспортом, но request schema 4 содержит authenticated envelope:

- `session_id`;
- `client_instance_id`;
- `nonce`;
- `sequence_number`;
- `profile_digest`;
- `payload_hmac`.

Managed launcher создаёт session secret и передаёт его только дочерним доверенным процессам. Bridge проверяет HMAC, session, source process, anti-replay state, profile permission и актуальный NX context перед dispatch.

Подробности: [api.md](api.md) и [SAFETY_MODEL.md](SAFETY_MODEL.md).

## Что проверяет CI

Без установленного Siemens NX CI подтверждает:

- schema/profile invariants;
- v8 alias expansion;
- отсутствие root collision у workspace keys;
- hardcoded/no-profile fallback;
- protocol/state-machine tests;
- Sheet Metal canonicalization/security permissions;
- Command Bridge contract build против NXOpen stubs;
- generated documentation invariants;
- сборку desktop-компонентов.

CI **не может** доказать чувствительность конкретной NX кнопки, лицензионную доступность или фактический результат интерактивной команды в NX 2512.

## Что обязательно проверить в живой NX

После обновления полностью закройте NX, чтобы выгрузить `NX2512_CommandBridge.dll`, затем запустите NX через managed launcher и проверьте:

1. один CapsLock не порождает несколько Leader событий;
2. Modeling `M → L → S` открывает Layer Settings;
3. Sketch `L`, `K → C`, `D → Q` вызывают ожидаемые команды;
4. Sheet Metal показывает и запускает реальные `UG_SBSM_*` команды;
5. `0…4` меняют Selection Intent в активном collector;
6. wrong-context, stale, unsigned и неподписанные profile permissions requests отклоняются;
7. интерактивные команды корректно открывают NX dialogs/collectors.

Последний пункт остаётся runtime-проверкой: `DialogTester.InvokeMenuButtonAction(...)` может иметь особенности для интерактивных команд, которые нельзя подтвердить contract build-ом.

## Приоритет документации

1. исполняемый код и tests;
2. этот runtime-контракт и остальные canonical docs;
3. generated reports того же commit;
4. historical audits и старые планы.

Старые упоминания schema 6, IPC schema 3 или sequence policy v7 в historical snapshots не переписываются: они описывают прошлое состояние и не являются текущей инструкцией.
