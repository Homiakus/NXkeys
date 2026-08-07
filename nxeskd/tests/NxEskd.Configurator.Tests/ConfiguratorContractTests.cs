using System.Text.Json.Nodes;
using NxEskd.Core.Planning;

namespace NxEskd.Configurator.Tests;

public sealed class ConfiguratorContractTests
{
    [Fact]
    public void InvalidTypedNumberDoesNotBecomeJsonString()
    {
        var setting = new EditableSetting(
            "$.layoutSolver.minimumGapMm",
            "minimumGapMm",
            "not-a-number",
            "number",
            "Minimum gap",
            _ => { });

        var exception = Assert.Throws<FormatException>(() => setting.ToJsonNode());

        Assert.Contains("ожидается конечное число", exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ViewTypeEditorUsesRuntimeSupportedKinds()
    {
        var values = SettingMetadataResolver.AllowedValues(
            "$.job.sheetPlan[0].views[0].type",
            "string");

        Assert.Equal(DrawingViewKinds.RuntimeSupported, values);
        Assert.Contains(DrawingViewKinds.HalfSection, values,
            StringComparer.OrdinalIgnoreCase);
        Assert.Contains(DrawingViewKinds.SteppedSection, values,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentModelWritesDestructiveFlagsToExpectedPaths()
    {
        var root = new JsonObject();
        var changes = 0;
        var model = new DocumentSettingsModel(root, () => changes++);

        model.AllowOverwriteExisting = true;
        model.AllowOverwriteReleasedDocument = true;
        model.DeleteManagedObjectsMissingFromConfig = true;
        model.ConfirmManagedDeletion = true;

        Assert.True(root["output"]?["allowOverwriteExisting"]?.GetValue<bool>());
        Assert.True(root["output"]?["allowOverwriteReleasedDocument"]?.GetValue<bool>());
        Assert.True(root["execution"]?["idempotency"]?
            ["deleteManagedObjectsMissingFromConfig"]?.GetValue<bool>());
        Assert.True(root["execution"]?["idempotency"]?
            ["confirmManagedDeletion"]?.GetValue<bool>());
        Assert.Equal(4, changes);
    }

    [Fact]
    public void ProfileStoreBlocksSemanticallyInvalidPlan()
    {
        using var fixture = ProfileFixture.Create();
        var store = new ProfileEditorDocument();
        store.Load(fixture.ProfilePath);
        var projected = store.Root["job"]?["sheetPlan"]?[0]?["views"]?[1]?.AsObject()
                        ?? throw new InvalidDataException("Projected fixture view missing.");
        projected.Remove("direction");

        var report = store.Validate();

        Assert.Contains(report.Issues, issue =>
            issue.Code == "PLAN_PROJECTED_DIRECTION_MISSING");
    }

    [Fact]
    public void ProfileStoreDoesNotPersistInvalidJsonType()
    {
        using var fixture = ProfileFixture.Create();
        var store = new ProfileEditorDocument();
        store.Load(fixture.ProfilePath);
        var before = File.ReadAllText(fixture.ProfilePath);
        var root = store.Root.DeepClone().AsObject();
        root["execution"]!["dryRun"] = "false";

        var report = store.Save(root.ToJsonString(), fixture.ProfilePath);

        Assert.True(report.HasErrors);
        Assert.Equal(before, File.ReadAllText(fixture.ProfilePath));
    }
}
