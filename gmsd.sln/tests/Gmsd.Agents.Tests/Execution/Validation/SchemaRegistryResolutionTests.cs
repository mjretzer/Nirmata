using Gmsd.Aos.Engine.Registry;
using Gmsd.Aos.Public;
using Moq;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Validation;

public class SchemaRegistryResolutionTests
{
    [Fact]
    public void Registry_ShouldResolveAllCanonicalSchemas()
    {
        // Arrange
        var mockWorkspace = new Mock<IWorkspace>();
        mockWorkspace.Setup(w => w.RepositoryRootPath).Returns(Path.GetTempPath());
        
        var registry = new SchemaRegistry(mockWorkspace.Object);
        var expectedSchemas = new[]
        {
            "gmsd:aos:schema:phase-plan:v1",
            "gmsd:aos:schema:task-plan:v1",
            "gmsd:aos:schema:verifier-input:v1",
            "gmsd:aos:schema:verifier-output:v1",
            "gmsd:aos:schema:fix-plan:v1",
            "gmsd:aos:schema:diagnostic:v1"
        };

        // Act & Assert
        foreach (var schemaId in expectedSchemas)
        {
            var schema = registry.GetSchema(schemaId);
            Assert.NotNull(schema);
            Assert.Equal(schemaId, schema.RootElement.GetProperty("$id").GetString());
        }
    }
}
