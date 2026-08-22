# NXKeys — Аудит и план переработки: декомпозиция на 3 модуля

**Проект:** NXKeys v8 (контекстный клавиатурный слой для Siemens NX 2512, Windows x64, .NET 8)
**Проверено на:** ветка `main`, коммит `567603e`
**Анализ статический:** выполнен чтением кода и доков. Сборка и live-NX проверка здесь недоступны (хост = Android/Termux, нет .NET SDK для Windows и нет NX), поэтому план валидируется существующими тестами, contract build в CI и ручной проверкой на целевой NX.

---

## 1. Аудит текущего состояния

### 1.1 Размер и структура

| Проект | LOC (code) | Тесты | Роль |
|---|---|---|---|
| `NX2512_HotkeyStudio` | 12312 | 752 LOC в `NX2512_HotkeyStudio.Tests` | out-of-NX: профиль, adaptive module, Leader/HUD, hook, DFA/HFSM, клиент, deployment |
| `NX2512_Catalog_Studio` | 4760 | 0 | экспорт фактических NX UI commands |
| `NX2512_CommandBridge` | 1868 | 169 LOC (только NXOpen-стабы) | in-NX: контекст, admission, dispatch, module switch, Selection Intent |
| `NX2512_ControlCenter` | 528 | 0 | диагностика profile/runtime/Bridge |
| `NXKeys.Protocol` | 1197 | нет отдельного тест-проекта | IPC schema 4, security/permission/signing |
| `NXKeys.StateMachines` | 1155 | 691 LOC | DFA/HFSM/guards/policy |
| `nxeskd` (вложенная солюшн) | ~12k | свои тесты | Drawing Automation (ЕСКД), отдельный продукт |

Итого: ~189 .cs файлов, ~39k LOC (без учёта nxeskd). Единого `.sln` для основных проектов **нет** — есть только `nxeskd/NxEskdDrawingAutomation.sln`; основные проекты собираются по отдельным `csproj` через CI/`build.ps1`/`install-nxkeys.ps1`.

### 1.2 Архитектура: два god-object'а и переплетение трёх тем

Система — modular monolith в **двух процессах**:
- **HotkeyStudio** (вне NX): захват клавиш, контекст-клиент, движок выбора, подпись и отправка запроса.
- **Command Bridge** (внутри NX): построение авторитетного контекста, admission, диспетчеризация, исполнение, Selection Intent.

Три целевые темы сейчас **переплетены**:

**`NX2512_CommandBridge/Program.cs` (1224 стр.) — выраженный god-object.** Один `static class` (~40 методов, ~16 статических полей) совмещает: lifecycle NX add-in, файловую IPC-очередь, **мониторинг контекста** (`BuildCurrentContext`, `AskCurrentApplicationId`, selection snapshot, work/display part, modal, `NormalizeModule`/`ModuleIdFromRuntimeContext`), **исполнение** (`ExecuteNxCommand` → `DialogTester.InvokeMenuButtonAction`, `ProbeNxCommand`, `SwitchModule`, `ExecuteCapabilityRequest`), **выбор** (диспетчер `ProcessClaim`, `ValidateExpectedContext` с allowlist, `ApplySelectionCommand`/`SelectionFilterFromCommandId`), публикацию результата и Win32 P/Invoke.

**`NX2512_CommandBridge/SelectionIntentHotkeys.cs` (644 стр.) — второй god-object.** Совмещает низкоуровневый keyboard hook (Win32 P/Invoke), admission-guards (`IsCurrentNxForeground`, `IsFocusedInTextInput`, `HasSystemModifier`), применение Selection Intent (`TryApplyIntent`, `SetAllNativeToggles`, `BuildRule`, `ScRuleFactory` reflection) и построение геометрии правил. Сам регистрируется через `[ModuleInitializer]`, из `Program` **не вызывается** — два независимых входа в процесс NX.

**`NX2512_HotkeyStudio/Services/LeaderKeyEngine.cs` (848 стр.) — god-object (~15 ответственностей).** Совмещает: Win32 hook + hotkey fallback + CapsLock-восстановление + event-queue/pump + маппинг/двойной тап, **мониторинг окна/контекста** (`RefreshContext`, `ContextWatchTick`, `TryCreateForegroundNxFallbackContext`, `GetActiveNxWindow`, `ModuleIdFromWindowTitle`), **выбор модуля** (`AdaptiveModuleResolver.Resolve`), **драйв DFA** (`stateMachine.InputToken`/`Activate`), **резолвинг** (`RankedCandidates`), **отправку** (`Dispatch`/`NxCommandBridgeClient.Enqueue`), обработку 12 `LeaderActionKind`, HUD + 5 таймеров, управление модификаторами.

