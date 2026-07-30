# Эксплуатационный runbook NXKeys

## Область

Документ предназначен для поддержки установленного managed package NXKeys на Windows x64 с Siemens NX / Designcenter NX 2512.

Канонический managed root из bootstrap-профиля:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000
```

Фактическое значение берётся из `deployment.managed_root` установленного профиля.

## Компоненты процесса

| Процесс/компонент | Ожидаемое состояние |
|---|---|
| Siemens NX (`ugraf`, `run_nx` или NX launcher) | запущен пользователем через NXKeys launcher |
| `NX2512_HotkeyStudio.exe` | один instance; desktop, tray или background |
| `NX2512_CommandBridge.dll` | загружен NX через managed custom directory |
| `NX2512_ControlCenter.exe` | запускается по необходимости |

## Быстрая проверка

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
$studio = "$root\NX2512_HotkeyStudio.exe"
$config = "$root\nx2512-pro-hybrid.json"

& $studio validate --config $config
& $studio health --config $config
& $studio bridge-status --config $config
```

Порядок интерпретации:

1. `validate` должен подтвердить корректность профиля;
2. `health` должен подтвердить managed manifest и required files;
3. при запущенном NX `bridge-status` должен показывать свежий context;
4. при закрытом NX Bridge OFFLINE является нормальным состоянием.

## Файловая структура runtime

```text
%LOCALAPPDATA%\NXKeys\
├── managed\NX2512.6000\
│   ├── NX2512_HotkeyStudio.exe
│   ├── nx2512-pro-hybrid.json
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
├── logs\
└── staging\
```

Не редактируйте managed package и очередь вручную во время выполнения команды.

## Состояние Bridge

### ONLINE

Признаки:

- `status.json` существует;
- `context.json` существует и обновляется;
- `updated_utc` имеет допустимый возраст;
- application/module соответствует открытому NX;
- queue не накапливается без движения.

### STALE

Контекст существует, но давно не обновлялся.

Действия:

1. убедитесь, что NX не завис;
2. проверьте Bridge log;
3. проверьте, не загружена ли старая DLL после обновления;
4. закройте NX полностью;
5. запустите через managed launcher;
6. повторите `bridge-status`.

### OFFLINE

При закрытом NX — нормально. При открытом NX:

1. проверьте launcher;
2. проверьте `UGII_CUSTOM_DIRECTORY_FILE`;
3. проверьте наличие DLL в `custom\application`;
4. проверьте package manifest/hash;
5. проверьте MenuScript version и custom directories;
6. перезапустите NX.

## Очередь запросов

```text
pending → processing → completed | failed
```

### Нормальное поведение

- HotkeyStudio атомарно создаёт request в `pending`;
- Bridge перемещает request в `processing`;
- результат сохраняется;
- request архивируется в `completed` или `failed`;
- повторный `request_id` не должен исполняться повторно.

### Накопление `pending`

Возможные причины:

- Bridge offline;
- NX не запущен через managed launcher;
- custom directory не подключён;
- Bridge не может читать IPC root;
- process NX завис.

Не переносите request вручную в `completed`. Сначала сохраните копию queue для диагностики и восстановите Bridge.

### Файл остаётся в `processing`

Это означает, что request был захвачен и результат может быть неизвестен. После recovery такой запрос должен получить `interrupted_unknown`, а не автоматически выполняться повторно.

Перед повтором действия:

1. проверьте фактическое состояние детали в NX;
2. изучите `last_result`, logs и request JSON;
3. повторите команду только вручную и осознанно.

### Рост `failed`

Соберите последние request/result pairs. Типовые причины:

- expired request;
- stale context revision;
- selection count changed;
- wrong application/module;
- modal dialog;
- unavailable или insensitive `BUTTON ID`;
- destructive request без confirmation;
- invalid schema.

## Логи и доказательства

Основные места:

