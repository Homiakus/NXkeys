using System.Text.Json.Nodes;
using NxEskd.Core.Configuration;
using NxEskd.Core.Planning;

namespace NxEskd.Core.Tests;

public sealed class ResolvedPolicyPlanTests
{
    [Fact]
    public void PlannerCompilesExecutionAndPublicationPolicies()
    {
        using var fixture = ProfileFixture.Create();
        var execution = EnsureObject(fixture.Profile.Root, "execution");
        var idempotency = EnsureObject(execution, "idempotency");
        execution["preserveManualViewPositions"] = false;
        idempotency["deleteManagedObjectsMissingFromConfig"] = true;
        idempotency["confirmManagedDeletion"] = true;

        var output = EnsureObject(fixture.Profile.Root, "output");
        output["savePart"] = true;
        output["updateBeforeSave"] = false;
        output["saveMode"] = "save_as";
        output["allowOverwriteExisting"] = true;
        output["allowOverwriteReleasedDocument"] = false;
        var native = EnsureObject(output, "nativeDrawing");
        native["enabled"] = true;
        native["file"] = "out/test.prt";

        var (plan, report) = new DrawingPlanner().Build(fixture.Profile, ModelSnapshot.Empty);

        Assert.False(report.HasErrors, string.Join(Environment.NewLine, report.Issues.Select(issue => issue.Message)));
        Assert.NotNull(plan);
        Assert.False(plan!.ExecutionPolicy.PreserveManualViewPositions);
        Assert.True(plan.ExecutionPolicy.DeleteManagedObjectsMissingFromConfig);
        Assert.True(plan.ExecutionPolicy.ConfirmManagedDeletion);
        Assert.True(plan.Publication.SavePart);
        Assert.False(plan.Publication.UpdateBeforeSave);
        Assert.Equal("save_as", plan.Publication.SaveMode);
        Assert.True(plan.Publication.NativeDrawingEnabled);
        Assert.Equal("out/test.prt", plan.Publication.NativeDrawingFile);
        Assert.True(plan.Publication.AllowOverwriteExisting);
        Assert.False(plan.Publication.AllowOverwriteReleasedDocument);
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
                "nx-eskd-policy-tests-" + Guid.NewGuid().ToString("N"));
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