**`NX2512_HotkeyStudio/Services/NxCommandBridgeClient.cs` (424) — мини-god-object:** транспорт файловой очереди + security/allowlist/подпись (`PrepareAuthenticatedRequest`, `NxRequestAuthenticator.Sign`) + запуск внешнего nxeskd-конфигуратора (`LaunchNxEskdConfigurator`).

**8 точек сильной связи между темами (в Bridge):**
1. `ProcessClaim` — главный хаб: строит контекст (A) → выбирает ветку по `request.Action` (C) → исполняет (B) → снова строит контекст и завершает claim (A/D/E).
2. `ValidateExpectedContext` — смешивает A + C: принимает `NxContextSnapshot`, но содержит allowlist действий/CommandId-префиксов (это «какие команды разрешены», т.е. выбор, а не мониторинг).
3. `BuildCurrentContext` → `IsButtonReady("UG_SKETCH_*")` — контекст (A) измеряется через кнопки исполнения (B); смена модуля опирается на доступность кнопок.
4. `ApplySelectionCommand`/`SelectionFilterFromCommandId` — маппит подстроки CommandId в фильтр (C), затем зовёт `SelectionManager.SetEnabledGlobalFilterMembers`/`ClearGlobalSelectionList` (B), а при ошибке — fallback на исполнение `ExecuteRelaxedMenuButton` (B→B).
5. `RememberResult` → `WriteContext` — исполнение (B) загрязняет модель контекста (A) через общие статические поля.
6. Общее статическое состояние (`contextRevision`, `lastContextFingerprint`, `lastRequestId/lastResult/lastMessage`, `securityGate`, `requestInbox`, `isProcessing`) используется A/B/D/E одновременно.
7. Дублирование context/guard логики: `Program.IsModalDialogActive` vs `SelectionIntentHotkeys.IsCurrentNxForeground`/`IsFocusedInTextInput` — разные реализации «активно ли NX/безопасно ли вмешиваться», с дублированием P/Invoke.
8. Рефлексия как скрытая связь: расширенные NX-API дергаются по строковым именам (`ScRuleFactory`, `ApplicationSwitchRequest`, `ScCollectors`, `SelectionIntentRule`), что даёт сборку против минимальных stubs, но делает связь A/B/C неявной и немоделируемой.

**Переплетение в HotkeyStudio:**
- (А)↔(Б): единый `currentContext` (`NxBridgeContext`) становится и источником выбора модуля для HUD, и обязательным свежим входом для подписи запроса. Свежесть проверяется **дважды**: `RefreshContext`/`ContextWatchTick` (для HUD/модуля) и `NxCommandBridgeClient.RequireFreshContext` (для запроса). Fallback-контекст по заголовку окна **не аутентифицирован** → расхождение «HUD показывает, а исполнение отклоняет».
- (Б)↔(В): `LeaderActionKind` — один объект и «ходит» по DFA (выбор), и делает файловый IO (исполнение) через `Apply()`-switch (12 веток), объединяя HUD-таймауты, IO и учёт статистики.
- (А)↔(В): `ContextGuardEvaluator.Evaluate(command, context)` и `stateMachine.UpdateContext(context)` — guard совмещает module-фильтр, bridge-status, interaction-state, selection-guard с тем же снапшотом; выбор модуля (мониторинг) и допустимость команды (выбор) делят один контекст и один резолвер.

### 1.3 Тестовое покрытие (критично)

| Слой | Покрытие | Оценка |
|---|---|---|
| `NXKeys.StateMachines` (выбор: DFA/HFSM/guards/policy) | 691 LOC (`DeclarativePolicyTests`, `ReplayAndRandomizedTests`, `ProtocolInvariantTests`) | ✅ Хорошо |
| `NXKeys.Protocol` (контракт + крипто/permission) | **нет отдельного тест-проекта**; косвенно покрыт внутри `StateMachines.Tests` | ⚠️ Слабо — самый чувствительный слой |
| `NX2512_CommandBridge` (в-процессе: context/admission/dispatch/execute) | 169 LOC — **только NXOpen-стабы для contract build**, логики нет | ❌ Фактически 0 |
| `NX2512_HotkeyStudio` (движок/engine) | 752 LOC: `V8MnemonicMatchTests`, `V8AliasRegression` — генерация конфига/мнемоник, **но не** `LeaderKeyEngine`, `NxCommandBridgeClient`, `AdaptiveModuleResolver` | ❌ Движок не покрыт |
| `NX2512_ControlCenter`, `NX2512_Catalog_Studio` | 0 | ❌ |

