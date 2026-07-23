# Диагностика NXKeys

Документ относится к C#-only архитектуре NXKeys для Siemens NX 2512.

## 1. Базовый порядок проверки

1. Закройте Siemens NX перед обновлением DLL и MenuScript.
2. Проверьте JSON-профиль.
3. Постройте план без записи.
4. Выполните установку или обновление.
5. Проверьте package manifest.
6. Запустите NX через managed launcher.
7. Проверьте Bridge и конкретную команду в правильном модуле.

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
$studio = "$root\NX2512_HotkeyStudio.exe"
$config = "$root\nx2512-pro-hybrid.json"

& $studio validate --config $config
& $studio plan --config $config
& $studio health --config $config
& $studio bridge-status --config $config
```

## 2. NX не видит NXKeys

Проверьте:

- NX запущен через `launch-nx2512-with-nxkeys.cmd`;
- существует `%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\custom_dirs.dat`;
- `custom_dirs.dat` содержит managed custom root;
- `custom\startup` содержит `.men`, `.rtb`, `.tbr` и launch CMD;
- `custom\application` содержит `NX2512_CommandBridge.dll` и `nxkeys_command_bridge.men`;
- Bridge DLL отсутствует в `custom\startup`;
- путь `deployment.nx_executable` корректен либо C# auto-discovery находит NX 2512;
- `package-manifest.json` существует;
- `health` не сообщает SHA mismatch;
- MenuScript содержит ожидаемые версии.

## 3. Launcher сообщает, что NX не найден

Укажите точный путь в профиле:

```json
{
  "deployment": {
    "nx_executable": "C:\\Program Files\\Siemens\\NX2512\\NXBIN\\ugraf.exe"
  }
}
```

Затем повторно примените профиль.

C# launcher также проверяет:

- `UGII_ROOT_DIR`;
- `UGII_BASE_DIR`;
- `scan.install_hints`;
- стандартные каталоги Siemens в Program Files.

При нескольких версиях NX рекомендуется всегда задавать абсолютный путь.

## 4. NX запускается, но загружается без кастомизации

Проверьте, что NX создан дочерним процессом C# launcher, а не запущен старым ярлыком.

Managed launcher задаёт только:

```text
UGII_CUSTOM_DIRECTORY_FILE=<managed-root>\custom_dirs.dat
```

Команда проверки:

```powershell
& $studio launch --config $config
```

NXKeys не изменяет глобальный `PATH` и не устанавливает `UGII_USER_DIR`.

## 5. Ошибка VERSION в `.men`

Для `.men` требуется:

```text
VERSION 139
```

Проверка:

```powershell
& $studio health --config $config
```

После исправления закройте NX и переустановите пакет.

## 6. Ошибка VERSION в `.tbr` или `.rtb`

Для toolbar/ribbon требуется:

```text
VERSION 170
```

`.tbr/.rtb` должны оставаться слоем размещения. Определения кнопок и действий находятся в `.men`.

## 7. Установка остановилась из-за запущенного NX

C# и PowerShell используют общий набор признаков:

- `ugraf.exe`;
- `run_nx.exe`;
- `nx.exe`, подтверждённый путём или описанием Siemens/Designcenter/NXBIN.

Сохраните открытые документы, закройте NX и повторите установку.

Параметр `-AllowRunningNX` предназначен только для диагностических сценариев. Загруженная Bridge DLL не заменится в уже работающем процессе.

## 8. Не удаётся заменить CommandBridge DLL

Причина обычно в блокировке процессом NX.

1. Закройте все окна NX.
2. Проверьте процессы `ugraf`, `run_nx`, `nx`.
3. Убедитесь, что не остался фоновый NX-процесс.
4. Повторите установку.

`AtomicFileWriter` выполняет несколько повторных попыток. При окончательной ошибке общий deployment восстанавливает backup manifest.

## 9. Ошибка staging SHA-256

Deployment сначала записывает каждый файл в:

```text
%LOCALAPPDATA%\NXKeys\staging\<guid>
```

и сравнивает SHA-256 с исходным буфером.

Причины ошибки:

- сбой диска;
- антивирус изменяет временный файл;
- нехватка места;
- повреждённая файловая система;
- сторонний процесс вмешивается в staging.

Проверьте свободное место и журнал антивируса. Не отключайте защиту без согласования с администратором.

## 10. Установка завершилась rollback

При исключении после создания резервной копии NXKeys вызывает восстановление backup manifest.

Проверьте последнюю папку:

```text
%LOCALAPPDATA%\NXKeys\backups\<timestamp>
```

В `manifest.json` находятся:

- исходные пути;
- SHA-256 до изменения;
- SHA-256 после попытки;
- резервные копии существовавших файлов;
- отметки новых файлов.

Повторяйте установку только после устранения исходной ошибки.

## 11. `package-manifest.json` отсутствует

Это означает, что:

- установка не дошла до commit-фазы;
- установлен старый пакет;
- manifest был удалён вручную;
- сработал rollback.

Выполните чистую установку:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-nx-ribbon-buttons.ps1 `
  -Clean `
  -NxRoot "C:\Program Files\Siemens\NX2512"
```

