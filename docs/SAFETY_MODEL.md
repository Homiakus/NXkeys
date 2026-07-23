# Модель безопасности NXKeys

NXKeys предназначен для обратимой настройки Siemens NX 2512 без изменения файлов установки Siemens, без массовой записи в обнаруженные профили и без неявного вмешательства во внутренние форматы ролей.

## 1. Основные инварианты

1. Файлы установки Siemens не перезаписываются.
2. Непрозрачные бинарные форматы не патчатся.
3. Перед изменением или удалением целевой файл включается в backup manifest.
4. При `atomic_writes: true` запись выполняется через временный файл в том же каталоге.
5. Временный файл принудительно сбрасывается на диск до commit-фазы.
6. При ошибке локальной замены исходный файл восстанавливается из rollback-копии.
7. При общей ошибке deployment выполняется восстановление backup manifest.
8. `package-manifest.json` записывается только после установки остальных файлов.
9. После установки SHA-256 каждого управляемого файла проверяется повторно.
10. Устаревший файл удаляется только если он принадлежал предыдущему package manifest и находится внутри `managed_root`.
11. Неразрешённые и неоднозначные команды исключаются из активного MenuScript.
12. Разрушительные Leader-команды должны быть помечены и подтверждаться пользователем.
13. Command Bridge не выполняет отсутствующую, недоступную или нечувствительную кнопку.
14. Запрос Bridge имеет ограниченный срок действия.
15. Launcher передаёт NX только `UGII_CUSTOM_DIRECTORY_FILE` и не подменяет `PATH` или `UGII_USER_DIR`.
16. CommandBridge DLL устанавливается только в `custom\application`.

## 2. Что NXKeys создаёт или изменяет

В режиме `managed-wrapper`:

- `%LOCALAPPDATA%\NXKeys\managed\<version>`;
- приватный `custom_dirs.dat`;
- `.men`, `.tbr`, `.rtb`;
- CMD-обёртки;
- C# runtime-файлы;
- `package-manifest.json`;
- отчёты и radial-план;
- backup, cache, staging, bridge queue и журналы.

В режиме `existing-custom-dirs` дополнительно изменяется только явно указанный файл `deployment.existing_custom_dirs_file`.

При включённом `role_deployment` копируется только явно заданная экспортированная роль `.mtx` в явно заданный target directory.

## 3. Что NXKeys не изменяет

- исполняемые файлы и библиотеки Siemens;
- штатные MenuScript-файлы установки NX;
- внутреннюю структуру `user.mtx` и других `.mtx`;
- лицензии и сервер лицензирования;
- базы Teamcenter;
- глобальные системные переменные Windows;
- глобальный `PATH`;
- `UGII_USER_DIR`;
- все найденные каталоги `%APPDATA%\Siemens\*`;
- модели и чертежи напрямую.

Команда, выполненная через Bridge, может изменить открытую модель так же, как ручное нажатие кнопки. За смысл операции отвечает документ NX и пользовательский контекст.

## 4. Граница PowerShell и C#

PowerShell-скрипты используются только для:

- проверки установленного .NET 8;
- запуска сборки;
- разрешения NXOpen;
- формирования чистого staging bundle;
- вызова C# deployment.

PowerShell не копирует файлы непосредственно в рабочий managed root.

Build-скрипты:

- не устанавливают SDK автоматически;
- не загружают и не исполняют `dotnet-install.ps1`;
- не выбирают произвольную NXOpen DLL без проверки;
- очищают `dist` перед новой поставкой.

## 5. Транзакционная модель

```text
validate
  → build exact file set
  → create staging
  → verify staging SHA-256
  → create backup manifest
  → atomic commit
  → remove manifest-owned stale files
  → write package manifest
  → verify installed SHA-256
  → success
```

При ошибке после backup:

```text
exception
  → finalize attempted state
  → restore backup manifest with force
  → report rollback result
```

`force` используется внутри автоматического rollback, потому что текущее состояние ожидаемо отличается от состояния до транзакции.

## 6. Package manifest

`package-manifest.json` содержит:

- версию схемы;
- версию NXKeys package;
- целевую версию NX;
- managed root;
- дату формирования;
- относительный путь;
- размер;
- SHA-256;
- required-флаг.

Manifest не содержит внешние пути роли или existing `custom_dirs.dat`. Эти файлы защищаются backup manifest, но не считаются частью managed package.

Относительный путь при health-check не должен выходить за `managed_root`.

## 7. Сохранение `custom_dirs.dat`

