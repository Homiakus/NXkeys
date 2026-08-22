using System;
using System.Collections.Generic;

namespace NXKeys.Protocol
{
    /// <summary>
    /// Исход решения уровня выбора (Модуль C) — что сделать с учётом контекста и подтверждённого намерения.
    /// </summary>
    public enum CommandOutcome
    {
        Execute,
        SwitchModule,
        RequireConfirmation,
        Reject
    }

    /// <summary>Решение, возвращаемое Модулем C: какую команду отправить, либо переключить модуль/подтвердить/отклонить.</summary>
    public sealed class CommandDecision
    {
        public CommandOutcome Outcome { get; set; } = CommandOutcome.Reject;
        public string CommandId { get; set; } = string.Empty;
        public string Sequence { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
        public string TargetApplicationId { get; set; } = string.Empty;
        public string SelectionFilter { get; set; } = string.Empty;
        public bool RequiresConfirmation { get; set; }
        public string RejectReason { get; set; } = string.Empty;
    }

    /// <summary>Подтверждённое вводимое намерение (последовательность токенов) — вход Модуля C.</summary>
    public sealed class CommandIntent
    {
        public string Sequence { get; set; } = string.Empty;
        public string ModuleHint { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public string Actual { get; set; } = string.Empty; // диагностика
    }

    /// <summary>Кандидат команды для HUD-поиска (Модуль C).</summary>
    public sealed class CommandCandidate
    {
        public string CommandId { get; set; } = string.Empty;
        public string Sequence { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
        public int Score { get; set; }
    }

    /// <summary>
    /// Модуль B — канонический источник снапшота состояния системы/контекста NX.
    /// Единый контекст-модель (NxContextSnapshot); одна политика свежести/достоверности.
    /// </summary>
    public interface INxContextProvider
    {
        NxContextSnapshot? GetCurrent();
        bool TryGetFresh(out NxContextSnapshot ctx);
        bool IsBridgeReady { get; }
        event Action ContextChanged;
    }

    /// <summary>
    /// Модуль A — выполнение допущенного (аутентифицированного) запроса команды внутри NX
    /// либо через capability-адаптер (nxeskd). Не строит контекст и не выбирает команду.
    /// </summary>
    public interface INxCommandExecutor
    {
        NxCommandResult ExecuteCommand(NxCommandRequest request, NxContextSnapshot ctx);
        NxCommandResult ProbeCommand(NxCommandRequest request, NxContextSnapshot ctx);
        NxCommandResult SwitchModule(NxCommandRequest request, NxContextSnapshot ctx);
        NxCommandResult ApplySelectionFilter(NxCommandRequest request, NxContextSnapshot ctx);
    }

    /// <summary>
    /// Модуль C — решает, какую команду отправить (или переключить модуль / потребовать подтверждения / отклонить),
    /// на основе контекста (Модуль B) и подтверждённого намерения. Не выполняет IO.
    /// </summary>
    public interface ISelectionEngine
    {
        CommandDecision Decide(CommandIntent intent, NxContextSnapshot ctx);
        IReadOnlyList<CommandCandidate> Candidates(NxContextSnapshot ctx, string query);
    }
}
