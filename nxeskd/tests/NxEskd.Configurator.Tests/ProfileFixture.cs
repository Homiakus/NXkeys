namespace NxEskd.Configurator.Tests;

internal sealed class ProfileFixture : IDisposable
{
    private ProfileFixture(string directory, string profilePath)
    {
        Directory = directory;
        ProfilePath = profilePath;
    }

    private string Directory { get; }
    public string ProfilePath { get; }

    public static ProfileFixture Create()
    {
        var root = TestRepositoryLocator.Locate();
        var config = Path.Combine(root, "config");
        var directory = Path.Combine(Path.GetTempPath(),
            "nx-eskd-configurator-tests-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var profilePath = Path.Combine(directory, "active-profile.json");
        File.Copy(Path.Combine(config, "active-profile.example.json"), profilePath);
        File.Copy(Path.Combine(config, "nx-eskd-profile.schema.json"),
            Path.Combine(directory, "nx-eskd-profile.schema.json"));
        return new ProfileFixture(directory, profilePath);
    }

    public void Dispose()
    {
        try { System.IO.Directory.Delete(Directory, recursive: true); }
        catch { }
    }
}
