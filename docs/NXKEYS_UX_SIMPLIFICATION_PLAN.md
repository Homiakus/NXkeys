# План упрощения NXKeys для Siemens NX 2512.6000

**Статус:** план к реализации  
**Дата аудита:** 13 августа 2026 года  
**Проверенная ветка:** `main`  
**Базовый commit:** `13fc9c11ecb0722605a14f9cc9cf1facba3dbe0f`  
**Дополнительный аудит mnemonic runtime:** `1aac694ced3a6ec238453559f4b9537d3dd66212`
**Целевая среда:** Siemens NX / Designcenter NX 2512.6000, Windows x64

## 1. Итог аудита

Главная проблема NXKeys — не недостаток функций. Проблема в том, что пользователь видит несколько конкурирующих способов установить, запустить, настроить и диагностировать одну систему, а внутри кода нет одного общего представления о том, какие операции действительно доступны и безопасны.

Целевой пользовательский путь должен состоять из четырёх действий:

1. запустить один установщик;
2. открыть NX единственным ярлыком `Siemens NX 2512 + NXKeys`;
3. нажать Leader и увидеть только подходящие текущему контексту действия;
4. при проблеме получить одно понятное объяснение и одну подходящую кнопку восстановления.

При этом сохраняются:

- все реально исполняемые команды текущего v8-профиля;
- фактические пользовательские маршруты: однотокенный Sketch, смысловые ветви `D/K/J/U`, aliases, `G → …` и `S → …`;
- три разных канала ввода: контекстные Direct Keys, CapsLock Leader и Selection Intent `0…4`;
- поиск по полному каталогу;
- ручная настройка путей для опытного пользователя;
- authenticated IPC, allowlist, anti-replay и повторная проверка контекста в Bridge;
- резервные копии, атомарная установка и откат;
- Catalog Studio и NxEskd как отдельные, необязательные возможности.

Не следует добавлять ещё одну оболочку, ещё один профиль или ещё один launcher. Улучшения должны сокращать число сущностей.

## 2. Фактическое состояние на момент аудита

### 2.1. Размер и состав

| Метрика | Фактическое значение | UX-следствие |
|---|---:|---|
| Версионируемых файлов | 1 157 | трудно отличить продукт от сгенерированных данных и архивов |
| Размер файлов в `HEAD` | 235,1 MiB | тяжёлое получение исходников и сборочного контекста |
| C#-файлов | 196 | несколько приложений и параллельных моделей продукта |
| проектов `.csproj` | 23 | большая поверхность сборки и зависимостей |
| Markdown-файлов | 87 | пользователь не понимает, какой документ актуален |
| BMP-файлов | 748 | в пакет копируется большой набор исходных иконок |
| GitHub Actions workflows | 10 | проверки разделены и запускаются по разным наборам путей |
| опубликованных GitHub Releases | 0 | обычный пользователь вынужден устанавливать из исходников |

Два каталога NX API содержат одинаковые крупные CSV. Только семь продублированных файлов занимают примерно 177 MiB в рабочем дереве. `assets/` занимает около 51 MiB, `nxeskd/` — около 90 MiB.

### 2.2. Несколько точек входа

В репозитории одновременно существуют:

- `install-nxkeys.ps1` с пятью режимами обслуживания;
- корневой `nxkeys.cmd` с девятью пунктами developer-меню;
- `NX2512_HotkeyStudio.exe` в режимах GUI, tray, CLI и launcher;
- `NX2512_ControlCenter.exe`;
- команды запуска Studio и daemon из NX MenuScript;
- managed launcher `launch-nx2512-with-nxkeys.cmd`;
- отдельные Catalog Studio и NxEskd Configurator.

`NX2512_ControlCenter/Program.cs` уже только запускает `NX2512_HotkeyStudio.exe --gui`, а `ControlCenterForm.cs` исключён из компиляции. То есть отдельный проект и крупная старая форма остаются в дереве без самостоятельной пользовательской функции.

### 2.3. Расхождение профилей и launcher-файлов

Текущий default — `config/nx2512-v8-profile.json`, но:

- `DeploymentEngine.BuildGuiLauncherCmd()` и `BuildDaemonLauncherCmd()` указывают на `nx2512-pro-hybrid.json`;
- installer копирует профиль под именем `nx2512-v8-profile.json`;
- финальные строки installer и описания ярлыков всё ещё говорят о «главном профиле K3–K5»;
- CI продолжает валидировать compatibility-профиль как канонический в части сценариев;
- Pages и несколько workflows публикуют или используют старые generated profiles.

Это создаёт ситуацию, когда разные способы запуска одной установки могут загрузить разные правила или не найти профиль вовсе.

### 2.4. Нет единой модели доступной функции

В `config/nx2512-v8-profile.json` находится 453 operation contract:

- 145 записей имеют adapter `button_id`;
- эти 145 записей соответствуют 128 уникальным NX BUTTON ID;
- 308 записей имеют adapter `internal`;
- ни одна запись `internal` не имеет status `mapped` в смысле текущей проверки `IsTbdAdapter()`.

Desktop-транслятор в `ConfigRuntimeV5.cs` отключает такие internal-операции. Но `NxBridgePermissionSet.FromV8Operations()` в `NXKeys.Protocol/NxBridgeSecurity.cs` строит разрешение и для non-`button_id`, подставляя `operation_id` как `command_id`. Поэтому HUD/runtime и Bridge allowlist получают разные множества команд из одного JSON.

Дополнительный риск: v8 `OperationContract` не содержит явных полей destructive/confirmation/selection requirements. Например, `assemblies.remove_component` и `manufacturing.delete_operation` имеют BUTTON ID, но при трансляции не получают обязательное подтверждение из профиля.

### 2.5. Профиль после сохранения получает два источника истины

`Config.Load()` переводит `operations` в `modules`, только если `modules` ещё пуст. `Config.Save()` сериализует и `operations`, и производные `modules`. После первого сохранения UI редактирует `modules`, а исходные `operations` остаются рядом. При следующем чтении повторная компиляция уже не выполняется.

Следствие: один файл начинает содержать каноническое и производное представление, которые могут расходиться. Обновление базового v8-каталога, пользовательское редактирование и Bridge permission digest перестают иметь однозначный результат.

### 2.6. Fallback — ещё один вручную поддерживаемый профиль

Если JSON не найден, `BuildHardcodedModules()` создаёт отдельный сокращённый набор команд прямо в `ConfigRuntimeV5.cs`. Он содержит старые Sheet Metal IDs и собственные пути. Это не настоящий fallback текущего v8-профиля, а третья независимая конфигурация.

Молчаливый переход на неё особенно опасен для UX: программа запускается, но часть команд и путей меняется без ясного сообщения пользователю.

### 2.7. HUD показывает слишком много

`LeaderHudForm.MaximumRootRows` равен 28. `LeaderKeyEngine.RankedCandidates()` вызывает `AdaptiveLeaderPolicy.Rank(..., includeUnavailable: true)`. В результате корневой HUD способен показать до 28 карточек, включая недоступные команды, хотя в старом UX-аудите и текущем workflow зафиксирована цель в 10 строк.

Сейчас пользователь видит каталог. Целевое поведение — короткий список следующих действий, которые можно выполнить именно сейчас. Полнота должна сохраняться через префиксы и поиск, а не через одновременный вывод всех вариантов.

