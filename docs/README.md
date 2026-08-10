# Документация NXKeys

Документация ветки `main` описывает **NXKeys v8** для Siemens NX / Designcenter NX 2512.

Текущий контракт:

- profile schema **8**;
- IPC schema **4**;
- sequence policy **v8**;
- основной runtime profile: `config/nx2512-v8-profile.json`;
- fallback без JSON: hardcoded v8 profile;
- активный модуль NX определяется автоматически;
- частые Sketch-команды могут быть однотокенными;
- Selection Intent `0…4` работает внутри активных NX collectors.

Каноническое описание текущего runtime: [RUNTIME_V8.md](RUNTIME_V8.md).

## Начать отсюда

| Задача | Документ |
|---|---|
| понять текущий v8-контракт | [RUNTIME_V8.md](RUNTIME_V8.md) |
| быстро начать работать | [CHEATSHEET.md](CHEATSHEET.md) |
| установить/обновить NXKeys | [INSTALLATION.md](INSTALLATION.md) |
| понять `0…4` Selection Intent | [SELECTION_INTENT.md](SELECTION_INTENT.md) |
| освоить Sketch v8 | [SKETCH_INTENT_LANGUAGE.md](SKETCH_INTENT_LANGUAGE.md) |
| посмотреть архитектуру | [ARCHITECTURE.md](ARCHITECTURE.md) |
| настроить profile schema v8 | [CONFIGURATION.md](CONFIGURATION.md) |
| использовать CLI | [CLI.md](CLI.md) |
| интегрироваться с file IPC | [api.md](api.md) |
| диагностировать систему | [TROUBLESHOOTING.md](TROUBLESHOOTING.md) |
| сопровождать установку | [OPERATIONS.md](OPERATIONS.md) |
| понять security boundaries | [SAFETY_MODEL.md](SAFETY_MODEL.md) |
| разрабатывать локально | [DEVELOPMENT.md](../DEVELOPMENT.md) |
| вносить изменения | [CONTRIBUTING.md](../CONTRIBUTING.md) |
| увидеть последнюю сверку | [DOCUMENTATION_AUDIT.md](DOCUMENTATION_AUDIT.md) |

## Для пользователя NX

Основная последовательность чтения:

1. [CHEATSHEET.md](CHEATSHEET.md) — ежедневные команды и сценарии;
2. [RUNTIME_V8.md](RUNTIME_V8.md) — что именно считается текущим поведением;
3. [SKETCH_INTENT_LANGUAGE.md](SKETCH_INTENT_LANGUAGE.md) — Sketch;
4. [SELECTION_INTENT.md](SELECTION_INTENT.md) — `0…4` для collector selection;
5. [TROUBLESHOOTING.md](TROUBLESHOOTING.md) — если что-то не работает.

Ключевой принцип: пользователь **не вводит префикс активного модуля**. Runtime добавляет его сам.

Примеры в активном Sketch:

```text
CapsLock → L            Line
CapsLock → K → C        Coincident
CapsLock → D → Q        Rapid Dimension
```

Пример Manage в Modeling:

```text
CapsLock → M → L → S    Layer Settings
```

## Для администратора

- [INSTALLATION.md](INSTALLATION.md) — installer, modes, NXOpen и staging;
- [OPERATIONS.md](OPERATIONS.md) — health, queue, backup, restore;
- [CLI.md](CLI.md) — операторские команды;
- [SAFETY_MODEL.md](SAFETY_MODEL.md) — authenticated IPC и safety controls;
- [api.md](api.md) — request/context/result schema 4.

После обновления Bridge необходимо полностью закрыть NX: загруженная `NX2512_CommandBridge.dll` не обновляется в уже работающем процессе.

## Для разработчика

- [DEVELOPMENT.md](../DEVELOPMENT.md);
- [CONTRIBUTING.md](../CONTRIBUTING.md);
- [ARCHITECTURE.md](ARCHITECTURE.md);
- [CONFIGURATION.md](CONFIGURATION.md);
- [STATE_MACHINE_ARCHITECTURE.md](STATE_MACHINE_ARCHITECTURE.md);
- [NX_PRO_HYBRID_SOURCE_SPEC.md](NX_PRO_HYBRID_SOURCE_SPEC.md);
- [ADR](adr/README.md).

Component documentation:

- [HotkeyStudio](../NX2512_HotkeyStudio/README.md);
- [Command Bridge](../NX2512_CommandBridge/README.md);
- [Control Center](../NX2512_ControlCenter/README.md);
- [Catalog Studio](../NX2512_Catalog_Studio/README.md);
- [roles](../roles/README.md).

## Current, generated и historical

### Canonical/current

Описывает текущую ветку `main` и обязано совпадать с кодом. В первую очередь это:

- `RUNTIME_V8.md`;
- `README.md` и корневой `README.md`;
- `CHEATSHEET.md`;
- `INSTALLATION.md`;
- `CONFIGURATION.md`;
- `ARCHITECTURE.md`;
- `api.md`;
- `SAFETY_MODEL.md`;
- `OPERATIONS.md`;
- `TROUBLESHOOTING.md`;
- component README.

### Generated

Не редактируются вручную, когда они создаются генератором:

- `generated/*`;
- `audit/command-sequence-audit.md`;
- `audit/command-sequence-audit.json`;
- `command-tree.html`;
- `../config/nx2512-pro-main.generated.json`.

K1–K5 / K3–K5 pipeline остаётся частью репозитория и полезен для coverage, catalog analysis и исторической трассировки. Он не должен описываться как default runtime installer path, пока `install-nxkeys.ps1` по умолчанию выбирает `nx2512-v8-profile.json`.

### Historical

`docs/audit/00-* … 12-*`, датированные evidence files, старые build reports и `docs/superpowers/plans/*` фиксируют состояние на дату создания. Они могут корректно содержать старые schema/policy и старые пути.

При конфликте приоритет такой:

1. код и автоматические тесты;
2. [RUNTIME_V8.md](RUNTIME_V8.md) и canonical docs;
3. generated artifacts того же commit;
4. historical snapshots.

## Источники истины

| Область | Источник |
|---|---|
| current profile schema/range | `NX2512_HotkeyStudio/Models/ConfigRuntimeV5.cs` |
| v8 operation fields | `NX2512_HotkeyStudio/Models/V8Models.cs` |
| v8 aliases/workspace normalization | `NX2512_HotkeyStudio/Models/V8SecondaryAliasExpander.cs` |
| default runtime profile | `config/nx2512-v8-profile.json` |
| installer profile selection | `install-nxkeys.ps1` |
| desktop/CLI profile resolution | `NX2512_HotkeyStudio/Program.cs` |
| sequence policy | `scripts/sequence-policy.mjs` |
| IPC schema/security fields | `NXKeys.Protocol/NxProtocol.cs` |
| IPC permissions/canonicalization | `NXKeys.Protocol/NxBridgeSecurity.cs` |
| Selection Intent `0…4` | `NX2512_CommandBridge/SelectionIntentHotkeys.cs` |
| state machines | `config/nx2512-state-machines.json`, `NXKeys.StateMachines/` |
| actual NX UI IDs | target export `06_ui_commands_buttons.csv` |

## Правило поддержки

Изменение поведения не считается завершённым, пока в том же change set не обновлены соответствующая документация и machine-checkable invariants.

Минимальная документационная проверка:

```powershell
node .\scripts\validate-documentation.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\audit-command-sequences.mjs
```

Для runtime изменений дополнительно запускаются C# tests/builds из [DEVELOPMENT.md](../DEVELOPMENT.md).
