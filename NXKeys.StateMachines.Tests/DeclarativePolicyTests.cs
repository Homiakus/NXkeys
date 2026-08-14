using System;
using System.Runtime.CompilerServices;
using NXKeys.Protocol;
using NXKeys.StateMachines;

namespace NXKeys.StateMachines.Tests
{
    internal static class DeclarativePolicyTests
    {
        [ModuleInitializer]
        internal static void Run()
        {
            LeaderBehaviorProfile profile = LeaderBehaviorProfile.LoadDefault();
            Require(profile.Timeouts.RootMs == 4000, "root_ms не загружен из декларативного профиля.");
            Require(profile.Timeouts.PrefixMs == 2500, "prefix_ms не загружен из декларативного профиля.");
            Require(!string.IsNullOrWhiteSpace(profile.SourcePath), "Декларативный профиль не найден в output.");

            var evaluator = new ContextGuardEvaluator(profile);
            SequenceDefinition edgeBlend = Command("MEEB", "modeling");
            edgeBlend.RequiresSelection = true;
            edgeBlend.MinimumSelectionCount = 0;

            GuardResult noSelectionResult = evaluator.Evaluate(edgeBlend, Context("modeling", string.Empty, 0), true);
            Require(noSelectionResult.Allowed, "MEEB должен запускаться без preselection и открыть NX selection workflow.");

            GuardResult edgeResult = evaluator.Evaluate(edgeBlend, Context("modeling", "NXOpen.Edge"), true);
            Require(edgeResult.Allowed, "MEEB должен выполняться при выборе Edge.");

            SequenceDefinition sketchLine = Command("SCGL2", "sketch");
            GuardResult wrongModule = evaluator.Evaluate(sketchLine, Context("modeling", "NXOpen.Body"), true);
            Require(!wrongModule.Allowed, "Команда Sketch не должна выполняться в Modeling.");
            Require(!wrongModule.RequiresModuleSwitch, "Адаптивная команда не должна самовольно переключать модуль.");

            SequenceDefinition destructive = Command("AECR", "assembly");
            ResolvedCommandBehavior destructiveBehavior = profile.Resolve(destructive);
            Require(destructiveBehavior.ConfirmationRequired, "AECR должен требовать подтверждение из JSON.");

            SequenceDefinition preselect = Command("HS", "inspect_view");
            preselect.RequiresSelection = true;
            preselect.MinimumSelectionCount = 1;
            GuardResult preselectResult = evaluator.Evaluate(preselect, Context("inspect_view", string.Empty, 0), true);
            Require(!preselectResult.Allowed, "Команды над уже выбранным объектом должны блокироваться без выбора.");

            NxContextSnapshot lowConfidence = Context("modeling", "NXOpen.Edge");
            lowConfidence.ContextConfidence = 20;
            Require(!evaluator.Evaluate(edgeBlend, lowConfidence, true).Allowed, "Низкая достоверность контекста должна блокировать команду.");

            // Selection Intent 0..4 Admission Matrix Tests
            VerifySelectionIntentAdmissionMatrix();

            // Audit Fix Verification: SequenceAutomaton Search Ranking & Deterministic Tie-Break
            var searchCmd1 = new SequenceDefinition { Id = "1", Sequence = "ZM", CommandName = "Move", ModuleId = "UG_APP_MODELING", CommandId = "UG_CMD_MOVE_1" };
            var searchCmd2 = new SequenceDefinition { Id = "2", Sequence = "A", CommandName = "Extrude", ModuleId = "UG_APP_MODELING", CommandId = "UG_CMD_EXTRUDE_1" };
            var searchCmd3 = new SequenceDefinition { Id = "3", Sequence = "ZD", CommandName = "Move", ModuleId = "UG_APP_DRAFTING", CommandId = "UG_CMD_MOVE_2" };
            var searchAutomaton = new SequenceAutomaton(new[] { searchCmd1, searchCmd2, searchCmd3 });
            var searchResults = searchAutomaton.Search("Move");
            Require(searchResults.Count == 2 && searchResults[0].CommandName == "Move", "Поиск по имени команды должен отдавать приоритет точному совпадению имени.");
            Require(searchResults[0].ModuleId == "UG_APP_DRAFTING" && searchResults[1].ModuleId == "UG_APP_MODELING",
                "Tie-break при одинаковом Score должен детерминированно сортировать по Sequence / ModuleId.");

            // Audit Fix Verification: NxReplayGuard Reset on Sequence 1 & Memory Bounding
            var guard = new NxReplayGuard(128);
            var req1 = new NxCommandRequest
            {
                SessionId = Guid.NewGuid().ToString("N"),
                ClientInstanceId = Guid.NewGuid().ToString("N"),
                Nonce = Guid.NewGuid().ToString("N"),
                SequenceNumber = 50,
                CreatedUtc = DateTimeOffset.UtcNow.ToString("O"),
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5).ToString("O")
            };
            Require(guard.TryAccept(req1, out _), "First request should be accepted.");
            
