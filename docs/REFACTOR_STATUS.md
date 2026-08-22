# NXKeys — Статус рефакторинга

Статус на момент пуша: декомпозиция модульного монолита NXKeys по плану `docs/REFACTOR_PLAN_3MODULES.md`.
Рефакторинг выполнен статически (хост без .NET Windows SDK / NX) — поведение не менялось,
плюс исправлено 2 дефекта. Верификация чистых слоёв — в этой среде; конечная — на Windows/CI.

---

## ✅ Что сделано

### Фаза 0 — подготовка
- Единый `NX2512.sln` (раньше основные проекты собирались по отдельным csproj).
- Тест-проект `NXKeys.Protocol.Tests` (контракт IPC schema 4: крипто/permission/JSON/контекст) — 35 инвариантов.
- Module-контракты A/B/C в `NXKeys.Protocol/ModuleContracts.cs` (`INxContextProvider`, `INxCommandExecutor`, `ISelectionEngine`, `CommandDecision`/`CommandIntent`/`CommandCandidate`).
- Dedup ADR: `0003-authenticated-file-ipc.md` → `0004-authenticated-file-ipc.md` (V8-ADR остался 0003).
- `docs/REFACTOR_PLAN_3MODULES.md` (аудит + план).

### Модуль B — контекст (пункт 2 плана)
- In-NX: `NX2512_CommandBridge/NxContextMonitor.cs` — канонический владелец `NxContextSnapshot`
  (application module, selection, work/display part, modal, ревизия/фингерпринт, атомарная запись context.json).
- Out-of-NX: `NX2512_HotkeyStudio/Services/NxContextClient.cs` — чтение bridge/context.json + fallback по заголовку окна,
  единая политика свежести. Оба реализуют `INxContextProvider`.
- `Program.cs`/`LeaderKeyEngine.cs` переведены на контекст-провайдер; дубликаты удалены.
- Каноника нормализации: `NXKeys.Protocol/NxContextNormalization.cs` (NormalizeModule, v8-префиксы, app↔module,
  window-title→module, label, selection-filter) — единый источник для обеих сторон процесса.

### Модуль A — исполнение (пункт 3 плана)
- `NX2512_CommandBridge/NxMenuCommandExecutor.cs` — кнопки/MenuScript, Selection Intent-фильтры, switch модуля,
  capability-адаптер (nxeskd Assembly.LoadFrom + ExecuteCapability).
- `NX2512_CommandBridge/NxSelectionExecutor.cs` — применение Selection Intent 0..4 (нативные toggles +
  ScRuleFactory late-bound) + admission-гварды.
- `NX2512_CommandBridge/NxInterventionGuard.cs` — единый in-NX гвард «можно ли вмешиваться»
  (IsCurrentNxForeground/HasSystemModifier/IsFocusedInTextInput + P/Invoke).
- `Program.cs` 1224→577 строк (ProcessClaim → executor), `SelectionIntentHotkeys` 644→159 (только hook, делегирует).

### Модуль D — транспорт + security
- `NX2512_HotkeyStudio/Services/NxCommandBridgeClient.cs` разнесён на
  `INxQueueTransport`/`IRequestPolicy` (+ `NxFileQueueTransport`, `NxRequestSigningPolicy`), фасад — тонкая обёртка.

### Исправленные дефекты
- `ContextGuardEvaluator.NormalizeModule` (StateMachines) расходился с каноникой по `v8_sm`/`v8_sh`
  (было `modeling`, стало `sheet_metal`) — сведён к канонике (`c09cab0`), закреплён тестом (`623d702`).

### Верификация (чистые слои, в этой среде)
- `NXKeys.Protocol.Tests` — **37/37** зелёные (крипто/permission/JSON/контекст/нормализация/selection-filter).
- `NXKeys.StateMachines.Tests` — сборка 0 ошибок (после `dotnet build-server shutdown`), run exit 0
  (автоматы/guards/policy + новый тест каноники).

---

## ⚠️ Что осталось (требует Windows/CI или явного риска)

1. **Adopt `INxCommandExecutor`** (sig `NxCommandResult`+`ctx`): `NxMenuCommandExecutor`/`NxSelectionExecutor`
   сейчас обычные классы; приведение к интерфейсу требует смены семантики throw→result в `Program.ProcessClaim`
   (риск; файлы не собираются здесь).
2. **Фаза 3 `ISelectionEngine`/тонкий `LeaderKeyEngine`**: интерфейс `Decide(CommandIntent, ctx)`
   архитектурно конфликтует с интерактивным HFSM (префикс-набор/подтверждение/await-result).
   Нужно проектирование + Windows (живые тесты), чтобы сохранить поведение.
3. **Точка 7 (out-of-NX)**: `LeaderKeyEngine.IsFocusedInTextInput` процесс-локален (другой процесс) —
   прямое шаринг с in-NX `NxInterventionGuard` ограничен архитектурно.
4. **Фаза 3/4 тесты**: `NxContextMonitor` (fallback/freshness), `NxMenuCommandExecutor`
   (маппинг CommandId→кнопка/фильтр) — по возможности после появления компиляции.
5. **Фаза 4**: мелкая чистка неиспользуемых using/символов в перенесённых файлах.

---

## 🔍 Windows-верификация
Полный чек-лист — `docs/REFACTOR_VERIFICATION.md` (сборка `NX2512.sln`, `dotnet run` для трёх тест-проектов,
live-NX регресс по `RUNTIME_V8.md`: контекст, исполнение, switch, probe, Selection Intent 0..4, capability,
отказы, фильтры).
