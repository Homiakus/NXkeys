using System.Xml.Linq;

namespace NxEskd.Configurator.Tests;

public sealed class XamlContractTests
{
    private static readonly XNamespace XamlNamespace =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void EveryXamlClassHasMatchingPartialCodeBehind()
    {
        var directory = TestRepositoryLocator.ConfiguratorDirectory();
        foreach (var xamlPath in Directory.EnumerateFiles(directory, "*.xaml",
                     SearchOption.TopDirectoryOnly))
        {
            var document = XDocument.Load(xamlPath, LoadOptions.SetLineInfo);
            var className = document.Root?.Attribute(XamlNamespace + "Class")?.Value;
            Assert.False(string.IsNullOrWhiteSpace(className),
                $"x:Class missing: {xamlPath}");

            var codeBehindPath = xamlPath + ".cs";
            Assert.True(File.Exists(codeBehindPath),
                $"Code-behind missing for {xamlPath}");
            var source = File.ReadAllText(codeBehindPath);
            var shortName = className!.Split('.').Last();
            Assert.Contains("partial class " + shortName, source,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void XamlEventHandlersExistInPartialClassSources()
    {
        var directory = TestRepositoryLocator.ConfiguratorDirectory();
        var partialSources = Directory.EnumerateFiles(directory, "*.cs",
                SearchOption.TopDirectoryOnly)
            .ToDictionary(file => Path.GetFileName(file)!, file => File.ReadAllText(file),
                StringComparer.OrdinalIgnoreCase);

        foreach (var xamlPath in Directory.EnumerateFiles(directory, "*.xaml",
                     SearchOption.TopDirectoryOnly))
        {
            var document = XDocument.Load(xamlPath);
            var className = document.Root?.Attribute(XamlNamespace + "Class")?.Value;
            if (string.IsNullOrWhiteSpace(className)) continue;
            var shortName = className.Split('.').Last();
            var relevantSources = partialSources
                .Where(pair => pair.Value.Contains("partial class " + shortName,
                    StringComparison.Ordinal))
                .Select(pair => pair.Value)
                .ToArray();
            Assert.NotEmpty(relevantSources);
            var combined = string.Join(Environment.NewLine, relevantSources);

            var handlers = document.DescendantsAndSelf()
                .Attributes()
                .Where(attribute => IsEventAttribute(attribute.Name.LocalName))
                .Select(attribute => attribute.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            foreach (var handler in handlers)
                Assert.Contains(handler + "(", combined, StringComparison.Ordinal);
        }
    }

    private static bool IsEventAttribute(string name)
        => name is "Click" or "Checked" or "Unchecked" or "LostFocus"
            or "SelectionChanged" or "SelectedItemChanged" or "TextChanged"
            or "Closing" or "PreviewKeyDown" or "Loaded";
}
