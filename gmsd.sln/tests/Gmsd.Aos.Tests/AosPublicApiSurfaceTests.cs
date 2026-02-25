using System.Reflection;
using Gmsd.Aos.Public;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosPublicApiSurfaceTests
{
    [Fact]
    public void PublicApiSurface_DoesNotExposeEngineOrSharedNamespaces()
    {
        var assembly = typeof(AosPublicApi).Assembly;
        var exported = assembly.GetExportedTypes();

        var offenders = exported
            .Where(t =>
            {
                var ns = t.Namespace ?? "";
                return ns.StartsWith("Gmsd.Aos.Engine.", StringComparison.Ordinal) ||
                       ns == "Gmsd.Aos.Engine" ||
                       ns.StartsWith("Gmsd.Aos._Shared.", StringComparison.Ordinal) ||
                       ns == "Gmsd.Aos._Shared";
            })
            .Select(t => t.FullName ?? t.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Public API MUST NOT expose engine/shared namespaces. Offenders:{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}"
        );
    }

    [Fact]
    public void PublicApiSurface_OnlyExposesPublicAndContractsRoots()
    {
        var assembly = typeof(AosPublicApi).Assembly;
        var exported = assembly.GetExportedTypes();

        var offenders = exported
            .Where(t =>
            {
                var ns = t.Namespace ?? "";
                return !(ns == "Gmsd.Aos.Public" ||
                         ns.StartsWith("Gmsd.Aos.Public.", StringComparison.Ordinal) ||
                         ns == "Gmsd.Aos.Contracts" ||
                         ns.StartsWith("Gmsd.Aos.Contracts.", StringComparison.Ordinal));
            })
            .Select(t => $"{t.FullName ?? t.Name} (ns='{t.Namespace ?? ""}')")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Public API MUST ONLY expose Gmsd.Aos.Public.* and Gmsd.Aos.Contracts.*. Offenders:{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}"
        );
    }
}

