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
            SequenceDefinition edgeBlend = Command("MX", "modeling");
            edgeBlend.RequiresSelection = true;

            GuardResult faceResult = evaluator.Evaluate(edgeBlend, Context("modeling", "NXOpen.Face"), true);
            Require(!faceResult.Allowed, "MX не должен выполняться при выборе только Face.");
            Require(faceResult.Reason.Contains("рёбер", StringComparison.OrdinalIgnoreCase), "Не применено сообщение выбора рёбер.");

            GuardResult edgeResult = evaluator.Evaluate(edgeBlend, Context("modeling", "NXOpen.Edge"), true);
            Require(edgeResult.Allowed, "MX должен выполняться при выборе Edge.");

            SequenceDefinition sketchLine = Command("SW", "sketch");
            GuardResult wrongModule = evaluator.Evaluate(sketchLine, Context("modeling", "NXOpen.Body"), true);
            Require(!wrongModule.Allowed, "Команда Sketch не должна выполняться в Modeling.");
            Require(!wrongModule.RequiresModuleSwitch, "Адаптивная команда не должна самовольно переключать модуль.");

            SequenceDefinition destructive = Command("AX", "assembly");
            ResolvedCommandBehavior destructiveBehavior = profile.Resolve(destructive);
            Require(destructiveBehavior.ConfirmationRequired, "AX должен требовать подтверждение из JSON.");

            NxContextSnapshot lowConfidence = Context("modeling", "NXOpen.Edge");
            lowConfidence.ContextConfidence = 20;
            Require(!evaluator.Evaluate(edgeBlend, lowConfidence, true).Allowed, "Низкая достоверность контекста должна блокировать команду.");

            Console.WriteLine("[OK] Адаптивные guards, типы выбора и подтверждения.");
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

        private static NxContextSnapshot Context(string module, string selectedType)
        {
            var context = new NxContextSnapshot
            {
                SchemaVersion = NxProtocolConstants.SchemaVersion,
                Revision = 5,
                Status = "running",
                ApplicationId = "UG_APP_" + module.ToUpperInvariant(),
                ModuleId = module,
                ModuleLabel = module,
                SelectionCount = 1,
                SelectionState = "single",
                WorkPartAvailable = true,
                DisplayPartAvailable = true,
                ModalDialogActive = false,
                ContextConfidence = 100,
                UpdatedUtc = DateTimeOffset.UtcNow.ToString("O")
            };
            context.SelectedTypes.Add(selectedType);
            return context;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