### 2.8. Главная оболочка ориентирована на устройство системы

`HotkeyStudioForm` содержит восемь разделов:

1. Главная;
2. Базовые сочетания;
3. Команды;
4. Живой контекст NX;
5. Установка;
6. Backups / Profile;
7. Диагностика;
8. Настройки.

В основном сценарии показаны BUTTON ID, raw JSON контекста, deployment plan, manifest-пути и технические метрики. Это полезно разработчику, но не отвечает на три пользовательских вопроса: «готово ли всё к работе», «что доступно сейчас» и «как исправить проблему».

### 2.9. Установка выполняет разработческую сборку

Обычный installer требует .NET 8 SDK и собирает HotkeyStudio, Command Bridge, Control Center, а также NxEskd. В документации дополнительно упоминаются Node.js и Catalog export. Отдельного готового релиза нет.

Обычный пользователь не должен собирать продукт, понимать NXOpen references или ждать сборку необязательного модуля ЕСКД. Сборка — сценарий разработчика; установка — сценарий пользователя.

### 2.10. Проверки дают неполную картину

На audited commit локально получены результаты:

| Проверка | Результат |
|---|---|
| `validate-documentation.mjs` | PASS, проверено 28 канонических документов |
| `validate-main-command-map.mjs` | PASS, legacy K3–K5 pipeline |
| `validate-command-tree.mjs` | PASS, но отчёт относится к schema v6 compatibility runtime |
| `validate-full-command-map.mjs` | FAIL, 11 отсутствующих marker-проверок в текущих документах |
| `audit-command-sequences.mjs` | PASS, изменяет timestamp generated JSON |
| последний общий `ci` на GitHub | PASS |

`desktop-ui.yml` ищет текст `MaximumRootRows = 10`, тогда как исходник содержит `28`. Workflow не запускался на последних documentation-only commits, поэтому зелёный общий CI не обнаруживает это расхождение. Многие CI-проверки ищут строковые маркеры в исходниках вместо проверки поведения.

C#-тесты локально не запускались: в среде аудита отсутствовал .NET SDK. Статический вывод необходимо подтвердить на Windows и на лицензированной станции NX 2512.6000.

### 2.11. Заявленный mnemonic language шире фактически достижимого runtime

Дополнительно сопоставлены `docs/MNEMONIC_COMMAND_LANGUAGE.md`, `docs/RUNTIME_V8.md`,
`docs/SKETCH_INTENT_LANGUAGE.md`, `docs/SELECTION_INTENT.md`, v8-профиль, C# runtime и tests.
Документы правильно задают полезную трёхуровневую модель ввода, но часть описанного как
«фактически реализованное» сейчас является только profile intent или target design.

| Контракт языка | Что заявлено пользователю | Что делает текущий код | Решение для плана |
|---|---|---|---|
| Скрытый module prefix | приложение NX определяется автоматически | `LeaderKeyEngine` действительно вводит prefix активного модуля внутри DFA | сохранить; никогда не показывать и не редактировать prefix как пользовательский токен |
| CapsLock | одно физическое нажатие открывает Leader без autorepeat и смены регистра | latch реализован в `LeaderKeyEngine`, но Win32 edge/restore не покрыт отдельной автоматизированной регрессией | вынести admission-логику в тестируемую функцию; добавить hook integration test на Windows |
| Однотокенный Sketch | `CapsLock → L/R/C/A/T/…` | 94 operation используют только `workspace_key`, из них 20 имеют BUTTON ID; `SuppressWorkspaceLocalKeysAtRoot()` удаляет такие пути. Единственный `leader` длиной 1 также пропускается, потому что translator принимает только `Count >= 2` | обычные однотокенные Sketch-команды мигрировать в `leader:[key]`; `workspace_key` оставить только для явно открытого Workspace state |
| Direct Keys | частые команды работают без CapsLock при строгих guards | 58 operation имеют только `paths.direct`, из них 18 с BUTTON ID, но отдельного direct dispatch нет: translator превращает поле в однотокенный Leader route | либо реализовать отдельный guarded Direct state, либо маркировать route недоступным; не называть Leader route прямой клавишей |
| `secondary_aliases` | дополнительные Leader-маршруты | 84 aliases разворачиваются клонированием operation; однотокенный alias код переводит в `paths.direct`, смешивая разные семантики | компилировать aliases как Leader routes того же operation, без клонирования capability и без превращения в Direct Key |
| `G → …` | переход между приложениями из текущего контекста с ожиданием fresh context | 11 global operation транслируются в модуль `v8_g` как `execute_command`; явный `switch_module` и универсальный overlay из v8 profile не строятся | компилировать `action=switch_module`, `target_application_id` и общий route overlay; завершать только после нового Bridge context |
| `S → …` | универсальный фильтр типа объекта | 43 global `S` operation в v8 profile являются `internal`/не mapped; рабочие canonical filters добавляет `scripts/sequence-policy.mjs` только в compatibility/generated modules | описать проверенные фильтры в каноническом v8 operation contract как `set_selection_filter`; не импортировать весь target-каталог как готовый |
| Selection Intent `0…4` | отдельный in-process механизм «как распространять выбор» | реальный hook находится в `SelectionIntentHotkeys.cs`; есть foreground/modifier/focus/collector guards и physical latch, но код P/Invoke, admission и NX actions слит в одном static class; в режиме `1` дважды вызывается `KeepOnlyLastSelected()` | сохранить механизм отдельно от `S → …`; выделить чистое решение admission/intent transition внутри существующего Bridge-проекта и покрыть таблицей guards |
| Prefix-free DFA | primary path, alias и workspace scope не конфликтуют | prefix-проверка работает после legacy translation, но CI не фиксирует весь фактический v8 route set; Sketch test проверяет старые `C→L`/`E→T`, тогда как user docs требуют `L`/`T` | создать compiled route lock по scope и route kind; тестировать профиль, а не параллельную тестовую грамматику |
| BUTTON ID | только точное, подтверждённое сопоставление | правило в документации правильное, но Bridge permission parser всё ещё превращает non-button operation ID в command ID | применить fail-closed правило в едином compiler и permission snapshot |
| Sheet Metal | `UG_APP_SBSM` и `UG_SBSM_*` являются canonical | runtime нормализует старые IDs, но v8 profile всё ещё содержит `UG_APP_SHEETMETAL` для route | мигрировать canonical profile; compatibility mapping оставить только на входе legacy migration |
| Current против target | v8.3 Left-Hand First хранится как historical design | разделение формально есть, однако current guides описывают Direct/Workspace/global routes как уже работающие | ввести статусы `implemented`, `ci_verified`, `nx_verified`, `target_only`; current guide генерировать только из compiled snapshot |

Полезная идея документа — не «показать все команды», а дать три согласованных уровня:

1. Direct Key только там, где перехват доказанно безопасен;
2. `0…4` только внутри подходящего NX collector;
3. CapsLock Leader как полный, детерминированный и доступный для поиска язык.

Упрощение HUD не должно менять эту грамматику по статистике. Ranking выбирает подсказки, но уже
выученный путь всегда ведёт к той же operation, пока пользователь сам не подтвердил переназначение.

## 3. Целевой пользовательский путь

