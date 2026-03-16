using System.Text;
using System.Text.Json;
using nirmata.Aos.Engine;
using nirmata.Aos.Public;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class DeterministicJsonOptionsTests
{
    [Fact]
    public void Standard_ProducesStableKeyOrdering_AndLfEndings()
    {
        var serializer = new DeterministicJsonSerializer();

        // Same semantic data, different insertion order
        var valueA = new Dictionary<string, object?>
        {
            ["z"] = 3,
            ["a"] = 1,
            ["m"] = 2
        };

        var valueB = new Dictionary<string, object?>
        {
            ["m"] = 2,
            ["z"] = 3,
            ["a"] = 1
        };

        var bytesA = serializer.Serialize(valueA, DeterministicJsonOptions.Standard);
        var bytesB = serializer.Serialize(valueB, DeterministicJsonOptions.Standard);

        // Should produce identical output
        Assert.Equal(bytesA, bytesB);

        // Verify LF endings (not CRLF)
        var json = Encoding.UTF8.GetString(bytesA);
        Assert.DoesNotContain("\r\n", json);
        Assert.EndsWith("\n", json);

        // Verify stable key ordering (alphabetical)
        using var doc = JsonDocument.Parse(json);
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(new[] { "a", "m", "z" }, keys);
    }

    [Fact]
    public void Indented_ProducesStableKeyOrdering_AndLfEndings()
    {
        var serializer = new DeterministicJsonSerializer();

        var value = new Dictionary<string, object?>
        {
            ["c"] = 3,
            ["a"] = 1,
            ["b"] = 2
        };

        var bytes = serializer.Serialize(value, DeterministicJsonOptions.Indented, writeIndented: true);
        var json = Encoding.UTF8.GetString(bytes);

        // Verify LF endings (not CRLF)
        Assert.DoesNotContain("\r\n", json);
        Assert.EndsWith("\n", json);

        // Verify stable key ordering (alphabetical)
        using var doc = JsonDocument.Parse(json);
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(new[] { "a", "b", "c" }, keys);

        // Verify it's actually indented
        Assert.Contains("\n  \"", json);
    }

    [Fact]
    public void Standard_UsesCamelCaseNaming()
    {
        var serializer = new DeterministicJsonSerializer();

        var value = new TestRecord { PropertyName = "value", AnotherProperty = 42 };
        var bytes = serializer.Serialize(value, DeterministicJsonOptions.Standard);
        var json = Encoding.UTF8.GetString(bytes);

        Assert.Contains("\"propertyName\"", json);
        Assert.Contains("\"anotherProperty\"", json);
        Assert.DoesNotContain("\"PropertyName\"", json);
    }

    [Fact]
    public void Standard_IsConfiguredWithExpectedSettings()
    {
        var options = DeterministicJsonOptions.Standard;

        Assert.Equal(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.True(options.PropertyNameCaseInsensitive);
        Assert.False(options.WriteIndented);
    }

    [Fact]
    public void Indented_IsConfiguredWithExpectedSettings()
    {
        var options = DeterministicJsonOptions.Indented;

        Assert.Equal(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.True(options.PropertyNameCaseInsensitive);
        Assert.True(options.WriteIndented);
    }

    private sealed record TestRecord
    {
        public required string PropertyName { get; init; }
        public required int AnotherProperty { get; init; }
    }
}
