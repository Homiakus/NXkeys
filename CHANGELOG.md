# Changelog

Значимые изменения NXKeys фиксируются в этом файле. Проект пока не использует подтверждённую публичную схему версий releases, поэтому записи группируются по датам и commit-level milestones.

## Unreleased

### Documentation

- добавлена подробная пользовательская и эксплуатационная шпаргалка `docs/CHEATSHEET.md`;
- обновлены корневой README, оглавление документации, developer guide и contribution guide;
- документация разделена по аудиториям: пользователь, support/administrator, developer/reviewer;
- источники истины дополнены отдельным Sketch allocator;
- обязательный запуск `NX2512_HotkeyStudio.Tests` добавлен во все developer checklists;
- generated sequence audit переведён на текущую policy v7;
- аудит документации обновлён после внедрения Sketch intent taxonomy;
- добавлен аудит хрупкости, архитектуры NX-плагина и качества UI с реестром рисков, целевой архитектурой и поэтапным планом исправлений.

### Architecture audit

- подтверждено правильное общее разделение desktop companion и NX-loaded Command Bridge;
- выявлены P0-риски: неаутентифицированный file IPC, fail-open обработка неизвестного action, неполный selection fingerprint, silent profile schema coercion и неатомарное сохранение профиля;
- предложена целевая структура Contracts/Domain/Application/Infrastructure/Desktop/NxPlugin/NxOpenAdapter;
- определена модель bounded execution на NX UI thread и защищённый IPC с session capability, allowlist, nonce и context token;
- предложено объединение HotkeyStudio и Control Center в единый desktop shell, общий design system и доступный компактный HUD.

### UI

- HotkeyStudio и Control Center объединены в один canonical desktop shell;
- добавлены typed diagnostics, Leader/HUD settings и compatibility launcher;
- редактор профиля переведён на draft session с undo/redo, diff и atomic save;
- введена общая high-contrast-aware тема и compact HUD с десятью ближайшими действиями;
- добавлены Ctrl+Z, Ctrl+Y и Ctrl+S для работы с профилем.

### Architecture

- добавлены отдельные class libraries `NXKeys.Protocol` и `NXKeys.BridgeCore`;
- filesystem admission вынесен с NX UI thread в bounded background inbox;
- NX adapter исполняет не более одного admitted request за UI tick;
- single-instance scope привязан к local session и активному профилю;
- signal threads получили cancellation, а CapsLock сохраняет исходное состояние пользователя.

### Security

- IPC повышен до schema 4: ephemeral 256-bit launch capability и HMAC-SHA-256 для каждого request;
- добавлены client instance, nonce, monotonic sequence и replay protection;
- Bridge проверяет точный source process и профильный allowlist/digest до NX dispatch;
- unsigned requests и запуск вне managed launcher отклоняются fail-closed.

### Fixed

- protocol actions проверяются fail-closed; неизвестное действие больше не может попасть в обычный NX dispatch;
- profile schema проверяется до migration, а сохранение выполняется атомарно;
- IPC получил typed read errors, payload/queue limits и selection fingerprint против TOCTOU;
- universal selection normalization сохраняет catalog traceability заменяемой команды;
- `Select All` может одновременно быть universal support action `SA` и одним из selected intents;
- флаг `catalog_backed_support` позволяет сохранять `catalog_refs` без нарушения support frequency/path policy;
- main profile и command-tree validators проверяют нормализованную policy v7 и полное покрытие выбранных intents;
- Sketch больше не использует случайные сокращения и не уходит в чужие action roots при коллизиях;
- из Sketch-контекста исключены файловые, сборочные, материальные и другие посторонние команды.

### Known limitations

- текущий file IPC обеспечивает атомарность, но ещё не подтверждает отправителя session capability/HMAC и не ограничивает command ID профильным allowlist;
- selection revision не содержит полного fingerprint выбранных объектов;
- profile schema выше поддерживаемой версии должна быть переведена на fail-closed migration отдельным исправлением;
- фактическая доступность команд должна быть повторно проверена на целевой NX 2512 после изменения роли, лицензии, MenuScript или maintenance release;
- contract build не заменяет загрузку Bridge в реальный NX;
- historical audits могут содержать старые версии schema и policy и не являются текущими инструкциями.

## 2026-08-03

### Changed

- внедрена отдельная семантическая грамматика Sketch `действие → область → операция → вариант`;
- закреплены базовые пути `CGL`, `CGR`, `CGC`, `CGA`, `EGT`, `EGE`, `TGO`;
- варианты построения перенесены в prefix-free ветвь `CGV…`;
- Sketch разрешены пути длиной до пяти токенов независимо от K-частоты;
- legacy positional aliases удаляются, user-locked paths сохраняются;
- подтверждённое ядро Sketch сохраняется в runtime profile независимо от частотной фильтрации;
- добавлены регрессионные тесты и workflow `Sketch intent grammar`.

## 2026-07-30

### Changed

- source sequence policy обновлена до v7;
- в универсальную selection policy добавлены Select All (`SA`) и Deselect All (`SN`);
- добавлен рекомендуемый цикл модулей Modeling → Assembly → Drafting → Manufacturing;
- в модель команды добавлены `path_locked` и `path_source` для устойчивой кастомизации mnemonic paths.

### Documentation

- добавлена единая карта кодовой базы и аудит документации;
- добавлены developer, contribution, security, CLI и operations guides;
- разделены canonical, generated и historical документы;
- исправлены ссылки на config schema 6 и source sequence policy v7;
- добавлены `SA` Select All и `SN` Deselect All в каноническую документацию;
- описаны contract build Command Bridge и обязательные проверки на реальной NX workstation.

## 2026-07-29

### Changed

- installer начал принимать generated profile schema до версии 6;
- главным runtime scope закреплён единый профиль K3–K5;
- source catalog сохранён как полный каталог K1–K5;
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
- Изменение profile schema, protocol schema, sequence policy, Sketch grammar или deployment contract должно быть отражено здесь.
- Breaking change должен содержать migration и rollback notes.