| Сценарий | Сейчас | Должно стать |
|---|---|---|
| Установка | исходники, SDK, PowerShell modes, сборка нескольких продуктов | готовый release, автообнаружение NX, одна кнопка «Установить» |
| Запуск | обычный NX, managed launcher, Studio, Control Center, tray, NX toolbar | один ярлык `Siemens NX 2512 + NXKeys`; остальные входы прикрепляются к той же сессии |
| Ежедневная команда | до 28 карточек, доступные и недоступные вперемешку | до 8 готовых действий текущего шага; остальное через путь или поиск |
| Мнемонический ввод | `direct`, `leader`, `workspace_key` и global routes смешиваются при legacy translation | Direct, Leader, Workspace и Selection Intent имеют разные states/guards, но компилируются из одного operation contract |
| Выученный путь | документация и тесты могут описывать разные Sketch-маршруты | route lock гарантирует неизменность `L/R/C/T`, `D/K/J/U`, aliases, `G` и `S`; ranking меняет только порядок подсказок |
| Настройка | таблицы модулей, BUTTON ID, JSON и deployment | основные настройки простыми словами; mapping только в «Дополнительно» |
| Ошибка | raw status, пути, журнал, ручной выбор maintenance mode | причина, влияние и одна безопасная кнопка следующего действия |
| Обновление | повторная сборка и очистка конфликтов | «Обновить» с preflight, backup, apply, health-check и автоматическим откатом |
| Расширенные инструменты | ставятся и собираются вместе с продуктом | Catalog Studio и NxEskd доступны отдельно и не мешают базовому пути |

## 4. Неподвижные правила реализации

1. Один канонический v8 operation profile компилирует и HUD/DFA, и Bridge allowlist.
2. Производные runtime-модели не записываются обратно как второй источник истины.
3. Неизвестная или неготовая операция не становится NX-командой по эвристике.
4. Destructive-команда не может оказаться исполняемой без явной risk policy и подтверждения.
5. В штатном интерфейсе показываются только действия, имеющие смысл в текущем NX context.
6. Недоступная функция не удаляется: она остаётся в поиске/каталоге с понятной причиной.
7. Один экран — одна основная кнопка. Технические детали раскрываются по запросу.
8. NxEskd и Catalog Studio не входят в стандартную установку, но остаются доступными как optional packages.
9. В первых этапах не добавлять новые `.csproj`. Удалять или объединять старые проекты раньше, чем создавать новые.
10. Не дробить каждую форму или DTO на отдельный файл. Допустим один компилятор профиля и один тип состояния пользовательского потока; остальное следует размещать в существующих тематических файлах.
11. `leader`, `direct`, `workspace_key` и Selection Intent — разные виды ввода с разными admission rules; compiler не должен преобразовывать один вид в другой ради совместимости.
12. Активное NX application является неявным scope. Пользователь не вводит и не редактирует внутренний module prefix.
13. Однотокенный Leader path является полноценным route. Минимальная длина два токена запрещена как универсальное правило.
14. Primary route, aliases и universal overlays образуют prefix-free DFA внутри своего scope; Workspace local keys проверяются только внутри явно открытого Workspace state.
15. Adaptive ranking может менять только подсказки. Автоматически менять выученный route или Direct Key по telemetry запрещено без preview и подтверждения пользователя.
16. Historical v8.3 specification остаётся backlog дизайна. Функция попадает в current guide только после compiler test и, когда задействован NX UI/collector, live NX verification.

## 5. План работ

### P0. Сначала сделать проверки честными

**Зачем:** дальнейшее упрощение нельзя безопасно выполнять, пока зелёный CI не гарантирует совпадение профиля, UI и package.

**Что и где менять**

- `.github/workflows/ci.yml`, `desktop-ui.yml`, `runtime-hardening.yml`, `full-command-map.yml`, `main-profile-runtime.yml`:
  - свести обязательные проверки в один `verify` pipeline;
  - оставить отдельно только `docs` и `release`;
  - запускать проверку UI также при изменениях профиля, ranking policy, installer и launcher;
  - убрать проверки наличия строк в C# и заменить их вызовом тестов;
  - перестать считать schema v6 compatibility profile доказательством работы v8.
- `scripts/validate-full-command-map.mjs`:
  - либо включить в обязательный pipeline и исправить текущие 11 расхождений;
  - либо удалить как устаревший валидатор. Оставлять падающий, но нигде не вызываемый скрипт нельзя.
- `NX2512_HotkeyStudio.Tests/Program.cs`:
  - заменить параллельную тестовую Sketch-грамматику `C→L`/`E→T` проверкой compiled routes из `config/nx2512-v8-profile.json`;
  - добавить behavioral checks для лимита HUD, источника профиля, launcher arguments и совпадения runtime/permission capabilities;
  - проверить однотокенные `L/R/C/A/T`, семейства `D/K/J/U`, alias `M→L→S`, universal `G→…`/`S→…` и отсутствие видимого module prefix.
- `NX2512_HotkeyStudio.Tests/V8AliasRegression.cs`:
  - проверять aliases как routes той же capability, а не как клонированные operation ID;
  - отдельно проверять, что `workspace_key` недоступен в root и доступен только в Workspace state.
- `NX2512_CommandBridge/SelectionIntentHotkeys.cs`, `NXKeys.StateMachines/LeaderStateMachines.cs` и `NXKeys.StateMachines.Tests/DeclarativePolicyTests.cs`:
  - вынести чистую таблицу admission для `0…4` из P/Invoke/NX вызовов в существующий state-machine layer, не создавая нового проекта;
  - покрыть foreground, Ctrl/Alt/Win, injected event, autorepeat, text/numeric focus, collector/no collector и seed/no seed.
- добавить один versioned capability-and-route lock для audited baseline: `operation_id`, route kind, application scope, primary path, aliases, action, BUTTON ID, risk и availability. Изменение lock допускается только с явным описанием миграции.

**Как тестировать**

1. На clean checkout выполнить все команды, которые выполняет `verify`.
2. Намеренно изменить лимит HUD, имя профиля и один permission — каждый дефект должен уронить CI.
3. Заменить `workspace_key:L` на `leader:[L]`, удалить `K→C` alias и превратить `switch_module` в `execute_command` по одному разу: каждый дефект должен уронить route tests.
4. Проверить path filters: изменение `AdaptiveLeaderPolicy.cs`, `V8SecondaryAliasExpander.cs`, `SelectionIntentHotkeys.cs` или compiler обязано запускать UI/runtime tests.
5. Проверить, что generated timestamp сам по себе не создаёт commit и не меняет рабочее дерево после verify.

**Критерий готовности:** один обязательный workflow отвечает на вопрос «этот commit можно устанавливать», а не только «отдельные файлы содержат ожидаемые слова».

### P1. Создать один компилятор v8-профиля

**Зачем:** это основа предсказуемого UX и сохранения функциональности.

**Что и где менять**

- `NX2512_HotkeyStudio/Models/V8Models.cs`:
  - добавить явные поля `action`, `enabled`, `risk`, `confirmation_required`, `requires_selection`, `minimum_selection_count`, допустимые selected types и user-facing unavailable reason;
  - отделить `local_behavior` от `execute_command`, `switch_module` и `set_selection_filter`;
  - представить route как типизированные `leader`, `direct` и `workspace`, сохранив совместимое чтение старых `paths`; scope route состоит из application + optional workspace, а не из буквы module prefix.
