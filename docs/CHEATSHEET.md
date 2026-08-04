# Подробная шпаргалка NXKeys

Эта шпаргалка предназначена для ежедневной работы с NXKeys, первоначальной настройки, диагностики и безопасного изменения профиля. Она дополняет, но не заменяет подробные документы:

- [README проекта](../README.md);
- [установку и обновление](INSTALLATION.md);
- [мнемонический язык](MNEMONIC_COMMAND_LANGUAGE.md);
- [язык намерений Sketch](SKETCH_INTENT_LANGUAGE.md);
- [CLI](CLI.md);
- [эксплуатационный runbook](OPERATIONS.md);
- [диагностику](TROUBLESHOOTING.md).

> NXKeys не является продуктом Siemens. Команда считается реально доступной только после проверки её точного `BUTTON ID` на целевой установке Siemens NX / Designcenter NX 2512 с нужной ролью и лицензией.

---

## 1. Модель работы за 30 секунд

NXKeys — контекстный клавиатурный слой. Пользователь не запоминает сотни независимых сочетаний, а вводит смысловой путь:

```text
CapsLock → действие → объект/область → операция → вариант
```

Примеры:

```text
CapsLock → C → E        Create → Extrude
CapsLock → S → F        Select → Face
CapsLock → S → A        Select All
CapsLock → G → D        Go → Drafting
CapsLock → C → L    Sketch: Create → Line
```

Главные правила:

1. Активный модуль NX определяется автоматически.
2. Пользователь не вводит префикс модуля.
3. Одинаковый путь может означать разные контекстные команды в разных модулях.
4. `S*` зарезервирован для универсального выбора.
5. `G*` зарезервирован для переходов между приложениями NX (например, `G S` для перехода в эскиз).
6. Неоднозначные и неразрешённые команды видимы, но отключены.
7. Sketch использует эффективную 2-токенную грамматику для частых команд (и до пяти токенов для вариантов).

---

## 2. Управление Leader HUD

| Клавиша | Действие |
|---|---|
| `CapsLock` | открыть Leader в текущем контексте; повторное нажатие закрывает его |
| двойной `CapsLock` | открыть sticky-режим, если он разрешён настройкой |
| буквы и цифры | вводить токены пути |
| `Space` | перейти к поиску команд текущего модуля |
| `Enter` | подтвердить путь или запустить первый результат поиска |
| `Backspace` | удалить последний токен; на корневом уровне закрыть Leader |
| `Esc` | отменить ввод и закрыть Leader |
| `Tab` | следующий доступный модуль |
| `Shift+Tab` | предыдущий доступный модуль |

### Когда использовать путь, а когда поиск

Используйте мнемонический путь, когда команда уже знакома и применяется регулярно. Используйте `Space` и поиск, когда:

- не помните путь;
- команда редкая;
- хотите проверить, доступна ли команда в текущем модуле;
- команда имеет статус `ambiguous` или `unresolved`;
- после изменения профиля нужно проверить фактическое разрешение команды.

---

## 3. Корни действий

| Токен | Мнемоника | Смысл |
|---|---|---|
| `C` | Create | создать или добавить объект |
| `E` | Edit | изменить существующий объект |
| `T` | Transform | переместить, отразить, повернуть или размножить |
| `X` | Remove | удалить, убрать или подавить |
| `P` | Process | выполнить вычисление, генерацию или технологический процесс |
| `I` | Inspect | измерить, проверить или проанализировать |
| `V` | View | изменить отображение, ориентацию или представление |
| `S` | Select | выбор и фильтры выбора |
| `A` | Annotate | размер, PMI, символ, текст или примечание |
| `M` | Manage | слои, материалы, библиотеки, навигаторы и управление |
| `F` | File / Finish | файловая операция или завершение режима |
| `G` | Go | переход в другое приложение NX |
| `U` | Utilities | служебная функция или настройка |
| `H` | Help | справка, поиск или диагностика |

### Как придумывать путь

Правильный путь отвечает на вопросы в таком порядке:

