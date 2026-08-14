# Матрица возможностей NX ESKD

Статусы верификации:

- **`implemented`** — логика полностью реализована в кодовой базе.
- **`ci_verified`** — функциональность верифицирована модульными, архитектурными и регрессионными тестами в CI без NX.
- **`nx_verified`** — функциональность проверена на рабочей станции Siemens NX 2512.6000 со всеми лицензиями.
- **`target_only`** — целевая архитектурная функция, требующая дальнейшей реализации или внешних лицензий.

## Основные режимы backend (5 режимов)

| Backend-режим | Точка входа в UI | Статус | Архитектурная гарантия |
|---|---|---|---|
| `Preview` | Workflow экран 2 / CLI / Protocol | `ci_verified` | Построение DAG и Layout без мутации NX модели |
| `Validate` | Workflow экран 3 / Preflight / Protocol | `ci_verified` | Проверка схемы, preconditions, правил и топологии |
| `Inventory` | Диагностика / Protocol | `ci_verified` | Инвентаризация NX API без мутации и без WorkPart |
| `Generate` | Workflow экран 3 (при отсутствии managed scope) | `ci_verified` / `target_only` (NX) | Запрещён поверх существующего scope; требует Undo mark |
| `Update` | Workflow экран 3 (при наличии managed scope) | `ci_verified` / `target_only` (NX) | Требует явного scope; stale reconciliation только exact-owned |

## Миграция маршрутов (One-Route Integration)

| Старый маршрут / кнопка | Новый маршрут | Новая capability | Статус |
|---|---|---|---|
| `E → C` (Control Center) | `CapsLock → E` | `nxeskd.open_workflow` | `ci_verified` |
| `E → G` (Generate) | `CapsLock → E` (шаг 3) | `nxeskd.generate` | `ci_verified` |
| `E → U` (Update) | `CapsLock → E` (шаг 3) | `nxeskd.update` | `ci_verified` |
| `E → P` (Preview) | `CapsLock → E` (шаг 2) | `nxeskd.preview` | `ci_verified` |
| `E → V` (Validate) | `CapsLock → E` (шаг 3) | `nxeskd.validate` | `ci_verified` |
| `E → I` (Inventory) | `CapsLock → E` (диагностика) | `nxeskd.inventory` | `ci_verified` |

## Матрица предметных возможностей

| Возможность | Статус | Защита и проверка |
|---|---|---|
| Строгая JSON Schema профиля | `ci_verified` | `ProfileValidator` проверяет bundled schema; неизвестные поля блокируются |
| Идемпотентные миграции профиля | `ci_verified` | Явные миграции `0.9.x → 1.0.0` с provenance и backup |
| Анализ модели до planning | `ci_verified` | Immutable `ModelSnapshot`, bbox, units, Work/Display relation |
| Model-aware applicability | `ci_verified` | Неприменимые операции удаляются до NX mutation |
| Auto scale | `ci_verified` | Расчёт по bbox и usable sheet area |
| Исполняемый operation DAG | `ci_verified` | Топологическая сортировка; проверка циклов и зависимостей |
| Dry run / Preview | `ci_verified` | Mutation adapter не вызывается, детерминированный preview hash |
| Generate / Update preconditions | `ci_verified` | Раздельные контракты; Generate запрещён поверх scope, Update требует scope |
| Managed Ownership | `ci_verified` | Составной ключ `profileId + jobId + objectKind + logicalId` |
| Duplicate managed IDs | `ci_verified` | Registry блокирует выполнение до Undo |
| Reconciliation | `ci_verified` | Exact-owned stale objects удаляются только после Preview и confirmation |
| Stale balloons | `ci_verified` | Сверка с текущими BOM positions |
| Builder lifecycle | `ci_verified` | Single-commit, single-destroy в `finally` |
| Undo / rollback | `implemented` / `target_only` (NX) | Postconditions внутри Undo; rollback при исключении |
| WorkPart protection | `ci_verified` | Проверка перед и после каждой операции и перед сохранением |
| Capability preflight | `implemented` / `target_only` (NX) | Проверка лицензий и доступности builder до Undo |
| Листы: create/update | `implemented` / `target_only` (NX) | Формат, ориентация, масштаб, номер, postconditions |
| Явный target sheet | `ci_verified` | Активация листа по роли с маркером target |
| Виды: create/update | `implemented` / `target_only` (NX) | Base, projected, section, detail, flat_pattern |
| Layout Solver | `ci_verified` | Оценочные и фактические bounds, коллизии |
| PMI inheritance | `implemented` / `target_only` (NX) | Binding marker, ownership и deduplication |
| Flat Pattern | `implemented` / `target_only` (NX) | Stationary face и X direction |
| Parts List | `implemented` / `target_only` (NX) | Snapshot дерева компонентов, roll-up, позиции |
| Balloons | `implemented` / `target_only` (NX) | Ассоциация с representative component и managed view |
| Technical requirements | `implemented` / `target_only` (NX) | Unresolved tokens check, дедупликация |
| Bend table | `implemented` / `target_only` (NX) | Запрет пустой таблицы, валидация строк |
| Validation rules | `ci_verified` | Rule registry исполняет `validation.checks` |
| Native `.prt` SaveAs | `implemented` / `target_only` (NX) | `.prt` guard, непустой файл, atomic publish |
| PDF export | `implemented` / `target_only` (NX) | Temporary output, non-empty check, atomic move |
| Flat Pattern DXF | `implemented` / `target_only` (NX) | Экспорт только exact managed Flat Pattern |
| Package manifest planning | `ci_verified` | Уникальность ID, topological order, headless CLI |
| API inventory без WorkPart | `ci_verified` | Session-level inventory NXOpen/NXOpen.UF |