- `NX2512_HotkeyStudio/Models/ConfigRuntimeV5.cs`:
  - удалить ручной `BuildHardcodedModules()`;
  - заменить `TranslateV8OperationsToLegacy()` детерминированной компиляцией в immutable runtime snapshot;
  - принимать Leader path длиной 1–5; не использовать правило `leader.Count >= 2`;
  - строить universal overlay для `G→…`, проверенных `S→…`, search/help и других global actions поверх каждого разрешённого application scope вместо создания псевдомодулей `v8_g`/`v8_s`;
  - не выводить route kind из длины пути и не превращать `direct`/`workspace_key` в Leader terminal;
  - не сериализовать производные `Modules`, `Keyboard` и `LeaderKey.Sequences` в schema v8;
  - legacy schemas 3–7 загружать через read-only migration path и сохранять уже как однозначный v8 profile.
- `NX2512_HotkeyStudio/Models/V8SecondaryAliasExpander.cs`:
  - после появления compiler удалить клонирование operation для aliases;
  - каждый alias должен ссылаться на тот же стабильный `operation_id`, наследовать action/risk/guards и отличаться только route;
  - `secondary_aliases:["K->C"]` остаётся Leader route; однотокенный alias не становится Direct Key.
- `NX2512_HotkeyStudio/Models/LeaderConfigV5.cs` и `NXKeys.StateMachines/LeaderStateMachines.cs`:
  - строить DFA из compiled routes, а не из legacy modules;
  - хранить application scope отдельно от user tokens;
  - валидировать duplicate и terminal-prefix collision по `(route_kind, application_scope, workspace_scope)`;
  - добавить один явный Workspace state; local key допускается только после перехода в него.
- `NX2512_HotkeyStudio/NX2512_HotkeyStudio.csproj`:
  - встраивать канонический `config/nx2512-v8-profile.json` как resource;
  - при отсутствии installed profile восстанавливать копию из этого resource и явно сообщать о repair, а не включать другой набор команд.
- `NXKeys.Protocol/NxBridgeSecurity.cs`:
  - строить permission set из результата того же компилятора;
  - не создавать `execute_command` для `internal`/`local_behavior`/`tbd_adapter`;
  - переносить destructive и confirmation policy в permission;
  - запретить подстановку `operation_id` как NX BUTTON ID.
- `NX2512_HotkeyStudio/Services/NxCommandBridgeClient.cs`:
  - подписывать запрос только для capability, присутствующей в compiled snapshot;
  - проверять, что digest runtime snapshot совпадает с digest Bridge context.
- `config/nx2512-v8-profile.json`:
  - разметить 145 button-id records по risk/action;
  - нормализовать Sketch `L/R/C/A/S/P/W/G/I/T/E/O/F/H/M/V/Y/N/Z`: текущие `workspace_key`, дубли и псевдо-direct записи заменить однотокенными Leader routes там, где контрактом является `CapsLock → key`;
  - исправить `paths.direct` на реально guarded Direct routes; значения вроде `"S direct"` заменить однозначным token + explicit direct policy;
  - устранить расхождение Direct Dimension: current guide называет базовой клавишей `Q`, профиль содержит `D`; после live verification зафиксировать базовые `Q/S`, а `D/T/F/E/O` оставить отдельным opt-in extended set;
  - разметить routing operations как `switch_module` с canonical target (`UG_APP_SBSM` для Sheet Metal);
  - для заявленных, но отсутствующих в v8 profile `G→S` и `G→U` добавить verified switch/create contracts либо оставить видимый `target_only` result с причиной; не синтезировать target application по букве;
  - разметить только проверенные selection type filters как `set_selection_filter`, оставив Smart Selection/scope/mode target-only до adapter verification;
  - отдельно описать local behaviors и настоящие Workspace routes;
  - оставить 308 незавершённых contracts в каталоге, но не выдавать их за исполняемые команды.
- удалить исключённый `NX2512_HotkeyStudio/Models/ConfigModels.cs`: Git уже хранит историю, архивная копия в активном проекте не нужна.

Компилятор должен быть одним нейтральным компонентом в существующем core/protocol-слое. Не создавать отдельные проекты для parser, validator, migration и compiler.

**Как тестировать**

1. `compile(source) → runtime commands → permissions`: множества executable capability должны совпадать точно.
2. Ни одна internal/TBD operation не должна попасть в execute allowlist.
3. `assemblies.remove_component` и `manufacturing.delete_operation` должны требовать подтверждения и в HUD, и в Bridge.
4. `load → save → load` не добавляет derived `modules` в v8 JSON и сохраняет семантический digest.
5. Удаление внешнего JSON восстанавливает тот же набор возможностей, что и embedded canonical profile.
6. Legacy fixture schema 3–7 мигрирует один раз; повторная миграция идемпотентна.
7. Capability lock подтверждает, что ни один ранее исполняемый BUTTON ID, local action, selection intent или module switch не исчез.
8. Route lock подтверждает точные user paths и тип ввода: `CapsLock→L` не становится direct `L`, а Direct Key не требует CapsLock.
9. `G→H` создаёт `switch_module` к `UG_APP_SBSM`, затем остаётся в switching state до fresh context; обычный execute permission для этого route отсутствует.
10. Проверенные `S→B/F/E/T/C/U/D/R/A/N` доступны во всех разрешённых applications и не смешиваются с Selection Intent `0…4`.
11. Один и тот же `operation_id` с primary и secondary routes создаёт одну capability/permission и несколько DFA routes.

**Критерий готовности:** профиль, input admission, HUD, DFA, desktop dispatch и Bridge allowlist используют один compiled snapshot и один digest; документация может перечислить фактические routes без эвристики.

### P2. Оставить один установленный способ запуска

**Что и где менять**

- `NX2512_HotkeyStudio/Services/DeploymentEngine.cs`:
  - генерировать все внутренние launcher commands одной функцией аргументов;
  - везде использовать installed `nx2512-v8-profile.json`;
  - MenuScript-команды «Open settings» и «Start Leader» должны сигнализировать уже запущенному instance, а не создавать независимую конфигурацию;
  - не устанавливать пользовательские GUI/daemon `.cmd` как самостоятельные точки входа.
- `install-nxkeys.ps1`:
  - заменить остаточные `K3–K5` в сообщениях и описаниях ярлыков;
  - создавать один пользовательский ярлык `Siemens NX 2512 + NXKeys`;
  - shortcut настроек направлять на тот же executable `--gui`, без отдельного продукта.
- `NX2512_ControlCenter/`:
  - удалить `ControlCenterForm.cs`;
  - после одного compatibility release удалить проект-shim целиком;
  - старый shortcut при обновлении перенаправить на HotkeyStudio и затем удалить как stale managed file.
- корневой `nxkeys.cmd`:
  - убрать из пользовательской документации;
  - оставить только как developer helper под `tools/` либо удалить после переноса нужных команд в `DEVELOPMENT.md`.
- `Program.ResolveConfigPath()` и `HotkeyStudioForm.ResolveConfig()`:
  - использовать общий resolver;
  - исключить разные списки кандидатов в двух классах.

**Как тестировать**

