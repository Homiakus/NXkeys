# Generated documentation

Файлы в этом каталоге создаются profile toolchain и не редактируются вручную.

Основной output:

```text
main-profile-resolution.md
```

Генерация:

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -CompileOnly
```

или прямым `scripts/compile-main-command-map.mjs`.

## Требования к commit

- generated file создан текущим source compiler;
- source commit и target catalog известны reviewer;
- `selected_frequencies` равны K3/K4/K5;
- `selected_intents` равно 885;
- sequence policy соответствует `scripts/sequence-policy.mjs`;
- unexpected resolution deltas объяснены;
- machine paths, user names и proprietary data очищены;
- ручные исправления Markdown не используются вместо исправления generator.

## Интерпретация resolution

- `existing` — exact ID из bootstrap;
- `resolved` — confident target catalog match;
- `ambiguous` — disabled;
- `unresolved` — disabled.

Resolution report является evidence конкретной generation, а не универсальной гарантией для всех NX workstations.
