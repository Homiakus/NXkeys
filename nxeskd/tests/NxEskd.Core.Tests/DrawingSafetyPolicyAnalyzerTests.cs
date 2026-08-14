using System.Text.Json.Nodes;
using NxEskd.Core.Configuration;
using NxEskd.Core.Planning;

namespace NxEskd.Core.Tests;

public sealed class DrawingSafetyPolicyAnalyzerTests
{
    [Fact]
    public void ReleasedOverwriteRequiresExistingOverwritePermission()
    {
        using var fixture = ProfileFixture.Create();
        EnsureObject(fixture.Profile.Root, "output")["allowOverwriteExisting"] = false;
        EnsureObject(fixture.Profile.Root, "output")["allowOverwriteReleasedDocument"] = true;

        var report = new DrawingSafetyPolicyAnalyzer().Analyze(fixture.Profile);

        Assert.Contains(report.Issues, issue =>
            issue.Code == "SAFETY_RELEASED_OVERWRITE_REQUIRES_EXISTING_OVERWRITE");
    }

    [Fact]
    public void ManagedDeletionWithoutConfirmationProducesDiagnostic()
    {
        using var fixture = ProfileFixture.Create();
        var execution = EnsureObject(fixture.Profile.Root, "execution");
        var idempotency = EnsureObject(execution, "idempotency");
        idempotency["deleteManagedObjectsMissingFromConfig"] = true;
        idempotency["confirmManagedDeletion"] = false;

        var report = new DrawingSafetyPolicyAnalyzer().Analyze(fixture.Profile);

        Assert.Contains(report.Issues, issue =>
            issue.Code == "SAFETY_MANAGED_DELETION_NOT_CONFIRMED");
    }

    [Fact]
    public void NativeSaveAsRequiresPrtPath()
    {
        using var fixture = ProfileFixture.Create();
        var output = EnsureObject(fixture.Profile.Root, "output");
        output["saveMode"] = "save_as";
        var native = EnsureObject(output, "nativeDrawing");
        native["enabled"] = true;
        native["file"] = "drawing.pdf";

        var report = new DrawingSafetyPolicyAnalyzer().Analyze(fixture.Profile);

        Assert.Contains(report.Issues, issue =>
            issue.Code == "SAFETY_NATIVE_OUTPUT_EXTENSION_INVALID");
    }

    [Theory]
    [InlineData("ISSUED")]
    [InlineData("REL_PROD")]
    [InlineData("FROZEN")]
    [InlineData("OBSOLETE")]
    [InlineData("УТВЕРЖДЕНО")]
    public void PlmReleasedStatusesAreRecognizedAsReleased(string status)
    {
        using var fixture = ProfileFixture.Create();
        EnsureObject(fixture.Profile.Root, "output")["allowOverwriteExisting"] = true;
        EnsureObject(fixture.Profile.Root, "output")["allowOverwriteReleasedDocument"] = true;
        var job = EnsureObject(fixture.Profile.Root, "job");
        var doc = EnsureObject(job, "document");
        doc["status"] = status;

        var report = new DrawingSafetyPolicyAnalyzer().Analyze(fixture.Profile);

        Assert.Contains(report.Issues, issue =>
            issue.Code == "SAFETY_RELEASED_OVERWRITE_ARMED");
    }

    private static JsonObject EnsureObject(JsonObject owner, string name)
    {
        if (owner[name] is JsonObject existing) return existing;
        var created = new JsonObject();
        owner[name] = created;
        return created;
    }

    private sealed class ProfileFixture : IDisposable
    {
        private ProfileFixture(string directory, ProfileDocument profile)
        {
            Directory = directory;
            Profile = profile;
        }

        private string Directory { get; }
        public ProfileDocument Profile { get; }

        public static ProfileFixture Create()
        {
            var root = LocateRepositoryRoot();
            var config = Path.Combine(root, "config");
            var directory = Path.Combine(Path.GetTempPath(),
                "nx-eskd-safety-tests-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            var profilePath = Path.Combine(directory, "active-profile.json");
            File.Copy(Path.Combine(config, "active-profile.example.json"), profilePath);
            File.Copy(Path.Combine(config, "nx-eskd-profile.schema.json"),
                Path.Combine(directory, "nx-eskd-profile.schema.json"));
            return new ProfileFixture(directory, ProfileLoader.Load(profilePath));
        }

        public void Dispose()
        {
            try { System.IO.Directory.Delete(Directory, recursive: true); }
            catch { }
        }

        private static string LocateRepositoryRoot()
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
    }
}
