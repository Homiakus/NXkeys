using NxEskd.Core.Configuration;
using NxEskd.Core.Planning;

namespace NxEskd.Core.Tests;

public sealed class ProjectedDirectionTests
{
    [Fact]
    public void PlannerPreservesProjectedDirectionAndOperationPayload()
    {
        using var fixture = ProfileFixture.Create();

        var (plan, report) = new DrawingPlanner().Build(fixture.Profile, ModelSnapshot.Empty);

        Assert.False(report.HasErrors, string.Join(Environment.NewLine, report.Issues.Select(x => x.Message)));
        Assert.NotNull(plan);

        var projected = plan!.Sheets.SelectMany(sheet => sheet.Views)
            .Single(view => view.Id == "VIEW_TOP");
        Assert.Equal("top", projected.ProjectionDirection);

        var operation = plan.Operations.Single(item => item.OperationId == "view:VIEW_TOP");
        Assert.Equal("top", operation.Payload["direction"]?.ToString());

        var completeness = new DrawingCompletenessAnalyzer().Analyze(plan);
        Assert.DoesNotContain(completeness.Issues,
            issue => issue.Code == "PLAN_PROJECTED_DIRECTION_MISSING");
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
                "nx-eskd-direction-tests-" + Guid.NewGuid().ToString("N"));
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
