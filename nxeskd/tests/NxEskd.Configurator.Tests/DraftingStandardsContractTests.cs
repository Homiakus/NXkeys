using System.Text.Json.Nodes;

namespace NxEskd.Configurator.Tests;

public sealed class DraftingStandardsContractTests
{
    [Fact]
    public void AllowedScalesAreNormalizedAndDeduplicated()
    {
        var root = new JsonObject();
        var changes = 0;
        var model = new DraftingStandardsModel(root, () => changes++);

        model.AllowedScales = "1:1, 1:2; 1:2\n1:5";

        var allowed = root["scalePolicy"]?["allowed"]?.AsArray()
            .Select(node => node?.GetValue<string>())
            .OfType<string>()
            .ToArray();
        Assert.Equal(new[] { "1:1", "1:2", "1:5" }, allowed);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void EmptyAllowedScaleListIsRejected()
    {
        var model = new DraftingStandardsModel(new JsonObject(), () => { });

        Assert.Throws<InvalidOperationException>(() => model.AllowedScales = " , ; ");
    }

    [Fact]
    public void InvalidLayoutValuesDoNotWriteInvalidProperties()
    {
        var root = new JsonObject();
        var model = new DraftingStandardsModel(root, () => { });

        Assert.Throws<ArgumentOutOfRangeException>(() => model.MinimumGapMm = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => model.MaxLayoutIterations = 0);

        var layout = root["layoutSolver"]?.AsObject();
        Assert.NotNull(layout);
        Assert.False(layout!.ContainsKey("minimumGapMm"));
        Assert.False(layout.ContainsKey("maxIterations"));
    }

    [Fact]
    public void TextStyleRejectsNonPositiveHeight()
    {
        var node = new JsonObject
        {
            ["id"] = "TEXT_MAIN",
            ["heightMm"] = 3.5
        };
        var item = new TextStyleEditorItem(node, () => { });

        Assert.Throws<ArgumentOutOfRangeException>(() => item.HeightMm = 0);
        Assert.Equal(3.5, node["heightMm"]?.GetValue<double>());
    }

    [Fact]
    public void TemplateOptionalObjectNamesAreRemovedWhenCleared()
    {
        var node = new JsonObject
        {
            ["id"] = "A3",
            ["borderObjectName"] = "BORDER",
            ["titleBlockObjectName"] = "TITLE"
        };
        var item = new TemplateEditorItem(node, () => { });

        item.BorderObjectName = "";
        item.TitleBlockObjectName = "   ";

        Assert.False(node.ContainsKey("borderObjectName"));
        Assert.False(node.ContainsKey("titleBlockObjectName"));
    }

    [Fact]
    public void RefreshNotifiesAllExpectedProperties()
    {
        var root = new JsonObject();
        var model = new DraftingStandardsModel(root, () => { });

        var notified = new List<string?>();
        model.PropertyChanged += (_, args) => notified.Add(args.PropertyName);
        model.Refresh();

        Assert.NotEmpty(notified);
        Assert.Contains(nameof(DraftingStandardsModel.ProjectionMethod), notified);
        Assert.Contains(nameof(DraftingStandardsModel.ArrowLengthMm), notified);
        Assert.Contains(nameof(DraftingStandardsModel.MinimumGapMm), notified);
        Assert.Contains(nameof(DraftingStandardsModel.AllowedScales), notified);
    }
}
