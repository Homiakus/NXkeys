# NX 2512 Catalog Studio

Catalog Studio — NXOpen library с WinForms-панелью для инвентаризации **конкретной** установки Siemens NX / Designcenter NX 2512. Export используется для проверки реальных UI command IDs, анализа NXOpen/UFUN API и legacy catalog-resolution pipeline.

Catalog Studio не является runtime keyboard layer, installer или источником permission policy.

## Зачем он нужен current v8 runtime

Current NXKeys использует `config/nx2512-v8-profile.json`. Catalog Studio помогает проверить, что `adapter.value` в v8 operations соответствует фактической целевой NX installation.

Особенно полезно повторно проверить export после:

- NX maintenance update;
- изменения role/localization/license;
- добавления corporate MenuScript extensions;
- переноса на другую workstation;
- появления `unavailable` / `insensitive` command;
- изменения Sheet Metal / Sketch / Drafting command mappings.

Для Sheet Metal current canonical namespace — `UG_APP_SBSM` / `UG_SBSM_*`.

## Возможности

Можно независимо инвентаризировать:

- NX Open managed API;
- UI commands и `BUTTON ID`;
- Open C / UFUN;
- candidate mapping UI command → API.

Доступны выбор output directory, timestamp folders, depth настройки, user/site/group roots, background execution, progress и cancellation.

## Основные output files

```text
04_nxopen_members.csv
05_nxopen_entry_points.csv
06_ui_commands_buttons.csv
07_ufun_functions.csv
08_ui_command_api_candidates.csv
```

Для проверки runtime UI adapters главный источник:

```text
06_ui_commands_buttons.csv
```

`08_ui_command_api_candidates.csv` — только candidates. Similarity/candidate score не доказывает semantic equivalence и не разрешает command автоматически.

## JSON profiles Catalog Studio

Сохраняемые настройки export относятся только к Catalog Studio: output selection, depth, thresholds и т. п. Это **не** NXKeys runtime profile schema 8.

## Safety

Catalog Studio предназначен для чтения metadata и создания новых export files. Он не должен изменять:

- открытую деталь;
- NX role;
- NXKeys mnemonic paths;
- MenuScript installation;
- NX installation.

Перед публикацией export удалите machine/user paths, corporate names и чувствительную информацию. Не добавляйте proprietary NX binaries или license material в repository.

## Требования

- Windows x64;
- .NET 8 SDK;
- Siemens NX / Designcenter NX 2512;
- `NXOpen.dll` target installation;
- PowerShell 5.1 или 7;
- для `-Sign` — доступные `SignDotNet.exe` и `NXSigningResource.res` согласно локальной политике.

## Сборка

```powershell
.\NX2512_Catalog_Studio\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

С точным assembly:

```powershell
.\NX2512_Catalog_Studio\build.ps1 `
  -NxOpenDll "D:\Siemens\NX2512\NXBIN\managed\NXOpen.dll" `
  -Clean
```

Output:

```text
NX2512_Catalog_Studio\dist\NX2512_CatalogStudio.dll
```

`-AllowVersionMismatch` используйте только для осознанного compatibility experiment. Export другой NX version нельзя автоматически считать evidence для 2512.

## Подпись

```powershell
.\NX2512_Catalog_Studio\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Sign `
  -Clean
```

Signing tools/resources не должны попадать в version control, если они proprietary.

## Запуск в NX

Обычно library загружается через NX Open execution UI и выбирается:

```text
NX2512_Catalog_Studio\dist\NX2512_CatalogStudio.dll
```

Точный menu path зависит от localization/role и должен подтверждаться на target workstation.

## Current v8 workflow

1. Соберите Catalog Studio против target NXOpen.
2. Запустите его в той же NX role/license, где будет работать NXKeys.
3. Создайте новый export directory.
4. Проверьте, что `06_ui_commands_buttons.csv` существует и непуст.
5. Сверьте важные `adapter.value` из `config/nx2512-v8-profile.json` с export.
6. Для изменённых mappings обновите profile/runtime normalization и regression tests.
7. Выполните current validators/builds.
8. Подтвердите interactive behavior на тестовой детали.

Catalog Studio export является evidence для IDs, но не доказывает command sensitivity в каждом context.

## Legacy K1–K5 / K3–K5 generation

Для исторического/full-command-map pipeline export всё ещё может передаваться explicit compiler:

```powershell
node .\scripts\compile-main-command-map.mjs `
  --profile .\config\nx2512-pro-hybrid.json `
  --intents .\config\full-command-map `
  --probe .\docs\audit\runtime-command-probe-2026-07-28.json `
  --catalog-dir "D:\NX2512_Catalog_Output" `
  --out .\config\nx2512-pro-main.generated.json `
  --report .\docs\generated\main-profile-resolution.md
```

Важно: `install-nxkeys.ps1 -CompileOnly` с default v8 profile **не запускает этот K3–K5 compiler**. Он разрешает/проверяет выбранный v8 profile и завершает работу до build/install.

## Когда повторять export

- NX build/maintenance release изменился;
- role/localization/license изменились;
- corporate UI extensions изменились;
- command unexpectedly unavailable/insensitive;
- runtime canonicalization была изменена;
- workstation отличается от той, на которой получен предыдущий evidence.

## Ограничения

- UI ID ≠ гарантированная sensitivity;
- NXOpen member ≠ гарантированная license availability;
- candidate mapping ≠ verified adapter;
- export machine-specific;
- contract/build evidence не заменяет живой NX smoke test.

Current runtime contract: [`docs/RUNTIME_V8.md`](../docs/RUNTIME_V8.md).