**Вывод:** тестируемый слой — *выбор* (`StateMachines`) и *генерация конфига*, но **не** — *контекст-мониторинг*, *исполнение* и *движок-оркестровка*. Именно те слои, которые нужно разделять согласно задаче, автотестами не покрыты.

### 1.4 Безопасность

**Сильные стороны (позитив):** модель уже mature — authenticated IPC schema 4 (session secret, `payload_hmac` HMAC-SHA-256, `NxReplayGuard` anti-replay с монотонным `sequence_number`, `NxBridgePermissionSet` allowlist, canonicalization `UG_SHEET_METAL_*→UG_SBSM_*`, `NxRequestAuthenticator.Sign/Verify` с constant-time), at-most-once recovery, explicit `confirmation_accepted` для destructive, `ValidateExpectedContext` перед dispatch, `probe_command` для безопасной диагностики. Это правильная основа для разделения: **контракт уже существует и его надо сохранить как границу модулей**.

**Слабые места:**
- Самый чувствительный слой (`NXKeys.Protocol` крипто/permission) — без полноценных автотестов.
- Reflection-шлюзы (`NxReflection`, reflection по `ScRuleFactory`/`ApplicationSwitchRequest`) обходят compile-time проверки и усложняют анализ безопасности.
- Кросспроцессный контракт через файловую очередь — допустим для локальной desktop-системы, но требует, чтобы `NxCommandBridgeClient` не тащил политику подписи в транспорт (сейчас они склеены).

### 1.5 Сборка / CI / зависимости

- CI: 9 воркфлоу (`ci`, `desktop-ui`, `documentation`, `full-command-map`, `main-profile-runtime`, `mnemonic-command-language`, `pages`, `runtime-hardening`, `sketch-intent`) — зрелость высокая, но build/test discovery размазан по отдельным csproj из-за отсутствия единого `.sln` (кроме nxeskd).
- Зависимость на NXOpen через `HintPath $(NXOpenDir)` — внешняя, не NuGet; сборка зависит от окружения. Для декомпозиции это плюс: границу NXOpen можно изолировать на стадии исполнения.
- Вложенная солюшн `nxeskd` со своим `.sln` и статусом «1.0.2-rc1 / development» усложняет монолит; интеграция через `run_capability` + `capability_id` + `NxEskdCapabilityHandler` (принимает подписанный NXKeys-запрос). В коде одновременно существуют старый unsigned v1 `CommandRequest` и schema 4 `CapabilityHandler` — **кандидат на удаление**.

### 1.6 Документация

Зрелая: `RUNTIME_V8.md` (канонический runtime-контракт), `ARCHITECTURE.md`, `SELECTION_INTENT.md`, `STATE_MACHINE_ARCHITECTURE.md`, `api.md` (IPC), `SAFETY_MODEL.md`, ADR в `docs/adr/`. Приоритеты доков зафиксированы (код/tests > канонические доки > generated reports > historical). Замечания: в `docs/adr/` **дублируется номер 0003** (два файла `0003-*`); часть планов (`NXKEYS_UX_SIMPLIFICATION_PLAN`, `NXESKD_INTEGRATION_PLAN`) опережает код и содержит целевые состояния, которых в коде ещё нет.

### 1.7 Сводная оценка

| Измерение | Оценка (0–10) | Комментарий |
|---|---|---|
| Архитектура (SRP/границы) | 4 | Два god-object'а, 8 точек переплетения A/B/C |
| Тестовое покрытие | 5 | Выбор и конфиг покрыты, контекст/исполнение/движок — нет |
| Безопасность | 8 | Mature authenticated IPC; минус за отсутствие тестов крипто-слоя |
| Документация | 8 | Зрелая и каноническая; мелкие замечания |
| Сборка/CI | 6 | Много CI, но нет единого `.sln`; NXOpen зависит от окружения |
| Адемо-риск (переработка) | 5 | Контракт готов; главный риск — без тестов и без живой NX |

