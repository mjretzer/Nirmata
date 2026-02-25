using System;
using System.Linq;
using Gmsd.Aos.Engine.Schemas;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosValidateSchemasTests
{
    [Fact]
    public void LoadEmbeddedSchemas_RejectsNonCanonicalSchemaFilenames()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => AosEmbeddedSchemaRegistryLoader.LoadEmbeddedSchemas(typeof(AosValidateSchemasTests).Assembly)
        );

        Assert.Contains("Non-canonical schema filename 'context.pack.schema.json'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Expected 'context-pack.schema.json'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateEmbeddedSchemas_FlagsMalformedJson()
    {
        var schemas = new[]
        {
            new AosEmbeddedSchemaRegistryLoader.EmbeddedSchema(
                Id: "malformed",
                FileName: "malformed.schema.json",
                ResourceName: "test://malformed.schema.json",
                Json: "{"
            )
        };

        var report = AosSchemaPackValidator.ValidateEmbeddedSchemas(schemas);

        Assert.Equal(1, report.SchemaCount);
        Assert.Contains(
            report.Issues,
            i => i.SchemaFileName == "malformed.schema.json" && i.Message.StartsWith("Invalid JSON:", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void ValidateLocalSchemas_FlagsDuplicateSchemaIds()
    {
        const string schemaUri = "https://json-schema.org/draft/2020-12/schema";
        const string id = "gmsd:aos:schema:project:v1";

        var jsonA = $$"""
            {
              "$schema": "{{schemaUri}}",
              "$id": "{{id}}",
              "type": "object"
            }
            """;

        var jsonB = $$"""
            {
              "$schema": "{{schemaUri}}",
              "$id": "{{id}}",
              "type": "object"
            }
            """;

        var schemas = new[]
        {
            new AosLocalSchemaRegistryLoader.LocalSchema(
                Id: "ignored",
                FileName: "a.schema.json",
                FullPath: "c:\\temp\\a.schema.json",
                Json: jsonA
            ),
            new AosLocalSchemaRegistryLoader.LocalSchema(
                Id: "ignored",
                FileName: "b.schema.json",
                FullPath: "c:\\temp\\b.schema.json",
                Json: jsonB
            )
        };

        var report = AosSchemaPackValidator.ValidateLocalSchemas(schemas);

        Assert.Contains(
            report.Issues,
            i => i.SchemaFileName == "a.schema.json" && i.Message.Contains("Duplicate '$id' value", StringComparison.Ordinal)
        );
        Assert.Contains(
            report.Issues,
            i => i.SchemaFileName == "b.schema.json" && i.Message.Contains("Duplicate '$id' value", StringComparison.Ordinal)
        );
        Assert.Contains(
            report.Issues,
            i => i.Message.Contains("a.schema.json", StringComparison.Ordinal) &&
                 i.Message.Contains("b.schema.json", StringComparison.Ordinal) &&
                 i.Message.Contains(id, StringComparison.Ordinal)
        );
    }
}