1. После clean install найти все `.lnk`, `.cmd`, MenuScript actions и process arguments: каждый путь должен разрешать один installed profile и один instance scope.
2. Поиск `nx2512-pro-hybrid.json` в production/install/runtime-коде должен дать ноль совпадений; допустимы только compatibility tests и migration tooling.
3. Последовательно открыть NX ярлыком, Studio из tray и Studio из NX ribbon: должен существовать один keyboard hook и одна security session.
4. Запуск обычного `ugraf.exe` не должен молча показывать рабочий HUD: пользователь получает понятное состояние «NX запущен без NXKeys» и безопасный сценарий перезапуска.

**Критерий готовности:** пользователь знает один поддерживаемый launcher; все остальные действия прикрепляются к той же сессии.

### P3. Отделить установку от сборки

**Что и где менять**

- `.github/workflows/`:
  - добавить release job, выпускающий versioned, проверенный пакет NXKeys;
  - пакет не должен содержать Siemens DLL;
  - Bridge должен быть заранее собран и проверен на binary compatibility с NX 2512.6000;
  - публиковать SHA-256 и package manifest.
- `NX2512_HotkeyStudio/Program.cs` и существующий `DeploymentEngine`:
  - добавить first-run/setup mode в тот же executable;
  - автоматически найти NX 2512, показать обнаруженный путь и одну кнопку `Установить`;
  - показать выбор только если найдено несколько подходящих NX installations;
  - выполнить preflight → backup → install → health-check → создать ярлык;
  - при ошибке автоматически откатить и показать конкретное действие.
- `install-nxkeys.ps1`:
  - оставить developer/source-build wrapper и headless enterprise automation;
  - стандартный режим должен устанавливать готовый `dist`, не требовать .NET SDK или Node.js;
  - `Audit`, `CleanConflicts`, `RepairCustomDirs` и `CleanInstall` перенести в контекстные repair actions, а не показывать первым экраном.
- `NX2512_HotkeyStudio/build.ps1`, `NX2512_CommandBridge/build.ps1`:
  - использовать только в CI и developer flow.

**Как тестировать**

1. Чистая Windows 11 VM без .NET SDK и Node.js: установка из release, запуск NX и первая команда.
2. NX не установлена: setup ничего не меняет и объясняет, что требуется.
3. Найдены Siemens NX и Designcenter NX: setup просит один осмысленный выбор.
4. Обновление поверх предыдущей версии сохраняет user overrides и usage data.
5. Заблокированная Bridge DLL: setup не повреждает пакет, просит закрыть NX и предлагает повторить.
6. Искусственный сбой после staging: установленная предыдущая версия остаётся работоспособной.
7. Uninstall удаляет только managed files из manifest и не трогает чужие custom dirs.

**Критерий готовности:** обычная установка не требует терминала, SDK, Node или знания структуры репозитория.

### P4. Переделать оболочку вокруг состояния пользователя

**Что и где менять**

- `NX2512_HotkeyStudio/UI/HotkeyStudioForm.cs` и `HotkeyStudioForm.UnifiedShell.cs`:
  - сократить восемь верхнеуровневых разделов до трёх: `Состояние`, `Команды`, `Настройки`;
  - `Состояние` показывает один из режимов: `Готово`, `NX не запущен`, `Нужен правильный запуск`, `Нужно восстановление`, `Идёт обновление`;
  - для каждого режима показывать одну primary action;
  - raw context JSON, manifests, paths и logs убрать под `Технические подробности` / `Копировать отчёт`;
  - Backups не показывать списком в основном меню: использовать `Вернуть последнюю рабочую версию`, а полный список оставить в Advanced;
  - Installation plan убрать из ежедневного shell и оставить в setup/repair flow.
- `NX2512_HotkeyStudio/Models/HealthModels.cs` и `Services/NxKeysHealthService.cs`:
  - вернуть не набор несвязанных bool, а итоговый user flow state, issue code, impact и рекомендуемое безопасное действие;
  - технические сведения сохранить для диагностического отчёта.
- `NX2512_HotkeyStudio/Program.cs`:
  - tray menu сократить до `Открыть NXKeys`, `Приостановить/включить Leader`, `Выход`;
  - status tray должен кратко отражать Ready/Attention, без отдельной логики продукта.

Не создавать новую форму. Использовать существующий unified shell и удалить старые экраны после переноса уникальной функции.

**Как тестировать**

1. Для каждого health state построить shell без живой NX и проверить title, primary action и отсутствие ложной кнопки.
2. Keyboard-only проход: Tab order, Enter, Esc, Ctrl+S, доступные имена controls.
3. Layout screenshots на 100/125/150/200% DPI и разрешениях 1366×768, 1920×1080, 3840×2160.
4. Основной экран не содержит BUTTON ID, JSON, абсолютных путей и английских внутренних статусов.
5. Опытный пользователь достигает raw report и полного backup list не более чем за два явных перехода в Advanced.

**Критерий готовности:** при открытии приложения без чтения документации понятно, готова ли система и что делать дальше.

### P5. Сохранить mnemonic language и показывать только следующий полезный шаг

Пользовательский путь остаётся один — контекстная система NXKeys, — но внутри неё существуют три
разных безопасных канала ввода. Их нельзя сводить к одной таблице клавиш.

#### P5.1. Единый порядок admission

**Что и где менять**

- `NX2512_HotkeyStudio/Services/LeaderKeyEngine.cs`, `NX2512_CommandBridge/SelectionIntentHotkeys.cs` и существующий state-machine layer:
  - зафиксировать общий порядок: protected text/numeric input → system modifiers → active Leader/Workspace → active NX workflow → Selection Intent → Direct Keys → стандартное поведение NX;
  - использовать один снимок context revision для решения и повторно проверять его перед dispatch;
  - physical key-down принимать один раз до real key-up для CapsLock, Direct Keys и `0…4`;
  - убрать дублированный вызов `KeepOnlyLastSelected()` в intent `1`;
  - не делать blind retry интерактивной NX-команды: успешное открытие dialog может не совпадать с boolean результата invocation API.
- `NX2512_HotkeyStudio/Services/LeaderKeyEngine.cs`:
  - автоматически вводить application scope в DFA, но никогда не показывать его как первый пользовательский токен;
  - поддержать однотокенные Leader routes как обычные terminals;
  - сохранить повторный CapsLock = закрыть, double tap = Sticky, Backspace = один уровень назад, Enter = однозначное действие и Esc = ровно одна отмена;
  - `G→…` выполнять только как module-switch transition и завершать после fresh Bridge context;
  - `S→…` менять тип выбора, а `0…4` — способ распространения от seed; состояния должны комбинироваться и отображаться отдельно.
- Direct Keys реализовать в существующем input engine, без второго global-hook сервиса:
  - базовый набор включать только после live verification;
  - расширенный набор включается пользователем явно;
  - конфликтующий `F`/Fit остаётся выключенным по умолчанию;
  - при любом сомнении событие передаётся NX, а не поглощается.

#### P5.2. Контекстный HUD поверх стабильной грамматики

**Что и где менять**

- `NX2512_HotkeyStudio/Services/AdaptiveLeaderPolicy.cs`:
  - ранжировать сначала исполняемые команды;
  - учитывать application/module, active command/collector, work part, selection count, selected types, recent usage и frequency;
  - unavailable items возвращать только для поиска и объяснения, но не для корневых рекомендаций;
  - destructive items не поднимать только из-за частого использования;
  - ranking не меняет route, Direct Key или alias и не записывает автоматические переназначения.
