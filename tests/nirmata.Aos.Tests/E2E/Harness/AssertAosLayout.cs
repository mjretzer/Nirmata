namespace nirmata.Aos.Tests.E2E.Harness;

using System;
using System.IO;
using Xunit;

/// <summary>
/// Provides assertions for validating AOS workspace layout.
/// </summary>
public static class AssertAosLayout
{
    /// <summary>
    /// Asserts that all six AOS layers exist in the repository.
    /// </summary>
    /// <param name="repoRoot">The root path of the repository.</param>
    public static void AssertAllLayersExist(string repoRoot)
    {
        var aosPath = Path.Combine(repoRoot, ".aos");
        Assert.True(Directory.Exists(aosPath), $".aos/ directory does not exist at {aosPath}");

        // All 6 required layers
        AssertLayerExists(repoRoot, "schemas");
        AssertLayerExists(repoRoot, "spec");
        AssertLayerExists(repoRoot, "state");
        AssertLayerExists(repoRoot, "evidence");
        AssertLayerExists(repoRoot, "context");
        AssertLayerExists(repoRoot, "codebase");
        AssertLayerExists(repoRoot, "cache");
    }

    /// <summary>
    /// Asserts that a specific layer exists.
    /// </summary>
    /// <param name="repoRoot">The root path of the repository.</param>
    /// <param name="layerName">The name of the layer (e.g., "schemas", "spec").</param>
    public static void AssertLayerExists(string repoRoot, string layerName)
    {
        var layerPath = Path.Combine(repoRoot, ".aos", layerName);
        Assert.True(Directory.Exists(layerPath), $"Layer '{layerName}' does not exist at {layerPath}");
    }

    /// <summary>
    /// Asserts that a specific file exists within the AOS workspace.
    /// </summary>
    /// <param name="repoRoot">The root path of the repository.</param>
    /// <param name="relativePath">The relative path within .aos/ (e.g., "spec/project.json").</param>
    public static void AssertFileExists(string repoRoot, string relativePath)
    {
        var filePath = Path.Combine(repoRoot, ".aos", relativePath);
        Assert.True(File.Exists(filePath), $"File '{relativePath}' does not exist at {filePath}");
    }

    /// <summary>
    /// Asserts that the project.json spec file exists and is valid JSON.
    /// </summary>
    /// <param name="repoRoot">The root path of the repository.</param>
    public static void AssertProjectSpecExists(string repoRoot)
    {
        AssertFileExists(repoRoot, "spec/project.json");
    }
}
