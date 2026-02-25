using Gmsd.Agents.Execution.Evidence;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Evidence;

public sealed class DeterministicHashGeneratorTests
{
    [Fact]
    public void ComputeHashFromContent_SameContent_ProducesSameHash()
    {
        // Arrange
        var generator = new DeterministicHashGenerator();
        var toolCalls = "tool1\ntool2\n";
        var patch = "--- a\n+++ b\n";
        var summary = "{\"status\": \"success\"}";

        // Act
        var hash1 = generator.ComputeHashFromContent(toolCalls, patch, summary);
        var hash2 = generator.ComputeHashFromContent(toolCalls, patch, summary);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHashFromContent_DifferentContent_ProducesDifferentHash()
    {
        // Arrange
        var generator = new DeterministicHashGenerator();
        var toolCalls1 = "tool1\n";
        var toolCalls2 = "tool2\n";
        var patch = "--- a\n+++ b\n";
        var summary = "{\"status\": \"success\"}";

        // Act
        var hash1 = generator.ComputeHashFromContent(toolCalls1, patch, summary);
        var hash2 = generator.ComputeHashFromContent(toolCalls2, patch, summary);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHashFromContent_NormalizeLineEndings()
    {
        // Arrange
        var generator = new DeterministicHashGenerator();
        var toolCalls1 = "tool1\r\ntool2\r\n";
        var toolCalls2 = "tool1\ntool2\n";
        var patch = "--- a\n+++ b\n";
        var summary = "{\"status\": \"success\"}";

        // Act
        var hash1 = generator.ComputeHashFromContent(toolCalls1, patch, summary);
        var hash2 = generator.ComputeHashFromContent(toolCalls2, patch, summary);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void WriteHashFile_CreatesFile()
    {
        // Arrange
        var generator = new DeterministicHashGenerator();
        var tempFile = Path.GetTempFileName();
        var hash = "abc123def456";

        try
        {
            // Act
            generator.WriteHashFile(tempFile, hash);

            // Assert
            Assert.True(File.Exists(tempFile));
            var content = File.ReadAllText(tempFile);
            Assert.Equal(hash, content);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