```text
Что сделать? → С чем / Какая операция? → Какой вариант?
```

Пример для линии в Sketch:

```text
Create → Line
C      → L
```

Путь не должен зависеть от случайного положения клавиши или от порядка команды в меню.

---

## 4. Прямые системные сочетания

Эти сочетания работают вне Leader и сохраняют привычное поведение:

| Сочетание | Действие | `BUTTON ID` |
|---|---|---|
| `Ctrl+N` | новый файл | `UG_FILE_NEW` |
| `Ctrl+O` | открыть | `UG_FILE_OPEN` |
| `Ctrl+S` | сохранить | `UG_FILE_SAVE_PART` |
| `Ctrl+Shift+S` | сохранить как | `UG_FILE_SAVE_AS` |
| `Ctrl+Z` | отменить | `UG_EDIT_UNDO` |
| `Ctrl+Y` | повторить | `UG_EDIT_REDO` |
| `Ctrl+X` | вырезать | `UG_EDIT_CUT` |
| `Ctrl+C` | копировать | `UG_EDIT_COPY` |
| `Ctrl+V` | вставить | `UG_EDIT_PASTE` |
| `Delete` | удалить выбранное | `UG_EDIT_DELETE` |
| `Ctrl+F` | вписать геометрию в окно | `UG_VIEW_FIT` |
| `F5` | обновить отображение | `UG_VIEW_REFRESH` |

Удаление и другие destructive-команды могут требовать подтверждения NXKeys и/или Siemens NX.

---

## 5. Универсальный выбор

| Путь после `CapsLock` | Действие |
|---|---|
| `S → B` | выбирать тела |
| `S → F` | выбирать грани |
| `S → E` | выбирать рёбра |
| `S → T` | выбирать элементы построения |
| `S → C` | выбирать компоненты сборки |
| `S → U` | выбирать кривые |
| `S → D` | выбирать базовые объекты: плоскости, оси и подобные объекты |
| `S → R` | сбросить фильтр выбора |
| `S → A` | выбрать всё доступное в текущем контексте |
| `S → N` | снять текущий выбор |

### Быстрые сценарии

```text
CapsLock → S → F    включить выбор граней
CapsLock → S → E    переключиться на рёбра
CapsLock → S → R    сбросить фильтр
CapsLock → S → N    снять выбор
```

Если команда не принимает выбранный объект, сначала проверьте активный фильтр и текущий модуль.

---

## 6. Переходы между приложениями NX

| Путь | Приложение |
|---|---|
| `G → S` | Создать эскиз / Переход в Sketch |
| `G → A` | Сборка |
| `G → C` | Обработка / CAM |
| `G → D` | Чертёж |
| `G → H` | Листовой металл |
| `G → L` | Библиотека повторного использования |
| `G → M` | Моделирование |
| `G → N` | Расчёты / Simulation |
| `G → O` | Проектирование пресс-форм |
| `G → P` | PMI и аннотации модели |
| `G → R` | Трассировка |
| `G → U` | Поверхностное моделирование |
| `G → V` | Просмотр и анализ |

Переход на текущее приложение не добавляется. В контекстах `sketch` и `selection_object` ветвь переходов не дублируется.

---

## 7. Sketch: основная грамматика

Sketch строится по оптимальной prefix-free схеме:

```text
действие → операция → вариант
```

### Семейства Sketch

| Семейство | Назначение |
|---|---|
| `C → …` | создать геометрию |
| `E → …` | редактировать геометрию |
| `T → …` | преобразовать геометрию |
| `F → S` / `F` | завершить эскиз (Finish Sketch) |
| `C → K → …` | создать геометрическое ограничение |
| `A → D → …` | создать размер |
| `I → S → …` | проверить эскиз |

### Базовые команды Sketch

