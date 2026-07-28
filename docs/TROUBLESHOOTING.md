# Диагностика NXKeys

Документ относится к актуальной архитектуре NXKeys: source profile schema 4, runtime schema 5, IPC schema 3, 14 модулей и полная карта из 1169 намерений.

## 1. Базовый порядок проверки

1. Закройте Siemens NX перед заменой DLL и MenuScript.
2. Проверьте базовую и полную карты.
3. Для полного профиля проверьте каталог `06_ui_commands_buttons.csv`.
4. Скомпилируйте профиль и изучите отчёт.
5. Постройте deployment plan без записи.
6. Выполните установку.
7. Проверьте package manifest и health.
8. Запустите NX только через managed launcher.
9. Проверьте Bridge context и безопасную команду.

```powershell
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs

$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
$studio = "$root\NX2512_HotkeyStudio.exe"
$config = "$root\nx2512-pro-hybrid.json"

& $studio validate --config $config
& $studio plan --config $config
& $studio health --config $config
& $studio bridge-status --config $config
```

## 2. Полный профиль не компилируется

Проверьте:

- Node.js 20+;
- путь `-CatalogDir`;
- наличие `06_ui_commands_buttons.csv`;
- доступ на чтение каталога;
- целостность трёх частей `config/full-command-map/*.part1–part3`;
- успешное выполнение `validate-full-command-map.mjs`.

```powershell
node --version
.\install-full-command-profile.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -CompileOnly
```

## 3. Много `unresolved` или `ambiguous`

Это не ошибка безопасности: такие команды намеренно отключаются.

Причины:

- каталог сформирован не из целевой NX;
- роль не содержит нужный модуль;
- лицензия скрывает команды;
- локализация изменила labels;
- несколько `BUTTON ID` имеют похожие названия;
- корпоративный MenuScript переименовал команду.

Откройте:

```text
docs/generated/full-command-resolution.md
```

Для критичных команд найдите точный ID через Catalog Studio или CLI:

```powershell
& $studio catalog --config $config --query "Extrude" --catalog "D:\NX2512_Catalog_Output"
```

Не подставляйте ID по догадке.

## 4. NX не видит NXKeys

Проверьте:

- запуск через `launch-nx2512-with-nxkeys.cmd`;
- `%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\custom_dirs.dat`;
- путь managed custom root внутри файла;
- `.men`, `.rtb`, `.tbr` в управляемом layout;
- `NX2512_CommandBridge.dll` в путях, перечисленных `package-manifest.json`;
- отсутствие ручных конфликтующих копий другой версии Bridge;
- корректный `deployment.nx_executable`;
- MenuScript VERSION 139 для `.men` и 170 для `.tbr/.rtb`;
- отсутствие SHA mismatch.

Не используйте правило «Bridge обязан быть только в одной заранее заданной папке». Точный layout определяет текущий installer и package manifest.

## 5. NX запускается без кастомизации

Managed launcher задаёт:

```text
UGII_CUSTOM_DIRECTORY_FILE=<managed-root>\custom_dirs.dat
```

Проверьте, что NX не запущен старым ярлыком. NXKeys не изменяет глобальный `PATH` и `UGII_USER_DIR`.

## 6. NX executable не найден

Укажите абсолютный путь:

```json
{
  "deployment": {
    "nx_executable": "C:\\Program Files\\Siemens\\NX2512\\NXBIN\\ugraf.exe"
  }
}
```

Auto-discovery проверяет environment variables, `scan.install_hints` и стандартные каталоги Siemens. При нескольких версиях всегда задавайте точный путь.

## 7. Установка остановилась из-за запущенного NX

Проверьте процессы:

```text
ugraf.exe
run_nx.exe
nx.exe, подтверждённый Siemens/Designcenter/NXBIN path
```

Закройте NX и повторите установку. `-AllowRunningNX` предназначен только для диагностики: загруженная DLL не заменится в уже работающем процессе.

## 8. Не заменяется CommandBridge DLL

1. Закройте все окна NX.
2. Завершите оставшиеся процессы NX.
3. Проверьте, что Control Center и HotkeyStudio не используют старый managed root.
4. Повторите установку.

Deployment выполняет retry и rollback по backup manifest.

## 9. Ошибка VERSION

```text
.men       VERSION 139
.tbr/.rtb  VERSION 170
```

Toolbar/ribbon являются слоем размещения; действия определяются `.men` и Bridge.

## 10. Staging SHA-256 или rollback

Staging:

