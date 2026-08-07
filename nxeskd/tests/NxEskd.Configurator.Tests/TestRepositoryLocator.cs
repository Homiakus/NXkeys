namespace NxEskd.Configurator.Tests;

internal static class TestRepositoryLocator
{
    public static string Locate()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var current = new DirectoryInfo(Path.GetFullPath(start));
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName,
                        "config", "active-profile.example.json")))
                    return current.FullName;
                current = current.Parent;
            }
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    public static string ConfiguratorDirectory()
        => Path.Combine(Locate(), "src", "NxEskd.Configurator");
}
