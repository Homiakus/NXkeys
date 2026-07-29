# Диагностика NXKeys

Документ относится к главному профилю K3–K5 из 885 намерений.

## Базовая проверка

```powershell
node .\scripts\validate-main-command-map.mjs

$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
$studio = "$root\NX2512_HotkeyStudio.exe"
$config = "$root\nx2512-pro-hybrid.json"

& $studio validate --config $config
& $studio health --config $config
& $studio bridge-status --config $config
```

Затем откройте `docs/generated/main-profile-resolution.md`.

## Установился bootstrap вместо K3–K5

Признаки: мало команд, отсутствует metadata `full_command_catalog`, нет `selected_intents: 885`.

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Не копируйте `config/nx2512-pro-hybrid.json` вручную в managed root: это bootstrap.

## В main появились K1–K2

Validator должен сообщить reference outside K3–K5 scope. Убедитесь, что compiler запускается без `--frequencies`, включающих K1/K2.

## Команда есть, но отключена

Проверьте `main-profile-resolution.md`:

- `unresolved` — ID не найден;
- `ambiguous` — несколько близких кандидатов;
- `resolved`/`existing` — команда должна быть включена, если ID доступен.

Обновите каталог через Catalog Studio и убедитесь, что существует `06_ui_commands_buttons.csv`, затем повторите компиляцию с `-CatalogDir`.

## Количество строк больше 885

Это нормально при дублировании глобальных команд. Проверяйте `selected_intents` и уникальные `catalog_refs`, а не суммарное число module rows.

## NX не видит NXKeys

Проверьте launcher, `custom_dirs.dat`, Bridge DLL в `custom\application`, MenuScript-файлы, `package-manifest.json`, SHA-256 и закрытие NX во время установки.

## Bridge OFFLINE или STALE

Когда NX закрыт, OFFLINE нормален. При открытом NX проверьте `%LOCALAPPDATA%\NXKeys\bridge\status.json`, `context.json` и `%LOCALAPPDATA%\NXKeys\logs`. Перезапустите NX через launcher после обновления Bridge DLL.

## Команда попала в failed

Типовые причины: отсутствующий ID, лицензия, неверное приложение, изменившаяся context revision, modal dialog, истёкший request, selection или confirmation. Сначала проверьте команду вручную в NX, затем сопоставьте ID с `06_ui_commands_buttons.csv`.

## Selection-фильтр не включается

В pending request должны быть:

```json
{
  "action": "set_selection_filter",
  "selection_filter": "edge"
}
```

Проверьте `action`, `selection_type`, свежий Bridge и отсутствие modal dialog.

## Ошибка установки или rollback

Проверьте последнюю папку `%LOCALAPPDATA%\NXKeys\backups\<timestamp>` и `manifest.json`. Устраните первичную ошибку и повторите установку. Не изменяйте managed root вручную.

## Профиль с другим scope отклонён

`install-nxkeys.ps1` принимает только единый K3–K5 пресет на 885 намерений. Если передан generated profile с K1/K2 или другим числом selected intents, соберите профиль заново через `install-nxkeys.ps1 -CompileOnly`.
