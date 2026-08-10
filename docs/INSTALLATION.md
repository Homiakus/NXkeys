# Установка и обновление NXKeys v8

Каноническая точка входа:

```powershell
.\install-nxkeys.ps1
```

Текущий installer по умолчанию выбирает **`config\nx2512-v8-profile.json`**. Старые generated K3–K5 profiles schemas 3…7 ещё принимаются как compatibility input, но не являются default path.

## Требования

- Windows 10/11 x64;
- Siemens NX или Designcenter NX 2512;
- .NET 8 SDK x64;
- Node.js 20+;
- PowerShell 5.1 или 7;
- `NXOpen.dll` и `NXOpenUI.dll` целевой NX для production Bridge build;
- права записи в `%LOCALAPPDATA%\NXKeys`.

Catalog Studio export с `06_ui_commands_buttons.csv` полезен для проверки фактических IDs, но v8 profile может устанавливаться без `-CatalogDir`.

## Перед установкой

1. сохраните работу в NX;
2. **полностью закройте Siemens NX**;
3. убедитесь, что не остались `ugraf`, `run_nx` или связанные NX процессы;
4. при обновлении сохраните последний рабочий backup/manifest;
5. при изменении NX/роли/лицензии обновите Catalog Studio export.

Загруженная `NX2512_CommandBridge.dll` не обновляется в памяти работающего NX.

## Интерактивное меню

Без параметров:

```powershell
.\install-nxkeys.ps1
```

installer предлагает:

```text
1  Установить или обновить NXKeys
2  Проверить конфликты без изменений
3  Очистить старый NxHotkeys и внутренние конфликты
4  Восстановить UGII_CUSTOM_DIRECTORY_FILE
5  Полная очистка конфликтов и чистая переустановка
0  Выход
```

Для автоматизации используйте `-Mode`.

## Рекомендуемая чистая установка

### Siemens NX

```powershell
.\install-nxkeys.ps1 `
  -Mode CleanInstall `
  -Yes `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

### Designcenter NX

```powershell
.\install-nxkeys.ps1 `
  -Mode CleanInstall `
  -Yes `
  -NxRoot "C:\Program Files\Siemens\DesigncenterNX2512" `
  -Clean
```

С точным `NXOpen.dll`:

```powershell
.\install-nxkeys.ps1 `
  -Mode Install `
  -NxOpenDll "D:\Siemens\NX2512\NXBIN\managed\NXOpen.dll" `
  -Clean
```

`NXOpenUI.dll` должен находиться рядом.

## Как выбирается profile

Если задан `-ConfigPath`, используется указанный файл.

Если `-ConfigPath` отсутствует, installer выбирает:

```text
config\nx2512-v8-profile.json
```

Допустимый source schema range: **3…8**.

- schema 8 — current v8 operations profile;
- schemas 3…7 — compatibility path; installer дополнительно требует legacy generated metadata `K3|K4|K5` и `selected_intents=885`.

Это означает, что проверки 885/K3–K5 относятся **только к старому compatibility profile path**, а не к v8 source profile.

## Параметры обслуживания

| Параметр | Назначение |
|---|---|
| `-Mode Menu` | интерактивное меню по умолчанию |
| `-Mode Install` | установить/обновить |
| `-Mode Audit` | показать конфликты без изменений |
| `-Mode CleanConflicts` | архивировать и убрать известные конфликты |
| `-Mode RepairCustomDirs` | восстановить canonical `UGII_CUSTOM_DIRECTORY_FILE` |
| `-Mode CleanInstall` | cleanup conflicts + чистая установка |
| `-Yes` | подтверждать maintenance action без prompt |
| `-AutoCleanConflicts` | перед установкой автоматически обработать найденные конфликты |
| `-Clean` | очистить предыдущий managed package перед build/staging |
| `-NoBuild` | использовать существующие build artifacts |
| `-AllowRunningNX` | ослабить guard «NX должен быть закрыт»; **не даёт hot reload DLL** |
| `-NoShortcut` | не создавать ярлыки |
| `-CatalogDir` | подключить NX UI catalog export |
| `-NxRoot` | указать root NX |
| `-NxOpenDll` | указать точный NXOpen DLL |
| `-ConfigPath` | установить конкретный profile |
| `-CompileOnly` | завершить после выбора/проверки profile; для default v8 **не создаёт новый v8 profile** |

`-AllowRunningNX` не применяется как обход safety requirement для conflict cleanup.

## Что делает обычная установка

В current v8 flow installer:

1. разрешает profile и проверяет schema;
2. останавливает процессы NXKeys;
3. собирает HotkeyStudio с выбранным profile;
4. собирает Command Bridge против установленного NXOpen;
5. публикует Control Center;
6. при наличии собирает/интегрирует NxEskd artifacts;
7. формирует staging;
8. помещает profile в staging как `nx2512-v8-profile.json`;
9. разворачивает Bridge в managed `custom\application`;
10. запускает C# deployment apply;
11. синхронизирует runtime dependencies;
12. устраняет известные duplicate/legacy conflicts;
13. выполняет installed health-check;
14. создаёт launcher/shortcuts;
15. нормализует `UGII_CUSTOM_DIRECTORY_FILE`.

## Managed layout

Основной root:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000
```

