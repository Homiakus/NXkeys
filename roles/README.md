# Экспортированные роли Siemens NX 2512

## Назначение

Каталог `roles/` предназначен только для проверенных `.mtx`, экспортированных штатными средствами целевой Siemens NX 2512 и разрешённых к хранению/распространению организацией.

NXKeys runtime не зависит от расположения кнопок на ribbon роли: dispatch использует exact `BUTTON ID`, а active command set выбирается по application/module context. Однако роль влияет на фактическую доступность команд и поэтому является частью integration environment.

## Что может изменять роль

- доступные applications по лицензии;
- visibility и sensitivity UI commands;
- application/module IDs;
- selection defaults;
- corporate ribbon/MenuScript extensions;
- локализованные labels;
- placement системных элементов.

Роль не должна использоваться как единственный источник command IDs. Для target environment выполняйте Catalog Studio export.

## Правила NXKeys

- NXKeys не редактирует бинарный `.mtx`.
- Роль создаётся и экспортируется средствами NX.
- Копируется только целый проверенный файл.
- `role_deployment.enabled` по умолчанию false.
- `source_mtx` и `target_directory` задаются явно.
- Deployment не должен перезаписывать неизвестную роль без backup/явного плана.
- Несовпадение роли не должно приводить к fallback на команду другого module.
- После смены роли требуется новый Catalog Studio export и profile compilation.

## Рекомендуемое имя

```text
NX_Adaptive_Modules_2512.6000.mtx
```

Имя является соглашением проекта, а не требованием Siemens.

## Проверка перед добавлением

1. Подтвердите NX build и локализацию.
2. Подтвердите право распространять файл.
3. Удалите персональные и corporate-sensitive элементы, если это возможно без повреждения роли.
4. Проверьте загрузку роли средствами NX.
5. Экспортируйте Catalog Studio.
6. Пересоберите main profile.
7. Изучите resolution delta.
8. Проверьте все лицензированные modules.
9. Проверьте selection и module switches.
10. Проверьте critical/destructive commands на копии детали.

## Runtime-проверка

После включения роли:

1. запустите NX через managed launcher;
2. откройте доступные applications;
3. проверьте `context.json` application/module;
4. убедитесь, что HUD выбирает правильный module;
5. проверьте safe read-only commands;
6. проверьте `SB…SN`;
7. проверьте `G*` для лицензированных applications;
8. destructive commands тестируйте отдельно на test data.

## Конфиденциальность

`.mtx` может содержать сведения о корпоративной настройке рабочего места. Не публикуйте файл без проверки прав.

Не добавляйте рядом:

- license data;
- user profile directories;
- machine paths;
- proprietary extensions;
- production parts;
- screenshots с чувствительными project names.

## Compatibility

Роль из другой версии, maintenance pack, localization или набора лицензий требует повторной проверки. Совпадение filename не доказывает compatibility.

## Восстановление

Перед deployment роли сохраните исходный target file и способ возврата к стандартной роли NX. Если role change нарушает module detection:

1. отключите `role_deployment`;
2. восстановите исходную роль;
3. перезапустите NX;
4. повторите context/Catalog Studio checks;
5. не исправляйте проблему заменой IDs на предположительные.
