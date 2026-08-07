using NXOpen;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;
using NxEskd.Core.Runtime;

namespace NxEskd.NxRuntime;

public sealed class NxInventoryAdapter : IExecutionAdapter
{
    private readonly Session _session = Session.GetSession();
    private readonly string _rootDirectory;

    public NxInventoryAdapter(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    public string? CurrentPartPath => _session.Parts.Work?.FullPath;

    public void Inventory(ProfileDocument profile, ExecutionReport report)
        => NxCapabilityScanner.WriteInventory(_session, _rootDirectory, report);

    public ModelSnapshot AnalyzeModel(ProfileDocument profile, ExecutionReport report)
        => throw Unsupported();

    public void Preview(ProfileDocument profile, DrawingPlan plan, ExecutionReport report)
        => throw Unsupported();

    public ValidationReport Execute(DrawingCommand command, ProfileDocument profile, DrawingPlan plan, ExecutionReport report)
        => throw Unsupported();

    public ValidationReport ValidateResult(ProfileDocument profile, DrawingPlan plan, ExecutionReport report)
        => throw Unsupported();

    public void Export(ProfileDocument profile, DrawingPlan plan, ExecutionReport report)
        => throw Unsupported();

    private static InvalidOperationException Unsupported()
        => new("NxInventoryAdapter поддерживает только команду Inventory.");
}