Актуальный installed profile:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\nx2512-v8-profile.json
```

Bridge:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\custom\application\NX2512_CommandBridge.dll
```

Launcher:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\launch-nx2512-with-nxkeys.cmd
```

Backup/conflict backup roots находятся под `%LOCALAPPDATA%\NXKeys`.

## Проверка после установки

До запуска NX:

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
$studio = "$root\NX2512_HotkeyStudio.exe"
$config = "$root\nx2512-v8-profile.json"

& $studio validate --config $config
& $studio health --config $config
```

Затем запустите **managed launcher**, дождитесь открытия NX и выполните:

```powershell
& $studio bridge-status --config $config
```

Проверяйте последовательность:

```text
validate → health → запуск NX через managed launcher → bridge-status
```

Managed launcher важен для IPC schema 4: он создаёт shared authenticated session capability для HotkeyStudio и NX/Bridge.

## Smoke test в живой NX

На тестовой детали проверьте:

1. один физический CapsLock открывает Leader один раз;
2. active module определяется корректно;
3. Modeling `M → L → S` работает как Manage/Layer Settings;
4. Sketch `L`, `K → C`, `D → Q` работают;
5. Sheet Metal использует `UG_APP_SBSM` / `UG_SBSM_*`;
6. Selection Intent `0…4` работает только в подходящем collector;
7. stale/unsigned/wrong-context requests не исполняются;
8. destructive actions требуют confirmation.

## `-CompileOnly`

Для current default v8 profile `-CompileOnly` **не запускает K3–K5 compiler**: installer разрешает `nx2512-v8-profile.json`, проверяет schema и завершает работу до build/install.

Для legacy K3–K5 generation используйте соответствующий Node compiler явно:

```powershell
node .\scripts\compile-main-command-map.mjs `
  --profile .\config\nx2512-pro-hybrid.json `
  --intents .\config\full-command-map `
  --probe .\docs\audit\runtime-command-probe-2026-07-28.json `
  --out .\config\nx2512-pro-main.generated.json `
  --report .\docs\generated\main-profile-resolution.md
```

Generated artifacts не редактируются вручную.

## Audit conflicts

Без изменений:

```powershell
.\install-nxkeys.ps1 -Mode Audit
```

Безопасная очистка с резервированием:

```powershell
.\install-nxkeys.ps1 -Mode CleanConflicts -Yes
```

Восстановление custom dirs:

```powershell
.\install-nxkeys.ps1 -Mode RepairCustomDirs -Yes
```

Cleanup архивирует известные старые NxHotkeys layouts, duplicate Bridge artifacts, legacy toolbar и локальные NXOpen DLL в conflict backup вместо безвозвратного удаления.

## Обновление

1. обновите source tree;
2. полностью закройте NX;
3. выполните `-Mode Install` или `CleanInstall`;
4. проверьте `validate` и `health`;
5. запустите NX через новый managed launcher;
6. проверьте `bridge-status` и smoke test;
7. не удаляйте предыдущий backup до приемки.

## Если Bridge DLL заблокирована

Сообщение о locked DLL означает, что какой-то NX process ещё держит файл.

```powershell
Get-Process ugraf,run_nx,nx -ErrorAction SilentlyContinue
```

Закройте найденные NX processes и повторите установку. Не используйте `-AllowRunningNX` для попытки фактически заменить уже загруженную DLL.

## Restore

```powershell
& $studio backups --config $config
& $studio restore --config $config
```

С конкретным manifest:

```powershell
& $studio restore `
  --config $config `
  --manifest "C:\path\to\manifest.json"
```

`--force` используйте только после ручной проверки причины отказа обычного restore.

## Ограничения

- installer/UI всё ещё содержит отдельные исторические текстовые строки «K3–K5», хотя default input уже v8; эти строки не определяют фактический profile selection;
- production Bridge build требует реальных NXOpen DLL;
- CI contract build не подтверждает лицензию/sensitivity/interactive semantics;
- `BUTTON ID` следует перепроверять после изменения NX maintenance release, роли или лицензии.

Эксплуатационный runbook: [OPERATIONS.md](OPERATIONS.md). Диагностика: [TROUBLESHOOTING.md](TROUBLESHOOTING.md).
