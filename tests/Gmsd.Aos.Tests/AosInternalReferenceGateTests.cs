using System.Diagnostics;
using System.Text;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosInternalReferenceGateTests
{
    [Fact]
    public async Task ConsumerProject_CannotCompileAgainstEngineInternals()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
            var aosProjectPath = Path.Combine(repoRoot, "Gmsd.Aos", "Gmsd.Aos.csproj");
            Assert.True(File.Exists(aosProjectPath), $"Expected AOS project at '{aosProjectPath}'.");

            var consumerDir = Path.Combine(tempRoot, "consumer");
            Directory.CreateDirectory(consumerDir);

            var consumerProjPath = Path.Combine(consumerDir, "Consumer.csproj");
            var consumerProgramPath = Path.Combine(consumerDir, "Program.cs");

            File.WriteAllText(
                consumerProjPath,
                $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <!-- This test is about C# accessibility, not project layer boundaries. -->
                    <GmsdBoundaryEnforcementEnabled>false</GmsdBoundaryEnforcementEnabled>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="{aosProjectPath}" />
                  </ItemGroup>
                </Project>
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            );

            File.WriteAllText(
                consumerProgramPath,
                """
                using Gmsd.Aos.Engine.Paths;

                internal static class Program
                {
                    public static int Main()
                    {
                        // This MUST fail to compile if internals are correctly hidden.
                        _ = AosPathRouter.WorkspaceLockContractPath;
                        return 0;
                    }
                }
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            );

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                workingDirectory: consumerDir,
                "build",
                consumerProjPath,
                "-v",
                "minimal"
            );

            Assert.True(exitCode != 0, $"Expected consumer build to fail, got exit code 0. STDOUT:{Environment.NewLine}{stdout}");
            var combined = $"{stdout}{Environment.NewLine}{stderr}";
            Assert.Contains("AosPathRouter", combined, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("inaccessible", combined, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "gmsd-aos-internal-ref-gate", Guid.NewGuid().ToString("N"));
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

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunDotNetAsync(
        string workingDirectory,
        params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory
        };

        psi.Environment["DOTNET_NOLOGO"] = "1";
        psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi);
        Assert.NotNull(process);

        var stdoutTask = process!.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (process.ExitCode, stdout, stderr);
    }

    private static string FindRepositoryRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Gmsd.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        // Fallback: assume current directory is close enough.
        return Directory.GetCurrentDirectory();
    }
}