| Путь | Команда |
|---|---|
| `C → L` | линия |
| `C → R` | прямоугольник |
| `C → C` | окружность |
| `C → A` | дуга |
| `E → T` | обрезать |
| `E → E` | удлинить |
| `E → F` | скруглить |
| `E → H` | фаска |
| `T → O` | смещение кривой |
| `F → S` / `F` | закончить эскиз (Finish Sketch) |
| `A → D → R` | быстрый размер |
| `I → S → C` | Sketch Checker |

### Варианты построения

Варианты вынесены в отдельную prefix-free ветвь:

```text
C → V → тип объекта → вариант
```

Подтверждённые примеры:

| Путь | Вариант |
|---|---|
| `C → V → L → 2` | линия по двум точкам |
| `C → V → L → M` | линия от середины |
| `C → V → R → C` | прямоугольник из центра |
| `C → V → C → 3` | окружность по трём точкам |
| `C → V → A → C` | дуга из центра |

### Ограничения Sketch

Ограничения находятся в ветви `C → K`. Конкретные конечные токены показываются в HUD и интерактивной карте для текущего скомпилированного профиля. Ветка предназначена для совпадения, касательности, параллельности, перпендикулярности, горизонтальности, вертикальности и других подтверждённых ограничений.

Не назначайте ограничениям случайные корни только ради сокращения пути.

### Размеры Sketch

Размеры находятся в ветви `A → D`. Быстрый размер имеет путь `A → D → R`. Остальные подтверждённые типы размеров распределяются внутри этой же ветви.

### Что запрещено в Sketch

- сокращать смысловой путь до случайных `C → L`, `K → C`, `D → Q`;
- переносить команду в чужой корень из-за коллизии;
- добавлять глобальные файловые команды в дерево Sketch;
- дублировать навигатор сборки, материалы, сшивку поверхностей или переходы между приложениями;
- включать команду без подтверждённого точного `BUTTON ID`;
- восстанавливать старые позиционные алиасы `W/E/D/C/X/Z/A/Q`;
- изменять пользовательский `path_locked: true` без явного решения пользователя.

### Почему Sketch допускает пять токенов

Для обычного каталога частота влияет на целевую длину пути. Для Sketch смысловая и prefix-free структура важнее формального сокращения. Поэтому путь варианта может содержать до пяти токенов независимо от K-частоты.

---

## 8. Типовые рабочие сценарии

### Создать Extrude в Modeling

```text
1. Убедитесь, что активен Modeling.
2. При необходимости выберите эскиз или область.
3. CapsLock → C → E.
4. Заполните диалог NX.
```

### Создать линию в Sketch

```text
1. Войдите в редактирование Sketch.
2. CapsLock → C → G → L.
3. Укажите точки.
4. Завершите команду средствами NX.
```

### Обрезать геометрию в Sketch

```text
1. CapsLock → E → G → T.
2. Укажите удаляемый участок.
3. Проверьте, что результат не нарушил нужные ограничения.
```

### Создать смещённую кривую

```text
1. Выберите исходную кривую или активируйте подходящий фильтр.
2. CapsLock → T → G → O.
3. Задайте сторону и расстояние в диалоге NX.
```

### Перейти в Drafting

```text
CapsLock → G → D
```

После перехода дождитесь обновления контекста Bridge перед вводом следующей команды.

### Найти неизвестную команду

```text
1. CapsLock.
2. Space.
3. Введите часть русского или английского названия.
4. Проверьте модуль, путь и статус команды.
5. Enter запускает первый результат только после осознанной проверки.
```

### Команда видна, но отключена

Проверьте по порядку:

1. статус `ambiguous` или `unresolved`;
2. наличие точного `BUTTON ID`;
3. актуальность Catalog Studio export;
4. активное приложение и модуль;
5. требуемый selection/context guard;
6. доступность команды в установленной роли и лицензии NX.

Не включайте такую команду вручную только для увеличения процента покрытия.

---

## 9. Профили и источники истины

