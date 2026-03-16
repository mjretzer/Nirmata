using nirmata.Aos.Engine.Registry;
using nirmata.Aos.Public;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Validation;

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
            "nirmata:aos:schema:phase-plan:v1",
            "nirmata:aos:schema:task-plan:v1",
            "nirmata:aos:schema:verifier-input:v1",
            "nirmata:aos:schema:verifier-output:v1",
            "nirmata:aos:schema:fix-plan:v1",
            "nirmata:aos:schema:diagnostic:v1"
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
