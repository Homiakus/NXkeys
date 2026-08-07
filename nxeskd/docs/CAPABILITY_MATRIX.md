# Матрица возможностей NX ESKD

Статусы:

- **Implemented** — логика реализована и покрыта Core-тестами или статическими контрактами.
- **NX verification required** — реализация присутствует, но точные NX Open API и лицензии должны быть подтверждены в установленной NX 2512:6000.
- **Manual review** — автоматический результат допускается только после явной проверки конструктора.
- **Planned** — функция ещё не должна восприниматься как исполняемая.

| Возможность | Статус | Защита/проверка |
|---|---|---|
| Строгая JSON Schema профиля | Implemented | `ProfileValidator` выполняет bundled schema; неизвестные поля блокируются при `allowUnknownJsonProperties=false` |
| Миграции профиля | Implemented | Только явные миграции `0.9.x → 1.0.0`, с provenance и без скрытой перезаписи файла |
| Анализ модели до planning | Implemented | Immutable `ModelSnapshot`, bbox, units, Work/Display relation, assembly/PMI/sheet-metal indicators и datum planes |
| Model-aware applicability | Implemented | Неприменимые flat-pattern/PMI/section operations удаляются до NX mutation; production profile остаётся строгим |
| Auto scale | Implemented / Manual review | Рассчитывается по bbox и usable sheet area; окончательный масштаб требует проверки состава видов и аннотаций |
| Исполняемый operation DAG | Implemented | Нормализация setup→mutation→reconciliation→validation→output; duplicate/missing dependency/cycle блокируются |
| Preview фактического порядка | Implemented | Preview использует тот же топологический порядок, который получает NX runtime |
| Dry run | Implemented | Generate/Update маршрутизируются в Preview, mutation adapter не вызывается |
| Generate / Update preconditions | Implemented | Generate запрещён поверх текущего managed scope; Update требует managed scope |
| Ownership | Implemented | `profileId + jobId/scope + objectKind + logicalId`, read-back обязательных атрибутов |
| Duplicate managed IDs | Implemented | Registry блокирует выполнение до Undo |
| Reconciliation | Implemented / NX verification required | Строится по effective `DrawingPlan`; exact-owned stale objects удаляются только после Preview и confirmation |
| Stale balloons | Implemented / NX verification required | Сравниваются с текущими BOM positions; удаление подчиняется общей destructive policy |
| Builder lifecycle | Implemented | Object/command commits разделены; `Destroy` выполняется ровно одним владельцем |
| Undo / rollback | NX verification required | Postconditions внутри Undo; rollback и cleanup mark требуют station test |
| WorkPart protection | Implemented | Проверка перед и после каждой DAG-операции и перед save/export |
| Capability preflight | NX verification required | Collections/builders и доступные license-query API проверяются до Undo |
| Листы: create/update | NX verification required | Формат, ориентация, масштаб, номер, template fingerprint и postconditions |
| Явный target sheet для аннотаций | Implemented / NX verification required | Parts List, technical requirements и bend table активируют лист по role и записывают target marker |
| Виды: create/update | NX verification required | Source, parent, section plane, scale, hidden lines, placement и kind immutability |
| Проекционная раскладка | Implemented / NX verification required | Top/bottom сохраняют X родителя, left/right сохраняют Y; отсутствующий parent блокирует placement |
| Проверка alignment projected views | Implemented / NX verification required | После NX update отклонение от оси родителя формирует blocking diagnostic |
| Layout Solver | Implemented / NX verification required | Два прохода: оценочные и фактические bounds; collision или нарушенная проекция блокируют результат |
| Ручная позиция managed view | Implemented | Three-way marker: previous applied/current/config |
| Проверка достаточности видов | Implemented / Manual review | Нет views/base, ошибки parent/direction, flat-pattern requirements; один вид пространственной детали вызывает review |
| Автоматический выбор оптимального состава видов | Planned | Нужен feature/visibility/occlusion analyzer и критерии информационной достаточности |
| Основная надпись через part attributes | NX verification required | Required values блокируют rollback; direct TitleBlock cell API зависит от MR |
| Полный набор корпоративных стилей ЕСКД | Template-dependent | Runtime применяет только подтверждённые параметры; основная стилизация должна находиться в PRT template |
| PMI inheritance | NX verification required | Один детерминированный call, binding marker, ownership и deduplication |
| Автоматическая размерная схема по геометрии | Planned | При отсутствии PMI создаётся `PLAN_DIMENSION_SOURCE_MISSING`; ручная проверка обязательна |
| Flat Pattern | NX verification required / Manual review | Stationary face и X-edge обязательны; автоматический выбор помечается ManualReview |
| Parts List | NX verification required | BOM snapshot из component tree, roll-up, позиции, сортировка, обязательная запись строк |
| Balloons | NX verification required | Ассоциация с representative component и managed view обязательна |
| Technical requirements | NX verification required | Повторный Update не создаёт duplicate note; unresolved tokens проверяются; интеллектуальный вывод требований не заявлен |
| Bend table | NX verification required | Пустая таблица запрещена; требуются фактические строки bends; target sheet явный |
| Validation rules | Implemented / NX verification required | Rule registry исполняет `validation.checks`; неизвестные правила не считаются пройденными |
| Native `.prt` SaveAs | NX verification required | Разрешение переменных, `.prt` guard, postcondition path/non-empty и блокировка дальнейшего export при ошибке |
| PDF export | NX verification required | Temporary output, non-empty check, atomic publish |
| Flat Pattern DXF | NX verification required | Экспорт только exact managed Flat Pattern; whole-part fallback запрещён |
| Package manifest planning | Implemented | Уникальность ID/обозначений/выходов, files, dependencies, cycles и topological order |
| Package dry-run CLI | Implemented | `tools/NxEskd.PackagePlanner`, JSON output и exit code 0/2 |
| Package journal/resume | Implemented | Статус считается пригодным для resume только вместе с существующим непустым staging file |
| Package staged publication | Implemented | Все обязательные документы готовятся до публикации; backup/rollback защищает предыдущие файлы |
| Multi-WorkPart NX package adapter | Planned / Station required | Требуется безопасное открытие копий, профиль overlay, вызов команд, закрытие и обработка аварий NX |
| API inventory без WorkPart | Implemented | Session-level inventory NXOpen/NXOpen.UF |
| Core CI | Implemented | xUnit + smoke + package planner build + Configurator + JSON/PowerShell + закрытые DLL guard |
| Exact NX 2512:6000 build | Station required | `build.ps1` проверяет release/MR, ProductVersion, token и SHA-256 обеих NX DLL |
| Authenticode и release signing | Planned | Требуются сертификат организации и защищённый signing pipeline |
| Полностью автоматический NX station runner | Planned | Нельзя заявлять без подтверждённого способа запуска команд в вашей установке NX |

## Правило интерфейса

Настройка, не имеющая статуса **Implemented** или **NX verification required**, не должна отображаться как гарантированно исполняемая. Для `Planned` Configurator должен показывать статус и ссылку на требуемую реализацию, а Runtime — диагностическое сообщение вместо молчаливого игнорирования.

## Правило доказательности

`Implemented` для Core означает проверяемую логику без NX. Любая операция, создающая или изменяющая объект NX, остаётся `NX verification required` до прохождения fixture-сценария на точной NX 2512:6000.