| Область | Источник истины |
|---|---|
| состав и частоты K1–K5 | `config/full-command-map/` |
| bootstrap, safety и deployment defaults | `config/nx2512-pro-hybrid.json` |
| скомпилированный основной профиль | `config/nx2512-pro-main.generated.json` |
| универсальная sequence policy | `scripts/sequence-policy.mjs` |
| runtime-назначение путей | `NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs` и Sketch partial |
| profile schema и migration | `NX2512_HotkeyStudio/Models/ConfigRuntimeV5.cs` |
| поля команд | `NX2512_HotkeyStudio/Models/ModuleConfigTypesV5.cs` |
| IPC schema | `NXKeys.Protocol/NxProtocol.cs` |
| guards и state machines | `config/nx2512-state-machines.json`, `NXKeys.StateMachines/` |
| фактические команды NX | `06_ui_commands_buttons.csv` целевой установки |
| установка | `install-nxkeys.ps1` |

### Термины

| Термин | Значение |
|---|---|
| bootstrap profile | исходный `config/nx2512-pro-hybrid.json` с safety/deployment и подтверждённым ядром |
| main generated profile | рабочий K3–K5 профиль после компиляции |
| installed main profile | установленный рабочий профиль с compatibility-именем `nx2512-pro-hybrid.json` |
| full intent catalog | исходный каталог K1–K5; не равен runtime-профилю |
| generated document | файл, созданный скриптом; вручную не редактируется |
| historical audit | снимок состояния старого commit; не текущая инструкция |
| `existing` | ID уже подтверждён исходным профилем |
| `resolved` | ID разрешён компилятором по каталогу/доказательствам |
| `ambiguous` | найдено несколько недостаточно различимых кандидатов |
| `unresolved` | точный ID не найден |

---

## 10. Компиляция профиля

Рабочая директория — корень репозитория.

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -CompileOnly
```

Результаты:

```text
config/nx2512-pro-main.generated.json
docs/generated/main-profile-resolution.md
```

После компиляции проверьте:

- выбранный scope `K3`, `K4`, `K5`;
- ожидаемое число selected intents;
- количество `existing`, `resolved`, `ambiguous`, `unresolved`;
- отсутствие включённых команд без точного ID;
- отсутствие чужих команд в контексте Sketch;
- prefix-free пути и алиасы;
- diff generated-файлов.

Не редактируйте generated profile и resolution report вручную.

---

## 11. Установка и обновление

### Рекомендуемая установка Siemens NX

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

### Designcenter NX

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -NxRoot "C:\Program Files\Siemens\DesigncenterNX2512" `
  -Clean
```

### Установленные пути

```text
Managed package:
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000

Backups:
%LOCALAPPDATA%\NXKeys\backups

Launcher:
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\launch-nx2512-with-nxkeys.cmd
```

### Перед обновлением

1. Сохраните рабочие детали.
2. Закройте NX и HotkeyStudio.
3. Обновите исходники.
4. Повторно экспортируйте Catalog Studio после изменения версии NX, роли, лицензии или MenuScript.
5. Выполните установку с `-Clean`.
6. Проверьте `validate`, `health`, затем `bridge-status` после запуска NX.
7. Не удаляйте предыдущий backup до завершения приёмки.

### Рискованные параметры

| Параметр | Когда допустим | Риск |
|---|---|---|
| `-NoBuild` | только с проверенными согласованными artifacts | можно установить старую или несогласованную сборку |
| `-AllowRunningNX` | только когда Bridge не обновляется | уже загруженная DLL не перезагрузится |
| `-NoGlobalDuplication` | осознанное изменение UX профиля | меняет набор строк в модулях |
| `-Clean` | штатное обновление после сохранения backup | очищает build outputs и управляемый package |

---

## 12. CLI установленного пакета

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
$studio = "$root\NX2512_HotkeyStudio.exe"
$config = "$root\nx2512-pro-hybrid.json"
```

