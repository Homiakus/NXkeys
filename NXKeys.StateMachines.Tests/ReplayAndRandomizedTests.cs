using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NXKeys.Protocol;
using NXKeys.StateMachines;

namespace NXKeys.StateMachines.Tests
{
    internal static class ReplayAndRandomizedTests
    {
        [ModuleInitializer]
        internal static void Run()
        {
            ReplayIsDeterministic();
            RandomInputNeverViolatesIdleInvariant();
            DestructiveCommandNeverSkipsConfirmation();
            Console.WriteLine("[OK] Replay и randomized-инварианты автоматов.");
        }

        private static void ReplayIsDeterministic()
        {
            string[] events = { "activate", "M", "E", "queued", "completed", "activate", "search", "M", "back", "cancel" };
            string first = Replay(events);
            string second = Replay(events);
            Require(string.Equals(first, second, StringComparison.Ordinal), "Одинаковый event log дал разные переходы.");
        }

        private static string Replay(IEnumerable<string> events)
        {
            LeaderStateMachine machine = Machine(false);
            var trace = new List<string>();
            foreach (string value in events)
            {
                LeaderTransition transition;
                switch (value)
                {
                    case "activate": transition = machine.Activate(false, Context()); break;
                    case "queued": transition = machine.MarkRequestQueued("replay-request"); break;
                    case "completed": transition = machine.CompleteRequest(true, "OK"); break;
                    case "search": transition = machine.EnterSearch(); break;
                    case "back": transition = machine.Backspace(); break;
                    case "cancel": transition = machine.Cancel(); break;
                    default: transition = machine.InputToken(value); break;
                }
                trace.Add(transition.Action + ":" + transition.State + ":" + transition.Prefix);
            }
            return string.Join("|", trace);
        }

        private static void RandomInputNeverViolatesIdleInvariant()
        {
            var random = new Random(2512);
            for (int run = 0; run < 100; run++)
            {
                LeaderStateMachine machine = Machine(false);
                machine.Activate(false, Context());
                for (int step = 0; step < 200; step++)
                {
                    int choice = random.Next(8);
                    switch (choice)
                    {
                        case 0: machine.InputToken("M"); break;
                        case 1: machine.InputToken("E"); break;
                        case 2: machine.InputToken("X"); break;
                        case 3: machine.Backspace(); break;
                        case 4: machine.EnterSearch(); break;
                        case 5: machine.Timeout(); break;
                        case 6: machine.Cancel(); break;
                        case 7:
                            if (machine.State == LeaderState.Dispatching) machine.CompleteRequest(false, "random failure");
                            break;
                    }

                    if (machine.State == LeaderState.Idle)
                    {
                        Require(!machine.IsCapturing, "Idle не должен удерживать keyboard capture.");
                        machine.Activate(false, Context());
                    }
                }
            }
        }

        private static void DestructiveCommandNeverSkipsConfirmation()
        {
            LeaderStateMachine machine = Machine(true);
            machine.Activate(false, Context());
            machine.InputToken("D");
            LeaderTransition transition = machine.InputToken("X");
            Require(transition.Action == LeaderActionKind.RequireConfirmation, "Разрушительная команда миновала AwaitingConfirmation.");
            Require(machine.State == LeaderState.AwaitingConfirmation, "Разрушительная команда находится не в AwaitingConfirmation.");
        }

        private static LeaderStateMachine Machine(bool destructive)
        {
            var commands = new List<SequenceDefinition>
            {
                new SequenceDefinition
                {
                    Id = "ME",
                    Sequence = "ME",
                    ModuleId = "modeling",
                    CommandId = "UG_TEST_ME",
                    CommandName = "ME",
                    NeedsWorkPart = true,
                    Enabled = true
                }
            };
            if (destructive)
            {
                commands.Add(new SequenceDefinition
                {
                    Id = "DX",
                    Sequence = "DX",
                    ModuleId = "modeling",
                    CommandId = "UG_TEST_DX",
                    CommandName = "DX",
                    NeedsWorkPart = true,
                    Destructive = true,
                    ConfirmBeforeExecute = true,
                    Enabled = true
                });
            }
            return new LeaderStateMachine(
                new SequenceAutomaton(commands),
                new ContextGuardEvaluator(new LeaderBehaviorProfile()));
        }

        private static NxContextSnapshot Context()
        {
            return new NxContextSnapshot
            {
                SchemaVersion = NxProtocolConstants.SchemaVersion,
                Revision = 1,
                Status = "running",
                ApplicationId = "UG_APP_MODELING",
                ModuleId = "modeling",
                ModuleLabel = "Modeling",
                SelectionCount = 1,
                SelectionState = "single",
                WorkPartAvailable = true,
                DisplayPartAvailable = true,
                ContextConfidence = 100,
                UpdatedUtc = DateTimeOffset.UtcNow.ToString("O")
            };
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
