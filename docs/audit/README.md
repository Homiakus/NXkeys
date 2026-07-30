# Аудиторские материалы NXKeys

Каталог содержит два типа файлов.

## Исторические snapshots

Датированные документы `00-*` … `12-*`, traceability matrices, baseline reports и evidence JSON фиксируют состояние конкретного commit/даты. Они сохраняются для истории и не должны массово переписываться при каждом изменении проекта.

Исторический audit может содержать старые:

- profile/runtime schema;
- sequence policy version;
- counts generated rows/support commands;
- component status;
- выводы о CI.

При противоречии он не имеет приоритета над текущим кодом и канонической документацией.

## Generated audits

Файлы вроде:

```text
command-sequence-audit.md
command-sequence-audit.json
```

создаются `scripts/audit-command-sequences.mjs`. Их нельзя исправлять вручную. После изменения sequence policy, compiler, paths или intent catalog запустите:

```powershell
node .\scripts\audit-command-sequences.mjs
```

и проверьте diff.

Generated audit должен быть создан тем же source commit, который его публикует. Если source policy v7, а audit показывает v6, audit считается stale.

## Runtime evidence

Probe/evidence files, полученные на workstation NX, должны содержать:

- дату и source commit;
- NX build/localization;
- описание role/license scope;
- способ получения;
- очистку персональных, proprietary и production данных.

Не добавляйте proprietary DLL, license files, parts/drawings, credentials и закрытые corporate extensions.

## Приоритет источников

1. исполняемый код и validators;
2. канонические docs из `docs/README.md`;
3. generated output текущего commit;
4. historical snapshot.
