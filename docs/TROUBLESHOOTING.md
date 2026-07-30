# Диагностика NXKeys

## Сначала соберите факты

Рабочая директория — корень репозитория для source checks либо managed root для installed checks.

### Source profile

```powershell
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs
```

### Installed package

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
$studio = "$root\NX2512_HotkeyStudio.exe"
$config = "$root\nx2512-pro-hybrid.json"

& $studio validate --config $config
& $studio health --config $config
& $studio bridge-status --config $config
```

Сохраните точный текст ошибки, commit/profile hash, NX build и время инцидента.

## Быстрая классификация

| Симптом | Сначала проверить |
|---|---|
| profile не загружается | schema, JSON, path, runtime error text |
| мало команд | установлен bootstrap вместо generated main |
| команда disabled | resolution report |
| неверный mnemonic path | source policy/generator против stale generated profile |
| Bridge OFFLINE | NX process, launcher, custom directory, DLL |
| очередь растёт | Bridge claim и context freshness |
| command failed | request/result/context и actual NX command availability |
| installation health failed | package manifest, hashes, backup |
| новая DLL не работает | NX не был полностью перезапущен |

## Profile schema error

Current profile schema — 6; runtime принимает 3–6.

В `Program.cs` некоторые error strings всё ещё могут говорить `schema v4`. Это устаревший текст сообщения, а не фактический current contract. Проверьте `schema_version`, `Config.CurrentSchemaVersion` и полный validation error.

JSON должен быть UTF-8. Runtime reader допускает comments/trailing commas, но Node validators и installer expectations могут быть строже для generated files; храните канонические profiles как обычный JSON без comments.

## Установился bootstrap вместо main profile

Признаки:

- отсутствует `full_command_catalog`;
- нет `selected_intents: 885`;
- мало команд;
- installed file совпадает с source bootstrap.

Исправление:

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Не копируйте source `config/nx2512-pro-hybrid.json` вручную в managed root.

## Generated profile или audit устарел

Признаки:

- source `scripts/sequence-policy.mjs` сообщает v7;
- generated metadata/audit сообщает v6;
- отсутствуют `SA`/`SN` support rows;
- support counts не соответствуют текущей policy.

Пересоздайте:

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -CompileOnly

node .\scripts\audit-command-sequences.mjs
```

После generation expected source-policy metadata: v7. Не исправляйте version/counts вручную.

## Main profile содержит K1/K2

Validator должен отклонить reference outside K3–K5. Используйте `compile-main-command-map.mjs` или installer без неподдерживаемого custom scope.

Стандартный installer принимает только K3/K4/K5 и 885 selected intents.

## Команда есть, но disabled

Откройте:

```text
docs/generated/main-profile-resolution.md
```

- `unresolved` — ID не найден;
- `ambiguous` — несколько candidates;
- `resolved`/`existing` — row может быть enabled при валидном ID и других условиях.

Обновите Catalog Studio export и повторите compilation. Не включайте ambiguous row вручную.

## Количество rows больше 885

Это нормально при global duplication. Проверяйте:

- `selected_intents = 885`;
- unique selected `catalog_refs`;
- resolution coverage;
- executable rows.

## Mnemonic path неожиданно изменился

Проверьте порядок источников:

1. explicit `command.path` в source profile;
2. known definition по exact ID;
3. generated candidate;
4. collision fallback.

Также проверьте:

- frequency target;
- новые support paths;
- prefix conflict;
- aliases;
- `path_locked/path_source` metadata;
- stale generated profile.

Текущая модель содержит `path_locked/path_source`, но отдельный fully implemented user override workflow не подтверждён.

## `SA` или `SN` отсутствует

Source policy v7 включает Select All (`SA`) и Deselect All (`SN`) во все enabled modules. Если runtime profile их не содержит, пересоберите generated profile текущим compiler и запустите validators.

Фактическое selection behavior дополнительно проверьте в target NX.

## NX не видит NXKeys

Проверьте:

