namespace NxEskd.Core.Tests;

public sealed class RepositoryArchitectureBoundaryTests
{
    [Fact]
    public void NativePublisherDoesNotReadDestructiveFlagsFromProfile()
    {
        var root = LocateRepositoryRoot();
        var path = Path.Combine(root, "src", "NxEskd.NxRuntime", "NxNativePartPublisher.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("plan.Publication", source, StringComparison.Ordinal);
        Assert.Contains("publication.AllowOverwriteExisting", source, StringComparison.Ordinal);
        Assert.Contains("publication.AllowOverwriteReleasedDocument", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "JsonNavigator.GetBool(context.Profile.Root, \"$.output.allowOverwriteExisting\"",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "JsonNavigator.GetBool(context.Profile.Root, \"$.output.allowOverwriteReleasedDocument\"",
            source,
            StringComparison.Ordinal);
    }

    private static string LocateRepositoryRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var current = new DirectoryInfo(Path.GetFullPath(start));
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "src", "NxEskd.NxRuntime")))
                    return current.FullName;
                current = current.Parent;
            }
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
