# Диагностика NXKeys v8

Current contracts: profile schema **8**, IPC schema **4**, sequence policy **v8**. Default installed profile: `nx2512-v8-profile.json`.

Если симптом противоречит старому audit/generated report, начните с [RUNTIME_V8.md](RUNTIME_V8.md).

## Сначала соберите факты

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
$studio = "$root\NX2512_HotkeyStudio.exe"
$config = "$root\nx2512-v8-profile.json"

& $studio validate --config $config
& $studio health --config $config
```

Если NX запущен через managed launcher:

```powershell
& $studio bridge-status --config $config
```

Сохраните commit SHA, profile digest/schema, NX exact build, role/license context, время и точную sequence.

## Быстрая классификация

| Симптом | Первое действие |
|---|---|
| profile не загружается | проверить schema 3…8, JSON и фактический path |
| загружается не тот profile | проверить `nx2512-v8-profile.json` и `--config` |
| CapsLock срабатывает несколько раз | проверить актуальность HotkeyStudio physical-key latch |
| неверный active module | проверить Bridge context/application mapping |
| Modeling `M` конфликтует | проверить текущие aliases/workspace-root normalization |
| Sketch `K→…` не работает | убедиться, что активен Sketch и Bridge/profile свежие |
| Sheet Metal пустой/не запускается | проверить `UG_APP_SBSM` / `UG_SBSM_*` canonicalization |
| `0…4` не меняют выбор | нужен active collector или seed selection |
| цифры мешают вводу | зафиксировать focused control — это дефект guard-логики |
| Bridge `authentication_required` | NX/HotkeyStudio запущены не через managed launcher |
| очередь растёт | Bridge/session/context/permissions |
| UI открылся, а result failed | проверить interactive invocation semantics/logs |
| DLL не обновилась | полностью закрыть NX и повторить install |

## Profile schema

Current schema: **8**. Runtime читает source schema **3…8**.

Default source:

```text
config\nx2512-v8-profile.json
```

Installed source:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\nx2512-v8-profile.json
```

Если JSON отсутствует, HotkeyStudio может создать hardcoded v8 fallback. Это полезно для resilience/CI, но production diagnosis должен начинаться с проверки установленного versioned profile.

## «Установился bootstrap вместо main»

Эта формулировка относится к старому K3–K5 pipeline и больше не является правильной диагностикой default v8 install.

Текущий вопрос:

```text
Какой именно profile выбрал installer/HotkeyStudio?
```

Проверьте `--config`, installed `nx2512-v8-profile.json` и runtime log.

Generated `nx2512-pro-main.generated.json` остаётся отдельным compatibility/catalog artifact.

## Sequence policy / generated audits

Source policy — **v8**.

Проверка:

```powershell
node .\scripts\audit-command-sequences.mjs
node .\scripts\validate-documentation.mjs
```

Generated audit должен соответствовать текущему `SEQUENCE_POLICY_VERSION`. Historical reports со старыми policy не переписываются.

## CapsLock порождает несколько событий

Current HotkeyStudio защёлкивает физическое нажатие Leader trigger до `key-up`.

Если проблема воспроизводится:

1. проверьте, что запускается новый `NX2512_HotkeyStudio.exe`, а не старая копия;
2. завершите все HotkeyStudio instances;
3. проверьте `%LOCALAPPDATA%\NXKeys\logs\leader-key.log`;
4. запустите через managed launcher;
5. зафиксируйте keyboard driver/layout и наличие сторонних remappers.

## Неверно определяется модуль

Adaptive resolver сначала должен использовать exact NX application mapping.

Особенно проверьте:

```text
Sketch
Sheet Metal
Surface
Simulation
```

Если `Sheet Metal` определяется как Sketch или другой `S*` context, это признак старой сборки resolver/profile mapping.

## Modeling `M = Manage`

Ожидаемый пользовательский path:

```text
CapsLock → M → L → S    Layer Settings
```

Если одиночный `M` перехватывает subtree или возникает prefix conflict:

- проверьте актуальный `V8SecondaryAliasExpander`;
- убедитесь, что workspace-only operation не была превращена в root command;
- запустите HotkeyStudio regression tests.

## Sketch constraints

В активном Sketch:

```text
CapsLock → K → C    Coincident
CapsLock → K → H    Horizontal
CapsLock → K → V    Vertical
CapsLock → K → T    Tangent
```

Если не работает:

1. убедитесь, что Bridge context показывает Sketch;
2. проверьте `UG_SKETCH_*_CONSTRAINT` command availability в вашей NX;
3. полностью перезапустите NX после Bridge update;
4. изучите request/result/log;
5. проверьте, не открылся ли dialog/collector несмотря на `InvokeMenuButtonAction=false`.

Последний случай требует live-NX проверки: return value интерактивной menu command не следует трактовать изолированно.

## Sketch frequent commands

Current user paths:

```text
CapsLock → L    Line
CapsLock → R    Rectangle
CapsLock → T    Trim
CapsLock → D → Q    Rapid Dimension
```

