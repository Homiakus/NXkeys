using System.Text.Json;
using NxEskd.Core.Planning;

namespace NxEskd.Core.Tests;

public sealed class DrawingPackagePlannerTests
{
    [Fact]
    public void OrdersDependenciesBeforeAssemblyDocument()
    {
        using var fixture = PackageFixture.Create();
        var manifest = fixture.WriteManifest(new
        {
            packageId = "PKG-001",
            outputDirectory = "out",
            publication = new { atomicPublish = true },
            documents = new object[]
            {
                fixture.Document("ASSEMBLY", "A.000СБ", new[] { "PART-1", "PART-2" }, "assembly_drawing"),
                fixture.Document("PART-2", "A.002", Array.Empty<string>(), "part_drawing"),
                fixture.Document("PART-1", "A.001", Array.Empty<string>(), "part_drawing")
            }
        });

        var (plan, report) = new DrawingPackagePlanner().Build(manifest);

        Assert.False(report.HasErrors);
        Assert.NotNull(plan);
        Assert.True(IndexOf(plan!, "PART-1") < IndexOf(plan!, "ASSEMBLY"));
        Assert.True(IndexOf(plan!, "PART-2") < IndexOf(plan!, "ASSEMBLY"));
    }

    [Fact]
    public void DuplicateDocumentIdsProduceDiagnosticInsteadOfException()
    {
        using var fixture = PackageFixture.Create();
        var manifest = fixture.WriteManifest(new
        {
            packageId = "PKG-002",
            outputDirectory = "out",
            documents = new object[]
            {
                fixture.Document("DUPLICATE", "A.001", Array.Empty<string>(), "part_drawing"),
                fixture.Document("DUPLICATE", "A.002", Array.Empty<string>(), "part_drawing")
            }
        });

        var exception = Record.Exception(() => new DrawingPackagePlanner().Build(manifest));
        var (plan, report) = new DrawingPackagePlanner().Build(manifest);

        Assert.Null(exception);
        Assert.Null(plan);
        Assert.Contains(report.Issues, issue => issue.Code == "PKG_DUPLICATE_DOCUMENT_ID");
    }

    [Fact]
    public void DependencyCycleBlocksPackagePlan()
    {
        using var fixture = PackageFixture.Create();
        var manifest = fixture.WriteManifest(new
        {
            packageId = "PKG-003",
            outputDirectory = "out",
            documents = new object[]
            {
                fixture.Document("A", "A.001", new[] { "B" }, "part_drawing"),
                fixture.Document("B", "A.002", new[] { "A" }, "part_drawing")
            }
        });

        var (plan, report) = new DrawingPackagePlanner().Build(manifest);

        Assert.Null(plan);
        Assert.Contains(report.Issues, issue => issue.Code == "PKG_DEPENDENCY_CYCLE");
    }

    [Fact]
    public void AtomicPublicationRejectsOutputOutsidePackageDirectory()
    {
        using var fixture = PackageFixture.Create();
        var outside = Path.Combine(fixture.Directory, "outside", "A.001.prt");
        var document = fixture.Document("PART", "A.001", Array.Empty<string>(), "part_drawing");
        document["nativeOutput"] = outside;
        var manifest = fixture.WriteManifest(new
        {
            packageId = "PKG-004",
            outputDirectory = "out",
            publication = new { atomicPublish = true },
            documents = new object[] { document }
        });

        var (plan, report) = new DrawingPackagePlanner().Build(manifest);

        Assert.Null(plan);
        Assert.Contains(report.Issues, issue => issue.Code == "PKG_ATOMIC_OUTPUT_OUTSIDE_ROOT");
    }

    private static int IndexOf(DrawingPackagePlan plan, string id)
        => plan.ExecutionOrder.Select((value, index) => (value, index))
            .Single(pair => pair.value == id).index;

    private sealed class PackageFixture : IDisposable
    {
        private PackageFixture(string directory)
        {
            Directory = directory;
            System.IO.Directory.CreateDirectory(Path.Combine(directory, "parts"));
            System.IO.Directory.CreateDirectory(Path.Combine(directory, "profiles"));
        }

        public string Directory { get; }

        public static PackageFixture Create()
            => new(Path.Combine(Path.GetTempPath(), "nx-eskd-package-tests-" + Guid.NewGuid().ToString("N")));

        public Dictionary<string, object?> Document(
            string id,
            string designation,
            IReadOnlyList<string> dependencies,
            string kind)
        {
            var sourceRelative = Path.Combine("parts", id + ".prt");
            var profileRelative = Path.Combine("profiles", id + ".json");
            File.WriteAllText(Path.Combine(Directory, sourceRelative), "fixture");
            File.WriteAllText(Path.Combine(Directory, profileRelative), "{}");
            return new Dictionary<string, object?>
            {
                ["documentId"] = id,
                ["sourcePart"] = sourceRelative,
                ["profilePath"] = profileRelative,
                ["designation"] = designation,
                ["name"] = id,
                ["documentKind"] = kind,
                ["nativeOutput"] = designation + ".prt",
                ["dependencies"] = dependencies
            };
        }

        public string WriteManifest(object manifest)
        {
            var path = Path.Combine(Directory, "package.json");
            File.WriteAllText(path, JsonSerializer.Serialize(manifest));
            return path;
        }

        public void Dispose()
        {
            try { System.IO.Directory.Delete(Directory, recursive: true); }
            catch { }
        }
    }
}