---

## 2. Целевая декомпозиция: 3 модуля

> **Важно про масштаб.** Это desktop-система в **двух процессах** (WinForms-приложение вне NX + библиотека-аддон внутри NX). Поэтому «модуль» здесь — **бounded component** внутри существующих проектов, а **не** отдельный сервис/микросервис. Процессный барьер уже задаёт естественную границу, а `NXKeys.Protocol` (schema 4) — готовый интерфейс между процессами.

Три модуля образуют **конвейер**: `ВВОД → ВЫБОР (модуль C) → EXECUTION (модуль A)`, вокруг которого по обе стороны процесса стоит **МОНИТОРИНГ КОНТЕКСТА (модуль B)** как источник состояния.

### Принципы границ

1. **Один модуль = одна ось изменения.** Контекст меняется по ключу «как устроен NX/окружение», исполнение — «какие NX API/кнопки», выбор — «как сопоставить ввод и контекст с командой». Три независимых темпа изменения.
2. **Зависимость направлена внутрь.** `Ввод → Выбор → Исполнение`; модуль B (контекст) — источник данных для C и A, но B **не зависит** от A/C (не должен мерить контекст через исполнение кнопок — устранить точку 3).
3. **Контракт прежде всего.** Каждый модуль общается через интерфейс (сейчас — поверх `NxContextSnapshot`/`NxCommandRequest`/`NxCommandResult` из Protocol). Никакого доступа к чужим полям/статике.
4. **Admission — сквозной security-гейт, а не бизнес-модуль** (стоит между очередью и исполнением), но **отделён** от исполнения.
5. **Selection Intent (0…4)** — отдельный узкий слой внутри NX; его admission (`SelectionIntentAdmissionEvaluator`) относится к «выбору», а фактическое применение кнопок/правил — к «исполнению».

### 2.1 Модуль A — Выполнение команд в NX (`INxCommandExecutor`)

**Mission:** получив проверенный (аутентифицированный и допущенный) `NxCommandRequest` и текущий контекст, вызвать фактическую операцию NX и вернуть `NxCommandResult`.

**Что знает/владеет:** как дергать NX (`MenuBarManager.GetButtonFromName` + `DialogTester.InvokeMenuButtonAction`, `UF.AskApplicationModule`, `SelectionManager.*`, `ApplicationSwitchRequest`), как загружать/исполнять capability (`nxeskd` через `Assembly.LoadFrom` + `NxEskdCapabilityHandler`), как строить rule-геометрию (`ScRuleFactory`, `ScCollectors`), как применять Selection Intent toggles.

**Что НЕ знает:** как строить контекст (B), как выбирать команду (C), как устроена очередь/подпись (D) — всё приходит готовым.

**Интерфейс (эскиз):**
```csharp
public interface INxCommandExecutor {
    NxCommandResult ExecuteCommand(NxCommandRequest req, NxContextSnapshot ctx);
    NxCommandResult ProbeCommand(NxCommandRequest req, NxContextSnapshot ctx);
    NxCommandResult SwitchModule(NxCommandRequest req, NxContextSnapshot ctx);
    NxCommandResult ApplySelectionFilter(NxCommandRequest req, NxContextSnapshot ctx);
    // capability — за адаптером (nxeskd)
}
```
**Реализации:** `NxMenuCommandExecutor` (кнопки/MenuScript), `NxSelectionExecutor` (фильтры/Selection Intent применённые), `NxCapabilityExecutor` (адаптер над `NxEskdCapabilityHandler`/`IExecutionAdapter`).
**Выделяется из:** `Program` (`ExecuteNxCommand`, `ProbeNxCommand`, `RequireRunnableButton`, `SwitchModule`, `ExecuteCapabilityRequest`, `ResolveInstalledCapabilityAssembly`, `ExecuteRelaxedMenuButton`, `ApplySelectionCommand`, `ApplyGlobalSelectionFilter`, `FilterMembersFor`) и `SelectionIntentHotkeys` (применяющий блок: `TryApplyIntent`, `SetAllNativeToggles`, `TryExpandSelectedSeed`, `BuildRule`, `SelectRuleObjects`, `SetToggle`).

### 2.2 Модуль B — Мониторинг состояния системы и контекста NX (`INxContextProvider`)