- `NX2512_HotkeyStudio/Services/LeaderKeyEngine.cs`:
  - передавать в HUD `recommended now`, `next tokens` и `searchable catalog`;
  - ручное циклическое переключение модуля не показывать как основной сценарий; сохранить `G→…` и явную Advanced-настройку;
  - `Space` внутри Leader открывает поиск, вне Leader остаётся стандартным вводом, пока отдельное безопасное правило Hide Selected не реализовано и не проверено.
- `NX2512_HotkeyStudio/UI/LeaderHudForm.cs`:
  - заменить `MaximumRootRows = 28` на `PrimarySuggestionLimit = 8`;
  - корень показывает до восьми готовых действий или смысловых ветвей, но при вводе токена всегда показывает все допустимые следующие токены DFA;
  - явно различать `Direct`, `Leader`, `Selection: тип`, `Intent: правило` и Workspace state;
  - для недоступного search result показывать короткую причину и подходящий route восстановления: «сначала выберите грань», «нужна рабочая деталь», `G→C — открыть Manufacturing`;
  - заменить `BRIDGE OFF`, `SEL 0` и другие технические chips на короткие русские состояния;
  - при недостоверном контексте не показывать обычный каталог как готовый к исполнению.
- `NX2512_HotkeyStudio/UI/CommandListPreviewPanel.cs`:
  - использовать тот же compiled snapshot и ranking policy, что и живой HUD;
  - preview показывает реальный user route без скрытого prefix и помечает `target_only` как недоступный design intent.

#### P5.3. Workspace только как явное состояние

Target v8.3 содержит полезные Layer, WAVE и Geometry Display workspaces. Их следует реализовывать
по одному после P1, а не активировать существующие `workspace_key` как корневые команды.

- `NXKeys.StateMachines/LeaderStateMachines.cs` и `LeaderBehaviorProfile.cs`:
  - добавить один общий Workspace state с `workspace_id`, local routes, snapshot availability и back/close semantics;
  - вход выполняется обычным Leader route, затем HUD остаётся открыт; local key действует только в этом state;
  - `Esc` возвращает на уровень выше, повторный `Esc`/CapsLock закрывает; одно нажатие не выполняет два действия.
- `config/nx2512-v8-profile.json`:
  - сначала включить минимальный Layer Workspace с проверенным `UG_LAYER_SETTINGS` и disabled target actions с причинами;
  - WAVE и Geometry Display включать только после явных input/snapshot/safety contracts и live NX tests;
  - logical layer aliases хранятся в профиле; универсальный runtime не фиксирует корпоративные номера слоёв.
- существующие Bridge/runtime services:
  - Layer/Display actions до изменения создают восстанавливаемый snapshot;
  - нельзя скрыть Work Layer, молча показать скрытый слой, загрузить component, выполнить Unsuppress или изменить Reference Set;
  - WAVE требует явных source, target, associativity, проверки duplicate link и preview перед break/replace.

Не создавать отдельные приложения или формы для трёх workspaces. Это состояния существующего Leader HUD;
уникальная логика размещается в текущем state-machine/Bridge слое и включается capability за capability.

**Как тестировать**

1. Modeling без выбора: не более восьми доступных рекомендаций, нет selection-only destructive actions.
2. Modeling с выбранной гранью: появляются релевантные face operations, но полный каталог остаётся в поиске.
3. Sketch: `CapsLock→L/R/C/A/T/E/O/F/H/M/V/Y/N/Z`, `D→*`, `K→*`, `J→*`, `U→*` и `C→V→…` совпадают с route lock; active Sketch prefix не виден.
4. Direct Key нажимается при graphics focus и передаётся без перехвата при text/numeric focus, unknown modal, system modifier или неподходящем application.
5. CapsLock/Direct/`0…4` autorepeat создаёт ровно одно действие до key-up; CapsLock state ОС восстанавливается.
6. Повторный CapsLock, double tap, Backspace, Enter и Esc проходят таблицу состояний без двойного dispatch/cancel.
7. `G→M/A/D/P/U/H/C/N/R/O/L/V/S` использует switch action и ждёт новый context; Sheet Metal использует `UG_APP_SBSM` / `UG_SBSM_*`.
8. `S→B/F/E/T/C/U/D/R/A/N` меняет selection type; затем `0…4` меняет intent, не сбрасывая тип без явной причины.
9. Цифры `0…4` остаются обычным вводом без collector/seed и в числовом поле; Ctrl/Alt/Win и injected events не поглощаются.
10. Bridge offline/stale/authentication/profile mismatch: HUD не отправляет команду и показывает одно понятное действие.
11. Поиск находит каждую capability и каждый user route из lock, даже если они не входят в top 8.
12. Primary и secondary route выполняют одну capability и записывают одну usage identity.
13. Workspace local key недоступен на root; Layer snapshot/restore идемпотентен; скрытие Work Layer блокируется.
14. Destructive action всегда проходит отдельное confirmation state и повторно проверяется Bridge.

**Критерий готовности:** полнота и выученная грамматика не уменьшились, ложных перехватов нет, а в каждый момент пользователь видит не более восьми релевантных вариантов и все допустимые продолжения уже введённого пути.

### P6. Упростить настройку команд без потери контроля

**Что и где менять**

- `HotkeyStudioForm.BuildModulesPage()`:
  - заменить открытую по умолчанию DataGridView на поиск команд и карточку выбранной операции;
  - обычные действия: включить/выключить, изменить Leader path, добавить в избранное, вернуть default;
  - Direct Key и Workspace route редактировать в отдельных явно названных controls; пользователь не может случайно превратить один вид маршрута в другой;
  - hidden application prefix не показывать в поле пути;
  - BUTTON ID, adapter status и raw module ID показывать только в `Advanced mapping`;
  - primary route и aliases показывать как маршруты одной команды;
  - ошибки duplicate/prefix conflict показывать рядом с редактируемым путём до сохранения с указанием application/workspace scope.
- `ProfileDraftSession.cs`:
  - сохранить существующие undo/redo/diff/atomic save;
  - draft должен хранить overrides по стабильному `operation_id`, а не мутировать производные runtime modules;
  - обновление base profile должно повторно применить overrides и вывести конфликт только для реально изменившейся операции.
- `ConfigRuntimeV5.cs`:
  - хранить user overrides в одном профиле/настройке, не создавать отдельный файл на каждую роль, модуль или команду;
  - default и effective значения должны быть различимы для UI, но effective snapshot остаётся единым.

**Как тестировать**

1. Изменить путь одной команды, перезапустить приложение и убедиться, что изменился только её override.
2. Undo/redo, close with unsaved changes, atomic save и restore after corrupted save.
3. Обновить base profile с неизменённым `operation_id`: override сохраняется.
4. Удалить/переименовать operation: UI один раз предлагает сбросить или переназначить orphan override.
5. Попытка создать duplicate или prefix conflict блокирует сохранение и указывает обе конфликтующие команды.
6. В обычном режиме пользователь не может случайно заменить BUTTON ID.
7. Изменение primary route не удаляет aliases; alias нельзя сохранить как Direct Key без отдельного подтверждения и guard policy.
8. Один и тот же путь допустим в Modeling и Sketch, но конфликтует внутри одного application scope; Workspace local key не конфликтует с root вне Workspace.
9. Telemetry может предложить новый Direct Key, но не применяет его до preview, проверки конфликтов и подтверждения пользователя.