## 12. Health показывает SHA mismatch

Не копируйте файлы в managed root вручную.

Порядок действий:

1. Сохраните изменённый файл отдельно, если он нужен.
2. Сравните его с текущей сборкой.
3. Закройте NX.
4. Повторно установите пакет.
5. Повторите `health`.

Package manifest является источником контроля целостности установленного набора.

## 13. После обновления остался старый файл

Автоматически удаляются только файлы, перечисленные в предыдущем `package-manifest.json` и отсутствующие в новом наборе.

Пользовательские файлы NXKeys намеренно не удаляются.

При отсутствии старого manifest неизвестные файлы требуется проверить и удалить вручную.

## 14. Existing `custom_dirs.dat` изменил формат

Новая C#-реализация сохраняет:

- UTF-8;
- UTF-8 BOM;
- UTF-16 LE/BE;
- исходный CRLF/LF;
- существующие комментарии и строки.

Убедитесь, что используется явный путь:

```json
{
  "deployment": {
    "mode": "existing-custom-dirs",
    "existing_custom_dirs_file": "D:\\NX\\custom_dirs.dat",
    "patch_existing_custom_dirs": true
  }
}
```

NXKeys больше не выбирает первый найденный файл автоматически.

## 15. Bridge показывает OFFLINE

Когда NX закрыт, это нормально.

Когда NX открыт:

- убедитесь, что NX запущен через managed launcher;
- проверьте `%LOCALAPPDATA%\NXKeys\bridge\status.json`;
- проверьте `context.json`;
- откройте журналы `%LOCALAPPDATA%\NXKeys\logs`;
- проверьте `custom\application\NX2512_CommandBridge.dll`;
- убедитесь, что нет второй Bridge DLL в `custom\startup`.

## 16. Bridge показывает STALE

Control Center считает контекст свежим приблизительно 10 секунд после `updated_utc`.

Возможные причины:

- таймер Bridge остановился;
- библиотека не загрузилась;
- `context.json` заблокирован или повреждён;
- NX завис;
- установлена старая DLL;
- NX был запущен не через managed launcher.

Закройте NX, выполните установку и повторите запуск.

## 17. Команда попала в `failed`

Результаты находятся в:

```text
%LOCALAPPDATA%\NXKeys\bridge\failed
```

Типовые причины:

- `BUTTON ID` отсутствует в данной сборке NX;
- команда недоступна в текущем приложении;
- кнопка нечувствительна без выбранного объекта;
- открыт несовместимый диалог;
- отсутствует лицензия;
- команда переименована;
- запрос просрочен;
- ожидаемый контекст изменился.

Сначала выполните ту же команду вручную в NX, затем сверяйте точный ID с каталогом целевой установки.

