# Аудит актуальности документации NXKeys

Дата сверки: **10 августа 2026 года**.  
Область: ветка `main`, current user/developer/operations docs, component READMEs, ADR, generated references и historical evidence.

## Итог

Документация переведена на фактический current runtime:

```text
profile schema 8
minimum readable profile 3
IPC schema 4
sequence policy v8
default profile config/nx2512-v8-profile.json
```

Главная архитектурная коррекция: K1–K5 / generated K3–K5 pipeline больше не описывается как default runtime installer path. Он сохранён как legacy/catalog analysis и compatibility layer.

Добавлены два новых current documents:

- [RUNTIME_V8.md](RUNTIME_V8.md) — единый runtime contract;
- [SELECTION_INTENT.md](SELECTION_INTENT.md) — `0…4` collector selection semantics.

## Почему понадобилась полная переработка

До этой сверки одновременно существовали противоречащие друг другу утверждения:

- schema 6 против runtime `CurrentSchemaVersion = 8`;
- IPC schema 3 против protocol schema 4;
- policy v7 против source policy v8;
- generated K3–K5 как «main runtime» против default installer `nx2512-v8-profile.json`;
- Sketch `C→L` / `CGL` против current one-token `L`;
- отсутствие документации Selection Intent `0…4`;
- старые Sheet Metal `UG_SHEET_METAL_*` против current canonical `UG_SBSM_*`;
- security text «file IPC не аутентифицирован» против уже реализованного schema-4 HMAC/session layer;
- ADR-0001 всё ещё называл generated K3–K5 текущим installed runtime;
- большая v8.3 Left-Hand target-spec была оформлена так, будто вся целевая эргономика уже является исполняемым контрактом.

## Источники истины

| Область | Source |
|---|---|
| profile schema/range | `NX2512_HotkeyStudio/Models/ConfigRuntimeV5.cs` |
| v8 operation shape | `NX2512_HotkeyStudio/Models/V8Models.cs` |
| alias/workspace expansion | `V8SecondaryAliasExpander.cs` |
| default profile | `config/nx2512-v8-profile.json` |
| installer profile resolution | `install-nxkeys.ps1` |
| CLI profile resolution | `NX2512_HotkeyStudio/Program.cs` |
| sequence policy | `scripts/sequence-policy.mjs` |
| IPC schema | `NXKeys.Protocol/NxProtocol.cs` |
| security permissions | `NXKeys.Protocol/NxBridgeSecurity.cs` |
| Selection Intent | `NX2512_CommandBridge/SelectionIntentHotkeys.cs` |
| live command catalog | target `06_ui_commands_buttons.csv` |

## Current user documentation

| Документ | Роль | Статус после аудита |
|---|---|---|
| `README.md` | главный вход | v8 current |
| `docs/README.md` | оглавление | v8 current |
| `docs/RUNTIME_V8.md` | canonical runtime | добавлен |
| `docs/MNEMONIC_COMMAND_LANGUAGE.md` | реализованный mnemonic contract | переписан под current runtime |
| `docs/CHEATSHEET.md` | ежедневная работа | полностью перестроен под v8 |
| `docs/SKETCH_INTENT_LANGUAGE.md` | Sketch | current paths + диагностика |
| `docs/SELECTION_INTENT.md` | selection collectors | добавлен |
| `docs/INSTALLATION.md` | install/update | default v8 profile |
| `docs/CLI.md` | CLI | v8 resolution/fallback |
| `docs/TROUBLESHOOTING.md` | диагностика | v8/security/Selection Intent |

## Current engineering documentation

| Документ | Статус |
|---|---|
| `docs/ARCHITECTURE.md` | v8 + authenticated bridge |
| `docs/CONFIGURATION.md` | schema 8 operation contract |
| `docs/api.md` | schema 4 authoritative |
| `docs/STATE_MACHINE_ARCHITECTURE.md` | one-token paths + signed request lifecycle |
| `docs/SAFETY_MODEL.md` | HMAC/session/permission current model |
| `docs/OPERATIONS.md` | installed `nx2512-v8-profile.json` |
| `DEVELOPMENT.md` | v8 tests/build/current profile |
| `CONTRIBUTING.md` | v8 change matrix/invariants |
| `SECURITY.md` | current security reporting/boundaries |
| `NX2512_HotkeyStudio/README.md` | schema 8 / fallback / aliases |
| `NX2512_CommandBridge/README.md` | schema 4 + Selection Intent |
| `NX2512_ControlCenter/README.md` | current v8 metrics vs legacy coverage |
| `NX2512_Catalog_Studio/README.md` | v8 ID-verification workflow vs legacy compiler |
| `roles/README.md` | role change → v8 adapter/context revalidation |

## ADR status

### ADR-0001

`docs/adr/0001-profile-layers.md` теперь имеет статус **Superseded**. Историческое решение K3–K5/885 сохранено, но больше не считается current installed-runtime contract.

### ADR-0002

At-most-once file queue остаётся **Accepted**. ADR дополнен schema-4 authentication: queue transport не является authority, admission требует session/HMAC/source-process/anti-replay/profile permission.

### ADR-0003

