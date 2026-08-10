# Эксплуатационный runbook NXKeys v8

Документ предназначен для поддержки managed NXKeys на Windows x64 с Siemens NX / Designcenter NX 2512.

Current installed profile:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\nx2512-v8-profile.json
```

## Быстрая проверка

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
$studio = "$root\NX2512_HotkeyStudio.exe"
$config = "$root\nx2512-v8-profile.json"

& $studio validate --config $config
& $studio health --config $config
```

После запуска NX через managed launcher:

```powershell
& $studio bridge-status --config $config
```

Ожидаемый порядок:

```text
validate → health → managed launch NX → bridge-status → safe smoke test
```

## Runtime layout

```text
%LOCALAPPDATA%\NXKeys\
├── managed\NX2512.6000\
│   ├── NX2512_HotkeyStudio.exe
│   ├── nx2512-v8-profile.json
│   ├── nx2512-state-machines.json
│   ├── package-manifest.json
│   ├── launch-nx2512-with-nxkeys.cmd
│   ├── custom\application\NX2512_CommandBridge.dll
│   └── control-center\NX2512_ControlCenter.exe
├── bridge\
│   ├── pending\
│   ├── processing\
│   ├── completed\
│   ├── failed\
│   ├── context.json
│   └── status.json
├── backups\
├── conflict-backups\
├── logs\
└── staging\
```

## Нормальные состояния Bridge

### OFFLINE

Нормально, если NX закрыт.

### ONLINE

Ожидаются fresh `status.json`/`context.json`, правильный module/application, действующая authenticated session и отсутствие бесконтрольного роста queue.

### STALE

Context существует, но устарел. Не выполняйте новые commands до восстановления fresh context.

### `authentication_required`

NX/HotkeyStudio не разделяют valid schema-4 launch session. Полностью перезапустите их через:

```text
launch-nx2512-with-nxkeys.cmd
```

Не создавайте security fields вручную.

## Queue lifecycle

```text
pending → processing → completed | failed
```

Schema 4 добавляет HMAC/session/anti-replay/profile-permission admission. Queue file сам по себе не является разрешением.

### `pending` растёт

Проверьте:

- Bridge online;
- authenticated session;
- queue limits;
- file/ACL/antivirus issues;
- process health;
- profile digest agreement.

### `processing` остался после crash

Результат считается неизвестным. Recovery не должен автоматически retry request.

Перед ручным повтором проверьте фактический NX state.

### `failed` растёт

Смотрите reason categories:

- schema/expiry;
- authentication/HMAC;
- anti-replay;
- permission/profile digest;
- stale/wrong context;
- selection fingerprint changed;
- unavailable command;
- destructive confirmation;
- interactive invocation ambiguity.

## Обновление

Рекомендуемая чистая установка:

```powershell
.\install-nxkeys.ps1 `
  -Mode CleanInstall `
  -Yes `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Перед обновлением:

1. сохраните production work;
2. полностью закройте NX;
3. сохраните предыдущий backup/manifest;
4. выполните `-Mode Audit`, если подозреваются legacy conflicts.

После обновления:

1. `validate`;
2. `health`;
3. managed launch;
4. `bridge-status`;
5. safe v8 smoke test.

## V8 smoke test

На тестовой детали:

```text
CapsLock              один физический trigger
Modeling M→L→S        Manage / Layer Settings
Sketch L              Line
Sketch K→C            Coincident
Sketch D→Q            Rapid Dimension
Sheet Metal           canonical UG_APP_SBSM / UG_SBSM_*
0…4                   Selection Intent в collector
```

Дополнительно проверьте wrong-session/stale requests и destructive confirmation.

## Bridge DLL lock

Bridge загружена в NX process и не поддерживает hot reload.

```powershell
Get-Process ugraf,run_nx,nx -ErrorAction SilentlyContinue
```

Перед Bridge upgrade эти процессы должны быть завершены.

`-AllowRunningNX` может ослабить installer guard, но не выгружает старую DLL из памяти.

## Conflict maintenance

Audit без изменений:

```powershell
.\install-nxkeys.ps1 -Mode Audit
```

Cleanup с резервированием:

```powershell
.\install-nxkeys.ps1 -Mode CleanConflicts -Yes
```

Repair custom dirs:

```powershell
.\install-nxkeys.ps1 -Mode RepairCustomDirs -Yes
```

Cleanup известных legacy/duplicate files архивирует их в `%LOCALAPPDATA%\NXKeys\conflict-backups`.

## Backups / restore

```powershell
& $studio backups --config $config
& $studio restore --config $config
```

Выбранный manifest:

```powershell
& $studio restore --config $config --manifest "C:\path\manifest.json"
```

После restore снова выполните health + managed launch + bridge-status.

## Runtime logs/evidence

```text
%LOCALAPPDATA%\NXKeys\logs\leader-key.log
%LOCALAPPDATA%\NXKeys\bridge\status.json
%LOCALAPPDATA%\NXKeys\bridge\context.json
%LOCALAPPDATA%\NXKeys\bridge\pending\
%LOCALAPPDATA%\NXKeys\bridge\processing\
%LOCALAPPDATA%\NXKeys\bridge\completed\
%LOCALAPPDATA%\NXKeys\bridge\failed\
```

Перед передачей evidence удалите production names, персональные данные, закрытые пути и secrets. Session secret не должен попадать в repository/issues.

## Инцидент: неверный command

1. остановите Leader;
2. не повторяйте sequence;
3. сохраните деталь безопасно;
4. зафиксируйте active module, user path и time;
5. сохраните request/result/context/log;
6. проверьте фактический NX side effect;
7. сравните canonical command ID и profile permission;
8. отключите проблемную operation до расследования.

Если UI/dialog открылся, но result reported failure, не делайте automatic retry: interactive invocation мог иметь false-negative return.

## Инцидент: Selection Intent

Если `0…4` не работает:

- подтвердите active collector/seed;
- убедитесь, что цифра не вводится в text control;
- подтвердите текущую Bridge DLL после restart;
- проверьте native selection toggles NX.

Если цифры перехватываются в numeric fields, это guard defect; зафиксируйте focused control и active command.

## Изменение NX/роли/лицензии

После изменения окружения:

1. обновите Catalog Studio export;
2. проверьте IDs текущих v8 operations;
3. отдельно проверьте Sheet Metal/Sketch/Selection Intent;
4. запустите current profile validators;
5. legacy K3–K5 coverage pipeline используйте только как отдельный analytical report.

## Ограничения

- CI не подтверждает actual NX license/sensitivity;
- contract stubs не подтверждают dialog semantics;
- corporate roles/custom controls могут менять поведение;
- managed health не заменяет functional acceptance test.

Подробная диагностика: [TROUBLESHOOTING.md](TROUBLESHOOTING.md).
