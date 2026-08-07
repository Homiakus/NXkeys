namespace NxEskd.Core.Tests;

public sealed class RepositoryArchitectureAdditionalTests
{
    [Fact]
    public void ConfiguratorDoesNotUseModuleInitializerOrVisualTreePatching()
    {
        var root = LocateRepositoryRoot();
        var directory = Path.Combine(root, "src", "NxEskd.Configurator");
        var sources = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .ToArray();

        Assert.DoesNotContain(sources, item =>
            item.Text.Contains("[ModuleInitializer]", StringComparison.Ordinal)
            || item.Text.Contains("FindVisualChildren<", StringComparison.Ordinal)
            || item.Text.Contains("DispatcherPriority.ContextIdle", StringComparison.Ordinal));
    }

    [Fact]
    public void NxDependentProjectsAreMarkedExplicitly()
    {
        var root = LocateRepositoryRoot();
        var targets = File.ReadAllText(Path.Combine(root, "Directory.Build.targets"));
        var runtimeProject = File.ReadAllText(Path.Combine(root,
            "src", "NxEskd.NxRuntime", "NxEskd.NxRuntime.csproj"));
        var commandProps = File.ReadAllText(Path.Combine(root,
            "src", "NxEskd.Commands", "Directory.Build.props"));
        var configuratorProject = File.ReadAllText(Path.Combine(root,
            "src", "NxEskd.Configurator", "NxEskd.Configurator.csproj"));

        Assert.Contains("'$(RequiresNxOpen)' == 'true'", targets, StringComparison.Ordinal);
        Assert.Contains("<RequiresNxOpen>true</RequiresNxOpen>", runtimeProject, StringComparison.Ordinal);
        Assert.Contains("<RequiresNxOpen>true</RequiresNxOpen>", commandProps, StringComparison.Ordinal);
        Assert.DoesNotContain("RequiresNxOpen", configuratorProject, StringComparison.Ordinal);
    }

    private static string LocateRepositoryRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var current = new DirectoryInfo(Path.GetFullPath(start));
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "src", "NxEskd.Configurator")))
                    return current.FullName;
                current = current.Parent;
            }
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
