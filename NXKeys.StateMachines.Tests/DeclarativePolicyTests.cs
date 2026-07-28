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

            Console.WriteLine("[OK] Мнемонические guards, типы выбора и подтверждения.");
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
