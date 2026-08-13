# План упрощения NXKeys для Siemens NX 2512.6000

**Статус:** план к реализации  
**Дата аудита:** 13 августа 2026 года  
**Проверенная ветка:** `main`  
**Базовый commit:** `13fc9c11ecb0722605a14f9cc9cf1facba3dbe0f`  
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

## 3. Целевой пользовательский путь

| Сценарий | Сейчас | Должно стать |
|---|---|---|
| Установка | исходники, SDK, PowerShell modes, сборка нескольких продуктов | готовый release, автообнаружение NX, одна кнопка «Установить» |
| Запуск | обычный NX, managed launcher, Studio, Control Center, tray, NX toolbar | один ярлык `Siemens NX 2512 + NXKeys`; остальные входы прикрепляются к той же сессии |
| Ежедневная команда | до 28 карточек, доступные и недоступные вперемешку | до 8 готовых действий текущего шага; остальное через путь или поиск |
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
  - добавить behavioral checks для лимита HUD, источника профиля, launcher arguments и совпадения runtime/permission capabilities.
- добавить один versioned capability lock для audited baseline: уникальные исполняемые BUTTON ID, local actions, selection intents и module switches. Изменение lock допускается только с явным описанием миграции.

**Как тестировать**

1. На clean checkout выполнить все команды, которые выполняет `verify`.
2. Намеренно изменить лимит HUD, имя профиля и один permission — каждый дефект должен уронить CI.
3. Проверить path filters: изменение `AdaptiveLeaderPolicy.cs` обязано запускать UI/runtime tests.
4. Проверить, что generated timestamp сам по себе не создаёт commit и не меняет рабочее дерево после verify.

**Критерий готовности:** один обязательный workflow отвечает на вопрос «этот commit можно устанавливать», а не только «отдельные файлы содержат ожидаемые слова».

### P1. Создать один компилятор v8-профиля

**Зачем:** это основа предсказуемого UX и сохранения функциональности.

**Что и где менять**

- `NX2512_HotkeyStudio/Models/V8Models.cs`:
  - добавить явные поля `action`, `enabled`, `risk`, `confirmation_required`, `requires_selection`, `minimum_selection_count`, допустимые selected types и user-facing unavailable reason;
  - отделить `local_behavior` от `execute_command`, `switch_module` и `set_selection_filter`.
- `NX2512_HotkeyStudio/Models/ConfigRuntimeV5.cs`:
  - удалить ручной `BuildHardcodedModules()`;
  - заменить `TranslateV8OperationsToLegacy()` детерминированной компиляцией в immutable runtime snapshot;
  - не сериализовать производные `Modules`, `Keyboard` и `LeaderKey.Sequences` в schema v8;
  - legacy schemas 3–7 загружать через read-only migration path и сохранять уже как однозначный v8 profile.
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
  - отдельно описать local behaviors;
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

**Критерий готовности:** профиль, HUD, desktop dispatch и Bridge allowlist используют один и тот же compiled snapshot и один digest.

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

### P5. Показывать в HUD только следующий полезный шаг

**Что и где менять**

- `NX2512_HotkeyStudio/Services/AdaptiveLeaderPolicy.cs`:
  - ранжировать сначала исполняемые команды;
  - учитывать application/module, active command/collector, work part, selection count, selected types, recent usage и frequency;
  - unavailable items возвращать только для поиска и объяснения, но не для корневых рекомендаций;
  - destructive items не поднимать только из-за частого использования.
- `NX2512_HotkeyStudio/Services/LeaderKeyEngine.cs`:
  - передавать в HUD два множества: `recommended now` и `searchable catalog`;
  - ручное циклическое переключение модуля не показывать как основной сценарий, поскольку модуль определяется автоматически; сохранить переключение через явную ветвь и Advanced setting.
