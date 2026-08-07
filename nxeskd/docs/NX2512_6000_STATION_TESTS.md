# Протокол испытаний на Siemens NX 2512:6000

Этот протокол является обязательным release gate. Успешная компиляция Core или наличие reflection aliases не заменяют запуск в целевой NX.

## 1. Подготовка станции

1. Зафиксировать полную версию NX и maintenance release.
2. Выполнить:

```powershell
.\scripts\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -ExpectedNxRelease 2512 `
  -ExpectedNxMaintenance 6000
```

3. Сохранить `build-info.json`, SHA-256 архива и API inventory.
4. Запретить работу с единственной копией производственной детали.
5. Каждый fixture запускать из отдельной копии в каталоге тестового прогона.

## 2. Обязательные fixtures

| ID | Fixture | Назначение |
|---|---|---|
| F01 | `empty_metric.prt` | Пустая метрическая деталь |
| F02 | `single_body_metric.prt` | Простая деталь с одним телом |
| F03 | `multi_body_metric.prt` | Многотельная деталь |
| F04 | `manual_drawing.prt` | Ручные листы, виды, размеры и notes |
| F05 | `managed_drawing.prt` | Результат успешного Generate |
| F06 | `assembly_basic.prt` | Сборка с повторяющимися прототипами |
| F07 | `assembly_suppressed.prt` | Suppressed/unloaded components |
| F08 | `pmi_groups.prt` | Model views и PMI groups |
| F09 | `sheet_metal_named_refs.prt` | Именованные stationary face и X-edge |
| F10 | `sheet_metal_automatic_refs.prt` | Нет именованных references; требуется ManualReview |
| F11 | `readonly_part.prt` | Read-only source |
| F12 | `Юникод путь с пробелами.prt` | Unicode и пробелы |

Имена fixtures являются контрактом тестового набора, а не утверждением, что они уже присутствуют в репозитории. Скрипт подготовки блокируется, если обязательный файл отсутствует.

## 3. Before/after inventory

Для каждого сценария сохранить:

- копию исходного `.prt`;
- API inventory;
- список листов, видов, PMI, notes, tables, parts lists, balloons и features;
- managed attributes каждого объекта;
- active WorkPart и DisplayPart;
- файл execution report;
- NX syslog при ошибке;
- копию результата после сохранения и повторного открытия.

## 4. Матрица сценариев

| № | Fixture | Действие | Ожидаемый результат | Rollback/повторный запуск |
|---:|---|---|---|---|
| 1 | F01 | Preview | NX-объекты не изменены | inventory before/after идентичен |
| 2 | F02 | Generate | Листы/виды/штамп созданы и имеют полный ownership key | второй Generate блокируется; Update разрешён |
| 3 | F05 | Update без изменений | Нет дубликатов; fingerprint стабилен | повторный Update идемпотентен |
| 4 | F05 | Удалить view из профиля, deletion=false | Объект сохранён, warning | NX не удаляет объект |
| 5 | F05 | То же, deletion=true без confirmation | Blocking error | полный rollback |
| 6 | F05 | То же с confirmation=true | Удаляется только exact managed view | ручные объекты остаются |
| 7 | F04 | Generate | Name collision/manual object не усыновляется | полный rollback |
| 8 | F05 | Создать duplicate AUTO_DWG_ID | Preflight блокирует до Undo | модель не изменена |
| 9 | F06 | Generate assembly drawing | BOM roll-up, стабильные позиции и balloons | Update не меняет позиции без причины |
| 10 | F07 | BOM с suppressed components | Поведение соответствует профилю | количество проверяется вручную |
| 11 | F08 | PMI inheritance | Одна связь source→target | повторный Update не создаёт PMI duplicate |
| 12 | F09 | Flat Pattern | Использованы named refs | DXF содержит только развертку |
| 13 | F10 | Flat Pattern | ManualReview об automatic refs | без review релиз не принимается |
| 14 | F11 | Save | `NX_SAVE_FAILED`, export прекращён | исходный файл не изменён |
| 15 | F12 | Generate/Save/PDF | Unicode-путь корректен | повторное открытие успешно |
| 16 | F02 | Исключение до первого commit | Нет созданных объектов | rollback подтверждён inventory |
| 17 | F02 | Исключение после нескольких commit | Частичные объекты отсутствуют | rollback и Undo mark cleanup |
| 18 | F02 | Ошибка postcondition | Save/export не выполняются | rollback |
| 19 | F02 | Смена WorkPart между этапами | Команда отменена | другая деталь не изменена |
| 20 | Любой | Отсутствующая лицензия | Ошибка до Undo либо точный station diagnostic | нет частичных изменений |
| 21 | F05 | Смена типа managed view | Recreate требуется явно | silent conversion запрещена |
| 22 | F05 | Ручное перемещение managed view | Позиция сохраняется при preserve=true | marker обновляется корректно |
| 23 | F05 | Пересекающиеся actual view bounds | Blocking layout diagnostic | результат не сохраняется |
| 24 | F09 | PDF + DXF export | Временные файлы опубликованы атомарно | нулевые/частичные файлы отсутствуют |

## 5. Критерий прохождения

Релиз допускается только если:

- все P0/P1-сценарии прошли;
- rollback подтверждён inventory, а не только флагом отчёта;
- повторный Update не создаёт новые NX objects без изменения профиля;
- ручные объекты не удалены и не получили ownership;
- PDF/DXF повторно открываются и содержат ожидаемые листы/геометрию;
- NX не завершилась аварийно;
- все ManualReview закрыты подписью проверяющего;
- `build-info.json` подтверждает NX 2512:6000 и хэши локальных NX assemblies.

## 6. Запрещённые формулировки до прохождения

До выполнения протокола нельзя утверждать:

- «совместимо с NX 2512:6000»;
- «полностью идемпотентно»;
- «rollback гарантирован»;
- «PMI/Parts List/Flat Pattern проверены»;
- «готово к промышленной эксплуатации».