| Команда | Назначение | Пример |
|---|---|---|
| `validate` | загрузить, мигрировать и проверить профиль | `& $studio validate --config $config` |
| `health` | проверить package, manifest, hashes, custom dirs и Bridge | `& $studio health --config $config` |
| `bridge-status` | показать состояние контекста и файловой очереди | `& $studio bridge-status --config $config` |
| `scan` | сканировать NX roots, роли, MenuScript и каталоги | `& $studio scan --config $config --json` |
| `catalog` | искать команду в каталоге | `& $studio catalog --config $config --query "extrude"` |
| `plan` | построить deployment plan | `& $studio plan --config $config` |
| `apply` | применить deployment plan | `& $studio apply --config $config --yes` |
| `launch` | запустить NX через runtime service | `& $studio launch --config $config` |
| `leader` | запустить Leader Engine в foreground | `& $studio leader --config $config` |
| `backups` | показать backup manifests | `& $studio backups --config $config` |
| `restore` | восстановить backup | `& $studio restore --config $config` |
| `icons` | очистить/обновить icon cache | `& $studio icons --config $config` |

`validate` не доказывает доступность команды в NX. `health` при закрытой NX может показывать Bridge как offline — это ожидаемо.

---

## 13. Быстрая диагностика

### Leader не открывается

1. Убедитесь, что HotkeyStudio запущен.
2. Проверьте single-instance режим: второй процесс обычно только сигнализирует первому.
3. Выполните `health`.
4. Проверьте профиль командой `validate`.
5. Перезапустите HotkeyStudio после изменения hook/профиля.

### Контекст не обновляется

1. Запускайте NX через managed launcher.
2. Выполните `bridge-status`.
3. Проверьте свежесть `context.json`.
4. Убедитесь, что Bridge загружен текущей NX.
5. После обновления Bridge полностью перезапустите NX.

### Команда вызывает не то действие

1. Немедленно отмените действие средствами NX, если это безопасно.
2. Запишите путь, активный модуль и фактический `BUTTON ID`.
3. Проверьте Catalog Studio export текущей установки.
4. Не исправляйте проблему только изменением русского названия.
5. Исправляйте mapping и добавляйте регрессионный тест.

### Команда не исполняется

1. Проверьте `enabled` и resolution status.
2. Проверьте active application/module.
3. Проверьте selection guard.
4. Проверьте modal state NX.
5. Проверьте свежесть Bridge context.
6. Проверьте очередь `pending/processing/completed/failed`.

### После установки health-check не прошёл

1. Не запускайте рабочую NX.
2. Сохраните вывод installer.
3. Выполните `backups`.
4. Проверьте manifest и hashes.
5. Выполните обычный `restore`.
6. `--force` используйте только после ручного анализа причины отказа.

---

## 14. Разработка и обязательные проверки

### Проверки профиля

```powershell
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs
node .\scripts\audit-command-sequences.mjs
```

`audit-command-sequences.mjs` изменяет generated audit; обязательно проверьте diff.

### Тесты

```powershell
dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release

dotnet run --project .\NX2512_HotkeyStudio.Tests\NX2512_HotkeyStudio.Tests.csproj -c Release
```

### Сборка

```powershell
dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj `
  -c Release -p:Platform=x64 --nologo

dotnet build .\NX2512_ControlCenter\NX2512_ControlCenter.csproj `
  -c Release -p:Platform=x64 --nologo
```

Command Bridge без реальных NXOpen DLL собирается только против contract stubs. Production-проверка требует целевую NX 2512.

### После изменения Sketch

Обязательно проверьте:

- базовые пути `CGL`, `CGR`, `CGC`, `CGA`, `EGT`, `EGE`, `TGO`;
- ветвь вариантов `CGV…`;
- отсутствие legacy positional aliases;
- сохранение user-locked пути;
- отсутствие перехода в случайные корни при коллизии;
- отсутствие файловых, сборочных и иных чужих команд в Sketch;
- отсутствие enabled-команд без точного ID;
- prefix-free инвариант.

---

## 15. Как безопасно изменить команду

### Добавить намерение

1. Добавьте запись в `config/full-command-map/`.
2. Задайте стабильный `intent_id`.
3. Укажите K-частоту, модуль, русское и английское название.
4. Добавьте доказательство точного `BUTTON ID` либо оставьте команду disabled.
5. Задайте смысловой path hint.
6. Запустите full/main validators и sequence audit.
7. Пересоберите generated profile.
8. Обновите документацию, если меняется пользовательский язык.

