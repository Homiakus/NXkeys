# Внесение изменений в NXKeys v8

## Основные правила

- Исполняемый код, tests и validators являются источником истины.
- Current contracts: profile schema **8**, IPC schema **4**, sequence policy **v8**.
- Default runtime profile: `config/nx2512-v8-profile.json`.
- Не добавляйте выдуманные `BUTTON ID`, application IDs или NXOpen contracts.
- Не ослабляйте authenticated IPC/context/confirmation guards ради удобства.
- Не редактируйте generated artifacts вручную.
- Historical audits не переписываются под current state; они должны быть явно отделены от current instructions.
- Изменение пользовательского поведения требует документации и regression check в том же change set.

Перед началом прочитайте [README.md](README.md), [docs/RUNTIME_V8.md](docs/RUNTIME_V8.md), [DEVELOPMENT.md](DEVELOPMENT.md) и [docs/CHEATSHEET.md](docs/CHEATSHEET.md).

## Матрица изменений

| Изменяется | Проверить | Обновить |
|---|---|---|
| `config/nx2512-v8-profile.json` | HotkeyStudio tests, v8 runtime/path/security invariants | runtime/config/mnemonic docs |
| `V8Models.cs` | schema/load tests | `CONFIGURATION.md`, runtime contract |
| `V8SecondaryAliasExpander.cs` | aliases/prefix/workspace regressions | config/runtime docs |
| `LeaderKeyEngine.cs` | hook/latch tests + manual Windows UX | cheatsheet/runtime docs |
| `AdaptiveModuleResolver.cs` | context resolution regressions | architecture/troubleshooting |
| Sketch routing | HotkeyStudio tests + live NX | Sketch doc, cheatsheet |
| `SelectionIntentHotkeys.cs` | Bridge contract + live NX modes 0…4 | `SELECTION_INTENT.md`, cheatsheet |
| `NxProtocol.cs` | protocol/state-machine tests + Bridge build | `api.md`, safety/architecture |
| `NxBridgeSecurity.cs` | security regressions + strict build | `api.md`, safety |
| Bridge invocation | contract build + live NX | Bridge README, troubleshooting |
| installer/deployment | dry-run/install/health/rollback | installation/operations |
| legacy K1–K5 compiler | Node validators/generated diff | generated/catalog docs only |
| documentation | `validate-documentation.mjs` | `DOCUMENTATION_AUDIT.md` |

## V8 paths

### Active module prefix

Пользователь не вводит module prefix. Runtime добавляет его автоматически.

### One-token paths допустимы

Current Sketch использует frequent direct Leader paths:

```text
L
R
C
A
T
```

Не применяйте старое универсальное правило «path минимум 2 токена» к schema v8.

### `secondary_aliases`

Aliases являются исполняемыми routing paths, а не только search metadata. Они должны быть prefix-free после нормализации.

### `workspace_key`

Workspace-only key не должен автоматически появляться в root DFA. Пока нет explicit workspace state, такой key подавляется на root layer.

### Modeling `M = Manage`

Регрессионный path:

```text
M → L → S    Layer Settings
```

Не создавайте terminal root `M`, конфликтующий с Manage subtree.

## Sketch

Current grammar:

```text
L/R/C/A/T...     frequent commands
K → …            constraints
D → …            dimensions
C → V → …        variants
J → …            projection/derived
U → …            utilities
```

Regression examples:

```text
CapsLock → L
CapsLock → K → C
CapsLock → D → Q
```

Старые `CGL`, `C→L`, `C→G→L` не должны возвращаться как current user examples.

При добавлении constraint проверяйте реальный `UG_SKETCH_*_CONSTRAINT` ID в NX2512 catalog.

## Selection Intent

`0…4` реализованы внутри Command Bridge, а не обычными v8 operations:

```text
0 reset
1 single
2 connected/chain
3 tangent
4 inferred path/region boundary
```

Не убирайте:

- NX foreground guard;
- Ctrl/Alt/Win guard;
- injected-event guard;
- text-input guard;
- collector/seed requirement;
- physical key latch.

Любое изменение Selection Intent требует live test в NX collectors. Contract stubs этого не доказывают.

## Sheet Metal

Новые commands должны использовать:

```text
UG_APP_SBSM
UG_SBSM_*
```

Compatibility old→canonical mapping допустим в normalization layer. Runtime и security mapping должны меняться вместе.

## Profile schema

При schema bump обновите:

1. `Config.CurrentSchemaVersion`;
2. supported range/migration;
3. vNext models;
4. tests/CI;
5. installer acceptance;
6. documentation validator;
7. `RUNTIME_V8.md`, `CONFIGURATION.md`, examples.

Не оставляйте CI validator, который требует предыдущий номер schema.

## IPC/security

Protocol schema 4 включает authenticated request envelope. При добавлении поля/изменении signing semantics обновляйте вместе:

- shared model;
- canonical HMAC payload;
- writer;
- Bridge verifier;
- anti-replay state;
- permission digest;
- tests;
- API/safety docs.

Новый action должен быть explicit allowlisted; unknown actions должны оставаться fail-closed.

## Interactive NX commands

Не делайте вывод «command точно не запустилась» только из `InvokeMenuButtonAction=false` без понимания конкретной NX API semantics.

Если меняется invocation policy:

- сохраните availability/sensitivity checks;
- не создавайте blind retry;
- добавьте testable policy;
- проверьте target NX dialog/collector;
- обновите Bridge README и troubleshooting.

## Legacy K1–K5 pipeline

`config/full-command-map/`, K3–K5 compiler и generated profile остаются поддерживаемыми analytical/compatibility artifacts.

Не выдавайте их за default runtime profile, пока `install-nxkeys.ps1` по умолчанию выбирает `nx2512-v8-profile.json`.

Generated files обновляйте только соответствующими scripts.

## Документация

Canonical current docs должны использовать текущие версии и current profile path.

Generated/historical docs могут содержать старые значения, если это явно история.

Обязательная проверка:

```powershell
node .\scripts\validate-documentation.mjs
```

Validator должен получать schema/policy facts из source code, а не закреплять устаревшие числа вручную.

## Минимальные проверки

```powershell
node .\scripts\validate-documentation.mjs
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs

dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
dotnet run --project .\NX2512_HotkeyStudio.Tests\NX2512_HotkeyStudio.Tests.csproj -c Release

dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -c Release -p:Platform=x64 --nologo
dotnet build .\NX2512_ControlCenter\NX2512_ControlCenter.csproj -c Release -p:Platform=x64 --nologo
```

Bridge change → contract build. NXOpen/interactive change → live target-NX test.

## Review checklist

- [ ] current profile path v8;
- [ ] schema 8 / IPC 4 / policy v8 не рассинхронизированы;
- [ ] no invented IDs/API;
- [ ] aliases prefix-free;
- [ ] workspace-only keys не загрязняют root;
- [ ] Sketch routing контекстный;
- [ ] Selection Intent guards сохранены;
- [ ] Sheet Metal canonicalization согласована с security;
- [ ] authenticated IPC не ослаблен;
- [ ] destructive confirmation сохранена;
- [ ] generated diff воспроизводим;
- [ ] docs обновлены;
- [ ] ограничения live NX указаны явно.