## 18. Control Center показывает неизвестный выбор

`Selection: unknown` или `-1` означает, что Bridge не передал число выбранных объектов. Это не равнозначно нулю.

Команда с `requires_selection` в таком состоянии должна рассматриваться осторожно и проверяться непосредственно в NX.

## 19. Control Center не находит HotkeyStudio

Стандартная установка:

```text
managed-root\NX2512_HotkeyStudio.exe
managed-root\control-center\NX2512_ControlCenter.exe
```

Control Center ищет HotkeyStudio в своей папке и в родительском managed root.

Не переносите только один EXE без его зависимостей.

## 20. API-каталог не найден

Порядок поиска:

1. `--catalog`;
2. `NXKEYS_CATALOG_DIR`;
3. свежий каталог под `%LOCALAPPDATA%\NXKeys\catalog`.

Пример:

```powershell
& "$root\control-center\NX2512_ControlCenter.exe" `
  --config $config `
  --catalog "D:\NX2512_Full_Function_API_Catalog_YYYYMMDD_HHMMSS"
```

Либо:

```powershell
$env:NXKEYS_CATALOG_DIR = "D:\NX2512_Full_Function_API_Catalog_YYYYMMDD_HHMMSS"
```

Ожидаемые CSV:

```text
04_nxopen_members.csv
05_nxopen_entry_points.csv
06_ui_commands_buttons.csv
07_ufun_functions.csv
08_ui_command_api_candidates.csv
```

## 21. API-кэш не обновился

Ключ кэша включает MenuScript и API CSV по пути, размеру и времени изменения, а также версию NX и схему сканера.

Для принудительной очистки закройте приложения NXKeys и удалите:

```text
%LOCALAPPDATA%\NXKeys\cache
```

После следующего сканирования кэш будет создан заново.

## 22. Сборка CommandBridge выбрала не ту NXOpen

Передайте точный DLL:

```powershell
.\NX2512_CommandBridge\build.ps1 `
  -NxOpenDll "D:\Siemens\NX2512\NXBIN\managed\NXOpen.dll" `
  -Clean
```

По умолчанию build-скрипт отклоняет путь без признака версии 2512.

Не используйте `-AllowVersionMismatch`, пока совместимость не проверена вручную.

## 23. Сборка требует .NET 8

Build-скрипты больше не скачивают SDK автоматически.

Проверка:

```powershell
dotnet --list-sdks
```

Должна присутствовать версия `8.x`.

## 24. Неоднозначная команда

Заполните точный ID:

```json
{
  "command": {
    "id": "UG_EXACT_BUTTON_ID",
    "name": "Читаемое имя"
  }
}
```

Не снижайте порог fuzzy matching только ради прохождения плана.

## 25. Конфликт горячих клавиш

Проверьте:

- `Ctrl+1 → Customize → Keyboard`;
- корпоративную роль;
- сторонние custom directories;
- generated `.men`;
- `clear_detected_conflicts`;
- `resolution-report.md`.

NXKeys не может полностью прочитать ускорители внутри непрозрачного `.mtx`.

## 26. Восстановление вручную

Список резервных копий:

```powershell
& $studio backups --config $config
```

Конкретный manifest:

```powershell
& $studio restore `
  --config $config `
  --manifest "$env:LOCALAPPDATA\NXKeys\backups\YYYYMMDD_HHMMSS.mmm\manifest.json"
```

Используйте `--force` только после проверки, если файлы изменились после установки.

## 27. Что приложить к отчёту об ошибке

- версия Windows;
- точная версия NX;
- путь `ugraf.exe`;
- версия NXOpen assembly;
- используемая роль и приложение NX;
- команда или Leader-последовательность;
- вывод `validate`, `plan`, `health`;
- `package-manifest.json`;
- backup manifest последней неудачной установки;
- соответствующий результат из `bridge\failed`;
- последние строки журналов;
- обезличенный фрагмент профиля;
- сведения о лицензии проблемного модуля.
