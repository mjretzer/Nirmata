using System.Reflection;
using nirmata.Aos.Public;
using Xunit;

namespace nirmata.Aos.Tests;

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
                return ns.StartsWith("nirmata.Aos.Engine.", StringComparison.Ordinal) ||
                       ns == "nirmata.Aos.Engine" ||
                       ns.StartsWith("nirmata.Aos._Shared.", StringComparison.Ordinal) ||
                       ns == "nirmata.Aos._Shared";
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
                return !(ns == "nirmata.Aos.Public" ||
                         ns.StartsWith("nirmata.Aos.Public.", StringComparison.Ordinal) ||
                         ns == "nirmata.Aos.Contracts" ||
                         ns.StartsWith("nirmata.Aos.Contracts.", StringComparison.Ordinal));
            })
            .Select(t => $"{t.FullName ?? t.Name} (ns='{t.Namespace ?? ""}')")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Public API MUST ONLY expose nirmata.Aos.Public.* and nirmata.Aos.Contracts.*. Offenders:{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}"
        );
    }
}