```text
%LOCALAPPDATA%\NXKeys\staging\<guid>
```

Проверьте свободное место, файловую систему и вмешательство антивируса. Не отключайте защиту без согласования.

Backup:

```text
%LOCALAPPDATA%\NXKeys\backups\<timestamp>\manifest.json
```

При ошибке после backup deployment пытается восстановить предыдущий набор.

## 11. `package-manifest.json` отсутствует

Возможные причины:

- установка не дошла до commit;
- сработал rollback;
- установлен старый пакет;
- manifest удалён вручную.

Выполните чистую установку и не копируйте файлы в managed root вручную.

## 12. Health показывает SHA mismatch

1. Сохраните вручную изменённый файл отдельно.
2. Закройте NX.
3. Повторно установите пакет.
4. Снова выполните `health`.

Package manifest — источник контроля целостности установленного набора.

## 13. После обновления остались старые файлы

Автоматически удаляются только файлы, перечисленные в предыдущем manifest и отсутствующие в новом. Неизвестные пользовательские файлы намеренно не удаляются.

## 14. Existing `custom_dirs.dat`

При режиме:

```json
{
  "deployment": {
    "mode": "existing-custom-dirs",
    "existing_custom_dirs_file": "D:\\NX\\custom_dirs.dat",
    "patch_existing_custom_dirs": true
  }
}
```

NXKeys сохраняет кодировку, line endings, комментарии и существующие строки. Используйте явный путь — первый найденный файл автоматически не выбирается.

## 15. Bridge OFFLINE или STALE

Когда NX закрыт, OFFLINE нормально.

Когда NX открыт, проверьте:

- managed launcher;
- `bridge/status.json`;
- `bridge/context.json`;
- `%LOCALAPPDATA%\NXKeys\logs`;
- package manifest;
- отсутствие нескольких конфликтующих Bridge DLL;
- `updated_utc` и `status=running`.

Protocol freshness для dispatch по умолчанию — 3 секунды. Control Center может показывать контекст как визуально доступный дольше, но выполнение подчиняется policy.

## 16. Команда попала в `failed`

Типовые причины:

- ID отсутствует в этой NX;
- команда недоступна в приложении;
- кнопка нечувствительна;
- открыт modal dialog;
- отсутствует лицензия;
- context revision изменилась;
- request expired;
- destructive confirmation не принято;
- expected selection/application не совпали.

Сначала выполните команду вручную в NX, затем проверьте точный ID и runtime context.

## 17. Selection filter не работает

В pending request должно быть:

```json
{
  "schema_version": 3,
  "action": "set_selection_filter",
  "selection_filter": "edge"
}
```

Проверьте:

- `action: set_selection_filter` в runtime profile;
- корректный `selection_type`;
- свежий Bridge;
- отсутствие modal dialog;
- поддержку нужных `NXOpen.Select.FilterMember` в целевой NX.

`requires_selection` сам по себе не должен блокировать интерактивную команду. Ищите положительный minimum selection в policy.

## 18. HUD не показывает все 1169 команд

Это ожидаемо:

- HUD показывает только активный module scope;
- часть команд может быть unresolved и отключена;
- глобальное дублирование можно отключить;
- поиск удобнее полного визуального дерева;
- одна intent-команда может быть представлена только в целевом модуле.

Проверьте сгенерированный профиль и отчёт, а не только первый экран HUD.

## 19. Сочетание вызывает не ту команду

Проверьте:

1. активный `module_id`;
2. канонический `path` и aliases;
3. отсутствие старого профиля в managed root;
4. source profile, использованный установщиком;
5. `resolution_status` и `catalog_refs`;
6. фактический `command_id` в pending request.

Внутри другого модуля тот же пользовательский путь может быть корректно назначен другой команде.

## 20. Конфликт prefix или duplicate path

Запустите:

```powershell
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs
```

Не исправляйте конфликт случайным удалением токена. Сохраните модель `action → object → command` и назначьте детерминированный отличающийся leaf/variant.

## 21. Control Center показывает неизвестный выбор

`selection_count: -1` означает «неизвестно», а не ноль. Не делайте вывод о готовности selection-dependent команды только по этой карточке.

## 22. Сбор диагностических данных

Сохраните:

```text
active profile JSON
full-command-resolution.md
package-manifest.json
bridge/context.json
bridge/status.json
последний pending/processing/completed/failed request
runtime logs
версию NX, роль и набор лицензий
```

Не публикуйте корпоративные пути, имена деталей, customer data или закрытые постпроцессоры без очистки.