### Изменить путь

1. Определите источник пути: `user`, curated source или generated fallback.
2. Не меняйте `path_locked: true` автоматически.
3. Сохраняйте действие в первом токене.
4. Не занимайте `S*` и `G*` обычной командой.
5. Проверьте canonical path и aliases на prefix conflict.
6. Для обычного каталога учитывайте K-частоту.
7. Для Sketch сохраняйте его смысловую грамматику даже при пути длиной до пяти токенов.
8. Добавьте тест на новый путь и на отсутствие коллизии.

### Изменить `BUTTON ID`

1. Подтвердите ID на целевой установке.
2. Зафиксируйте источник доказательства.
3. Проверьте, что отображаемое намерение соответствует фактическому действию.
4. Не переносите API candidate в UI command ID без проверки.
5. Выполните runtime-тест на копии детали.

---

## 16. Безопасность

Перед выполнением команды NXKeys проверяет доступный контекст, модуль, Work/Display Part, modal state, selection, точный ID и confirmation policy.

Критические правила:

- destructive-команды тестируйте только на копии детали;
- не обходите confirmation;
- не повторяйте автоматически запрос со статусом `interrupted_unknown`;
- не обновляйте Bridge DLL при работающей NX;
- не редактируйте системные файлы Siemens вручную;
- не удаляйте файлы вне package manifest;
- не включайте ambiguous/unresolved команду ради покрытия;
- после изменения роли, лицензии или версии NX обновляйте Catalog Studio export.

NXKeys не является единственной мерой защиты производственных данных.

---

## 17. Мини-шпаргалка для печати

```text
LEADER
CapsLock             открыть/закрыть
Space                поиск
Enter                выполнить
Backspace            назад
Esc                  отмена
Tab / Shift+Tab      следующий/предыдущий модуль

ВЫБОР
SB тело     SF грань     SE ребро     ST feature
SC компонент SU кривая  SD datum      SR сброс
SA всё      SN снять выбор

ПЕРЕХОДЫ
GA Assembly      GC CAM          GD Drafting
GH Sheet Metal   GL Reuse        GM Modeling
GN Simulation    GO Mold         GP PMI
GR Routing       GU Surface      GV View/Analysis

SKETCH
CGL линия        CGR прямоугольник
CGC окружность   CGA дуга
EGT обрезать     EGE удлинить
EGF скруглить    EGH фаска
TGO смещение     ADR быстрый размер
ISC Sketch Checker
CGV… варианты построения

ПРОВЕРКА УСТАНОВКИ
validate → health → запуск NX → bridge-status

ВОССТАНОВЛЕНИЕ
backups → restore
```

---

## 18. Куда смотреть дальше

| Нужно | Документ |
|---|---|
| понять архитектуру | [ARCHITECTURE.md](ARCHITECTURE.md) |
| установить или обновить | [INSTALLATION.md](INSTALLATION.md) |
| изменить профиль | [CONFIGURATION.md](CONFIGURATION.md) |
| изучить все правила путей | [MNEMONIC_COMMAND_LANGUAGE.md](MNEMONIC_COMMAND_LANGUAGE.md) |
| глубоко разобраться со Sketch | [SKETCH_INTENT_LANGUAGE.md](SKETCH_INTENT_LANGUAGE.md) |
| использовать CLI | [CLI.md](CLI.md) |
| диагностировать эксплуатацию | [OPERATIONS.md](OPERATIONS.md) и [TROUBLESHOOTING.md](TROUBLESHOOTING.md) |
| изменить код и пройти проверки | [DEVELOPMENT.md](../DEVELOPMENT.md) и [CONTRIBUTING.md](../CONTRIBUTING.md) |
| понять состояние документации | [DOCUMENTATION_AUDIT.md](DOCUMENTATION_AUDIT.md) |