**Mission:** отдавать **единственный канонический** `NxContextSnapshot` (активное приложение NX, модуль, выборка, work/display part, модальность, контекст-конфиденс, security-state, revision/fingerprint) и определять его свежесть/достоверность.

**Что знает/владеет:** как собрать снапшот из NX (`UF.AskApplicationModule`, selection snapshot, `ScRuleFactory`); как нормализовать модуль (`NormalizeModule`/`ModuleIdFromApplication`/`Apply`-метафора v8-префиксов); как определить foreground-окно (`GetForegroundWindow`, `GetWindowThreadProcessId`, имя заголовка → `ModuleIdFromWindowTitle`); как читать опубликованный `bridge/context.json` и строить fallback-контекст; как считать семантический fingerprint и revision.

**Что НЕ знает:** как исполнять команды, как выбирать, как подписывать. **Не должен** мерить контекст через доступность кнопок (убрать `IsButtonReady`-обвязку из `BuildCurrentContext` — точка 3).

**Интерфейс (эскиз):**
```csharp
public interface INxContextProvider {
    NxContextSnapshot? GetCurrent();
    bool TryGetFresh(out NxContextSnapshot ctx);   // свежесть по NxProtocolConstants.DefaultContextFreshness
    bool IsBridgeReady { get; }
    event Action? ContextChanged;
}
```
**Реализации:**
- in-NX: `NxContextMonitor` (из `Program`: `BuildCurrentContext`, `AskCurrentApplicationId`, `AskSelectionSnapshot`, `AskWorkPartAvailable`, `AskDisplayPartAvailable`, `IsModalDialogActive`, `NormalizeModule*`). **Владелец авторитетного снапшота.**
- out-of-NX: `NxContextClient` (из `LeaderKeyEngine`: `RefreshContext`, `TryCreateForegroundNxFallbackContext`, `GetActiveNxWindow`, `ModuleIdFromWindowTitle`) — читает `bridge/context.json` (+ fallback), публикует `ContextChanged`.

**Устраняется:** дублирование «активен ли NX/безопасно ли вмешиваться» (точка 7) и двойная проверка свежести (в `ContextWatchTick` и `RequireFreshContext`) — единая политика свежести/конфиденса в одном компоненте; согласованное поведение HUD и исполнения (fallback-контекст больше не «показывает, но не исполняет»).

### 2.3 Модуль C — Логика выбора команды (`ISelectionEngine`)

**Mission:** из текущего контекста (B), ввода пользователя и v8-профиля принять решение: **какую команду отправить** (последовательность/command_id), либо переключить модуль, либо запросить подтверждение, либо отклонить.

**Что знает/владеет:** `SequenceAutomaton` (trie/DFA) + `LeaderStateMachine` (HFSM) + `ContextGuardEvaluator` (guards) + `LeaderBehaviorProfile` (декларативная политика) + `AdaptiveModuleResolver` (выбор модуля по контексту) + `AdaptiveLeaderPolicy.Rank` (ранжирование для HUD) + генерация канонических последовательностей (`LeaderKeyConfig.RebuildFromModules`/`MnemonicPathGenerator`). Это **уже изолировано в `NXKeys.StateMachines`** и хорошо покрыто тестами — ядро модуля C фактически существует.

**Что НЕ знает:** как строить контекст (B), как исполнять/подписывать (A/D). Порождает **намерение** (`SequenceDefinition`/`ResolvedCommandBehavior`/`GuardResult`), а не IO.

**Интерфейс (эскиз):**
```csharp
public enum Outcome { Execute, SwitchModule, RequireConfirmation, Reject }
public sealed class Decision {
    public Outcome Outcome;
    public string CommandId;      // канонический
    public string Sequence;       // диагностическая/внутренняя
    public string ModuleId;
    public string SelectionFilter;
    public bool RequiresConfirmation;
    public string RejectReason;
}
public interface ISelectionEngine {
    Decision Decide(CommittingIntent intent, NxContextSnapshot ctx, RuntimeProfile profile);
    IEnumerable<Candidate> CandidatesFor(NxContextSnapshot ctx, string query); // для HUD
}
```
**Реализация:** `NxKeySelectionEngine` — оркестратор поверх `SequenceAutomaton`/`LeaderStateMachine`/`ContextGuardEvaluator`/`AdaptiveModuleResolver`/`AdaptiveLeaderPolicy`.
**Выделяется из:** `LeaderKeyEngine` (DFA-драйв, `RankedCandidates`, `AdaptiveModuleResolver.Resolve`, `Apply`-transition) এবং `Program`/`SelectionIntentHotkeys` (часть адмиссиона `ValidateExpectedContext`, `MapVkToIntent` + `SelectionIntentAdmissionEvaluator`).

