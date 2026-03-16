using nirmata.Agents.Execution.ControlPlane.Tools.Firewall;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Firewall;

public sealed class ScopeFirewallTests
{
    [Fact]
    public void ValidatePath_AllowedPath_Succeeds()
    {
        // Arrange
        var allowedScope = Path.GetFullPath("C:\\workspace");
        var firewall = new ScopeFirewall(new[] { allowedScope });
        var filePath = Path.Combine(allowedScope, "file.cs");

        // Act & Assert
        firewall.ValidatePath(filePath); // Should not throw
    }

    [Fact]
    public void ValidatePath_OutOfScopePath_ThrowsScopeViolationException()
    {
        // Arrange
        var allowedScope = Path.GetFullPath("C:\\workspace");
        var firewall = new ScopeFirewall(new[] { allowedScope });
        var filePath = Path.GetFullPath("C:\\other\\file.cs");

        // Act & Assert
        var ex = Assert.Throws<ScopeViolationException>(() => firewall.ValidatePath(filePath));
        Assert.Contains("outside the allowed scope", ex.Message);
    }

    [Fact]
    public void ValidatePath_RelativePath_IsNormalized()
    {
        // Arrange
        var allowedScope = Path.GetFullPath("C:\\workspace");
        var firewall = new ScopeFirewall(new[] { allowedScope });
        var relativePath = Path.Combine(allowedScope, "subdir", "..", "file.cs");

        // Act & Assert
        firewall.ValidatePath(relativePath); // Should not throw - relative paths are normalized
    }

    [Fact]
    public void ValidatePath_SymlinkTraversal_IsBlocked()
    {
        // Arrange
        var allowedScope = Path.GetFullPath("C:\\workspace");
        var firewall = new ScopeFirewall(new[] { allowedScope });
        // Simulate a path that tries to traverse outside scope
        var maliciousPath = Path.Combine(allowedScope, "..", "..", "sensitive", "file.cs");

        // Act & Assert
        var ex = Assert.Throws<ScopeViolationException>(() => firewall.ValidatePath(maliciousPath));
        Assert.Contains("outside the allowed scope", ex.Message);
    }

    [Fact]
    public void ValidatePath_EmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var firewall = new ScopeFirewall(new[] { "C:\\workspace" });

        // Act & Assert
        Assert.Throws<ArgumentException>(() => firewall.ValidatePath(string.Empty));
    }

    [Fact]
    public void ValidatePath_MultipleAllowedScopes_AllowsPathInAnyScope()
    {
        // Arrange
        var scope1 = Path.GetFullPath("C:\\workspace1");
        var scope2 = Path.GetFullPath("C:\\workspace2");
        var firewall = new ScopeFirewall(new[] { scope1, scope2 });
        var filePath = Path.Combine(scope2, "file.cs");

        // Act & Assert
        firewall.ValidatePath(filePath); // Should not throw
    }

    [Fact]
    public void ValidatePath_CaseInsensitiveComparison()
    {
        // Arrange
        var allowedScope = Path.GetFullPath("C:\\Workspace");
        var firewall = new ScopeFirewall(new[] { allowedScope });
        var filePath = Path.Combine("C:\\workspace", "file.cs"); // Different case

        // Act & Assert
        firewall.ValidatePath(filePath); // Should not throw - case insensitive
    }
}