**Критерий готовности:** настройка выполняется в терминах задач и путей, а не структуры JSON/NX MenuScript.

### P7. Сделать диагностику контекстной и восстанавливаемой

**Что и где менять**

- `NxKeysHealthService.cs`, `HealthModels.cs`, `NxTransportReadResult.cs`:
  - ввести стабильные issue codes и recommended actions;
  - сохранить raw exception только в technical report;
  - различать `NX_NOT_RUNNING`, `SESSION_NOT_MANAGED`, `BRIDGE_NOT_LOADED`, `BRIDGE_STALE`, `PROFILE_DIGEST_MISMATCH`, `PACKAGE_CORRUPT`, `DLL_LOCKED`, `COMMAND_UNAVAILABLE`, `COMMAND_CONTEXT_CHANGED`, `ROUTE_NOT_COMPILED`, `INPUT_GUARD_BLOCKED`, `COLLECTOR_UNAVAILABLE`.
- `HotkeyStudioForm.UnifiedShell.cs`:
  - отобразить для issue: что произошло, что это меняет и одну кнопку действия;
  - `Repair installation` сначала создаёт backup и выполняет только действия из package manifest;
  - после repair автоматически повторить health-check и вернуть пользователя на главный экран.
- `LeaderHudForm.cs`:
  - краткие ошибки не должны исчезать без следа; последние result и repair action доступны из tray/shell;
  - не показывать stack trace или путь к queue в HUD.

**Как тестировать**

1. Для каждого issue code существует русское сообщение, impact и действие либо явное «действие не требуется».
2. Искусственно повредить context JSON, удалить managed DLL, изменить profile digest, заблокировать DLL и запустить NX не тем способом.
3. Repair не затрагивает файлы вне managed root и manifest.
4. Повторный repair идемпотентен.
5. Диагностический отчёт содержит commit/package version, NX build, profile digest и issue codes, но не session secret.
6. Target-only operation в поиске показывает `ROUTE_NOT_COMPILED`/причину adapter status, а не кнопку выполнения.
7. Заблокированный Direct Key или Selection Intent объясняет guard только по запросу в diagnostics и всегда оставляет исходное нажатие NX.

**Критерий готовности:** для штатных неисправностей пользователю не приходится выбирать maintenance mode или вручную искать файл.

### P8. Убрать необязательные продукты из стандартного пути

**Что и где менять**

- `install-nxkeys.ps1`:
  - не собирать и не устанавливать `nxeskd` по умолчанию;
  - включать его только явным optional component;
  - Catalog Studio также не включать в standard runtime package.
- `nxeskd/`:
  - оставить самостоятельную сборку и release artifact;
  - не копировать configurator, templates и MenuScript в NXKeys staging без явного выбора пользователя.
- `NX2512_Catalog_Studio/`:
  - позиционировать как инструмент разработчика/администратора для обновления evidence, а не обязательный этап ежедневной установки.
- удалить из корня несвязанный `setup-claude-code-go-mcp-deepseek.ps1` либо перенести его в профильный репозиторий инструментов.

**Как тестировать**

1. Standard install не содержит NxEskd/Catalog Studio файлов и не показывает их команды.
2. Optional install добавляет компонент через тот же setup, не меняя основной launcher.
3. Удаление optional component не затрагивает NXKeys core.
4. Capability lock подтверждает, что функции optional components не удалены из их собственных packages.

**Критерий готовности:** базовая установка содержит только то, что нужно для NXKeys hotkey workflow.

### P9. Сократить репозиторий и установленный пакет

**Что и где менять**

- удалить из активного дерева одну из двух идентичных копий `NX2512_Full_Function_API_Catalog_*` и затем обе полные выгрузки перенести в versioned release artifact;
- оставить в Git generator, компактный manifest/checksum и минимальные test fixtures;
- `config/nx2512-pro-full.generated.json` и `nx2512-pro-main.generated.json` генерировать в CI, не хранить как конкурирующие runtime profiles;
- `assets/nx-operation-icons/` заменить release-generated atlas PNG + один JSON index для реально используемых capabilities;
- procedural icons из `CadIconPainter` сохранить как fallback, поэтому отсутствие atlas не ломает функцию;
- `DeploymentEngine.CollectStaticAssets()` должен копировать только declared runtime assets, а не рекурсивно весь `assets/`;
- удалить исключённые из компиляции и неиспользуемые исходники после переноса уникальной логики;
- исторические generated reports публиковать как CI artifact/Pages snapshot, а не смешивать с current user guide.

Не переписывать историю Git в рамках обычного feature commit. Если потребуется уменьшить полный clone, history rewrite проводить отдельно, с резервной копией и согласованным окном миграции.

**Как тестировать**

1. Сравнить capability lock до/после очистки.
2. Standard runtime content без .NET runtime должен укладываться в установленный бюджет 15 MiB; self-contained installer измерять отдельно.
3. При отсутствии atlas все HUD icons отрисовываются процедурно без исключений.
4. В `HEAD` нет CSV/JSON-дубликатов крупнее 1 MiB.
5. Clean clone способен выполнить verify без скачивания полного workstation catalog; расширенный catalog test скачивает versioned artifact явно.

**Критерий готовности:** исходники и пользовательский пакет больше не являются хранилищем workstation dumps.

### P10. Оставить короткую каноническую документацию

**Что и где менять**

- `README.md`: назначение, готовый release, установка, один launcher, три первых команды;
- объединить ежедневную работу, cheatsheet и основные настройки в один `docs/USER_GUIDE.md`;
- `docs/TROUBLESHOOTING.md`: симптомы в терминах UI issue codes и действий;
- `DEVELOPMENT.md`: сборка из исходников, Catalog Studio, legacy generators;
- `docs/MNEMONIC_COMMAND_LANGUAGE.md`, `docs/SKETCH_INTENT_LANGUAGE.md`, `docs/SELECTION_INTENT.md`:
  - не поддерживать вручную таблицы, которые можно получить из compiled snapshot;
  - в current-раздел включать только `implemented` routes, отдельно помечая `ci_verified` и `nx_verified`;
  - Direct/Workspace/target-only operations не описывать как выполняемые только потому, что поле присутствует в JSON;
  - сохранить короткое объяснение различий: `S→…` задаёт тип, `0…4` задаёт intent, application prefix скрыт.
- advanced protocol/architecture оставить reference-документами, но убрать из основного пользовательского маршрута;
- historical/generated документы не должны попадать в поисковую выдачу как current behavior;
- убрать смешение русского пользовательского текста с `Main K3–K5 Profile`, `Adaptive Modules`, `Bridge OFF` и другими внутренними именами.

**Как тестировать**

