# Verification status — NX ESKD Drawing Automation

Дата обновления: 03.08.2026  
Версия исходников: **1.0.2-rc1**

## Итоговый статус

Проведён новый статический remediation pass по ранее зарегистрированным 98 проблемам и дополнительным дефектам. Предыдущие числа `32 исправлено / 6 частично / 60 не исправлено` относятся к версии `1.0.1-rc1` и больше не описывают текущий код.

Текущий пакет всё ещё **не является проверенным промышленным релизом**, потому что в доступной среде отсутствуют:

- установленная Siemens NX 2512:6000;
- фактические `NXOpen.dll`, `NXOpen.UF.dll`, `NXOpenUI.dll` этой станции;
- лицензии Drafting, PMI, Parts List и Sheet Metal;
- эталонные `.prt` fixtures;
- возможность подтвердить реальный Commit/Destroy, Undo/rollback, Save и экспорт внутри NX.

## Статически устранённые классы дефектов

- разделены reflection-команды и object factories;
- устранён `VoidResult` как ложный NXObject;
- установлен единый владелец Builder и single-destroy contract;
- модель анализируется до planning;
- добавлен immutable `ModelSnapshot` и auto-scale;
- добавлен operation DAG, topological ordering и cycle detection;
- Generate и Update имеют разные preconditions;
- ownership расширен до `profileId + jobId + objectKind + logicalId`;
- duplicate managed IDs блокируются до Undo;
- reconciliation удаляет только exact scoped objects и требует отдельного подтверждения;
- WorkPart проверяется до и после mutation stages;
- capability/license preflight выполняется до Undo;
- листы и виды синхронизируют требуемое состояние, а не только активируются/перемещаются;
- LayoutSolver подключён к Runtime и выполняет второй проход по actual bounds;
- PMI inheritance получил deterministic invocation, binding marker и deduplication;
- Flat Pattern требует stationary face и X direction;
- Parts List строится из component snapshot и не может быть пустой декоративной таблицей;
- balloons создаются с обязательной component/view association;
- пустая bend table запрещена;
- DXF whole-part fallback запрещён;
- PDF/DXF публикуются через temporary output и atomic move;
- JSON Schema реально исполняется;
- добавлены явные profile migrations и version gate;
- `validation.checks` исполняются через rule registry;
- Inventory работает без открытого WorkPart;
- добавлены Core smoke CI и xUnit contract tests;
- станционная сборка проверяет точный release/MR и согласованность NX assemblies;
- добавлены Authenticode/catalog/CMS release-signing процедуры;
- Configurator получил dirty tracking, atomic save, backup, draft recovery, typed editing, virtualization и переход по JSON-path.

Подробный текущий статус функций находится в `docs/CAPABILITY_MATRIX.md`.

## Обязательные проверки, которые нельзя подтвердить статически

| Gate | Что требуется | Статус |
|---|---|---|
| Core CI | Smoke + xUnit + JSON checks | Workflow добавлен; результат конкретного run должен быть проверен в GitHub Actions |
| Compile gate | Сборка solution против локальных DLL NX 2512:6000 | Требуется станция |
| API gate | Inventory фактических типов/методов/сигнатур | Требуется станция |
| Undo gate | Ошибка до/после нескольких commits и postconditions | Требуется станция |
| Builder gate | Object/command commits и single destroy на реальных builders | Требуется станция |
| Drafting gate | Sheets, views, sections, details, title block | Требуется станция |
| PMI gate | Inheritance, deduplication и repeat Update | Требуется лицензия/fixture |
| BOM gate | Parts List, stable positions и associative balloons | Требуется assembly fixture |
| Sheet Metal gate | Named/automatic refs, bend table и Flat Pattern DXF | Требуется лицензия/fixture |
| Save/export gate | Read-only, Unicode, PDF/DXF open verification | Требуется станция |
| Signing gate | Реальный certificate-backed signed package | Требуется code-signing certificate |

Полный протокол: `docs/NX2512_6000_STATION_TESTS.md`.

## Дополнительные ограничения

1. Reflection aliases по-прежнему должны быть подтверждены inventory конкретной maintenance release.
2. Detail/section boundary contracts, сложные stepped/revolved sections и часть PMI selection semantics требуют локального typed API mapping.
3. Автоматический выбор stationary face/X-edge считается ManualReview, а не бесспорным результатом.
4. Configurator защищает документ и типы scalar values, но сложные collection editors должны дополнительно проходить UX-тестирование на реальном профиле.
5. Поля профиля, не отмеченные как Implemented/NX verification required в capability matrix, не должны восприниматься как выполненные.

## Разрешённое применение RC

- только копии тестовых деталей;
- сначала API Inventory и Preview;
- независимая резервная копия исходного `.prt`;
- запрет автоматического сохранения единственного производственного файла;
- ручная проверка всех `ManualReview`;
- обязательное сравнение before/after inventory.

## Запрещённые утверждения

До успешного station protocol нельзя утверждать:

- «все 98 проблем подтверждённо исправлены в NX»;
- «других проблем нет»;
- «совместимость с NX 2512:6000 доказана»;
- «rollback гарантирован»;
- «пакет готов к промышленной эксплуатации».