            // Replay same sequence must fail
            var reqDuplicateSeq = new NxCommandRequest
            {
                SessionId = req1.SessionId,
                ClientInstanceId = req1.ClientInstanceId,
                Nonce = Guid.NewGuid().ToString("N"),
                SequenceNumber = 40,
                CreatedUtc = DateTimeOffset.UtcNow.ToString("O"),
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5).ToString("O")
            };
            Require(!guard.TryAccept(reqDuplicateSeq, out string seqError), "Lower sequence without restart must be rejected.");

            // Sequence 1 after restart must be accepted
            var reqRestart = new NxCommandRequest
            {
                SessionId = req1.SessionId,
                ClientInstanceId = req1.ClientInstanceId,
                Nonce = Guid.NewGuid().ToString("N"),
                SequenceNumber = 1,
                CreatedUtc = DateTimeOffset.UtcNow.ToString("O"),
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5).ToString("O")
            };
            Require(guard.TryAccept(reqRestart, out _), "Sequence 1 after client restart must be admitted.");

            // Audit Fix Verification: ContextGuardEvaluator Null Status Handling
            NxContextSnapshot nullStatusContext = Context("modeling", "NXOpen.Edge");
            nullStatusContext.Status = null;
            GuardResult nullStatusResult = evaluator.Evaluate(edgeBlend, nullStatusContext, true);
            Require(!nullStatusResult.Allowed, "Контекст с null статусом должен блокироваться.");
            Require(nullStatusResult.Reason.Contains("unknown"), "Сообщение об ошибке при null статусе должно содержать fallback 'unknown'.");

            Console.WriteLine("[OK] Мнемонические guards, типы выбора, подтверждения, матрица admission Selection Intent 0..4 и проверки хрупкости.");
        }

        private static void VerifySelectionIntentAdmissionMatrix()
        {
            var validBase = new SelectionIntentAdmissionContext
            {
                Intent = 2,
                IsNxForeground = true,
                HasSystemModifier = false,
                IsInjectedEvent = false,
                IsPhysicalRepeat = false,
                IsFocusedInTextInput = false,
                IsModalActive = false,
                HasWorkPart = true,
                IsNativeCollectorActive = true,
                SelectionCount = 0
            };

            // Valid base admission
            var res = SelectionIntentAdmissionEvaluator.Evaluate(validBase);
            Require(res.Admitted && !res.RequireSeedExpansion, "Base active collector intent 2 must be admitted without seed expansion.");

            // Foreground guard
            validBase.IsNxForeground = false;
            Require(!SelectionIntentAdmissionEvaluator.Evaluate(validBase).Admitted, "Non-foreground NX must be rejected.");
            validBase.IsNxForeground = true;

            // System modifier guard (Ctrl/Alt/Win)
            validBase.HasSystemModifier = true;
            Require(!SelectionIntentAdmissionEvaluator.Evaluate(validBase).Admitted, "Active system modifiers must be rejected.");
            validBase.HasSystemModifier = false;

            // Injected event guard
            validBase.IsInjectedEvent = true;
            Require(!SelectionIntentAdmissionEvaluator.Evaluate(validBase).Admitted, "Injected keyboard event must be rejected.");
            validBase.IsInjectedEvent = false;

            // Physical repeat guard
            validBase.IsPhysicalRepeat = true;
            Require(!SelectionIntentAdmissionEvaluator.Evaluate(validBase).Admitted, "Physical repeat must be rejected before real key-up.");
            validBase.IsPhysicalRepeat = false;

            // Text input guard
            validBase.IsFocusedInTextInput = true;
            Require(!SelectionIntentAdmissionEvaluator.Evaluate(validBase).Admitted, "Text/numeric input focus must reject intent and pass through.");
            validBase.IsFocusedInTextInput = false;

            // Modal dialog guard
            validBase.IsModalActive = true;
            Require(!SelectionIntentAdmissionEvaluator.Evaluate(validBase).Admitted, "Modal dialog active must reject intent.");
            validBase.IsModalActive = false;

            // Work part guard
            validBase.HasWorkPart = false;
            Require(!SelectionIntentAdmissionEvaluator.Evaluate(validBase).Admitted, "Missing work part must reject intent.");
            validBase.HasWorkPart = true;

            // Pass-through when no collector and no seed
            validBase.IsNativeCollectorActive = false;
            validBase.SelectionCount = 0;
            Require(!SelectionIntentAdmissionEvaluator.Evaluate(validBase).Admitted, "No collector and no seed must pass through numeric input.");

            // Seed-based expansion when collector inactive but seed geometry exists
            validBase.SelectionCount = 1;
            res = SelectionIntentAdmissionEvaluator.Evaluate(validBase);
            Require(res.Admitted && res.RequireSeedExpansion, "Seed geometry must trigger seed expansion when intent > 1.");

            // Intent 1 single object selection
            validBase.Intent = 1;
            validBase.SelectionCount = 3;
            res = SelectionIntentAdmissionEvaluator.Evaluate(validBase);
            Require(res.Admitted && res.ResetSelectionToLastOnly, "Intent 1 with multiple selection must request ResetSelectionToLastOnly.");

            validBase.SelectionCount = 1;
            res = SelectionIntentAdmissionEvaluator.Evaluate(validBase);
            Require(res.Admitted && !res.ResetSelectionToLastOnly, "Intent 1 with single selection does not need ResetSelectionToLastOnly.");

            // Intent 0 reset normal selection
            validBase.Intent = 0;
            res = SelectionIntentAdmissionEvaluator.Evaluate(validBase);
            Require(res.Admitted && !res.RequireSeedExpansion && !res.ResetSelectionToLastOnly, "Intent 0 must reset toggles cleanly.");
        }

        private static SequenceDefinition Command(string sequence, string module) => new SequenceDefinition
        {
            Id = sequence,
            Sequence = sequence,
            ModuleId = module,
            CommandId = "UG_TEST_" + sequence,
            CommandName = sequence,
            NeedsWorkPart = true,
            Enabled = true
        };

        private static NxContextSnapshot Context(string module, string selectedType, int selectionCount = 1)
        {
            var context = new NxContextSnapshot
            {
                SchemaVersion = NxProtocolConstants.SchemaVersion,
                Revision = 5,
                Status = "running",
                ApplicationId = "UG_APP_" + module.ToUpperInvariant(),
                ModuleId = module,
                ModuleLabel = module,
                SelectionCount = selectionCount,
                SelectionState = selectionCount < 0 ? "unknown" : selectionCount == 0 ? "none" : selectionCount == 1 ? "single" : "multiple",
                WorkPartAvailable = true,
                DisplayPartAvailable = true,
                ModalDialogActive = false,
                ContextConfidence = 100,
                UpdatedUtc = DateTimeOffset.UtcNow.ToString("O")
            };
            if (!string.IsNullOrWhiteSpace(selectedType)) context.SelectedTypes.Add(selectedType);
            return context;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