1. Link checker и terminology checker для current docs.
2. В current user docs нет `nx2512-pro-hybrid.json`, 885/K3–K5 и source-build как основного способа установки.
3. Новый пользователь по README устанавливает и выполняет первую Sketch-команду без перехода в другие документы.
4. Каждая видимая ошибка UI находится в Troubleshooting по issue code.
5. Documentation test сравнивает каждую current route table с route lock/compiled snapshot, а не ищет отдельные строки `CapsLock → L`.
6. Historical v8.3 path не может попасть в `CHEATSHEET.md`, пока status не стал как минимум `implemented`; NX-dependent feature требует `nx_verified` для заявления «проверено».

**Критерий готовности:** существует один актуальный пользовательский маршрут и отдельный маршрут разработчика.

### P11. Подтвердить результат на живой NX 2512.6000

CI не доказывает sensitivity BUTTON ID, работу collectors и семантику интерактивных NX dialogs. Финальный gate выполняется на лицензированной станции.

**Матрица живых сценариев**

| Контекст | Обязательная проверка |
|---|---|
| Gateway / без детали | запуск, отсутствие ложных part-dependent рекомендаций |
| Modeling | Create Sketch, Extrude, Hole, Layer Settings, WAVE, поиск |
| Sketch | однотокенные L/R/C/A/T/E/O/F/H/M/V/Y/N/Z; D/K/J/U families; C→V variants; Direct base set; finish |
| Assembly | Add/Move/Replace/Remove Component, подтверждение destructive |
| Drafting | base/projected/section view, dimensions |
| Sheet Metal | `UG_APP_SBSM`, Tab/Flange/Bend/Flat Pattern |
| Manufacturing | create/generate/postprocess/delete operation с подтверждением |
| Application switching | G→M/A/D/P/U/H/C/N/R/O/L/V/S, ожидание fresh context, отсутствие старого module prefix |
| Selection type | S→B/F/E/T/C/U/D/R/A/N в разрешённых applications, временный filter и восстановление после OK/Cancel |
| Selection collector | 0–4 до/после seed, text/numeric guard, autorepeat и смена selection fingerprint |
| Input safety | graphics/text/numeric/table/modal focus, Ctrl/Alt/Win, injected key, drag/navigation, переключение foreground |
| Workspace | local key не работает на root; Layer snapshot/restore; WAVE source/target; Display restore без Unsuppress/Load |
| Смена роли/лицензии | недоступная команда объясняется и не попадает в top suggestions |
| RU/EN UI | module detection и поиск не зависят от одной локализации title |

**Метрики приёмки**

- clean install: не более трёх осмысленных решений пользователя;
- первая рабочая команда: не более минуты после первого запуска NX;
- корневой HUD: не более восьми вариантов;
- 100% capabilities из baseline доступны старым путём либо имеют документированную миграцию;
- 100% primary и secondary routes из route baseline приводят к прежнему `operation_id` либо имеют подтверждённую пользователем миграцию;
- 0 ложных перехватов Direct/`0…4` в матрице текстового и числового ввода;
- одно физическое нажатие CapsLock/Direct/`0…4` создаёт не более одного transition до key-up;
- 100% enabled current routes имеют status `implemented`, а NX-dependent release routes — подтверждение `nx_verified`;
- 100% destructive capabilities требуют подтверждения в desktop и Bridge;
- ни одного silently ignored profile/launcher mismatch;
- после любой неуспешной установки предыдущая версия остаётся работоспособной;
- типовая проблема решается одной предложенной action без ручного редактирования файлов.

## 6. Порядок выполнения

Изменения следует делать небольшими завершёнными пакетами и не смешивать UX-перестройку с массовым перемещением файлов.

| Очередь | Пакет | Gate перед переходом дальше |
|---:|---|---|
| 1 | P0 — честный verify | все обязательные проверки воспроизводятся на clean checkout |
| 2 | P1 — единый profile compiler | runtime capabilities = Bridge permissions; capability-and-route lock зелёный |
| 3 | P2 — один launcher | все входы используют один profile/session |
| 4 | P3 — готовый setup/release | clean VM устанавливает продукт без SDK/Node |
| 5 | P4 + P7 — state-driven shell и repair | каждый health state имеет одну правильную action |
| 6 | P5 — input language и контекстный HUD | стабильные routes + safe admission + top 8 + полный поиск |
| 7 | P6 — простой editor | overrides, migration, undo/redo и update tests |
| 8 | P8 + P9 — optional packages и очистка | capability parity и package-size gate |
| 9 | P10 — каноническая документация | новый пользователь проходит путь только по README/User Guide |
| 10 | P11 — NX workstation release gate | полная матрица живых сценариев подписана результатами |

Каждый пакет должен завершаться отдельным рабочим commit. Нельзя одновременно менять canonical paths, удалять compatibility layer и переделывать UI без промежуточного green gate.

## 7. Что считать сохранением функциональности

Фраза «не потерять функционал» должна проверяться не количеством записей в JSON, а capability baseline:

- уникальные реально исполняемые NX BUTTON ID;
- primary и secondary Leader routes с application scope и скрытым module prefix;
- однотокенный Sketch и families `D/K/J/U`, включая prefix-free variant branch `C→V`;
- local Leader/search/sticky/backspace/confirmation behaviors;
- отдельно проверенные Direct Keys и их input guards;
- module switches `G→…` с fresh-context completion;
- selection type filters `S→…` и отдельный Selection Intent `0…4`;
- Workspace entry routes, local keys и восстанавливаемые snapshots после их фактической реализации;
- profile editing, backup, restore, install, update и diagnostics;
- Catalog Studio и NxEskd в optional packages.

453 operation contract нельзя автоматически называть 453 работающими функциями. Наличие `direct`,
`workspace_key` или строки в historical spec также не доказывает достижимость route. Незавершённый
internal/TBD contract сохраняется в каталоге и может быть доработан, но не должен появляться в HUD
как готовая команда, current cheatsheet или Bridge allowlist.

## 8. Запрещённые упрощения

- скрыть проблемы launcher/profile только новой надписью в UI;
- отключить HMAC, allowlist или context checks ради запуска обычного NX;
- удалить поиск или advanced mapping вместе с перегруженной таблицей;
- превращать `tbd_adapter` в BUTTON ID по строковому сходству;
- превращать `workspace_key` или `secondary_alias` в корневой/direct route ради прохождения старого translator;
- считать `paths.direct` реализованным Direct Key без отдельного hook/admission test;
- менять выученный mnemonic route автоматически на основании частоты использования;
- переносить команды из historical v8.3 spec в current cheatsheet до compiler/live verification;
- создавать новый Control Center, новый формат профиля рядом с v8 или отдельный settings-файл на каждый модуль;
- разбить монолиты на десятки однотипных файлов без уменьшения числа пользовательских состояний;
- считать зелёным release, который не прошёл живой smoke test на NX 2512.6000.

## 9. Конечная структура продукта

После выполнения плана пользователь взаимодействует только с:

1. одним setup/update flow;
2. одним ярлыком запуска NX;
3. одним tray process;
4. одним контекстным HUD;
5. одной оболочкой со страницами `Состояние`, `Команды`, `Настройки`;
6. одним стабильным языком ввода: guarded Direct, `0…4` в collector и CapsLock Leader;
7. одним каноническим профилем и одним compiled capability-and-route snapshot.

Всё остальное — Bridge DLL, protocol, state machines, catalog exporter, NxEskd, generated evidence и developer CLI — остаётся внутренней или явно расширенной частью системы и не конкурирует за внимание обычного пользователя.