- `NX2512_HotkeyStudio/UI/LeaderHudForm.cs`:
  - заменить `MaximumRootRows = 28` на `PrimarySuggestionLimit = 8`;
  - корень показывает до восьми готовых действий или групп;
  - `Space` ищет по полному текущему каталогу;
  - для недоступного search result показывать короткую причину: «сначала выберите грань», «нужна рабочая деталь», «команда отсутствует в вашей роли»;
  - заменить `BRIDGE OFF`, `SEL 0` и другие технические chips на короткие русские состояния;
  - при недостоверном контексте не показывать обычный каталог как готовый к исполнению.
- `NX2512_HotkeyStudio/UI/CommandListPreviewPanel.cs`:
  - использовать ту же ranking policy, что и живой HUD, чтобы preview не обещал другого поведения.

**Как тестировать**

1. Modeling без выбора: не более восьми доступных рекомендаций, нет selection-only destructive actions.
2. Modeling с выбранной гранью: появляются релевантные face operations, но полный каталог остаётся в поиске.
3. Sketch: `L`, `R`, `C`, `T`, `K→…`, `D→…` остаются достижимыми прежними путями.
4. Sheet Metal использует canonical `UG_APP_SBSM` / `UG_SBSM_*`.
5. Bridge offline/stale/authentication mismatch: HUD не отправляет команду и показывает одно понятное действие.
6. Поиск находит каждую capability из capability lock, даже если она не входит в top 8.
7. Destructive action всегда проходит отдельное confirmation state и повторно проверяется Bridge.

**Критерий готовности:** полнота не уменьшилась, но в каждый момент пользователь видит не более восьми действительно полезных следующих вариантов.

### P6. Упростить настройку команд без потери контроля

**Что и где менять**

- `HotkeyStudioForm.BuildModulesPage()`:
  - заменить открытую по умолчанию DataGridView на поиск команд и карточку выбранной операции;
  - обычные действия: включить/выключить, изменить mnemonic path, добавить в избранное, вернуть default;
  - BUTTON ID, adapter status и raw module ID показывать только в `Advanced mapping`;
  - ошибки duplicate/prefix conflict показывать рядом с редактируемым путём до сохранения.
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

**Критерий готовности:** настройка выполняется в терминах задач и путей, а не структуры JSON/NX MenuScript.

### P7. Сделать диагностику контекстной и восстанавливаемой

**Что и где менять**

- `NxKeysHealthService.cs`, `HealthModels.cs`, `NxTransportReadResult.cs`:
  - ввести стабильные issue codes и recommended actions;
  - сохранить raw exception только в technical report;
  - различать `NX_NOT_RUNNING`, `SESSION_NOT_MANAGED`, `BRIDGE_NOT_LOADED`, `BRIDGE_STALE`, `PROFILE_DIGEST_MISMATCH`, `PACKAGE_CORRUPT`, `DLL_LOCKED`, `COMMAND_UNAVAILABLE`, `COMMAND_CONTEXT_CHANGED`.
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
- advanced protocol/architecture оставить reference-документами, но убрать из основного пользовательского маршрута;
- historical/generated документы не должны попадать в поисковую выдачу как current behavior;
- убрать смешение русского пользовательского текста с `Main K3–K5 Profile`, `Adaptive Modules`, `Bridge OFF` и другими внутренними именами.

**Как тестировать**

1. Link checker и terminology checker для current docs.
2. В current user docs нет `nx2512-pro-hybrid.json`, 885/K3–K5 и source-build как основного способа установки.
3. Новый пользователь по README устанавливает и выполняет первую Sketch-команду без перехода в другие документы.
4. Каждая видимая ошибка UI находится в Troubleshooting по issue code.

**Критерий готовности:** существует один актуальный пользовательский маршрут и отдельный маршрут разработчика.

### P11. Подтвердить результат на живой NX 2512.6000

CI не доказывает sensitivity BUTTON ID, работу collectors и семантику интерактивных NX dialogs. Финальный gate выполняется на лицензированной станции.

**Матрица живых сценариев**