При existing mode C# `TextFileCodec`:

- определяет UTF-8/UTF-8 BOM;
- определяет UTF-16 LE/BE;
- использует строгий decode;
- сохраняет BOM;
- сохраняет CRLF/LF;
- не удаляет комментарии;
- добавляет только отсутствующий нормализованный путь.

Неизвестная или повреждённая кодировка должна приводить к диагностике, а не к безусловной полной перезаписи.

## 8. Обрабатываемые угрозы

- прерывание записи;
- частичная запись;
- временная блокировка;
- смешивание старого `dist` с новой сборкой;
- остаточные файлы предыдущей версии;
- повторное добавление custom path;
- изменение кодировки existing `custom_dirs.dat`;
- неоднозначное имя команды;
- изменение файла после установки;
- отсутствующий NX executable;
- выбор NXOpen другой версии;
- работа NX во время обновления Bridge;
- двойное размещение Bridge DLL;
- устаревший запрос;
- недоступный `BUTTON ID`;
- неправильная версия MenuScript;
- выход manifest path за managed root.

## 9. Ограничения вне NX

Нельзя полностью гарантировать:

- отсутствие конфликтов внутри непрозрачной роли `.mtx`;
- доступность команды для конкретной лицензии;
- наличие UI-команд, не представленных как `BUTTON`;
- сохранение семантики `BUTTON ID` между версиями NX;
- radial layout без роли, экспортированной из целевой версии;
- эквивалентность UI-команды и NXOpen/UFUN candidate;
- состояние выбора, если Bridge его не публикует;
- последствия команды для пользовательской модели;
- поведение корпоративных сторонних custom directories.

## 10. Уровни доверия к команде

| Уровень | Значение |
|---|---|
| Имя | только поиск и отображение |
| Кандидат | вероятное совпадение по имени или API-crosswalk |
| Точный ID | профиль содержит `BUTTON ID` |
| Найден в каталоге | ID найден в меню целевой установки |
| Доступен в контексте | NX сообщает availability/sensitivity |
| Выполнен | NX принял вызов, Bridge записал success |

Точный ID сам по себе не является гарантией выполнения.

## 11. Опасные операции

```json
{
  "destructive": true,
  "confirm_before_execute": true
}
```

Leader HUD требует `Enter`. `Esc` отменяет действие.

Примеры операций, требующих дополнительной осторожности:

- удаление;
- замена компонента;
- массовая регенерация;
- запуск расчёта;
- CAM postprocessing;
- операции над внешними ссылками;
- изменение структуры сборки.

## 12. Резервные копии

```text
%LOCALAPPDATA%\NXKeys\backups
```

Backup manifest содержит:

- original path;
- backup path;
- new-file marker;
- SHA-256 до применения;
- SHA-256 после попытки;
- профиль и timestamp.

Не удаляйте backup до успешной проверки NX, Bridge и ключевых команд.

## 13. Безопасная последовательность применения

```powershell
NX2512_HotkeyStudio.exe validate --config .\config\nx2512-pro-hybrid.json
NX2512_HotkeyStudio.exe scan --config .\config\nx2512-pro-hybrid.json
NX2512_HotkeyStudio.exe plan --config .\config\nx2512-pro-hybrid.json
NX2512_HotkeyStudio.exe apply --config .\config\nx2512-pro-hybrid.json --dry-run
NX2512_HotkeyStudio.exe apply --config .\config\nx2512-pro-hybrid.json --yes
NX2512_HotkeyStudio.exe health --config .\config\nx2512-pro-hybrid.json
```

Перед организационным развёртыванием:

1. используйте тестовую станцию;
2. применяйте `managed-wrapper`;
3. проверяйте package manifest;
4. проверяйте launcher;
5. проверяйте высокочастотные команды;
6. отдельно проверяйте опасные операции;
7. повторяйте тесты для каждой роли, лицензии и сборки NX;
8. проверяйте rollback при заблокированном файле;
9. храните профиль и проверенную роль в контроле версий.

## 14. Реагирование на ошибку

1. Не повторяйте установку вслепую.
2. Закройте NX.
3. Сохраните логи и `bridge\failed`.
4. Сохраните последний backup manifest.
5. Выполните `health`.
6. Сравните package manifest и фактические SHA-256.
7. Проверьте результат автоматического rollback.
8. При необходимости восстановите конкретный manifest вручную.
9. Не удаляйте staging/backup evidence до определения причины.

Дополнительные сценарии: [TROUBLESHOOTING.md](TROUBLESHOOTING.md).