> **Явная ловушка:** `NX2512_HotkeyStudio/Services/CommandResolver.cs` — **НЕ** live-резолвер клавиш, а конфиг-время фузи-резолвер для deployment (`DeploymentEngine.BuildPlan`). Его **нельзя** тащить в модуль C; он остаётся в модуле генерации/deployment.

### 2.4 Контракт между модулями

| Модуль | Публикует | Потребляет |
|---|---|---|
| B — Контекст | `NxContextSnapshot`, `ContextChanged`, `IsBridgeReady` | C (выбор), A (варианты исполнения), HUD |
| C — Выбор | `Decision` (`Outcome`, `CommandId`, `Sequence`, `ModuleId`, `SelectionFilter`) | A (исполнение), D (отправка/подпись), HUD |
| A — Исполнение | `NxCommandResult` | D (результат в очередь), B (ревизия/статус), HUD |
| D — Транспорт+Security (сквозной, не бизнес-модуль) | `NxCommandRequest` (подписанный), `NxCommandResult` | — |

Межпроцессный контракт уже задан: `NXKeys.Protocol` (schema 4) — он остаётся **неизменной границей**. Ничего нового в протокол вводить не нужно.

### 2.5 Маппинг текущих классов → целевые модули

| Текущий | Модуль | Комментарий |
|---|---|---|
| `Program.ExecuteNxCommand/ProbeNxCommand/RequireRunnableButton/SwitchModule/ExecuteRelaxedMenuButton` | A | кнопки/MenuScript |
| `Program.ExecuteCapabilityRequest/ResolveInstalledCapabilityAssembly` | A | capability (nxeskd) через адаптер |
| `Program.ApplySelectionCommand/ApplyGlobalSelectionFilter/FilterMembersFor/ParseFilterMembers/SelectionFilterFromCommandId` | A | применяемый фильтр |
| `SelectionIntentHotkeys.TryApplyIntent/SetAllNativeToggles/SelectRuleObjects/SetToggle` | A | применённый intent |
| `Program.BuildCurrentContext/WriteContext/AskCurrentApplicationId/AskSelectionSnapshot/AskWorkPartAvailable/AskDisplayPartAvailable/IsModalDialogActive/NormalizeModule*` | B | авторитетный in-NX контекст |
| `LeaderKeyEngine.RefreshContext/ContextWatchTick/TryCreateForegroundNxFallbackContext/GetActiveNxWindow/ModuleIdFromWindowTitle` | B | out-of-NX контекст-клиент + fallback |
| `SequenceAutomaton`/`LeaderStateMachine`/`ContextGuardEvaluator`/`LeaderBehaviorProfile` | C | (уже в `NXKeys.StateMachines`) |
| `LeaderKeyEngine` (DFA-драйв, `RankedCandidates`, `AdaptiveModuleResolver.Resolve`, `Apply`-transition) | C | оркестратор выбора |
| `AdaptiveModuleResolver` | C | выбор модуля |
| `LeaderKeyConfig.RebuildFromModules`/`MnemonicPathGenerator` | C | генерация последовательностей из конфига |
| `NxCommandBridgeClient` (транспорт+подпись) | D | split на транспорт и политику подписи |
| `CommandResolver`/`DeploymentEngine` | deployment | НЕ live-выбор |
| `BridgeSecurityGate`/`BridgeRequestInbox` | D | admission/очередь |
| `NxEskdCapabilityHandler`/`IExecutionAdapter`/`NxExecutionAdapter` | A (адаптер) | nxeskd за интерфейсом |

### 2.6 Диаграмма зависимостей (без циклов)

```text
[Ввод/Hook] ──▶ [C: Выбор/SelectionEngine] ──▶ [D: Transport+Security] ──▶ [A: Executor] ──▶ (NX)
      ▲                    │                            ▲
      │                    │                            │
      └──[B: ContextProvider]────────────────────────────┘
              (revision/status/result feed)     [A] → [B] (публикация ревизии/статуса, но не состояние)
```
Правило: `B → (C, A)` только как источник данных; `C → A` через порт/намерение; `A` никогда не строит контекст сам; `C` никогда не делает IO; `A` никогда не выбирает. Циклов нет.