| Контекст | Обязательная проверка |
|---|---|
| Gateway / без детали | запуск, отсутствие ложных part-dependent рекомендаций |
| Modeling | Create Sketch, Extrude, Hole, Layer Settings, WAVE, поиск |
| Sketch | L/R/C/T, K→constraints, D→dimensions, finish |
| Assembly | Add/Move/Replace/Remove Component, подтверждение destructive |
| Drafting | base/projected/section view, dimensions |
| Sheet Metal | `UG_APP_SBSM`, Tab/Flange/Bend/Flat Pattern |
| Manufacturing | create/generate/postprocess/delete operation с подтверждением |
| Selection collector | 0–4, text-input guard, seed selection и смена selection fingerprint |
| Смена роли/лицензии | недоступная команда объясняется и не попадает в top suggestions |
| RU/EN UI | module detection и поиск не зависят от одной локализации title |

**Метрики приёмки**

- clean install: не более трёх осмысленных решений пользователя;
- первая рабочая команда: не более минуты после первого запуска NX;
- корневой HUD: не более восьми вариантов;
- 100% capabilities из baseline доступны старым путём либо имеют документированную миграцию;
- 100% destructive capabilities требуют подтверждения в desktop и Bridge;
- ни одного silently ignored profile/launcher mismatch;
- после любой неуспешной установки предыдущая версия остаётся работоспособной;
- типовая проблема решается одной предложенной action без ручного редактирования файлов.

## 6. Порядок выполнения

Изменения следует делать небольшими завершёнными пакетами и не смешивать UX-перестройку с массовым перемещением файлов.

| Очередь | Пакет | Gate перед переходом дальше |
|---:|---|---|
| 1 | P0 — честный verify | все обязательные проверки воспроизводятся на clean checkout |
| 2 | P1 — единый profile compiler | runtime capabilities = Bridge permissions; capability lock зелёный |
| 3 | P2 — один launcher | все входы используют один profile/session |
| 4 | P3 — готовый setup/release | clean VM устанавливает продукт без SDK/Node |
| 5 | P4 + P7 — state-driven shell и repair | каждый health state имеет одну правильную action |
| 6 | P5 — контекстный HUD | top 8 + полный поиск + destructive tests |
| 7 | P6 — простой editor | overrides, migration, undo/redo и update tests |
| 8 | P8 + P9 — optional packages и очистка | capability parity и package-size gate |
| 9 | P10 — каноническая документация | новый пользователь проходит путь только по README/User Guide |
| 10 | P11 — NX workstation release gate | полная матрица живых сценариев подписана результатами |

Каждый пакет должен завершаться отдельным рабочим commit. Нельзя одновременно менять canonical paths, удалять compatibility layer и переделывать UI без промежуточного green gate.

## 7. Что считать сохранением функциональности

Фраза «не потерять функционал» должна проверяться не количеством записей в JSON, а capability baseline:

- уникальные реально исполняемые NX BUTTON ID;
- local Leader/search/sticky/backspace/confirmation behaviors;
- module switches;
- selection type filters и Selection Intent 0–4;
- profile editing, backup, restore, install, update и diagnostics;
- Catalog Studio и NxEskd в optional packages.

453 operation contract нельзя автоматически называть 453 работающими функциями. Незавершённый internal/TBD contract сохраняется в каталоге и может быть доработан, но не должен появляться в HUD как готовая команда или попадать в Bridge allowlist.

## 8. Запрещённые упрощения

- скрыть проблемы launcher/profile только новой надписью в UI;
- отключить HMAC, allowlist или context checks ради запуска обычного NX;
- удалить поиск или advanced mapping вместе с перегруженной таблицей;
- превращать `tbd_adapter` в BUTTON ID по строковому сходству;
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
6. одним каноническим профилем и одним compiled capability snapshot.

Всё остальное — Bridge DLL, protocol, state machines, catalog exporter, NxEskd, generated evidence и developer CLI — остаётся внутренней или явно расширенной частью системы и не конкурирует за внимание обычного пользователя.