```text
%LOCALAPPDATA%\NXKeys\logs\leader-key.log
%LOCALAPPDATA%\NXKeys\bridge\status.json
%LOCALAPPDATA%\NXKeys\bridge\context.json
%LOCALAPPDATA%\NXKeys\bridge\pending\
%LOCALAPPDATA%\NXKeys\bridge\processing\
%LOCALAPPDATA%\NXKeys\bridge\completed\
%LOCALAPPDATA%\NXKeys\bridge\failed\
```

Health service также выводит путь Bridge log и последние строки, если они доступны.

Перед передачей logs удалите:

- имена пользователей;
- абсолютные корпоративные пути;
- названия деталей и проектов;
- внутренние identifiers;
- содержимое, защищённое NDA.

## Обновление

Рекомендуемая команда:

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Перед обновлением:

1. закройте NX;
2. завершите незаконченные команды;
3. сохраните production data;
4. сохраните последний успешный backup manifest;
5. обновите Catalog Studio export, если изменились NX/роль/лицензия.

После обновления:

1. выполните `health`;
2. запустите NX через новый launcher;
3. проверьте context/module;
4. выполните безопасные read-only команды;
5. destructive-команды тестируйте на копии.

## Bridge DLL заблокирована

Загруженная DLL остаётся заблокированной процессом NX.

Правильное восстановление:

1. закройте все процессы NX;
2. убедитесь через Task Manager/PowerShell, что `ugraf`, `run_nx` и связанные процессы завершены;
3. повторите установку;
4. не используйте `-AllowRunningNX`, если требуется обновить Bridge;
5. перезапустите NX через managed launcher.

## Backups и restore

Список:

```powershell
& $studio backups --config $config
```

Восстановление последнего backup:

```powershell
& $studio restore --config $config
```

Восстановление выбранного manifest:

```powershell
& $studio restore `
  --config $config `
  --manifest "C:\path\to\manifest.json"
```

После restore:

1. выполните `health`;
2. проверьте hashes;
3. запустите NX;
4. проверьте Bridge context;
5. не удаляйте backup до подтверждения работоспособности.

## Rollback после неудачной установки

Deployment engine должен выполнить автоматический rollback при исключении. Если health-check после установки не проходит:

1. не запускайте destructive-команды;
2. сохраните staging/install logs;
3. найдите последний backup manifest;
4. выполните restore;
5. повторите health-check;
6. устраните первичную причину до новой установки.

## Полная переустановка

`-Clean` очищает управляемый package перед staging/install, но не должен удалять произвольные пользовательские файлы вне manifest boundaries.

Не рекомендуется вручную удалять:

- `%LOCALAPPDATA%\NXKeys\backups`;
- queue с request в `processing` до анализа;
- существующий `custom_dirs.dat`;
- корпоративные `.mtx` роли.

## Monitoring без отдельной системы

Проект не подтверждает встроенный Prometheus/OTel/Windows service monitoring. Минимальный локальный мониторинг можно строить на:

- exit code команды `health`;
- возрасте `context.json`;
- количестве `pending`, `processing`, `failed`;
- hash status package manifest;
- наличии NX/HotkeyStudio процессов.

Любая внешняя автоматизация должна читать файлы без изменения и учитывать, что queue меняется конкурентно.

## Инцидент: подозрение на неверную команду

1. прекратите ввод через Leader;
2. сохраните деталь под новым именем, если безопасно;
3. зафиксируйте время, sequence и активный module;
4. скопируйте request/result/context/status/log;
5. не повторяйте sequence;
6. сравните `command_id`, expected context и фактический NX action;
7. проверьте profile resolution source;
8. отключите проблемную command row до расследования;
9. сообщите по процессу из `SECURITY.md`, если возможен safety/security impact.

## Проверки после изменения NX, роли или лицензии

- повторно экспортировать Catalog Studio;
- пересобрать main profile;
- изучить resolution report;
- проверить ambiguous/unresolved deltas;
- проверить все доступные модули;
- проверить selection filters;
- проверить destructive commands на тестовой детали.

## Ограничения runbook

- пути могут отличаться при пользовательском `deployment.managed_root`;
- названия процессов NX могут различаться;
- лицензия и corporate extensions нельзя подтвердить из репозитория;
- автоматическое восстановление не отменяет резервное копирование производственных данных средствами организации.