---

## 3. Фазовый план переработки (behavior-preserving)

Каждая фаза — **инкремент без смены внешнего поведения**: интерфейсы добавляются, god-object методы переезжают, внешний runtime-контракт (schema 4, file queue) не меняется. Валидация на каждой фазе: `dotnet test` (StateMachines/Protocol) + contract build (NXOpen stubs) + при первой возможности live-NX прогон. Разделение делается на стороне Windows-workstation/CI; здесь (Android/Termux) — только проектирование и код-анализ.

### Фаза 0 — Подготовка (безопасно, без изменения поведения)
- Создать единый `NX2512.sln` для основных проектов (сейчас их собирают по одному `csproj`), чтобы `dotnet test`/`build` были консистентны.
- Починить дубль `docs/adr/0003-*` (перенумеровать).
- Завести тесты на контракт в `NXKeys.Protocol` (отдельный `NXKeys.Protocol.Tests`): HMAC, `Validate()`, `IsExpired`, `NxReplayGuard`, permission canonicalization. **Это страховка перед любым рефакторингом.**
- Зафиксировать границу в коде: разнести `NxCommandBridgeClient` на «транспорт» (`INxQueueTransport`) и «политику подписи» (`IRequestPolicy`), но без изменения публичного поведения.

### Фаза 1 — Выделить модуль B (контекст)
1. Создать `INxContextProvider` + `NxContextMonitor` (in-NX). Перенести `BuildCurrentContext`/`Ask*`/`NormalizeModule*` из `Program`.
2. Устранить точку 3: модуль не должен мерить контекст через `IsButtonReady` — заменить на надёжный источник состояния NX (если его нет — ввести минимальный реф-тип и оставить как issue, но с интерфейсным барьером).
3. Создать `NxContextClient` (out-of-NX): чтение `bridge/context.json` + fallback; одинаковая политика свежести/конфиденса.
4. Убрать дублирование (точка 7): `IsModalDialogActive`/`IsCurrentNxForeground`/`IsFocusedInTextInput` — единый компонент.
5. Проверка: `dotnet test` + contract build; HUD-и-исполнение используют один `GetCurrent()`.

### Фаза 2 — Выделить модуль A (исполнение)
1. Создать `INxCommandExecutor` + `NxMenuCommandExecutor`/`NxSelectionExecutor`/`NxCapabilityExecutor`. Перенести методы исполнения из `Program`.
2. Изолировать reflection-шлюз (`ScRuleFactory`, `ApplicationSwitchRequest`, `ScCollectors`) в одном helper, чтобы граница NXOpen была единственной точкой.
3. `SelectionIntentHotkeys`: разделить hook/guards (поток B-контекст + ввод) и применение intent (модуль A); `TryApplyIntent` идёт в executor.
4. Проверка: contract build против stubs + live-NX (command dispatch, Selection Intent 0…4).

### Фаза 3 — Изолировать модуль C (выбор)
1. Собрать `ISelectionEngine` поверх существующего `NXKeys.StateMachines` + `AdaptiveModuleResolver` + `AdaptiveLeaderPolicy`; оставить генерацию последовательностей в конфиге.
2. Превратить `LeaderKeyEngine` в **тонкий оркестратор**: «ввод → `ISelectionEngine.Decide` → отправить в `INxCommandExecutor`-клиент». `Apply()`-switch (12 веток) перенести в движок выбора; HUD-оркестровку и IO разделить.
3. Убедиться, что `CommandResolver` НЕ попал сюда (остался в deployment).
4. Проверка: `NXKeys.StateMachines.Tests` (уже зелёные) + `NX2512_HotkeyStudio.Tests`.

### Фаза 4 — Чистка и усиление тестов
- Удалить старый unsigned nxeskd v1 `CommandRequest` (остался schema 4 `CapabilityHandler`).
- Разнести общее статическое состояние (`isProcessing`, `contextRevision`, `last*`) по компонентам (B владеет ревизией/статусом, D владеет очередью, A владеет исполнением).
- Добавить тесты на `NxContextMonitor`/`NxContextClient` (fallback, freshness), на `NxMenuCommandExecutor` (маппинг CommandId→кнопка, фильтры), на `LeaderKeyEngine`-слой через `ISelectionEngine` (порты в тестах).
- Прогнать live-NX регресс по `RUNTIME_V8.md` чек-листу (7 пунктов).