Добавлен и принят `docs/adr/0003-v8-runtime-profile-and-legacy-catalog-separation.md`: default runtime — `nx2512-v8-profile.json`, K1–K5/K3–K5 — catalog/compatibility layer.

## Reclassified documentation

### `docs/NX_PRO_HYBRID_SOURCE_SPEC.md`

Переклассифицирован как **legacy/catalog profile specification**. Он сохраняет K1–K5/K3–K5 contracts, но явно говорит, что current runtime source — v8 profile.

### Mnemonic v8.3 Left-Hand target specification

Старая большая target-design specification не удалена. Она сохранена как historical/reference artifact:

```text
docs/historical/MNEMONIC_COMMAND_LANGUAGE_v8.3_LEFT_HAND_TARGET_SPEC.md
```

Current `docs/MNEMONIC_COMMAND_LANGUAGE.md` теперь описывает только реализованное поведение: hidden module prefix, one-token Sketch, `K→…`, `D→…`, `M→L→S`, `secondary_aliases`, workspace-root rule, `S→…` filters и Selection Intent `0…4`.

Это устраняет ключевую проблему: future ergonomic design больше не выглядит как runtime-verified feature.

### Generated

`docs/generated/*`, command-sequence audit, generated profile и command tree считаются актуальными только после воспроизводимой регенерации тем же commit.

### Historical

Датированные audit snapshots, build reports, старый Left-Hand target design и планы могут содержать старые schema/policy/paths. Их старые значения не исправляются задним числом.

## Исправленные пользовательские сценарии

### Sketch

Current:

```text
CapsLock → L
CapsLock → K → C
CapsLock → D → Q
CapsLock → C → V → …
```

Старые `CapsLock→C→L`, `CapsLock→C→G→L`, `CGL` не используются в current README/cheatsheet.

### Modeling Manage

```text
CapsLock → M → L → S
```

Документация объясняет hidden Modeling prefix и запрет root projection workspace-only key.

### Selection Intent

```text
0 reset
1 single
2 connected/chain
3 tangent
4 inferred path/region boundary
```

Отдельно объяснено отличие от Leader type filters `S→…`.

### Sheet Metal

Current canonical namespace:

```text
UG_APP_SBSM
UG_SBSM_*
```

Legacy names помечены только как compatibility mapping.

## Security corrections

Current docs теперь согласованы с IPC schema 4:

- managed launch session;
- HMAC-SHA-256;
- `client_instance_id`;
- nonce/sequence anti-replay;
- profile digest/permission;
- source process validation;
- selection fingerprint/context checks.

Устаревшее утверждение «отдельной cryptographic authentication нет» удалено из current API/safety docs.

## Catalog Studio correction

Старый пример предлагал `install-nxkeys.ps1 -CompileOnly` как способ «собрать main profile». Для default schema-8 v8 profile это неверно: `-CompileOnly` только разрешает/проверяет выбранный profile и завершается до build/install.

Current Catalog Studio guide теперь разделяет:

- v8 workflow — проверка `adapter.value` против target `06_ui_commands_buttons.csv`;
- legacy K3–K5 generation — явный вызов `scripts/compile-main-command-map.mjs`.

## Documentation validator

`scripts/validate-documentation.mjs` обновлён так, чтобы:

- извлекать `CurrentSchemaVersion` из C# source;
- извлекать IPC `SchemaVersion` из `NxProtocol.cs`;
- извлекать `SEQUENCE_POLICY_VERSION` из JS source;
- проверять current docs против полученных версий независимо от небольших различий Markdown formatting;
- проверять default `nx2512-v8-profile.json` в installer и CLI;
- проверять `CapsLock→L`, `K→C`, `M→L→S`;
- проверять Selection Intent doc/Bridge responsibility;
- проверять current Catalog Studio v8 workflow и ADR-0003;
- запрещать старые Sketch line examples в current README/cheatsheet;
- продолжать сверять generated sequence audit с source policy.

Это снижает вероятность повторной рассинхронизации после следующего schema/policy bump.

## Что остаётся live-NX only

Репозиторий/CI не может окончательно доказать:

1. sensitivity всех `BUTTON ID`;
2. наличие commands при конкретной лицензии/role;
3. фактическое поведение `0…4` во всех collectors;
4. module mapping корпоративной конфигурации;
5. interactive dialog/collector semantics;
6. destructive side effects.

Особенно отмечено, что `DialogTester.InvokeMenuButtonAction(...)` для interactive commands требует live-NX interpretation: return value нельзя считать абсолютным доказательством фактического UI effect.

## Правило дальнейшей поддержки

Любое изменение:

- schema;
- sequence policy;
- current profile path;
- user mnemonic path;
- Sketch grammar;
- Selection Intent;
- protocol/security;
- Sheet Metal canonicalization;
- installer/deployment;

должно обновлять соответствующий current doc и machine-checkable documentation invariant в том же change set.

## Проверки документационного change

Минимум:

```powershell
node .\scripts\validate-documentation.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-main-command-map.mjs
node .\scripts\audit-command-sequences.mjs

dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
dotnet run --project .\NX2512_HotkeyStudio.Tests\NX2512_HotkeyStudio.Tests.csproj -c Release
```

Для Bridge изменений дополнительно выполняется NXOpen contract build, а фактический UI behavior проверяется на target NX 2512.
