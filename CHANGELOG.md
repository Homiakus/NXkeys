# Changelog

Значимые изменения NXKeys фиксируются в этом файле. Проект пока не использует подтверждённую публичную схему версий releases, поэтому записи группируются по датам и commit-level milestones.

## Unreleased

### Documentation

- добавлена единая карта кодовой базы и аудит документации;
- добавлены developer, contribution, security, CLI и operations guides;
- разделены canonical, generated и historical документы;
- исправлены ссылки на config schema 6 и source sequence policy v7;
- добавлены `SA` Select All и `SN` Deselect All в каноническую документацию;
- описаны contract build Command Bridge и обязательные проверки на реальной NX workstation.

### Fixed

- universal selection normalization теперь сохраняет catalog traceability заменяемой команды;
- `Select All` может одновременно быть universal support action `SA` и одним из 885 selected intents;
- добавлен явный флаг `catalog_backed_support`, позволяющий сохранять `catalog_refs` без нарушения support frequency/path policy;
- main profile и command-tree validators проверяют нормализованную policy v7 и полное покрытие 885 intents.

### Known inconsistencies

- checked-in `docs/audit/command-sequence-audit.*` всё ещё отражает policy v6 и должен быть пересоздан текущим генератором;
- некоторые runtime error strings в `NX2512_HotkeyStudio/Program.cs` всё ещё упоминают schema v4, хотя `CurrentSchemaVersion` равен 6;
- фактическая доступность команд должна быть повторно проверена на целевой NX 2512 после regeneration.

## 2026-07-30

### Changed

- source sequence policy обновлена до v7;
- в универсальную selection policy добавлены Select All (`SA`) и Deselect All (`SN`);
- добавлен рекомендуемый цикл модулей Modeling → Assembly → Drafting → Manufacturing;
- в модель команды добавлены `path_locked` и `path_source` для будущей устойчивой кастомизации mnemonic paths.

## 2026-07-29

### Changed

- installer начал принимать generated profile schema до версии 6;
- главным runtime scope закреплён единый профиль K3–K5 из 885 намерений;
- source catalog сохранён как 1169 намерений K1–K5;
- усилены validators profile scope, installer compatibility и command sequence invariants;
- добавлена сборка Command Bridge против NXOpen contract stubs в CI;
- развёртывание переведено на managed package, manifest, health-check и rollback workflow.

## До 2026-07-29

### Architecture

- проект переведён на единый C#/.NET 8 контур;
- реализованы HotkeyStudio, Command Bridge, Control Center и Catalog Studio;
- общий IPC-контракт вынесен в `NXKeys.Protocol`;
- DFA/HFSM и guards вынесены в `NXKeys.StateMachines`;
- добавлена файловая очередь `pending → processing → completed|failed`;
- добавлены backup manifests, SHA-256 verification и managed launcher.

## Правила ведения

- Не добавляйте release number, которого нет в GitHub releases или утверждённом плане проекта.
- Generated profile timestamps не являются отдельным changelog событием.
- Изменение profile schema, protocol schema, sequence policy или deployment contract должно быть отражено здесь.
- Breaking change должен содержать migration и rollback notes.