---

## 4. Риски и стратегия валидации

| Риск | Влияние | Митигция |
|---|---|---|
| **Невозможность сборки/тестов здесь** (Android/Termux, нет .NET Windows SDK, нет NX) | Рефакторинг нельзя проверить в этой среде | План рассчитан на Windows-workstation/CI; здесь только проектирование. Каждая фаза — `dotnet test` + contract build |
| **Reflection-шлюзы** скрывают реальные NX-API | Границы модулей A/B/C могут быть построены на неявных зависимостях | Изолировать весь reflection в один helper; интерфейсный барьер; не менять поведение в фазе 1–3 |
| **Нет автотестов на движок/исполнение/контекст** | Рефакторинг без страховки | Фаза 0 сначала закрывает Protocol-тесты; после фаз B/A/C добавить тесты через порты (`INxContextProvider`,`INxCommandExecutor`,`ISelectionEngine`) |
| **Двойное понятие контекста** (bridge vs fallback, неаутентифицированный fallback) | HUD и исполнение расходятся | Единый `INxContextProvider` + единая политика свежести/конфиденса (Фаза 1) |
| **Interactive command caveat** | `DialogTester.InvokeMenuButtonAction` не доказывает открытие диалога | Оставить live-NX проверку; contract build не считать доказательством |
| **Разделение процессов** (HotkeyStudio vs Bridge) | Модули A/B/C на обеих сторонах | Зафиксировать inter-process контракт (schema 4, файлы) как неизменный; интерфейс внутри каждого процесса |
| **nxeskd** — separate product с полу-собранным статусом | Сложность и дублирование исполнения | Держать за адаптером модуля A; удалить v1 `CommandRequest` |

**Валидация каждого пошагового рефакторинга:** (1) `dotnet build` единого `.sln`; (2) `dotnet test` (`StateMachines`, `Protocol`, `HotkeyStudio`); (3) contract build против NXOpen stubs; (4) live-NX чек-лист `RUNTIME_V8.md`; (5) сравнение диффа с точки зрения "поведение не изменилось" (только перемещение методов, без изменения runtime-контракта).

---

## 5. Явные удаления

| Удаляется | Причина | Замена |
|---|---|---|
| Статическое мутное состояние в `Program` (`contextRevision`, `last*`, `isProcessing`) | Общее состояние A/B/D/E | Компоненты владеют своим состоянием (B — ревизия/статус, D — очередь, A — исполнение) |
| Reflection-шлюз, вшитый в god-object | Немоделируемая связь A/B/C | Изолированный `NxReflection`-helper за интерфейсом |
| unsigned nxeskd v1 `CommandRequest` | Дублирование с schema 4; канд-т на удаление по `NXESKD_INTEGRATION_PLAN` | `NxEskdCapabilityHandler` на schema 4 (`run_capability` + `capability_id`) |
| `Program` как единый диспетчер (god-object) | SRP-нарушение | `INxCommandExecutor` + `INxContextProvider` + security-гейт |
| Дублирование контекст-гвардов (`IsModalDialogActive` vs `IsFocusedInTextInput`) | Двойная реализация «можно ли вмешиваться» | Единый компонент модуля B |

---

## 6. Позитивные моменты (сохранить)

- **Готовый контракт** `NXKeys.Protocol` schema 4 — правильная, аутентифицированная граница; не ломать.
- **`NXKeys.StateMachines`** — выбранный слой уже изолирован и покрыт тестами; на него опирается модуль C.
- **Зрелая документация** (`RUNTIME_V8.md`, `ARCHITECTURE.md`, `api.md`, `SAFETY_MODEL.md`) — фиксирует приоритеты и канон.
- **Security-модель** (HMAC, anti-replay, allowlist, at-most-once) — солидная база.
- **CI-зрелость** (9 воркфлоу, включая contract build против stubs).

---

*Составлено по результатам аудита. Три целевых модуля — (A) выполнение команд в NX, (B) мониторинг состояния/контекста NX, (C) логика выбора отправляемой команды. Ядро модуля C уже существует в `NXKeys.StateMachines`; модули A/B требуют выноса из god-object'ов `Program`/`LeaderKeyEngine`/`SelectionIntentHotkeys`.*