Старые примеры `C→L`, `C→G→L`, `CGL` относятся к предыдущим grammar iterations и не являются current user input.

## Sheet Metal

Current canonical IDs:

```text
UG_APP_SBSM
UG_SBSM_TAB_FEATURE
UG_SBSM_FLANGE_FEATURE
UG_SBSM_CONTOUR_FLANGE_FEATURE
UG_SBSM_BEND_FEATURE
UG_SBSM_UNBEND_FEATURE
UG_SBSM_REBEND_FEATURE
UG_SBSM_FLAT_PATTERN_FEATURE
```

Runtime/security нормализуют legacy `UG_APP_SHEETMETAL` / `UG_SHEET_METAL_*`.

Если команда отклоняется security layer, проверьте одновременно command canonicalization и profile permission digest.

## Selection Intent `0…4` ничего не делает

Ожидается:

```text
0 reset
1 single
2 connected/chain
3 tangent
4 inferred path/region boundary
```

Handler специально не перехватывает цифры, если нет active native collector и нет seed selection.

Проверка:

1. NX должен быть foreground;
2. Ctrl/Alt/Win не зажаты;
3. focus не в text-like control;
4. откройте command с geometry collector;
5. попробуйте выбрать seed, затем нажать `2`, `3` или `4`;
6. после update полностью restart NX.

Подробнее: [SELECTION_INTENT.md](SELECTION_INTENT.md).

## Цифры перехватываются в числовом поле

Это не ожидаемое поведение. Сохраните:

- NX command/dialog;
- focused control class;
- режим `0…4`;
- наличие текущего selection;
- Bridge version/commit.

Не обходите проблему отключением всех guards глобально.

## Bridge OFFLINE

При закрытом NX это нормально.

При открытом NX проверьте:

```text
%LOCALAPPDATA%\NXKeys\bridge\status.json
%LOCALAPPDATA%\NXKeys\bridge\context.json
%LOCALAPPDATA%\NXKeys\logs
```

а также managed custom dirs и `custom\application\NX2512_CommandBridge.dll`.

## `authentication_required`

IPC schema 4 требует shared session capability.

Штатный запуск:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\launch-nx2512-with-nxkeys.cmd
```

Если NX и HotkeyStudio запущены независимо, перезапустите оба через managed launcher. Не пытайтесь вручную писать session/HMAC fields в queue.

## Request rejected: permission/profile digest

Проверьте:

- HotkeyStudio и Bridge используют один installed profile;
- profile не был изменён после launch;
- command/application canonicalization совпадает;
- Sheet Metal старые IDs нормализуются одинаково с обеих сторон;
- v8 availability содержит нужный application.

После изменения profile лучше перезапустить managed session.

## `pending` растёт

Возможные причины:

- Bridge offline;
- authentication session отсутствует;
- queue limit достигнут;
- Bridge не claims files;
- process завис;
- ACL/antivirus/IO error.

Не переносите files вручную между queue directories.

## Request остался в `processing`

После recovery он должен стать `interrupted_unknown`/failed. Это означает неизвестный side effect.

Перед повтором:

1. проверьте деталь в NX;
2. сопоставьте request/result/context;
3. не выполняйте automatic retry;
4. повторяйте только осознанно после проверки state.

## Command failed

Типовые причины:

- schema/expiry;
- HMAC/session failure;
- anti-replay reject;
- profile permission mismatch;
- stale context/revision;
- changed selection fingerprint;
- wrong module/application;
- modal state;
- unavailable/insensitive button;
- destructive without confirmation;
- interactive invocation false-negative.

## UI открылся, но Bridge считает command failed

Это отдельный класс проблемы интерактивных NX menu commands.

Соберите:

- `command_id`;
- return/exception text;
- фактический открывшийся dialog/collector;
- context before/after;
- request/result/log.

Не запускайте command повторно автоматически: UI action уже могла произойти.

## DLL заблокирована при install

Полностью закройте NX:

```powershell
Get-Process ugraf,run_nx,nx -ErrorAction SilentlyContinue
```

После завершения процессов повторите install. `-AllowRunningNX` не обеспечивает hot reload.

## Health failure

```powershell
& $studio health --config $config
& $studio backups --config $config
```

При необходимости:

```powershell
& $studio restore --config $config
```

Не удаляйте managed root до сохранения evidence.

## Source checks

```powershell
node .\scripts\validate-documentation.mjs
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs

dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
dotnet run --project .\NX2512_HotkeyStudio.Tests\NX2512_HotkeyStudio.Tests.csproj -c Release
```

Bridge changes также требуют NXOpen contract build.

## Что приложить к issue

- commit SHA;
- exact NX build/role/license context;
- installed profile path + schema + digest;
- точную пользовательскую sequence;
- active module/application;
- обезличенные request/result/context/status;
- relevant logs;
- указание, был ли NX запущен managed launcher;
- воспроизводится ли на тестовой детали.

Не прикладывайте proprietary NX DLL, license files, production parts, credentials или session secrets.
