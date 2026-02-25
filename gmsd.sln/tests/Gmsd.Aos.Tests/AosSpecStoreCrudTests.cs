using System.Text;
using System.Text.Json;
using Gmsd.Aos.Engine.Paths;
using Gmsd.Aos.Engine.Spec;
using Gmsd.Aos.Engine.Stores;
using Gmsd.Aos.Engine.Workspace;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosSpecStoreCrudTests
{
    [Fact]
    public void SpecStore_CreateShowList_AndIndexesAreDeterministic()
    {
        var repoRoot = CreateTempDirectory("gmsd-aos-spec-store-crud");
        try
        {
            var aosRootPath = AosWorkspaceBootstrapper.EnsureInitialized(repoRoot).AosRootPath;
            var store = new AosSpecStore(aosRootPath);

            var doc = ParseJsonElement("{\"schemaVersion\":1,\"title\":\"x\"}");

            // Create out-of-order to validate ordinal sorting.
            store.WriteMilestoneIfMissing("MS-0002", doc);
            store.WriteMilestoneIfMissing("MS-0001", doc);

            store.WritePhaseIfMissing("PH-0002", doc);
            store.WritePhaseIfMissing("PH-0001", doc);

            store.WriteTaskIfMissing("TSK-000002", doc);
            store.WriteTaskIfMissing("TSK-000001", doc);

            store.WriteIssueIfMissing("ISS-0002", doc);
            store.WriteIssueIfMissing("ISS-0001", doc);

            store.WriteUatIfMissing("UAT-0002", doc);
            store.WriteUatIfMissing("UAT-0001", doc);

            // Show (read) paths
            Assert.Equal(JsonValueKind.Object, store.ReadMilestone("MS-0001").ValueKind);
            Assert.Equal(JsonValueKind.Object, store.ReadPhase("PH-0001").ValueKind);
            Assert.Equal(JsonValueKind.Object, store.ReadTask("TSK-000001").ValueKind);
            Assert.Equal(JsonValueKind.Object, store.ReadIssue("ISS-0001").ValueKind);
            Assert.Equal(JsonValueKind.Object, store.ReadUat("UAT-0001").ValueKind);

            // List (via catalog index)
            Assert.Equal(new[] { "MS-0001", "MS-0002" }, store.ReadCatalogIndex(AosArtifactKind.Milestone).Items);
            Assert.Equal(new[] { "PH-0001", "PH-0002" }, store.ReadCatalogIndex(AosArtifactKind.Phase).Items);
            Assert.Equal(new[] { "TSK-000001", "TSK-000002" }, store.ReadCatalogIndex(AosArtifactKind.Task).Items);
            Assert.Equal(new[] { "ISS-0001", "ISS-0002" }, store.ReadCatalogIndex(AosArtifactKind.Issue).Items);
            Assert.Equal(new[] { "UAT-0001", "UAT-0002" }, store.ReadCatalogIndex(AosArtifactKind.Uat).Items);

            // Issue/UAT routing is flat files (not folder-based).
            var issueContractPath = AosPathRouter.GetContractPath(AosArtifactKind.Issue, "ISS-0001");
            var uatContractPath = AosPathRouter.GetContractPath(AosArtifactKind.Uat, "UAT-0001");
            Assert.Equal(".aos/spec/issues/ISS-0001.json", issueContractPath);
            Assert.Equal(".aos/spec/uat/UAT-0001.json", uatContractPath);

            var issueFullPath = AosPathRouter.ToAosRootPath(aosRootPath, issueContractPath);
            var uatFullPath = AosPathRouter.ToAosRootPath(aosRootPath, uatContractPath);

            Assert.True(File.Exists(issueFullPath));
            Assert.True(File.Exists(uatFullPath));
            Assert.False(Directory.Exists(Path.Combine(aosRootPath, "spec", "issues", "ISS-0001")));
            Assert.False(Directory.Exists(Path.Combine(aosRootPath, "spec", "uat", "UAT-0001")));

            AssertDeterministicJsonFile(issueFullPath);
            AssertDeterministicJsonFile(uatFullPath);
        }
        finally
        {
            TryDeleteDirectory(repoRoot);
        }
    }

    [Fact]
    public void SpecStore_UpdateAndDelete_UpdatesIndexes()
    {
        var repoRoot = CreateTempDirectory("gmsd-aos-spec-store-update-delete");
        try
        {
            var aosRootPath = AosWorkspaceBootstrapper.EnsureInitialized(repoRoot).AosRootPath;
            var store = new AosSpecStore(aosRootPath);

            store.WriteIssueIfMissing("ISS-0001", ParseJsonElement("{\"schemaVersion\":1,\"status\":\"new\"}"));
            store.WriteIssueOverwrite("ISS-0001", ParseJsonElement("{\"schemaVersion\":2,\"status\":\"updated\"}"));

            var updated = store.ReadIssue("ISS-0001");
            Assert.Equal(2, updated.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("updated", updated.GetProperty("status").GetString());

            store.DeleteIssue("ISS-0001");

            var issueFullPath = AosPathRouter.ToAosRootPath(aosRootPath, ".aos/spec/issues/ISS-0001.json");
            Assert.False(File.Exists(issueFullPath));
            Assert.DoesNotContain("ISS-0001", store.ReadCatalogIndex(AosArtifactKind.Issue).Items);
        }
        finally
        {
            TryDeleteDirectory(repoRoot);
        }
    }

    [Fact]
    public void SpecStore_TaskSubArtifacts_RoundTrip()
    {
        var repoRoot = CreateTempDirectory("gmsd-aos-spec-store-task-sub-artifacts");
        try
        {
            var aosRootPath = AosWorkspaceBootstrapper.EnsureInitialized(repoRoot).AosRootPath;
            var store = new AosSpecStore(aosRootPath);

            store.WriteTaskPlanIfMissing("TSK-000001", ParseJsonElement("{\"schemaVersion\":1,\"steps\":[]}"));
            store.WriteTaskLinksOverwrite("TSK-000001", ParseJsonElement("{\"schemaVersion\":1,\"links\":[]}"));

            Assert.Equal(JsonValueKind.Object, store.ReadTaskPlan("TSK-000001").ValueKind);
            Assert.Equal(JsonValueKind.Object, store.ReadTaskLinks("TSK-000001").ValueKind);

            var planPath = AosPathRouter.ToAosRootPath(aosRootPath, ".aos/spec/tasks/TSK-000001/plan.json");
            var linksPath = AosPathRouter.ToAosRootPath(aosRootPath, ".aos/spec/tasks/TSK-000001/links.json");
            AssertDeterministicJsonFile(planPath);
            AssertDeterministicJsonFile(linksPath);
        }
        finally
        {
            TryDeleteDirectory(repoRoot);
        }
    }

    [Fact]
    public void SpecStore_ProjectAndRoadmap_RoundTrip()
    {
        var repoRoot = CreateTempDirectory("gmsd-aos-spec-store-project-roadmap");
        try
        {
            var aosRootPath = AosWorkspaceBootstrapper.EnsureInitialized(repoRoot).AosRootPath;
            var store = new AosSpecStore(aosRootPath);

            var project = new ProjectSpecDocument(
                SchemaVersion: 1,
                Project: new ProjectSpec(Name: "GMSD", Description: "Test")
            );

            store.WriteProjectOverwrite(project);
            Assert.Equal("GMSD", store.ReadProject().Project.Name);

            var roadmap = new RoadmapSpecDocument(
                SchemaVersion: 1,
                Roadmap: new RoadmapSpec(
                    Title: "T",
                    Items: new[] { new RoadmapItemSpec(Id: "ISS-0001", Title: "Item", Kind: "issue") }
                )
            );

            store.WriteRoadmapOverwrite(roadmap);
            Assert.Equal("T", store.ReadRoadmap().Roadmap.Title);

            AssertDeterministicJsonFile(AosPathRouter.ToAosRootPath(aosRootPath, AosSpecStore.ProjectContractPath));
            AssertDeterministicJsonFile(AosPathRouter.ToAosRootPath(aosRootPath, AosSpecStore.RoadmapContractPath));
        }
        finally
        {
            TryDeleteDirectory(repoRoot);
        }
    }

    private static JsonElement ParseJsonElement(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static void AssertDeterministicJsonFile(string fullPath)
    {
        Assert.True(File.Exists(fullPath), $"Expected file at '{fullPath}'.");

        var bytes = File.ReadAllBytes(fullPath);

        // Guardrails: no UTF-8 BOM and canonical trailing LF.
        Assert.False(
            bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            $"Expected no UTF-8 BOM in '{fullPath}'."
        );
        Assert.True(bytes.Length > 0 && bytes[^1] == (byte)'\n', $"Expected '{fullPath}' to end with LF.");

        // Ensure parseable JSON.
        _ = JsonDocument.Parse(File.ReadAllText(fullPath, new UTF8Encoding(false)));
    }

    private static string CreateTempDirectory(string prefix)
    {
        var root = Path.Combine(Path.GetTempPath(), prefix, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}

