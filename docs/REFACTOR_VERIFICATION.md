# NXKeys — Чек-лист верификации рефакторинга (Windows)

**Контекст:** рефакторинг выполнен статически (хост = Android/Termux, нет .NET Windows SDK и NX),
поэтому финальная верификация обязательна на целевой Windows-машине / CI.
Этот документ — сводка что изменилось и что проверить.

---

## 1. Что изменилось (по слоям)

| Слой | Изменение | Файлы |
|---|---|---|
| Фаза 0 | Единый `NX2512.sln`, Protocol-контракт-тесты, module-контракты A/B/C, dedup ADR-0003→0004, split `NxCommandBridgeClient` на транспорт/политику | `NX2512.sln`, `NXKeys.Protocol.Tests/*`, `NXKeys.Protocol/ModuleContracts.cs`, `docs/adr/0003-authenticated-file-ipc.md→0004-*`, `NX2512_HotkeyStudio/Services/{NxCommandBridgeClient,INxQueueTransport,IRequestPolicy,NxFileQueueTransport,NxRequestSigningPolicy}.cs` |
| Модуль B (контекст) | `NxContextMonitor` (in-NX) + `NxContextClient` (out-NX) на `INxContextProvider`; каноника нормализации в `NxContextNormalization` (Protocol) | `NX2512_CommandBridge/NxContextMonitor.cs`, `NX2512_HotkeyStudio/Services/NxContextClient.cs`, `NXKeys.Protocol/NxContextNormalization.cs` |
| Модуль A (исполнение) | `NxMenuCommandExecutor` (кнопки/фильтр/capability) + `NxSelectionExecutor` (Selection Intent 0..4 + гварды); `Program.cs`/`SelectionIntentHotkeys.ts`/`LeaderKeyEngine.cs` делегируют | `NX2512_CommandBridge/{NxMenuCommandExecutor,NxSelectionExecutor}.cs`, рефактор `Program.cs` (1224→577), `SelectionIntentHotkeys.cs` (644→159), `LeaderKeyEngine.cs` |
| Модуль D (транспорт+security) | `INxQueueTransport`/`IRequestPolicy` + фасад | `NX2512_HotkeyStudio/Services/{INxQueueTransport,IRequestPolicy,NxFileQueueTransport,NxRequestSigningPolicy,NxCommandBridgeClient}.cs` |

god-object'ы декомпозированы: `Program.cs` 1224→577, `SelectionIntentHotkeys` 644→159, `LeaderKeyEngine` — после модуля B.
Перенос **не менял внешнее runtime-поведение** (schema 4, файловая очередь — неизменны; только перемещение методов + делегирование).

---

## 2. Обязательная проверка на Windows

### 2.1 Сборка
```bat
dotnet build NX2512.sln -c Release
```
- Должно собраться без ошибок. Чистые проекты (`NXKeys.Protocol`, `NXKeys.StateMachines*`, `NXKeys.Protocol.Tests`)
  собираются и на CI (в т.ч. contract build против NXOpen stubs). Проекты `net8.0-windows`+`$(NXOpenDir)` —
  на машине с NXOpen.

### 2.2 Тесты (консольные runner'ы)
```bat
dotnet run --project NXKeys.Protocol.Tests -c Release
dotnet run --project NXKeys.StateMachines.Tests -c Release
dotnet run --project NX2512_HotkeyStudio.Tests -c Release
```
- `NXKeys.Protocol.Tests` — ожидается **37** инвариантов (крипто/permission/JSON/контекст/нормализация/selection-filter).
- `NXKeys.StateMachines.Tests` — ожидается зелёный (автоматы/guards/policy).
- `NX2512_HotkeyStudio.Tests` — генерация конфига/мнемоник.

> Замечание: `StateMachines.Tests` в этом окружении требует перед сборкой `dotnet build-server shutdown`
> (снимает завис от ранее убитых сборок); на Windows/CI собирается штатно.

### 2.3 Live-NX регресс (по `RUNTIME_V8.md`)
1. Запуск Bridge в NX (`Start NXKeys Bridge`) → `bridge/context.json` пишется, ревизия растёт.
2. HotkeyStudio показывает HUD, контекст поступает из `NxContextClient` (в т.ч. fallback по заголовку окна).
3. Исполнение команды: `execute_command` через `NxMenuCommandExecutor` (кнопка, InvokeMenuButtonAction).
4. `switch_module` через `NxMenuCommandExecutor.SwitchModule` (reflection `ApplicationSwitchRequest` / fallback на кнопку).
5. `probe_command` возвращает availability/sensitivity.
6. Selection Intent 0..4 через `NxSelectionExecutor` (toggles + `TryExpandSelectedSeed`/`ScRuleFactory` late-bound).
7. capability `nxeskd.*` через `NxMenuCommandExecutor.ExecuteCapabilityRequest` (Assembly.LoadFrom + `ExecuteCapability`).
8. Отказ при модальном диалоге / устаревшем контексте / нет authenticated session.
9. Селект-фильтры `set_selection_filter` (нормализация через `NxContextNormalization`).

---

## 3. Известные follow-up (не сделано / требует Windows)

1. **~~`ContextGuardEvaluator.NormalizeModule` расходился с каноникой~~ — ИСПРАВЛЕНО (коммит `c09cab0`):**
   теперь делегирует в `NxContextNormalization.NormalizeModule` (`v8_sm`/`v8_sh` → `sheet_metal`).
   StateMachines.Tests собран и зелёный (exit 0).

2. **Точка 7 (консолидация гвардов):** in-NX гварды (`IsCurrentNxForeground`/`HasSystemModifier`/`IsFocusedInTextInput`)
   сейчас в `NxSelectionExecutor`; out-NX `LeaderKeyEngine.IsFocusedInTextInput` остаётся процесс-локальным
   (разные процессы — прямое шаринг ограничен). Рассмотреть общий `NxInterventionGuard` (context-снапшот
   несёт modal/status/security — уже единый).

3. **Adopt `INxCommandExecutor`** (в `ModuleContracts.cs`, сигнатуры с `ctx`+`NxCommandResult`):
   сейчас `NxMenuCommandExecutor`/`NxSelectionExecutor` — обычные классы без интерфейса; для приведения к интерфейсу
   нужна переработка claim/result-логики в `Program.ProcessClaim`.

4. **Фаза 3 (`ISelectionEngine`/тонкий `LeaderKeyEngine`):** интерфейс `Decide(CommandIntent, ctx)` архитектурно
   конфликтует с интерактивным HFSM (префикс-набор/подтверждение/await-result). Требует проектирования + сборки/тестов
   на Windows, чтобы сохранить поведение.

5. **Фаза 4 (чистка/тесты):** оставшиеся неиспользуемые using/символы (мелкие), + тесты на `NxContextMonitor`
   (fallback/freshness), `NxMenuCommandExecutor` (маппинг CommandId→кнопка/фильтры) — по возможности после появления
   компиляции.

---

## 4. Приоритеты доков
код/tests > канонические доки (`RUNTIME_V8.md`, `ARCHITECTURE.md`, `api.md`, `SAFETY_MODEL.md`) >
generated reports > historical. При расхождениях после массовых правок — сверить с каноном (scheme/error codes/версии).