- запуск через managed launcher;
- `UGII_CUSTOM_DIRECTORY_FILE`;
- generated `custom_dirs.dat`;
- Bridge DLL в `custom\application`;
- MenuScript files/version;
- `package-manifest.json`;
- health hashes;
- отсутствие параллельной старой установки.

Не копируйте Bridge в `custom\startup`.

## Bridge OFFLINE или STALE

OFFLINE при закрытом NX — нормально.

При открытом NX:

```powershell
& $studio bridge-status --config $config
& $studio health --config $config
```

Проверьте:

```text
%LOCALAPPDATA%\NXKeys\bridge\status.json
%LOCALAPPDATA%\NXKeys\bridge\context.json
%LOCALAPPDATA%\NXKeys\logs
```

Если DLL обновлялась, закройте все процессы NX и запустите через launcher заново.

## `pending` накапливается

- Bridge не загружен;
- context stale/offline;
- NX запущен не через managed custom directory;
- filesystem/antivirus блокирует queue;
- Bridge process завис.

Не перемещайте requests вручную в `completed`. Сохраните копию queue и восстановите Bridge.

## Request остался в `processing`

Результат потенциально неизвестен. Recovery должен переместить его в failed как `interrupted_unknown`.

Перед повтором:

1. проверьте фактическое состояние детали;
2. сопоставьте request ID и last result;
3. изучите logs;
4. повторите действие только вручную.

## Command попала в `failed`

Типовые причины:

- invalid schema;
- expired request;
- stale context revision;
- changed selection;
- wrong application/module;
- modal dialog;
- command unavailable/insensitive;
- destructive request без confirmation;
- malformed JSON.

Сначала проверьте command вручную в NX и exact ID в target catalog.

## Selection filter не применяется

Request должен иметь:

```json
{
  "action": "set_selection_filter",
  "selection_filter": "edge"
}
```

Допустимые values:

```text
none, all, reset, edge, face, body, component,
curve, datum, feature, operation
```

Проверьте fresh Bridge, modal state и target NX behavior.

## Module switch не завершается

Switch считается successful только после fresh context с target application/module.

Проверьте:

- `target_application_id`;
- target command ID;
- лицензию приложения;
- context revision;
- timeout;
- actual module mapping;
- наличие `G*` row в текущем module.

Sketch не содержит обычных module-switch rows по design.

## Confirmation не появляется или обходится

Проверьте command fields:

```text
destructive
confirm_before_execute
```

и declarative policy. Запустите state-machine tests. Не исправляйте проблему отключением confirmation.

## Ошибка сборки Command Bridge

### `NXOpenDir is not defined`

Используйте component build script:

```powershell
.\NX2512_CommandBridge\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512"
```

### Версия path не подтверждена

Передайте точный NXOpen DLL нужной установки. `-AllowVersionMismatch` — только для ручного compatibility experiment.

### Contract build проходит, production build нет

Contract stubs проверяют только форму используемого API. Сравните real assemblies, target framework, x64 и signing requirements.

## Installer сообщает, что NX запущен

Закройте NX. `-AllowRunningNX` не подходит для обновления Bridge DLL.

## Installation rollback или health failure

1. сохраните installer output;
2. найдите последний backup manifest;
3. выполните `health`;
4. восстановите backup;
5. повторите health;
6. устраните первичную ошибку до новой установки.

```powershell
& $studio backups --config $config
& $studio restore --config $config
```

Не изменяйте managed root вручную до сбора evidence.

## Restore отклонён

Проверьте manifest path, managed files, NX processes и conflicts. `--force` используйте только после ручной оценки и сохранения текущего package.

## Control Center показывает неверные metrics

Различайте:

- 885 unique selected intents;
- serialized module rows;
- enabled rows с IDs;
- runtime-verified commands.

Проверьте, что Control Center открыт с generated main profile, а не source bootstrap.

## Что приложить к issue

- commit SHA;
- profile schema и hash;
- NX build/localization;
- точная command/sequence;
- обезличенный request/result/context;
- relevant log fragment;
- resolution status/ID;
- выполненные validators;
- указание, воспроизводится ли на тестовой детали.

Не прикладывайте proprietary DLL, license files, production parts и секреты.
