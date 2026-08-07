using NxEskd.Core.Planning;
using NxEskd.Core.Runtime;

namespace NxEskd.Core.Tests;

public sealed class DrawingPackageExecutorTests
{
    [Fact]
    public async Task PublishesAllDocumentsOnlyAfterSuccessfulStaging()
    {
        using var fixture = PackageExecutionFixture.Create();
        var plan = fixture.Plan();
        var executor = new FakeExecutor();

        var report = await new DrawingPackageExecutor().ExecuteAsync(plan, executor);

        Assert.True(report.Success);
        Assert.True(report.Published);
        Assert.Equal(new[] { "PART", "ASSEMBLY" }, executor.Calls);
        Assert.True(File.Exists(plan.DocumentsById["PART"].NativeOutputPath));
        Assert.True(File.Exists(plan.DocumentsById["ASSEMBLY"].NativeOutputPath));
        Assert.False(Directory.Exists(Path.Combine(plan.OutputDirectory, ".staging", plan.PackageId)));
    }

    [Fact]
    public async Task FailedDocumentPreventsPartialPublication()
    {
        using var fixture = PackageExecutionFixture.Create();
        var plan = fixture.Plan();
        var partFinal = plan.DocumentsById["PART"].NativeOutputPath;
        Directory.CreateDirectory(Path.GetDirectoryName(partFinal)!);
        File.WriteAllText(partFinal, "released-version");
        var executor = new FakeExecutor(failDocumentId: "ASSEMBLY");

        var report = await new DrawingPackageExecutor().ExecuteAsync(plan, executor);

        Assert.False(report.Success);
        Assert.False(report.Published);
        Assert.Equal("released-version", File.ReadAllText(partFinal));
        Assert.False(File.Exists(plan.DocumentsById["ASSEMBLY"].NativeOutputPath));
        Assert.Contains(report.Issues, issue => issue.Code == "PKG_DOCUMENT_EXECUTION_FAILED");
        Assert.Contains(report.Issues, issue => issue.Code == "PKG_INCOMPLETE_NOT_PUBLISHED");
    }

    [Fact]
    public async Task ResumeSkipsConfirmedStagedDocument()
    {
        using var fixture = PackageExecutionFixture.Create();
        var plan = fixture.Plan();
        var firstExecutor = new FakeExecutor(failDocumentId: "ASSEMBLY");
        var first = await new DrawingPackageExecutor().ExecuteAsync(plan, firstExecutor);
        Assert.False(first.Success);

        var secondExecutor = new FakeExecutor();
        var second = await new DrawingPackageExecutor().ExecuteAsync(plan, secondExecutor);

        Assert.True(second.Success);
        Assert.Equal(new[] { "ASSEMBLY" }, secondExecutor.Calls);
        Assert.Contains("Возобновлено по журналу", second.Documents["PART"].Message ?? string.Empty);
        Assert.True(File.Exists(plan.DocumentsById["PART"].NativeOutputPath));
        Assert.True(File.Exists(plan.DocumentsById["ASSEMBLY"].NativeOutputPath));
    }

    [Fact]
    public async Task DryRunDoesNotCallDocumentExecutorOrPublishFiles()
    {
        using var fixture = PackageExecutionFixture.Create();
        var plan = fixture.Plan(dryRun: true);
        var executor = new FakeExecutor();

        var report = await new DrawingPackageExecutor().ExecuteAsync(plan, executor);

        Assert.True(report.Success);
        Assert.False(report.Published);
        Assert.Empty(executor.Calls);
        Assert.All(report.Documents.Values,
            state => Assert.Equal(PackageDocumentStatus.Skipped, state.Status));
    }

    private sealed class FakeExecutor(string? failDocumentId = null) : IDrawingPackageDocumentExecutor
    {
        public List<string> Calls { get; } = [];

        public Task<PackageDocumentExecutionResult> ExecuteAsync(
            DrawingPackageDocumentPlan document,
            string nativeOutputPath,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(document.DocumentId);
            if (document.DocumentId.Equals(failDocumentId, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new PackageDocumentExecutionResult(false, "fixture failure"));

            Directory.CreateDirectory(Path.GetDirectoryName(nativeOutputPath)!);
            File.WriteAllText(nativeOutputPath, "generated:" + document.DocumentId);
            return Task.FromResult(new PackageDocumentExecutionResult(true, "fixture success"));
        }
    }

    private sealed class PackageExecutionFixture : IDisposable
    {
        private PackageExecutionFixture(string directory) => Directory = directory;

        public string Directory { get; }

        public static PackageExecutionFixture Create()
        {
            var directory = Path.Combine(Path.GetTempPath(),
                "nx-eskd-package-executor-tests-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            return new PackageExecutionFixture(directory);
        }

        public DrawingPackagePlan Plan(bool dryRun = false)
        {
            var output = Path.Combine(Directory, "output");
            var source = Path.Combine(Directory, "source.prt");
            var profile = Path.Combine(Directory, "profile.json");
            File.WriteAllText(source, "source");
            File.WriteAllText(profile, "{}");
            var documents = new[]
            {
                new DrawingPackageDocumentPlan(
                    "PART", source, profile, "A.001", "Деталь", "part_drawing",
                    Path.Combine(output, "A.001.prt"), []),
                new DrawingPackageDocumentPlan(
                    "ASSEMBLY", source, profile, "A.000СБ", "Сборка", "assembly_drawing",
                    Path.Combine(output, "A.000СБ.prt"), ["PART"])
            };
            return new DrawingPackagePlan(
                "PKG-EXEC",
                "Комплект",
                "A",
                null,
                output,
                documents,
                ["PART", "ASSEMBLY"],
                new PackagePublicationPolicy(
                    dryRun,
                    true,
                    true,
                    true,
                    true,
                    Path.Combine(output, "journal.json")));
        }

        public void Dispose()
        {
            try { System.IO.Directory.Delete(Directory, recursive: true); }
            catch { }
        }
    }
}
