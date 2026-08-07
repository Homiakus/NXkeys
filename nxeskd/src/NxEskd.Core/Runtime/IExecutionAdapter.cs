using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;

namespace NxEskd.Core.Runtime;

public interface IExecutionAdapter
{
    string? CurrentPartPath { get; }
    ModelSnapshot AnalyzeModel(ProfileDocument profile, ExecutionReport report);
    void Preview(ProfileDocument profile, DrawingPlan plan, ExecutionReport report);
    ValidationReport Execute(DrawingCommand command, ProfileDocument profile, DrawingPlan plan, ExecutionReport report);
    ValidationReport ValidateResult(ProfileDocument profile, DrawingPlan plan, ExecutionReport report);
    void Export(ProfileDocument profile, DrawingPlan plan, ExecutionReport report);
    void Inventory(ProfileDocument profile, ExecutionReport report);
}
