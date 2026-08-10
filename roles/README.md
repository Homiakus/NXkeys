# Экспортированные роли Siemens NX 2512

Каталог `roles/` предназначен только для проверенных `.mtx`, экспортированных штатными средствами целевой Siemens NX 2512 и разрешённых к хранению/распространению организацией.

NXKeys runtime не зависит от расположения кнопок на ribbon роли: dispatch использует exact `BUTTON ID`, а active operation set выбирается по application/module context. Но роль влияет на фактическую availability/sensitivity и поэтому является частью integration environment.

## Что может изменять роль

- доступные applications по лицензии;
- visibility/sensitivity UI commands;
- application/module mapping;
- selection defaults;
- corporate MenuScript/ribbon extensions;
- localized labels;
- placement системных элементов.

Роль не должна использоваться как единственный источник command IDs. Для target environment выполняйте Catalog Studio export.

## Правила NXKeys

- NXKeys не редактирует бинарный `.mtx` как source configuration.
- Роль создаётся и экспортируется средствами NX.
- Копируется только целый проверенный файл.
- `role_deployment.enabled` по умолчанию false, если соответствующий deployment layer используется.
- Deployment не должен перезаписывать неизвестную роль без backup/явного плана.
- Несовпадение роли не должно приводить к fallback на command другого module.
- После смены роли нужен новый Catalog Studio export и повторная проверка current v8 adapters.

## Рекомендуемое имя

```text
NX_Adaptive_Modules_2512.6000.mtx
```

Это соглашение проекта, а не требование Siemens.

## Проверка перед добавлением

1. Подтвердите exact NX build и localization.
2. Подтвердите право распространять файл.
3. Удалите персональные/corporate-sensitive элементы, если это возможно без повреждения роли.
4. Проверьте загрузку роли средствами NX.
5. Экспортируйте Catalog Studio.
6. Сверьте важные `adapter.value` из `config/nx2512-v8-profile.json` с `06_ui_commands_buttons.csv`.
7. Проверьте application mapping, особенно Sketch / Sheet Metal / Surface / Simulation.
8. Проверьте лицензированные modules.
9. Проверьте selection type filters и Selection Intent `0…4`.
10. Проверяйте destructive commands только на копии детали.

Legacy K3–K5 compiler/resolution report можно запускать отдельно для catalog analysis, но это не default runtime profile flow.

## Runtime-проверка

После включения роли:

1. запустите NX через managed NXKeys launcher;
2. откройте доступные applications;
3. проверьте `context.json` application/module и security state;
4. убедитесь, что HUD выбирает правильный runtime module;
5. проверьте безопасные commands;
6. проверьте Leader type filters `S→…`;
7. проверьте module switches `G→…` для лицензированных applications;
8. проверьте Selection Intent `0…4` в реальном collector;
9. отдельно проверьте Sketch `K→C` и Sheet Metal `UG_APP_SBSM` mapping, если они используются;
10. destructive commands тестируйте отдельно на test data.

## Конфиденциальность

`.mtx` может раскрывать корпоративную настройку рабочего места. Не публикуйте файл без проверки прав.

Не добавляйте рядом:

- license data;
- user profile directories;
- machine/network paths;
- proprietary extensions;
- production parts;
- screenshots с чувствительными project names;
- credentials/session secrets.

## Compatibility

Роль из другой версии, maintenance pack, localization или license set требует повторной проверки. Совпадение filename не доказывает compatibility.

## Восстановление

Перед deployment роли сохраните исходный target file и способ возврата к стандартной роли NX. Если role change нарушает module detection:

1. отключите role deployment;
2. восстановите исходную роль;
3. полностью перезапустите NX;
4. повторите context/Catalog Studio checks;
5. не исправляйте проблему заменой IDs на предположительные.

Current runtime contract: [`docs/RUNTIME_V8.md`](../docs/RUNTIME_V8.md).
