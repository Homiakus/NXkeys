using NXOpen;
using NxEskd.Core.Configuration;
using NxEskd.Core.Runtime;

namespace NxEskd.NxRuntime;

internal sealed class NxServiceContext
{
    private string? _fallbackScopeId;

    public required Session Session { get; init; }
    public required UI Ui { get; init; }
    public required Part WorkPart { get; init; }
    public required ProfileDocument Profile { get; init; }
    public required ExecutionReport Report { get; init; }
    public required NxLog Log { get; init; }
    public required NxApiMap ApiMap { get; init; }
    public required string RootDirectory { get; init; }
    public required string ConfigHash { get; init; }

    public required string ScopeId
    {
        get => JsonNavigator.GetString(Profile.Root, "$.job.jobId")
               ?? JsonNavigator.GetString(Profile.Root, "$.job.document.designation")
               ?? _fallbackScopeId
               ?? WorkPart.Name;
        init => _fallbackScopeId = value;
    }
}
