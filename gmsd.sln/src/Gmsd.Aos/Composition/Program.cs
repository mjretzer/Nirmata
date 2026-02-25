using Gmsd.Aos.Engine;
using Gmsd.Aos.Engine.Errors;
using Gmsd.Aos.Engine.Schemas;
using Gmsd.Aos.Engine.Evidence;
using Gmsd.Aos.Engine.Evidence.Runs;
using Gmsd.Aos.Engine.Evidence.Calls;
using Gmsd.Aos.Engine.Evidence.Commands;
using Gmsd.Aos.Engine.Evidence.ExecutePlan;
using Gmsd.Aos.Engine.Locks;
using Gmsd.Aos.Engine.Config;
using Gmsd.Aos.Engine.ExecutePlan;
using Gmsd.Aos.Engine.Policy;
using Gmsd.Aos.Engine.StateTransitions;
using Gmsd.Aos.Engine.Paths;
using Gmsd.Aos.Engine.Repair;
using Gmsd.Aos.Engine.Stores;
using Gmsd.Aos.Engine.Cache;
using Gmsd.Aos.Engine.Validation;
using Gmsd.Aos.Engine.Workspace;
using Gmsd.Aos.Context.Packs;
using Gmsd.Aos.Public.Context.Packs;
using Gmsd.Aos.Contracts.Secrets;
using Gmsd.Aos.Composition;
using Microsoft.Extensions.DependencyInjection;
using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Gmsd.Aos;

internal static class Program
{
    private const int ExitCodeSuccess = 0;
    private const int ExitCodeInvalidUsage = 1;
    private const int ExitCodeKnownFailure = 2;
    private const int ExitCodePolicyViolation = 3;
    private const int ExitCodeLockContention = 4;
    private const int ExitCodeUnexpectedInternalError = 5;

    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            if (args.Length == 0)
            {
                // Missing command is invalid usage, but keep output focused on usage text.
                return ExitCodeInvalidUsage;
            }

            return ExitCodeSuccess;
        }

        var command = args[0];
        var rest = args.Skip(1).ToArray();

        return command switch
        {
            "init" => RunInit(rest),
            "run" => RunRun(rest),
            "validate" => RunValidate(rest),
            "config" => RunConfig(rest),
            "lock" => RunLock(rest),
            "cache" => RunCache(rest),
            "secret" => RunSecret(rest),
            "repair" => RunRepair(rest),
            "execute-plan" => RunExecutePlan(rest),
            "checkpoint" => RunCheckpoint(rest),
            "pack" => RunPack(rest),
            _ => FailUnknownCommand(command)
        };
    }

    private static int RunInit(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintInitUsage();
            return ExitCodeSuccess;
        }

        string repositoryRootPath;

        if (args.Length == 0)
        {
            if (!TryDiscoverRepositoryRoot(out repositoryRootPath))
            {
                return ExitCodeKnownFailure;
            }
        }
        else if (args.Length == 2 && args[0] == "--root")
        {
            repositoryRootPath = args[1];
        }
        else
        {
            PrintInitUsage();
            return ExitCodeInvalidUsage;
        }

        try
        {
            var aosRootPath = Path.Combine(repositoryRootPath, ".aos");
            var lockResult = AcquireWorkspaceLockOrNull(
                aosRootPath,
                command: "init",
                createDirectories: true
            );
            using var workspaceLock = lockResult.Handle;
            if (workspaceLock is null)
            {
                return lockResult.ExitCode;
            }

            var result = AosWorkspaceBootstrapper.EnsureInitialized(repositoryRootPath);
            Console.WriteLine($"{result.Outcome}: {result.AosRootPath}");
            return ExitCodeSuccess;
        }
        catch (AosWorkspaceNonCompliantException ex)
        {
            // Keep human-friendly message stable.
            Console.Error.WriteLine(ex.Message);
            WriteAosErrorLine(AosErrorMapper.Map(ex));
            return ExitCodeKnownFailure;
        }
    }

    private static int FailUnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return ExitCodeInvalidUsage;
    }

    private static int RunRun(string[] args)
    {
        if (args.Length == 0 || (args.Length == 1 && args[0] is "-h" or "--help"))
        {
            PrintRunUsage();
            return args.Length == 0 ? ExitCodeInvalidUsage : ExitCodeSuccess;
        }

        var subcommand = args[0];
        var rest = args.Skip(1).ToArray();

        return subcommand switch
        {
            "start" => RunRunStart(rest),
            "finish" => RunRunFinish(rest),
            "pause" => RunRunPause(rest),
            "resume" => RunRunResume(rest),
            _ => FailUnknownRunCommand(subcommand)
        };
    }

    private static int FailUnknownRunCommand(string subcommand)
    {
        Console.Error.WriteLine($"Unknown run command '{subcommand}'.");
        PrintRunUsage();
        return ExitCodeInvalidUsage;
    }

    private static int RunRunStart(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintRunStartUsage();
            return ExitCodeSuccess;
        }

        if (args.Length != 0)
        {
            Console.Error.WriteLine("Unexpected arguments for 'aos run start'.");
            PrintRunStartUsage();
            return ExitCodeInvalidUsage;
        }

        if (!TryDiscoverRepositoryRoot(out var repositoryRootPath))
        {
            return ExitCodeKnownFailure;
        }
        if (!EnsureWorkspaceValid(repositoryRootPath, out var aosRootPath))
        {
            return ExitCodeKnownFailure;
        }

        var lockResult = AcquireWorkspaceLockOrNull(
            aosRootPath,
            command: "run start",
            createDirectories: false
        );
        using var workspaceLock = lockResult.Handle;
        if (workspaceLock is null)
        {
            return lockResult.ExitCode;
        }

        var runId = AosRunId.New();

        // Scaffold deterministic run evidence structure:
        // .aos/evidence/runs/<run-id>/{ commands.json, summary.json, logs/, artifacts/, outputs/ }
        AosRunEvidenceScaffolder.EnsureRunEvidenceScaffold(
            aosRootPath,
            runId,
            startedAtUtc: DateTimeOffset.UtcNow,
            command: "run start",
            args: args
        );

        try
        {
            AosCommandLogWriter.AppendCommand(
                aosRootPath,
                command: "run start",
                args: args,
                exitCode: 0,
                runId: runId
            );

            // Maintain the per-run view for discoverability.
            AosRunCommandsViewWriter.WriteRunCommandsView(aosRootPath, runId);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Warning: failed to append to commands.json.");
            Console.Error.WriteLine(ex.Message);
        }

        // Spec requires: command produces a run ID.
        Console.WriteLine(runId);
        return ExitCodeSuccess;
    }

    private static int RunRunFinish(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintRunFinishUsage();
            return ExitCodeSuccess;
        }

        string? runId = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--run-id")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --run-id.");
                    PrintRunFinishUsage();
                    return ExitCodeInvalidUsage;
                }

                runId = args[++i];
                continue;
            }

            if (arg.StartsWith("--run-id=", StringComparison.Ordinal))
            {
                runId = arg["--run-id=".Length..];
                continue;
            }

            Console.Error.WriteLine($"Unknown option '{arg}'.");
            PrintRunFinishUsage();
            return ExitCodeInvalidUsage;
        }

        if (string.IsNullOrWhiteSpace(runId))
        {
            Console.Error.WriteLine("Missing required option --run-id.");
            PrintRunFinishUsage();
            return ExitCodeInvalidUsage;
        }

        if (!AosRunId.IsValid(runId))
        {
            Console.Error.WriteLine("Invalid run id.");
            return ExitCodeInvalidUsage;
        }

        if (!TryDiscoverRepositoryRoot(out var repositoryRootPath))
        {
            return ExitCodeKnownFailure;
        }
        if (!EnsureWorkspaceValid(repositoryRootPath, out var aosRootPath))
        {
            return ExitCodeKnownFailure;
        }

        var lockResult = AcquireWorkspaceLockOrNull(
            aosRootPath,
            command: "run finish",
            createDirectories: false
        );
        using var workspaceLock = lockResult.Handle;
        if (workspaceLock is null)
        {
            return lockResult.ExitCode;
        }

        try
        {
            AosRunEvidenceScaffolder.FinishRun(
                aosRootPath,
                runId,
                finishedAtUtc: DateTimeOffset.UtcNow
            );

            try
            {
                AosCommandLogWriter.AppendCommand(
                    aosRootPath,
                    command: "run finish",
                    args: args,
                    exitCode: 0,
                    runId: runId
                );

                // Maintain the per-run view for discoverability.
                AosRunCommandsViewWriter.WriteRunCommandsView(aosRootPath, runId);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Warning: failed to append to commands.json.");
                Console.Error.WriteLine(ex.Message);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Failed to finish run.");
            Console.Error.WriteLine(ex.Message);
            var error = AosErrorMapper.Map(ex);
            WriteAosErrorLine(error);

            // Best-effort: if the run exists, write a result.json capturing the failure.
            AosRunEvidenceScaffolder.TryWriteFailedRunResultIfRunExists(
                aosRootPath,
                runId,
                exitCode: ExitCodeUnexpectedInternalError,
                error: error
            );

            return ExitCodeUnexpectedInternalError;
        }
    }

    private static int RunRunPause(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintRunPauseUsage();
            return ExitCodeSuccess;
        }

        string? runId = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--run-id")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --run-id.");
                    PrintRunPauseUsage();
                    return ExitCodeInvalidUsage;
                }

                runId = args[++i];
                continue;
            }

            if (arg.StartsWith("--run-id=", StringComparison.Ordinal))
            {
                runId = arg["--run-id=".Length..];
                continue;
            }

            Console.Error.WriteLine($"Unknown option '{arg}'.");
            PrintRunPauseUsage();
            return ExitCodeInvalidUsage;
        }

        if (string.IsNullOrWhiteSpace(runId))
        {
            Console.Error.WriteLine("Missing required option --run-id.");
            PrintRunPauseUsage();
            return ExitCodeInvalidUsage;
        }

        if (!AosRunId.IsValid(runId))
        {
            Console.Error.WriteLine("Invalid run id.");
            return ExitCodeInvalidUsage;
        }

        if (!TryDiscoverRepositoryRoot(out var repositoryRootPath))
        {
            return ExitCodeKnownFailure;
        }
        if (!EnsureWorkspaceValid(repositoryRootPath, out var aosRootPath))
        {
            return ExitCodeKnownFailure;
        }

        var lockResult = AcquireWorkspaceLockOrNull(
            aosRootPath,
            command: "run pause",
            createDirectories: false
        );
        using var workspaceLock = lockResult.Handle;
        if (workspaceLock is null)
        {
            return lockResult.ExitCode;
        }

        try
        {
            AosRunEvidenceScaffolder.PauseRun(
                aosRootPath,
                runId,
                pausedAtUtc: DateTimeOffset.UtcNow
            );

            try
            {
                AosCommandLogWriter.AppendCommand(
                    aosRootPath,
                    command: "run pause",
                    args: args,
                    exitCode: 0,
                    runId: runId
                );

                AosRunCommandsViewWriter.WriteRunCommandsView(aosRootPath, runId);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Warning: failed to append to commands.json.");
                Console.Error.WriteLine(ex.Message);
            }

            Console.WriteLine($"Run '{runId}' paused successfully.");
            return ExitCodeSuccess;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine("Failed to pause run.");
            Console.Error.WriteLine(ex.Message);
            return ExitCodeKnownFailure;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Failed to pause run.");
            Console.Error.WriteLine(ex.Message);
            var error = AosErrorMapper.Map(ex);
            WriteAosErrorLine(error);
            return ExitCodeUnexpectedInternalError;
        }
    }

    private static int RunRunResume(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintRunResumeUsage();
            return ExitCodeSuccess;
        }

        string? runId = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--run-id")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --run-id.");
                    PrintRunResumeUsage();
                    return ExitCodeInvalidUsage;
                }

                runId = args[++i];
                continue;
            }

            if (arg.StartsWith("--run-id=", StringComparison.Ordinal))
            {
                runId = arg["--run-id=".Length..];
                continue;
            }

            Console.Error.WriteLine($"Unknown option '{arg}'.");
            PrintRunResumeUsage();
            return ExitCodeInvalidUsage;
        }

        if (string.IsNullOrWhiteSpace(runId))
        {
            Console.Error.WriteLine("Missing required option --run-id.");
            PrintRunResumeUsage();
            return ExitCodeInvalidUsage;
        }

        if (!AosRunId.IsValid(runId))
        {
            Console.Error.WriteLine("Invalid run id.");
            return ExitCodeInvalidUsage;
        }

        if (!TryDiscoverRepositoryRoot(out var repositoryRootPath))
        {
            return ExitCodeKnownFailure;
        }
        if (!EnsureWorkspaceValid(repositoryRootPath, out var aosRootPath))
        {
            return ExitCodeKnownFailure;
        }

        var lockResult = AcquireWorkspaceLockOrNull(
            aosRootPath,
            command: "run resume",
            createDirectories: false
        );
        using var workspaceLock = lockResult.Handle;
        if (workspaceLock is null)
        {
            return lockResult.ExitCode;
        }

        try
        {
            AosRunEvidenceScaffolder.ResumeRun(
                aosRootPath,
                runId,
                resumedAtUtc: DateTimeOffset.UtcNow
            );

            try
            {
                AosCommandLogWriter.AppendCommand(
                    aosRootPath,
                    command: "run resume",
                    args: args,
                    exitCode: 0,
                    runId: runId
                );

                AosRunCommandsViewWriter.WriteRunCommandsView(aosRootPath, runId);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Warning: failed to append to commands.json.");
                Console.Error.WriteLine(ex.Message);
            }

            Console.WriteLine($"Run '{runId}' resumed successfully.");
            return ExitCodeSuccess;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine("Failed to resume run.");
            Console.Error.WriteLine(ex.Message);
            return ExitCodeKnownFailure;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine("Failed to resume run.");
            Console.Error.WriteLine(ex.Message);
            return ExitCodeKnownFailure;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Failed to resume run.");
            Console.Error.WriteLine(ex.Message);
            var error = AosErrorMapper.Map(ex);
            WriteAosErrorLine(error);
            return ExitCodeUnexpectedInternalError;
        }
    }

    private static int RunValidate(string[] args)
    {
        if (args.Length == 0 || (args.Length == 1 && args[0] is "-h" or "--help"))
        {
            PrintValidateUsage();
            return args.Length == 0 ? ExitCodeInvalidUsage : ExitCodeSuccess;
        }

        var subcommand = args[0];
        var rest = args.Skip(1).ToArray();

        return subcommand switch
        {
            "schemas" => RunValidateSchemas(rest),
            "workspace" => RunValidateWorkspace(rest),
            _ => FailUnknownValidateCommand(subcommand)
        };
    }

    private static int RunConfig(string[] args)
    {
        if (args.Length == 0 || (args.Length == 1 && args[0] is "-h" or "--help"))
        {
            PrintConfigUsage();
            return args.Length == 0 ? ExitCodeInvalidUsage : ExitCodeSuccess;
        }

        var subcommand = args[0];
        var rest = args.Skip(1).ToArray();

        return subcommand switch
        {
            "validate" => RunConfigValidate(rest),
            _ => FailUnknownConfigCommand(subcommand)
        };
    }

    private static int RunLock(string[] args)
    {
        if (args.Length == 0 || (args.Length == 1 && args[0] is "-h" or "--help"))
        {
            PrintLockUsage();
            return args.Length == 0 ? ExitCodeInvalidUsage : ExitCodeSuccess;
        }

        var subcommand = args[0];
        var rest = args.Skip(1).ToArray();

        return subcommand switch
        {
            "status" => RunLockStatus(rest),
            "acquire" => RunLockAcquire(rest),
            "release" => RunLockRelease(rest),
            _ => FailUnknownLockCommand(subcommand)
        };
    }

    private static int FailUnknownLockCommand(string subcommand)
    {
        Console.Error.WriteLine($"Unknown lock command '{subcommand}'.");
        PrintLockUsage();
        return ExitCodeInvalidUsage;
    }

    private static int RunCache(string[] args)
    {
        if (args.Length == 0 || (args.Length == 1 && args[0] is "-h" or "--help"))
        {
            PrintCacheUsage();
            return args.Length == 0 ? ExitCodeInvalidUsage : ExitCodeSuccess;
        }

        var subcommand = args[0];
        var rest = args.Skip(1).ToArray();

        return subcommand switch
        {
            "clear" => RunCacheClear(rest),
            "prune" => RunCachePrune(rest),
            _ => FailUnknownCacheCommand(subcommand)
        };
    }

    private static int FailUnknownCacheCommand(string subcommand)
    {
        Console.Error.WriteLine($"Unknown cache command '{subcommand}'.");
        PrintCacheUsage();
        return ExitCodeInvalidUsage;
    }

    private static int RunSecret(string[] args)
    {
        if (args.Length == 0 || (args.Length == 1 && args[0] is "-h" or "--help"))
        {
            PrintSecretUsage();
            return args.Length == 0 ? ExitCodeInvalidUsage : ExitCodeSuccess;
        }

        var subcommand = args[0];
        var rest = args.Skip(1).ToArray();

        return subcommand switch
        {
            "set" => RunSecretSet(rest),
            "get" => RunSecretGet(rest),
            "list" => RunSecretList(rest),
            "delete" => RunSecretDelete(rest),
            _ => FailUnknownSecretCommand(subcommand)
        };
    }

    private static int FailUnknownSecretCommand(string subcommand)
    {
        Console.Error.WriteLine($"Unknown secret command '{subcommand}'.");
        PrintSecretUsage();
        return ExitCodeInvalidUsage;
    }

    private static int RunSecretSet(string[] args)
    {
        if (args.Length < 2 || (args.Length == 1 && args[0] is "-h" or "--help"))
        {
            PrintSecretSetUsage();
            return args.Length == 0 ? ExitCodeInvalidUsage : ExitCodeSuccess;
        }

        string? secretName = null;
        string? secretValue = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg is "-h" or "--help")
            {
                PrintSecretSetUsage();
                return ExitCodeSuccess;
            }

            if (secretName == null)
            {
                secretName = arg;
            }
            else if (secretValue == null)
            {
                secretValue = arg;
            }
            else
            {
                Console.Error.WriteLine($"Unexpected argument '{arg}'.");
                PrintSecretSetUsage();
                return ExitCodeInvalidUsage;
            }
        }

        if (string.IsNullOrWhiteSpace(secretName) || string.IsNullOrWhiteSpace(secretValue))
        {
            Console.Error.WriteLine("Missing required arguments: <name> and <value>.");
            PrintSecretSetUsage();
            return ExitCodeInvalidUsage;
        }

        try
        {
            var services = new ServiceCollection();
            services.AddSecretManagement();
            var serviceProvider = services.BuildServiceProvider();
            var secretStore = serviceProvider.GetRequiredService<ISecretStore>();

            secretStore.SetSecretAsync(secretName, secretValue).Wait();
            Console.WriteLine("SET");
            Console.WriteLine($"Secret name: {secretName}");
            return ExitCodeSuccess;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Invalid secret: {ex.Message}");
            return ExitCodeKnownFailure;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Failed to set secret.");
            Console.Error.WriteLine(ex.Message);
            return ExitCodeKnownFailure;
        }
    }

    private static int RunSecretGet(string[] args)
    {
        if (args.Length == 0 || (args.Length == 1 && args[0] is "-h" or "--help"))
        {
            PrintSecretGetUsage();
            return args.Length == 0 ? ExitCodeInvalidUsage : ExitCodeSuccess;
        }

        string? secretName = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg is "-h" or "--help")
            {
                PrintSecretGetUsage();
                return ExitCodeSuccess;
            }

            if (secretName == null)
            {
                secretName = arg;
            }
            else
            {
                Console.Error.WriteLine($"Unexpected argument '{arg}'.");
                PrintSecretGetUsage();
                return ExitCodeInvalidUsage;
            }
        }

        if (string.IsNullOrWhiteSpace(secretName))
        {
            Console.Error.WriteLine("Missing required argument: <name>.");
            PrintSecretGetUsage();
            return ExitCodeInvalidUsage;
        }

        try
        {
            var services = new ServiceCollection();
            services.AddSecretManagement();
            var serviceProvider = services.BuildServiceProvider();
            var secretStore = serviceProvider.GetRequiredService<ISecretStore>();

            var secretValue = secretStore.GetSecretAsync(secretName).Result;
            Console.WriteLine("GET");
            Console.WriteLine($"Secret name: {secretName}");
            Console.WriteLine($"Secret value: {new string('*', secretValue.Length)}");
            return ExitCodeSuccess;
        }
        catch (SecretNotFoundException)
        {
            Console.Error.WriteLine($"Secret '{secretName}' not found.");
            return ExitCodeKnownFailure;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Invalid secret name: {ex.Message}");
            return ExitCodeKnownFailure;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Failed to get secret.");
            Console.Error.WriteLine(ex.Message);
            return ExitCodeKnownFailure;
        }
    }

    private static int RunSecretList(string[] args)
    {
        if (args.Length > 0 && args[0] is "-h" or "--help")
        {
            PrintSecretListUsage();
            return ExitCodeSuccess;
        }

        if (args.Length > 0)
        {
            Console.Error.WriteLine($"Unexpected argument '{args[0]}'.");
            PrintSecretListUsage();
            return ExitCodeInvalidUsage;
        }

        try
        {
            var services = new ServiceCollection();
            services.AddSecretManagement();
            var serviceProvider = services.BuildServiceProvider();
            var secretStore = serviceProvider.GetRequiredService<ISecretStore>();

            var secrets = secretStore.ListSecretsAsync().Result;
            Console.WriteLine("LIST");
            if (secrets.Count == 0)
            {
                Console.WriteLine("No secrets found.");
            }
            else
            {
                foreach (var secretName in secrets.OrderBy(s => s))
                {
                    Console.WriteLine($"  {secretName}");
                }
            }
            return ExitCodeSuccess;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Failed to list secrets.");
            Console.Error.WriteLine(ex.Message);
            return ExitCodeKnownFailure;
        }
    }

    private static int RunSecretDelete(string[] args)
    {
        if (args.Length == 0 || (args.Length == 1 && args[0] is "-h" or "--help"))
        {
            PrintSecretDeleteUsage();
            return args.Length == 0 ? ExitCodeInvalidUsage : ExitCodeSuccess;
        }

        string? secretName = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg is "-h" or "--help")
            {
                PrintSecretDeleteUsage();
                return ExitCodeSuccess;
            }

            if (secretName == null)
            {
                secretName = arg;
            }
            else
            {
                Console.Error.WriteLine($"Unexpected argument '{arg}'.");
                PrintSecretDeleteUsage();
                return ExitCodeInvalidUsage;
            }
        }

        if (string.IsNullOrWhiteSpace(secretName))
        {
            Console.Error.WriteLine("Missing required argument: <name>.");
            PrintSecretDeleteUsage();
            return ExitCodeInvalidUsage;
        }

        try
        {
            var services = new ServiceCollection();
            services.AddSecretManagement();
            var serviceProvider = services.BuildServiceProvider();
            var secretStore = serviceProvider.GetRequiredService<ISecretStore>();

            secretStore.DeleteSecretAsync(secretName).Wait();
            Console.WriteLine("DELETED");
            Console.WriteLine($"Secret name: {secretName}");
            return ExitCodeSuccess;
        }
        catch (SecretNotFoundException)
        {
            Console.Error.WriteLine($"Secret '{secretName}' not found.");
            return ExitCodeKnownFailure;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Invalid secret name: {ex.Message}");
            return ExitCodeKnownFailure;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Failed to delete secret.");
            Console.Error.WriteLine(ex.Message);
            return ExitCodeKnownFailure;
        }
    }

    private static int RunCacheClear(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintCacheClearUsage();
            return ExitCodeSuccess;
        }

        string? repositoryRootPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--root")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --root.");
                    PrintCacheClearUsage();
                    return ExitCodeInvalidUsage;
                }

                repositoryRootPath = args[++i];
                continue;
            }

            if (arg.StartsWith("--root=", StringComparison.Ordinal))
            {
                repositoryRootPath = arg["--root=".Length..];
                continue;
            }

            Console.Error.WriteLine($"Unknown option '{arg}'.");
            PrintCacheClearUsage();
            return ExitCodeInvalidUsage;
        }

        if (string.IsNullOrWhiteSpace(repositoryRootPath))
        {
            if (!TryDiscoverRepositoryRoot(out var discovered))
            {
                return ExitCodeKnownFailure;
            }

            repositoryRootPath = discovered;
        }

        if (!EnsureWorkspaceValid(repositoryRootPath, out var aosRootPath))
        {
            return ExitCodeKnownFailure;
        }

        var lockResult = AcquireWorkspaceLockOrNull(
            aosRootPath,
            command: "cache clear",
            createDirectories: false
        );
        using var workspaceLock = lockResult.Handle;
        if (workspaceLock is null)
        {
            return lockResult.ExitCode;
        }

        try
        {
            AosCacheHygiene.Clear(aosRootPath);
            Console.WriteLine("CLEARED");
            Console.WriteLine("Cache path: .aos/cache/");
            return ExitCodeSuccess;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("cache clear failed.");
            Console.Error.WriteLine(ex.Message);
            WriteAosErrorLine(AosErrorMapper.Map(ex));
            return ExitCodeKnownFailure;
        }
    }

    private static int RunCachePrune(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintCachePruneUsage();
            return ExitCodeSuccess;
        }

        string? repositoryRootPath = null;
        var days = 30;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--root")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --root.");
                    PrintCachePruneUsage();
                    return ExitCodeInvalidUsage;
                }

                repositoryRootPath = args[++i];
                continue;
            }

            if (arg.StartsWith("--root=", StringComparison.Ordinal))
            {
                repositoryRootPath = arg["--root=".Length..];
                continue;
            }

            if (arg == "--days")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --days.");
                    PrintCachePruneUsage();
                    return ExitCodeInvalidUsage;
                }

                if (!int.TryParse(args[++i], out days) || days < 0)
                {
                    Console.Error.WriteLine("Invalid --days value (expected non-negative integer).");
                    PrintCachePruneUsage();
                    return ExitCodeInvalidUsage;
                }

                continue;
            }

            if (arg.StartsWith("--days=", StringComparison.Ordinal))
            {
                var raw = arg["--days=".Length..];
                if (!int.TryParse(raw, out days) || days < 0)
                {
                    Console.Error.WriteLine("Invalid --days value (expected non-negative integer).");
                    PrintCachePruneUsage();
                    return ExitCodeInvalidUsage;
                }

                continue;
            }

            Console.Error.WriteLine($"Unknown option '{arg}'.");
            PrintCachePruneUsage();
            return ExitCodeInvalidUsage;
        }

        if (string.IsNullOrWhiteSpace(repositoryRootPath))
        {
            if (!TryDiscoverRepositoryRoot(out var discovered))
            {
                return ExitCodeKnownFailure;
            }

            repositoryRootPath = discovered;
        }

        if (!EnsureWorkspaceValid(repositoryRootPath, out var aosRootPath))
        {
            return ExitCodeKnownFailure;
        }

        var lockResult = AcquireWorkspaceLockOrNull(
            aosRootPath,
            command: "cache prune",
            createDirectories: false
        );
        using var workspaceLock = lockResult.Handle;
        if (workspaceLock is null)
        {
            return lockResult.ExitCode;
        }

        try
        {
            var deleted = AosCacheHygiene.Prune(aosRootPath, days);
            Console.WriteLine("PRUNED");
            Console.WriteLine($"Deleted: {deleted}");
            Console.WriteLine("Cache path: .aos/cache/");
            return ExitCodeSuccess;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("cache prune failed.");
            Console.Error.WriteLine(ex.Message);
            WriteAosErrorLine(AosErrorMapper.Map(ex));
            return ExitCodeKnownFailure;
        }
    }

    private static int RunLockStatus(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintLockStatusUsage();
            return ExitCodeSuccess;
        }

        string? repositoryRootPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--root")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --root.");
                    PrintLockStatusUsage();
                    return ExitCodeInvalidUsage;
                }

                repositoryRootPath = args[++i];
                continue;
            }

            if (arg.StartsWith("--root=", StringComparison.Ordinal))
            {
                repositoryRootPath = arg["--root=".Length..];
                continue;
            }

            Console.Error.WriteLine($"Unknown option '{arg}'.");
            PrintLockStatusUsage();
            return ExitCodeInvalidUsage;
        }

        if (string.IsNullOrWhiteSpace(repositoryRootPath))
        {
            if (!TryDiscoverRepositoryRoot(out var discovered))
            {
                return ExitCodeKnownFailure;
            }

            repositoryRootPath = discovered;
        }

        var aosRootPath = Path.Combine(repositoryRootPath, ".aos");
        var lockPath = AosPathRouter.GetWorkspaceLockPath(aosRootPath);

        if (!File.Exists(lockPath))
        {
            Console.WriteLine("UNLOCKED");
            Console.WriteLine($"Lock path: {AosPathRouter.WorkspaceLockContractPath}");
            if (!Directory.Exists(aosRootPath))
            {
                Console.WriteLine("Hint: workspace is not initialized (run 'aos init').");
            }
            return ExitCodeSuccess;
        }

        var (doc, raw, _) = AosWorkspaceLockManager.TryReadExisting(aosRootPath);
        if (doc is null)
        {
            Console.WriteLine("LOCKED");
            Console.WriteLine($"Lock path: {AosPathRouter.WorkspaceLockContractPath}");
            Console.WriteLine("Details: lock file exists but could not be parsed.");
            if (!string.IsNullOrWhiteSpace(raw))
            {
                Console.WriteLine("Raw:");
                Console.WriteLine(raw);
            }

            WriteAosErrorLine(new AosErrorEnvelope(
                Code: AosErrorCodes.LockInvalid,
                Message: "Workspace lock file exists but is invalid / unparseable.",
                Details: new { LockContractPath = AosPathRouter.WorkspaceLockContractPath }
            ));
            return ExitCodeKnownFailure;
        }

        Console.WriteLine("LOCKED");
        Console.WriteLine($"Lock path: {AosPathRouter.WorkspaceLockContractPath}");
        Console.WriteLine($"Held since: {doc.AcquiredAtUtc}");
        Console.WriteLine($"Holder: {doc.Holder.User}@{doc.Holder.Machine} pid={doc.Holder.Pid}");
        if (!string.IsNullOrWhiteSpace(doc.Holder.ProcessName))
        {
            Console.WriteLine($"Process: {doc.Holder.ProcessName}");
        }
        if (!string.IsNullOrWhiteSpace(doc.Holder.Command))
        {
            Console.WriteLine($"Command: {doc.Holder.Command}");
        }
        if (!string.IsNullOrWhiteSpace(doc.Holder.WorkingDirectory))
        {
            Console.WriteLine($"Working directory: {doc.Holder.WorkingDirectory}");
        }
        if (!string.IsNullOrWhiteSpace(doc.ReleaseHint))
        {
            Console.WriteLine($"Hint: {doc.ReleaseHint}");
        }

        return ExitCodeSuccess;
    }

    private static int RunLockAcquire(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintLockAcquireUsage();
            return ExitCodeSuccess;
        }

        string? repositoryRootPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--root")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --root.");
                    PrintLockAcquireUsage();
                    return ExitCodeInvalidUsage;
                }

                repositoryRootPath = args[++i];
                continue;
            }

            if (arg.StartsWith("--root=", StringComparison.Ordinal))
            {
                repositoryRootPath = arg["--root=".Length..];
                continue;
            }

            Console.Error.WriteLine($"Unknown option '{arg}'.");
            PrintLockAcquireUsage();
            return ExitCodeInvalidUsage;
        }

        if (string.IsNullOrWhiteSpace(repositoryRootPath))
        {
            if (!TryDiscoverRepositoryRoot(out var discovered))
            {
                return ExitCodeKnownFailure;
            }

            repositoryRootPath = discovered;
        }

        var aosRootPath = Path.Combine(repositoryRootPath, ".aos");
        if (!Directory.Exists(aosRootPath))
        {
            Console.Error.WriteLine("AOS workspace not initialized.");
            Console.Error.WriteLine("Run 'aos init' first, then re-run this command.");
            WriteAosErrorLine(new AosErrorEnvelope(
                Code: AosErrorCodes.WorkspaceValidationFailed,
                Message: "AOS workspace is not initialized.",
                Details: new { AosRootPath = aosRootPath }
            ));
            return ExitCodeKnownFailure;
        }

        // IMPORTANT: do NOT dispose; this command intentionally leaves the lock held until released.
        var lockResult = AcquireWorkspaceLockOrNull(
            aosRootPath,
            command: "lock acquire",
            createDirectories: true
        );
        var handle = lockResult.Handle;
        if (handle is null)
        {
            return lockResult.ExitCode;
        }

        Console.WriteLine("ACQUIRED");
        Console.WriteLine($"Lock path: {AosPathRouter.WorkspaceLockContractPath}");
        Console.WriteLine($"Lock id: {handle.LockId}");
        return ExitCodeSuccess;
    }

    private static int RunLockRelease(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintLockReleaseUsage();
            return ExitCodeSuccess;
        }

        string? repositoryRootPath = null;
        var force = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--root")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --root.");
                    PrintLockReleaseUsage();
                    return ExitCodeInvalidUsage;
                }

                repositoryRootPath = args[++i];
                continue;
            }

            if (arg.StartsWith("--root=", StringComparison.Ordinal))
            {
                repositoryRootPath = arg["--root=".Length..];
                continue;
            }

            if (arg == "--force")
            {
                force = true;
                continue;
            }

            Console.Error.WriteLine($"Unknown option '{arg}'.");
            PrintLockReleaseUsage();
            return ExitCodeInvalidUsage;
        }

        if (string.IsNullOrWhiteSpace(repositoryRootPath))
        {
            if (!TryDiscoverRepositoryRoot(out var discovered))
            {
                return ExitCodeKnownFailure;
            }

            repositoryRootPath = discovered;
        }

        var aosRootPath = Path.Combine(repositoryRootPath, ".aos");
        var lockPath = AosPathRouter.GetWorkspaceLockPath(aosRootPath);

        if (!File.Exists(lockPath))
        {
            Console.WriteLine("NOT LOCKED");
            Console.WriteLine($"Lock path: {AosPathRouter.WorkspaceLockContractPath}");
            return ExitCodeSuccess;
        }

        if (!force)
        {
            var (doc, _, _) = AosWorkspaceLockManager.TryReadExisting(aosRootPath);
            if (doc is null)
            {
                Console.Error.WriteLine("Lock file exists but could not be parsed.");
                Console.Error.WriteLine("Re-run with '--force' to delete it anyway.");
                WriteAosErrorLine(new AosErrorEnvelope(
                    Code: AosErrorCodes.LockInvalid,
                    Message: "Workspace lock file exists but is invalid / unparseable.",
                    Details: new { LockContractPath = AosPathRouter.WorkspaceLockContractPath }
                ));
                return ExitCodeKnownFailure;
            }

            if (doc.SchemaVersion != 1 || !string.Equals(doc.LockKind, "workspace", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Lock file exists but does not match the expected workspace lock format.");
                Console.Error.WriteLine("Re-run with '--force' to delete it anyway.");
                WriteAosErrorLine(new AosErrorEnvelope(
                    Code: AosErrorCodes.LockInvalid,
                    Message: "Workspace lock file does not match the expected contract.",
                    Details: new { LockContractPath = AosPathRouter.WorkspaceLockContractPath }
                ));
                return ExitCodeKnownFailure;
            }
        }

        try
        {
            File.Delete(lockPath);
            Console.WriteLine("RELEASED");
            Console.WriteLine($"Lock path: {AosPathRouter.WorkspaceLockContractPath}");
            return ExitCodeSuccess;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Failed to release workspace lock.");
            Console.Error.WriteLine(ex.Message);
            WriteAosErrorLine(AosErrorMapper.Map(ex));
            return ExitCodeKnownFailure;
        }
    }

    private static int FailUnknownConfigCommand(string subcommand)
    {
        Console.Error.WriteLine($"Unknown config command '{subcommand}'.");
        PrintConfigUsage();
        return ExitCodeInvalidUsage;
    }

    private static int RunConfigValidate(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintConfigValidateUsage();
            return ExitCodeSuccess;
        }

        string? repositoryRootPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--root")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --root.");
                    PrintConfigValidateUsage();
                    return ExitCodeInvalidUsage;
                }

                repositoryRootPath = args[++i];
                continue;
            }

            if (arg.StartsWith("--root=", StringComparison.Ordinal))
            {
                repositoryRootPath = arg["--root=".Length..];
                continue;
            }

            Console.Error.WriteLine($"Unknown option '{arg}'.");
            PrintConfigValidateUsage();
            return ExitCodeInvalidUsage;
        }

        if (string.IsNullOrWhiteSpace(repositoryRootPath))
        {
            if (!TryDiscoverRepositoryRoot(out var discovered))
            {
                return ExitCodeKnownFailure;
            }

            repositoryRootPath = discovered;
        }

        AosConfigLoadResult result;
        try
        {
            result = AosConfigLoader.LoadAndValidate(repositoryRootPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Config validation failed.");
            Console.Error.WriteLine(ex.Message);
            WriteAosErrorLine(AosErrorMapper.Map(ex));
            return ExitCodeUnexpectedInternalError;
        }

        if (!result.Exists)
        {
            Console.Error.WriteLine($"Missing required config file: {AosConfigLoader.ConfigContractPath}");
            Console.Error.WriteLine("Create '.aos/config/config.json' (secrets-by-reference only) and re-run this command.");
            Console.Error.WriteLine("Tip: if the workspace is missing, run 'aos init' first.");
            WriteAosErrorLine(new AosErrorEnvelope(
                Code: AosErrorCodes.ConfigInvalid,
                Message: "Missing required config file.",
                Details: new { ContractPath = AosConfigLoader.ConfigContractPath }
            ));
            return ExitCodeKnownFailure;
        }

        if (result.Report.Issues.Count == 0)
        {
            Console.WriteLine($"PASS {AosConfigLoader.ConfigContractPath}");
            Console.WriteLine("OK");
            return ExitCodeSuccess;
        }

        foreach (var issue in result.Report.Issues)
        {
            Console.WriteLine($"FAIL {AosConfigLoader.ConfigContractPath} {issue.JsonPath} - {issue.Message}");
        }

        Console.Error.WriteLine($"Config validation failed: {result.Report.Issues.Count} issue(s).");
        WriteAosErrorLine(new AosErrorEnvelope(
            Code: AosErrorCodes.ConfigInvalid,
            Message: "Config validation failed.",
            Details: new
            {
                ContractPath = AosConfigLoader.ConfigContractPath,
                Issues = result.Report.Issues.Select(i => new { i.JsonPath, i.Message }).ToArray()
            }
        ));
        return ExitCodeKnownFailure;
    }

    private static int RunRepair(string[] args)
    {
        if (args.Length == 0 || (args.Length == 1 && args[0] is "-h" or "--help"))
        {
            PrintRepairUsage();
            return args.Length == 0 ? ExitCodeInvalidUsage : ExitCodeSuccess;
        }

        var subcommand = args[0];
        var rest = args.Skip(1).ToArray();

        return subcommand switch
        {
            "indexes" => RunRepairIndexes(rest),
            _ => FailUnknownRepairCommand(subcommand)
        };
    }

    private static int RunRepairIndexes(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintRepairIndexesUsage();
            return ExitCodeSuccess;
        }

        string? repositoryRootPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--root")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --root.");
                    PrintRepairIndexesUsage();
                    return ExitCodeInvalidUsage;
                }

                repositoryRootPath = args[++i];
                continue;
            }

            if (arg.StartsWith("--root=", StringComparison.Ordinal))
            {
                repositoryRootPath = arg["--root=".Length..];
                continue;
            }

            Console.Error.WriteLine($"Unknown option '{arg}'.");
            PrintRepairIndexesUsage();
            return ExitCodeInvalidUsage;
        }

        if (string.IsNullOrWhiteSpace(repositoryRootPath))
        {
            if (!TryDiscoverRepositoryRoot(out var discovered))
            {
                return ExitCodeKnownFailure;
            }

            repositoryRootPath = discovered;
        }

        var aosRootPath = Path.Combine(repositoryRootPath, ".aos");

        LockAcquireResult? lockResult = null;
        if (Directory.Exists(aosRootPath))
        {
            lockResult = AcquireWorkspaceLockOrNull(
                aosRootPath,
                command: "repair indexes",
                createDirectories: false
            );
        }

        using var workspaceLock = lockResult?.Handle;
        if (Directory.Exists(aosRootPath) && workspaceLock is null)
        {
            return lockResult?.ExitCode ?? ExitCodeKnownFailure;
        }

        try
        {
            var result = AosIndexRepairer.RepairIndexes(aosRootPath);
            Console.WriteLine($"AOS root: {result.AosRootPath}");
            Console.WriteLine("Repaired indexes:");
            Console.WriteLine($"  spec milestones: {GetSpecCount(result, AosArtifactKind.Milestone)}");
            Console.WriteLine($"  spec phases:     {GetSpecCount(result, AosArtifactKind.Phase)}");
            Console.WriteLine($"  spec tasks:      {GetSpecCount(result, AosArtifactKind.Task)}");
            Console.WriteLine($"  spec issues:     {GetSpecCount(result, AosArtifactKind.Issue)}");
            Console.WriteLine($"  spec uat:        {GetSpecCount(result, AosArtifactKind.Uat)}");
            Console.WriteLine($"  runs:            {result.RunCount}");

            try
            {
                AosCommandLogWriter.AppendCommand(
                    aosRootPath,
                    command: "repair indexes",
                    args: args,
                    exitCode: 0,
                    runId: null
                );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Warning: failed to append to commands.json.");
                Console.Error.WriteLine(ex.Message);
            }

            return 0;
        }
        catch (AosIndexRepairFailedException ex)
        {
            foreach (var issue in ex.Issues)
            {
                Console.Error.WriteLine($"FAIL {issue.ContractPath} - {issue.Message}");
                Console.Error.WriteLine($"  Fix: {issue.SuggestedFix}");
            }

            Console.Error.WriteLine($"Repair failed: {ex.Issues.Count} issue(s).");
            Console.Error.WriteLine("Tip: if the workspace is missing, run 'aos init' first.");
            WriteAosErrorLine(AosErrorMapper.Map(ex));

            try
            {
                AosCommandLogWriter.AppendCommand(
                    aosRootPath,
                    command: "repair indexes",
                    args: args,
                    exitCode: 2,
                    runId: null
                );
            }
            catch
            {
                // Best-effort; ignore.
            }

            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Repair failed.");
            Console.Error.WriteLine(ex.Message);
            WriteAosErrorLine(AosErrorMapper.Map(ex));

            try
            {
                AosCommandLogWriter.AppendCommand(
                    aosRootPath,
                    command: "repair indexes",
                    args: args,
                    exitCode: 2,
                    runId: null
                );
            }
            catch
            {
                // Best-effort; ignore.
            }

            return ExitCodeUnexpectedInternalError;
        }
    }

    private static int GetSpecCount(AosIndexRepairResult result, AosArtifactKind kind)
        => result.SpecCatalogCounts.TryGetValue(kind, out var count) ? count : 0;

    private static int FailUnknownRepairCommand(string subcommand)
    {
        Console.Error.WriteLine($"Unknown repair command '{subcommand}'.");
        PrintRepairUsage();
        return ExitCodeInvalidUsage;
    }

    private static int RunCheckpoint(string[] args)
    {
        if (args.Length == 0 || (args.Length == 1 && args[0] is "-h" or "--help"))
        {
            PrintCheckpointUsage();
            return args.Length == 0 ? ExitCodeInvalidUsage : ExitCodeSuccess;
        }

        var subcommand = args[0];
        var rest = args.Skip(1).ToArray();

        return subcommand switch
        {
            "create" => RunCheckpointCreate(rest),
            "restore" => RunCheckpointRestore(rest),
            _ => FailUnknownCheckpointCommand(subcommand)
        };
    }

    private static int RunPack(string[] args)
    {
        if (args.Length == 0 || (args.Length == 1 && args[0] is "-h" or "--help"))
        {
            PrintPackUsage();
            return args.Length == 0 ? ExitCodeInvalidUsage : ExitCodeSuccess;
        }

        var subcommand = args[0];
        var rest = args.Skip(1).ToArray();

        return subcommand switch
        {
            "build" => RunPackBuild(rest),
            _ => FailUnknownPackCommand(subcommand)
        };
    }

    private static int FailUnknownPackCommand(string subcommand)
    {
        Console.Error.WriteLine($"Unknown pack command '{subcommand}'.");
        PrintPackUsage();
        return ExitCodeInvalidUsage;
    }

    private static int RunPackBuild(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintPackBuildUsage();
            return ExitCodeSuccess;
        }

        string? repositoryRootPath = null;
        string? taskId = null;
        string? phaseId = null;
        var maxBytes = 262_144; // default: 256 KiB
        var maxItems = 128;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--root")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --root.");
                    PrintPackBuildUsage();
                    return ExitCodeInvalidUsage;
                }

                repositoryRootPath = args[++i];
                continue;
            }

            if (arg.StartsWith("--root=", StringComparison.Ordinal))
            {
                repositoryRootPath = arg["--root=".Length..];
                continue;
            }

            if (arg == "--task")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --task.");
                    PrintPackBuildUsage();
                    return ExitCodeInvalidUsage;
                }

                taskId = args[++i];
                continue;
            }

            if (arg.StartsWith("--task=", StringComparison.Ordinal))
            {
                taskId = arg["--task=".Length..];
                continue;
            }

            if (arg == "--phase")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --phase.");
                    PrintPackBuildUsage();
                    return ExitCodeInvalidUsage;
                }

                phaseId = args[++i];
                continue;
            }

            if (arg.StartsWith("--phase=", StringComparison.Ordinal))
            {
                phaseId = arg["--phase=".Length..];
                continue;
            }

            if (arg == "--max-bytes")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --max-bytes.");
                    PrintPackBuildUsage();
                    return ExitCodeInvalidUsage;
                }

                if (!int.TryParse(args[++i], out maxBytes) || maxBytes < 0)
                {
                    Console.Error.WriteLine("Invalid --max-bytes value.");
                    PrintPackBuildUsage();
                    return ExitCodeInvalidUsage;
                }

                continue;
            }

            if (arg.StartsWith("--max-bytes=", StringComparison.Ordinal))
            {
                var raw = arg["--max-bytes=".Length..];
                if (!int.TryParse(raw, out maxBytes) || maxBytes < 0)
                {
                    Console.Error.WriteLine("Invalid --max-bytes value.");
                    PrintPackBuildUsage();
                    return ExitCodeInvalidUsage;
                }

                continue;
            }

            if (arg == "--max-items")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --max-items.");
                    PrintPackBuildUsage();
                    return ExitCodeInvalidUsage;
                }

                if (!int.TryParse(args[++i], out maxItems) || maxItems < 1)
                {
                    Console.Error.WriteLine("Invalid --max-items value.");
                    PrintPackBuildUsage();
                    return ExitCodeInvalidUsage;
                }

                continue;
            }

            if (arg.StartsWith("--max-items=", StringComparison.Ordinal))
            {
                var raw = arg["--max-items=".Length..];
                if (!int.TryParse(raw, out maxItems) || maxItems < 1)
                {
                    Console.Error.WriteLine("Invalid --max-items value.");
                    PrintPackBuildUsage();
                    return ExitCodeInvalidUsage;
                }

                continue;
            }

            Console.Error.WriteLine($"Unknown option '{arg}'.");
            PrintPackBuildUsage();
            return ExitCodeInvalidUsage;
        }

        if (string.IsNullOrWhiteSpace(taskId) == string.IsNullOrWhiteSpace(phaseId))
        {
            Console.Error.WriteLine("Missing or ambiguous mode. Provide exactly one of --task <TSK-######> or --phase <PH-####>.");
            PrintPackBuildUsage();
            return ExitCodeInvalidUsage;
        }

        if (string.IsNullOrWhiteSpace(repositoryRootPath))
        {
            if (!TryDiscoverRepositoryRoot(out var discovered))
            {
                return ExitCodeKnownFailure;
            }

            repositoryRootPath = discovered;
        }

        if (!EnsureWorkspaceValid(repositoryRootPath, out var aosRootPath))
        {
            return ExitCodeKnownFailure;
        }

        var lockResult = AcquireWorkspaceLockOrNull(
            aosRootPath,
            command: "pack build",
            createDirectories: false
        );
        using var workspaceLock = lockResult.Handle;
        if (workspaceLock is null)
        {
            return lockResult.ExitCode;
        }

        try
        {
            var budget = new ContextPackBudget(MaxBytes: maxBytes, MaxItems: maxItems);
            var mode = taskId is not null ? AosContextPackBuilder.ModeTask : AosContextPackBuilder.ModePhase;
            var drivingId = (taskId ?? phaseId)!;

            var (packId, _, _, _) = AosContextPackWriter.BuildAndWriteNewPack(
                aosRootPath,
                mode: mode,
                drivingId: drivingId,
                budget: budget
            );

            try
            {
                AosCommandLogWriter.AppendCommand(
                    aosRootPath,
                    command: "pack build",
                    args: args,
                    exitCode: 0,
                    runId: null
                );
            }
            catch
            {
                // Best-effort only; ignore.
            }

            Console.WriteLine(packId);
            return ExitCodeSuccess;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("pack build failed.");
            Console.Error.WriteLine(ex.Message);
            WriteAosErrorLine(AosErrorMapper.Map(ex));
            return ExitCodeKnownFailure;
        }
    }

    private static int FailUnknownCheckpointCommand(string subcommand)
    {
        Console.Error.WriteLine($"Unknown checkpoint command '{subcommand}'.");
        PrintCheckpointUsage();
        return ExitCodeInvalidUsage;
    }

    private static int RunCheckpointCreate(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintCheckpointCreateUsage();
            return ExitCodeSuccess;
        }

        if (args.Length != 0)
        {
            Console.Error.WriteLine("Unexpected arguments for 'aos checkpoint create'.");
            PrintCheckpointCreateUsage();
            return ExitCodeInvalidUsage;
        }

        if (!TryDiscoverRepositoryRoot(out var repositoryRootPath))
        {
            return ExitCodeKnownFailure;
        }
        if (!EnsureWorkspaceValid(repositoryRootPath, out var aosRootPath))
        {
            return ExitCodeKnownFailure;
        }

        var lockResult = AcquireWorkspaceLockOrNull(
            aosRootPath,
            command: "checkpoint create",
            createDirectories: false
        );
        using var workspaceLock = lockResult.Handle;
        if (workspaceLock is null)
        {
            return lockResult.ExitCode;
        }

        try
        {
            var checkpointId = CreateCheckpoint(aosRootPath);

            // Spec doesn't prescribe output text; print the ID for orchestration friendliness.
            Console.WriteLine(checkpointId);
            return ExitCodeSuccess;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("checkpoint create failed.");
            Console.Error.WriteLine(ex.Message);
            WriteAosErrorLine(
                ex is AosInvalidStateTransitionException or JsonException
                    ? AosErrorMapper.Map(ex)
                    : new AosErrorEnvelope(AosErrorCodes.CheckpointFailed, ex.Message));
            return ExitCodeKnownFailure;
        }
    }

    private static int RunCheckpointRestore(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintCheckpointRestoreUsage();
            return ExitCodeSuccess;
        }

        string? checkpointId = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--checkpoint-id")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --checkpoint-id.");
                    PrintCheckpointRestoreUsage();
                    return ExitCodeInvalidUsage;
                }

                checkpointId = args[++i];
                continue;
            }

            if (arg.StartsWith("--checkpoint-id=", StringComparison.Ordinal))
            {
                checkpointId = arg["--checkpoint-id=".Length..];
                continue;
            }

            Console.Error.WriteLine($"Unknown option '{arg}'.");
            PrintCheckpointRestoreUsage();
            return ExitCodeInvalidUsage;
        }

        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            Console.Error.WriteLine("Missing required option --checkpoint-id.");
            PrintCheckpointRestoreUsage();
            return ExitCodeInvalidUsage;
        }

        if (!TryDiscoverRepositoryRoot(out var repositoryRootPath))
        {
            return ExitCodeKnownFailure;
        }
        if (!EnsureWorkspaceValid(repositoryRootPath, out var aosRootPath))
        {
            return ExitCodeKnownFailure;
        }

        var lockResult = AcquireWorkspaceLockOrNull(
            aosRootPath,
            command: "checkpoint restore",
            createDirectories: false
        );
        using var workspaceLock = lockResult.Handle;
        if (workspaceLock is null)
        {
            return lockResult.ExitCode;
        }

        try
        {
            RestoreCheckpoint(aosRootPath, checkpointId);
            Console.WriteLine(checkpointId);
            return ExitCodeSuccess;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("checkpoint restore failed.");
            Console.Error.WriteLine(ex.Message);
            WriteAosErrorLine(
                ex is AosInvalidStateTransitionException or JsonException
                    ? AosErrorMapper.Map(ex)
                    : new AosErrorEnvelope(AosErrorCodes.CheckpointFailed, ex.Message));
            return ExitCodeKnownFailure;
        }
    }

    private static int RunValidateSchemas(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintValidateSchemasUsage();
            return ExitCodeSuccess;
        }

        string? repositoryRootPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--root")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --root.");
                    PrintValidateSchemasUsage();
                    return ExitCodeInvalidUsage;
                }

                repositoryRootPath = args[++i];
                continue;
            }

            if (arg.StartsWith("--root=", StringComparison.Ordinal))
            {
                repositoryRootPath = arg["--root=".Length..];
                continue;
            }

            Console.Error.WriteLine($"Unknown option '{arg}'.");
            PrintValidateSchemasUsage();
            return ExitCodeInvalidUsage;
        }

        if (string.IsNullOrWhiteSpace(repositoryRootPath))
        {
            if (!TryDiscoverRepositoryRoot(out var discovered))
            {
                return ExitCodeKnownFailure;
            }

            repositoryRootPath = discovered;
        }

        IReadOnlyList<AosLocalSchemaRegistryLoader.LocalSchema> schemas;

        try
        {
            schemas = AosLocalSchemaRegistryLoader.LoadLocalSchemas(repositoryRootPath);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine("Schema validation failed.");
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Run 'aos init' to seed the local schema pack under '.aos/schemas/'.");
            WriteAosErrorLine(new AosErrorEnvelope(
                Code: AosErrorCodes.SchemaPackInvalid,
                Message: "Schema validation failed.",
                Details: new { Exception = ex.Message }
            ));
            return ExitCodeKnownFailure;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Schema validation failed.");
            Console.Error.WriteLine(ex.Message);
            WriteAosErrorLine(AosErrorMapper.Map(ex));
            return ExitCodeUnexpectedInternalError;
        }

        var report = AosSchemaPackValidator.ValidateLocalSchemas(schemas);

        Console.WriteLine($"Repository root: {repositoryRootPath}");
        Console.WriteLine($"Local schemas discovered: {report.SchemaCount}");

        if (report.Issues.Count == 0)
        {
            foreach (var schema in schemas)
            {
                Console.WriteLine($"PASS {schema.FileName} (id={schema.Id})");
            }

            Console.WriteLine("OK");
            return ExitCodeSuccess;
        }

        foreach (var schema in schemas)
        {
            var issues = report.Issues.Where(i => string.Equals(i.SchemaFileName, schema.FileName, StringComparison.Ordinal)).ToArray();
            if (issues.Length == 0)
            {
                Console.WriteLine($"PASS {schema.FileName} (id={schema.Id})");
                continue;
            }

            Console.WriteLine($"FAIL {schema.FileName} (id={schema.Id})");
            foreach (var issue in issues)
            {
                Console.WriteLine($"  - {issue.Message}");
            }
        }

        Console.Error.WriteLine($"Validation failed: {report.Issues.Count} issue(s).");
        WriteAosErrorLine(new AosErrorEnvelope(
            Code: AosErrorCodes.SchemaPackInvalid,
            Message: "Schema pack validation failed.",
            Details: new { Issues = report.Issues.Select(i => new { i.SchemaFileName, i.Message }).ToArray() }
        ));
        return ExitCodeKnownFailure;
    }

    private static int RunValidateWorkspace(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintValidateWorkspaceUsage();
            return ExitCodeSuccess;
        }

        string? repositoryRootPath = null;
        string? layersCsv = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--root")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --root.");
                    PrintValidateWorkspaceUsage();
                    return ExitCodeInvalidUsage;
                }

                repositoryRootPath = args[++i];
                continue;
            }

            if (arg.StartsWith("--root=", StringComparison.Ordinal))
            {
                repositoryRootPath = arg["--root=".Length..];
                continue;
            }

            if (arg == "--layers")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --layers.");
                    PrintValidateWorkspaceUsage();
                    return ExitCodeInvalidUsage;
                }

                layersCsv = args[++i];
                continue;
            }

            if (arg.StartsWith("--layers=", StringComparison.Ordinal))
            {
                layersCsv = arg["--layers=".Length..];
                continue;
            }

            Console.Error.WriteLine($"Unknown option '{arg}'.");
            PrintValidateWorkspaceUsage();
            return ExitCodeInvalidUsage;
        }

        if (string.IsNullOrWhiteSpace(repositoryRootPath))
        {
            if (!TryDiscoverRepositoryRoot(out var discovered))
            {
                return ExitCodeKnownFailure;
            }

            repositoryRootPath = discovered;
        }

        IEnumerable<AosWorkspaceLayer>? layers = null;

        if (!string.IsNullOrWhiteSpace(layersCsv))
        {
            var tokens = layersCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                Console.Error.WriteLine("Invalid --layers value.");
                PrintValidateWorkspaceUsage();
                return ExitCodeInvalidUsage;
            }

            var parsed = new List<AosWorkspaceLayer>(capacity: tokens.Length);
            foreach (var token in tokens)
            {
                if (!TryParseLayer(token, out var layer))
                {
                    Console.Error.WriteLine($"Invalid layer '{token}'. Expected a comma-separated list from: spec,state,evidence,codebase,context,config.");
                    PrintValidateWorkspaceUsage();
                    return ExitCodeInvalidUsage;
                }

                parsed.Add(layer);
            }

            layers = parsed;
        }

        var report = AosWorkspaceValidator.Validate(repositoryRootPath, layers);

        Console.WriteLine($"Repository root: {report.RepositoryRootPath}");
        Console.WriteLine($"AOS root: {report.AosRootPath}");
        Console.WriteLine($"Layers: {string.Join(",", report.Layers.Select(ToLayerName))}");

        if (report.Issues.Count == 0)
        {
            Console.WriteLine("OK");
            return ExitCodeSuccess;
        }

        foreach (var issue in report.Issues)
        {
            var layerLabel = issue.Layer is null ? "unknown" : ToLayerName(issue.Layer.Value);
            if (!string.IsNullOrWhiteSpace(issue.SchemaId))
            {
                var loc = issue.InstanceLocation ?? "";
                if (!string.IsNullOrWhiteSpace(loc))
                {
                    Console.WriteLine($"FAIL [{layerLabel}] {issue.ContractPath} - ({issue.SchemaId} @ {loc}) {issue.Message}");
                }
                else
                {
                    Console.WriteLine($"FAIL [{layerLabel}] {issue.ContractPath} - ({issue.SchemaId}) {issue.Message}");
                }
            }
            else
            {
                Console.WriteLine($"FAIL [{layerLabel}] {issue.ContractPath} - {issue.Message}");
            }
        }

        Console.Error.WriteLine($"Validation failed: {report.Issues.Count} issue(s).");
        foreach (var hint in BuildValidateWorkspaceHints(report))
        {
            Console.Error.WriteLine($"Hint: {hint}");
        }

        WriteAosErrorLine(AosErrorMapper.FromWorkspaceValidationReport(report));
        return ExitCodeKnownFailure;
    }

    private static int FailUnknownValidateCommand(string subcommand)
    {
        Console.Error.WriteLine($"Unknown validate command '{subcommand}'.");
        PrintValidateUsage();
        return ExitCodeInvalidUsage;
    }

    private static int RunExecutePlan(string[] args)
    {
        if (args.Length == 0 || (args.Length == 1 && args[0] is "-h" or "--help"))
        {
            PrintExecutePlanUsage();
            return args.Length == 0 ? ExitCodeInvalidUsage : ExitCodeSuccess;
        }

        string? planPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--plan")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --plan.");
                    PrintExecutePlanUsage();
                    return ExitCodeInvalidUsage;
                }

                planPath = args[++i];
                continue;
            }

            if (arg.StartsWith("--plan=", StringComparison.Ordinal))
            {
                planPath = arg["--plan=".Length..];
                continue;
            }

            Console.Error.WriteLine($"Unknown option '{arg}'.");
            PrintExecutePlanUsage();
            return ExitCodeInvalidUsage;
        }

        if (string.IsNullOrWhiteSpace(planPath))
        {
            Console.Error.WriteLine("Missing required option --plan.");
            PrintExecutePlanUsage();
            return ExitCodeInvalidUsage;
        }

        if (!TryDiscoverRepositoryRoot(out var repositoryRootPath))
        {
            return ExitCodeKnownFailure;
        }
        if (!EnsureWorkspaceValid(repositoryRootPath, out var aosRootPath))
        {
            return ExitCodeKnownFailure;
        }

        // Policy gates MUST run before any execution that could mutate the workspace (including lock acquisition).
        var policyResult = AosPolicyLoader.LoadAndValidate(repositoryRootPath);
        if (!policyResult.Exists)
        {
            Console.Error.WriteLine($"Missing required policy file: {AosPolicyLoader.PolicyContractPath}");
            WriteAosErrorLine(new AosErrorEnvelope(
                Code: AosErrorCodes.PolicyViolation,
                Message: $"Missing required policy file: {AosPolicyLoader.PolicyContractPath}"
            ));
            return ExitCodePolicyViolation;
        }

        if (policyResult.Report.Issues.Count > 0 || policyResult.Policy is null)
        {
            Console.Error.WriteLine($"Policy validation failed: {policyResult.Report.Issues.Count} issue(s).");
            foreach (var issue in policyResult.Report.Issues)
            {
                Console.Error.WriteLine($"{AosPolicyLoader.PolicyContractPath} {issue.JsonPath} - {issue.Message}");
            }
            WriteAosErrorLine(new AosErrorEnvelope(
                Code: AosErrorCodes.PolicyViolation,
                Message: "Policy validation failed."
            ));
            return ExitCodePolicyViolation;
        }

        try
        {
            var lockPath = AosPathRouter.GetWorkspaceLockPath(aosRootPath);
            AosPolicyEnforcer.EnsureWritePathAllowed(
                repositoryRootPath,
                policyResult.Policy.ScopeAllowlist.Write,
                lockPath,
                targetLabel: "workspace lock"
            );
        }
        catch (AosPolicyViolationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            WriteAosErrorLine(AosErrorMapper.Map(ex));
            return ExitCodePolicyViolation;
        }

        var lockResult = AcquireWorkspaceLockOrNull(
            aosRootPath,
            command: "execute-plan",
            createDirectories: false
        );
        using var workspaceLock = lockResult.Handle;
        if (workspaceLock is null)
        {
            return lockResult.ExitCode;
        }

        // Auto-start a new run for execute-plan.
        var runId = AosRunId.New();

        // Enforce policy scope for all run evidence/outputs/log writes before creating any directories/files.
        try
        {
            var runEvidenceRoot = AosPathRouter.GetRunEvidenceRootPath(aosRootPath, runId);
            var runOutputsRoot = AosPathRouter.GetRunOutputsRootPath(aosRootPath, runId);
            var runLogsRoot = AosPathRouter.GetRunLogsRootPath(aosRootPath, runId);

            AosPolicyEnforcer.EnsureWritePathAllowed(
                repositoryRootPath,
                policyResult.Policy.ScopeAllowlist.Write,
                runEvidenceRoot,
                targetLabel: "run evidence"
            );
            AosPolicyEnforcer.EnsureWritePathAllowed(
                repositoryRootPath,
                policyResult.Policy.ScopeAllowlist.Write,
                runOutputsRoot,
                targetLabel: "run outputs"
            );
            AosPolicyEnforcer.EnsureWritePathAllowed(
                repositoryRootPath,
                policyResult.Policy.ScopeAllowlist.Write,
                runLogsRoot,
                targetLabel: "run logs"
            );
        }
        catch (AosPolicyViolationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            WriteAosErrorLine(AosErrorMapper.Map(ex));
            return ExitCodePolicyViolation;
        }

        AosRunEvidenceScaffolder.EnsureRunEvidenceScaffold(
            aosRootPath,
            runId,
            startedAtUtc: DateTimeOffset.UtcNow,
            command: "execute-plan",
            args: args
        );

        try
        {
            var plan = ExecutePlanPlanLoader.LoadFromFile(planPath);
            AosRunEvidenceScaffolder.PopulateExecutePlanPacketFields(
                aosRootPath,
                runId,
                args,
                planPath,
                plan
            );

            var runOutputsRootPath = AosPathRouter.GetRunOutputsRootPath(aosRootPath, runId);

            // Provider/tool call envelopes: minimal record-only runtime path (replay disabled).
            var envelopeLogger = new AosCallEnvelopeFileLogger(aosRootPath, runId);
            var outputsWritten = AosCallEnvelopeRuntime.InvokeRecordOnly(
                runId: runId,
                provider: "aos",
                tool: "execute-plan.write-outputs",
                callId: "execute-plan.write-outputs",
                request: new { outputCount = plan.Outputs.Count },
                invoke: () => ExecutePlanExecutor.WriteOutputs(runOutputsRootPath, plan),
                logger: envelopeLogger
            );
            ExecutePlanActionsLogWriter.WriteActionsLog(aosRootPath, runId, outputsWritten);

            // Finish the run on success.
            AosRunEvidenceScaffolder.FinishRun(
                aosRootPath,
                runId,
                finishedAtUtc: DateTimeOffset.UtcNow,
                additionalProducedArtifacts:
                [
                    (
                        Kind: "log",
                        ContractPath: $".aos/evidence/runs/{runId}/logs/execute-plan.actions.json",
                        Sha256: null
                    )
                ]
            );

            try
            {
                AosCommandLogWriter.AppendCommand(
                    aosRootPath,
                    command: "execute-plan",
                    args: args,
                    exitCode: 0,
                    runId: runId
                );

                // Maintain the per-run view for discoverability.
                AosRunCommandsViewWriter.WriteRunCommandsView(aosRootPath, runId);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Warning: failed to append to commands.json.");
                Console.Error.WriteLine(ex.Message);
            }

            Console.WriteLine(runId);
            return ExitCodeSuccess;
        }
        catch (AosPolicyViolationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            var envelope = AosErrorMapper.Map(ex);
            WriteAosErrorLine(envelope);

            try
            {
                AosRunEvidenceScaffolder.FailRun(
                    aosRootPath,
                    runId,
                    finishedAtUtc: DateTimeOffset.UtcNow,
                    exitCode: ExitCodePolicyViolation,
                    error: envelope
                );
            }
            catch
            {
                // Best-effort only; preserve original failure path.
            }
            return ExitCodePolicyViolation;
        }
        catch (ExecutePlanPlanLoadException ex)
        {
            Console.Error.WriteLine(ex.Message);
            var envelope = AosErrorMapper.Map(ex);
            WriteAosErrorLine(envelope);

            try
            {
                AosRunEvidenceScaffolder.FailRun(
                    aosRootPath,
                    runId,
                    finishedAtUtc: DateTimeOffset.UtcNow,
                    exitCode: ExitCodeKnownFailure,
                    error: envelope
                );
            }
            catch
            {
                // Best-effort only; preserve original failure path.
            }
            return ExitCodeKnownFailure;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("execute-plan failed.");
            Console.Error.WriteLine(ex.Message);
            var envelope = AosErrorMapper.Map(ex);
            WriteAosErrorLine(envelope);

            try
            {
                AosRunEvidenceScaffolder.FailRun(
                    aosRootPath,
                    runId,
                    finishedAtUtc: DateTimeOffset.UtcNow,
                    exitCode: ExitCodeUnexpectedInternalError,
                    error: envelope
                );
            }
            catch
            {
                // Best-effort only; preserve original failure path.
            }
            return ExitCodeUnexpectedInternalError;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: aos <command> [subcommand] [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  init                Initialize the .aos workspace skeleton");
        Console.WriteLine("  run start           Start a run and scaffold evidence under .aos/evidence/runs/");
        Console.WriteLine("  run finish          Finish a run and update run evidence");
        Console.WriteLine("  pack build          Build a deterministic context pack under .aos/context/packs/");
        Console.WriteLine("  validate schemas    Validate the local JSON Schema pack under .aos/schemas/ (supports --root)");
        Console.WriteLine("  validate workspace  Validate the AOS workspace rooted at .aos/ (supports --layers, --root)");
        Console.WriteLine("  config validate     Validate the AOS config at .aos/config/config.json (supports --root)");
        Console.WriteLine("  lock status         Show workspace lock status (supports --root)");
        Console.WriteLine("  lock acquire        Acquire and hold the workspace lock until released (supports --root)");
        Console.WriteLine("  lock release        Release the workspace lock (supports --root, --force)");
        Console.WriteLine("  cache clear         Clear disposable cache under .aos/cache/ (supports --root)");
        Console.WriteLine("  cache prune         Prune disposable cache under .aos/cache/ (supports --days, --root)");
        Console.WriteLine("  repair indexes      Rebuild deterministic indexes from on-disk state (supports --root)");
        Console.WriteLine("  execute-plan        Execute a persisted plan file and record evidence");
        Console.WriteLine("  checkpoint create   Create a state checkpoint under .aos/state/checkpoints/");
        Console.WriteLine("  checkpoint restore  Restore a checkpoint back into .aos/state/state.json");
        Console.WriteLine();
        Console.WriteLine("Run 'aos <command> --help' for command-specific help.");
        Console.WriteLine("Run 'aos run <subcommand> --help' for run subcommand help.");
        Console.WriteLine("Run 'aos validate <subcommand> --help' for validation subcommand help.");
        Console.WriteLine("Run 'aos config <subcommand> --help' for config subcommand help.");
        Console.WriteLine("Run 'aos lock <subcommand> --help' for lock subcommand help.");
        Console.WriteLine("Run 'aos cache <subcommand> --help' for cache subcommand help.");
        Console.WriteLine("Run 'aos repair <subcommand> --help' for repair subcommand help.");
        Console.WriteLine("Run 'aos execute-plan --help' for execute-plan help.");
        Console.WriteLine("Run 'aos checkpoint <subcommand> --help' for checkpoint help.");
        Console.WriteLine("Run 'aos pack <subcommand> --help' for pack help.");
    }

    private static void PrintPackUsage()
    {
        Console.WriteLine("Usage: aos pack <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  build  Build a deterministic context pack under .aos/context/packs/");
        Console.WriteLine();
        Console.WriteLine("Run 'aos pack <command> --help' for command-specific help.");
    }

    private static void PrintPackBuildUsage()
    {
        Console.WriteLine("Usage: aos pack build (--task <TSK-######> | --phase <PH-####>) [options]");
        Console.WriteLine();
        Console.WriteLine("Builds a deterministic, budgeted context pack under '.aos/context/packs/'.");
        Console.WriteLine("On success, prints the created pack id (PCK-####) to STDOUT.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --task <TSK-######>     Build a task-mode pack driven by the task plan");
        Console.WriteLine("                         (also accepted: --task=<TSK-######>)");
        Console.WriteLine("  --phase <PH-####>       Build a phase-mode pack driven by the phase spec");
        Console.WriteLine("                         (also accepted: --phase=<PH-####>)");
        Console.WriteLine("  --max-bytes <n>         Max total embedded bytes (default: 262144)");
        Console.WriteLine("                         (also accepted: --max-bytes=<n>)");
        Console.WriteLine("  --max-items <n>         Max number of entries (default: 128)");
        Console.WriteLine("                         (also accepted: --max-items=<n>)");
        Console.WriteLine("  --root <path>           Repository root path (defaults to auto-detected root from current directory)");
        Console.WriteLine("                         (also accepted: --root=<path>)");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid usage / options");
        Console.WriteLine("  2  Failed (workspace invalid or pack build error)");
        Console.WriteLine("  4  Workspace locked (contention)");
    }

    private static void PrintRunUsage()
    {
        Console.WriteLine("Usage: aos run <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  start   Start a run and scaffold evidence under .aos/evidence/runs/");
        Console.WriteLine("  finish  Finish a run and update run evidence");
        Console.WriteLine("  pause   Pause a running run (status: started -> paused)");
        Console.WriteLine("  resume  Resume a paused run (status: paused -> started)");
        Console.WriteLine();
        Console.WriteLine("Run 'aos run <command> --help' for command-specific help.");
    }

    private static void PrintRunStartUsage()
    {
        Console.WriteLine("Usage: aos run start");
    }

    private static void PrintRunFinishUsage()
    {
        Console.WriteLine("Usage: aos run finish --run-id <run-id>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --run-id <run-id>   Run ID to finish (also accepted: --run-id=<run-id>)");
    }

    private static void PrintRunPauseUsage()
    {
        Console.WriteLine("Usage: aos run pause --run-id <run-id>");
        Console.WriteLine();
        Console.WriteLine("Pauses a running run, updating its status to 'paused'.");
        Console.WriteLine("Only runs in 'started' status can be paused.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --run-id <run-id>   Run ID to pause (also accepted: --run-id=<run-id>)");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid usage / options");
        Console.WriteLine("  2  Failed (run not found or invalid status)");
        Console.WriteLine("  4  Workspace locked (contention)");
        Console.WriteLine("  5  Unexpected internal error");
    }

    private static void PrintRunResumeUsage()
    {
        Console.WriteLine("Usage: aos run resume --run-id <run-id>");
        Console.WriteLine();
        Console.WriteLine("Resumes a paused run, updating its status back to 'started'.");
        Console.WriteLine("Only runs in 'paused' status can be resumed.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --run-id <run-id>   Run ID to resume (also accepted: --run-id=<run-id>)");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid usage / options");
        Console.WriteLine("  2  Failed (run not found or invalid status)");
        Console.WriteLine("  4  Workspace locked (contention)");
        Console.WriteLine("  5  Unexpected internal error");
    }

    private static void PrintValidateUsage()
    {
        Console.WriteLine("Usage: aos validate <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  schemas   Validate the local JSON Schema pack under .aos/schemas/ (supports --root)");
        Console.WriteLine("  workspace Validate the AOS workspace rooted at .aos/ (supports --layers, --root)");
        Console.WriteLine();
        Console.WriteLine("Run 'aos validate <command> --help' for command-specific help.");
    }

    private static void PrintConfigUsage()
    {
        Console.WriteLine("Usage: aos config <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  validate  Validate '.aos/config/config.json' (secrets-by-reference only)");
        Console.WriteLine();
        Console.WriteLine("Run 'aos config <command> --help' for command-specific help.");
    }

    private static void PrintConfigValidateUsage()
    {
        Console.WriteLine("Usage: aos config validate [--root <path>]");
        Console.WriteLine();
        Console.WriteLine("Validates the AOS config document at '.aos/config/config.json':");
        Console.WriteLine("  - file exists and is valid JSON");
        Console.WriteLine("  - schemaVersion is supported");
        Console.WriteLine("  - secrets are represented by reference only (no plaintext secret strings)");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --root <path>   Repository root path (defaults to auto-detected root from current directory)");
        Console.WriteLine("                 (also accepted: --root=<path>)");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid usage / options");
        Console.WriteLine("  2  Validation failed / missing config");
    }

    private static void PrintLockUsage()
    {
        Console.WriteLine("Usage: aos lock <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  status   Show whether the workspace is locked");
        Console.WriteLine("  acquire  Acquire and hold the workspace lock until released");
        Console.WriteLine("  release  Release the workspace lock");
        Console.WriteLine();
        Console.WriteLine("Run 'aos lock <command> --help' for command-specific help.");
    }

    private static void PrintCacheUsage()
    {
        Console.WriteLine("Usage: aos cache <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  clear  Clear disposable cache under '.aos/cache/**'");
        Console.WriteLine("  prune  Prune disposable cache entries under '.aos/cache/**'");
        Console.WriteLine();
        Console.WriteLine("Run 'aos cache <command> --help' for command-specific help.");
    }

    private static void PrintCacheClearUsage()
    {
        Console.WriteLine("Usage: aos cache clear [--root <path>]");
        Console.WriteLine();
        Console.WriteLine("Clears all entries under '.aos/cache/**' but preserves the '.aos/cache/' directory.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --root <path>   Repository root path (defaults to auto-detected root from current directory)");
        Console.WriteLine("                 (also accepted: --root=<path>)");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid usage / options");
        Console.WriteLine("  2  Failed (workspace invalid or cache clear error)");
        Console.WriteLine("  4  Workspace locked (contention)");
    }

    private static void PrintCachePruneUsage()
    {
        Console.WriteLine("Usage: aos cache prune [--days <n>] [--root <path>]");
        Console.WriteLine();
        Console.WriteLine("Prunes entries under '.aos/cache/**' older than N days using filesystem timestamps.");
        Console.WriteLine("Default N is 30. Use '--days 0' to prune all cache entries.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --days <n>       Age threshold in days (non-negative integer; default: 30)");
        Console.WriteLine("                 (also accepted: --days=<n>)");
        Console.WriteLine("  --root <path>    Repository root path (defaults to auto-detected root from current directory)");
        Console.WriteLine("                 (also accepted: --root=<path>)");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid usage / options");
        Console.WriteLine("  2  Failed (workspace invalid or cache prune error)");
        Console.WriteLine("  4  Workspace locked (contention)");
    }

    private static void PrintLockStatusUsage()
    {
        Console.WriteLine("Usage: aos lock status [--root <path>]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --root <path>   Repository root path (defaults to auto-detected root from current directory)");
        Console.WriteLine("                 (also accepted: --root=<path>)");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success (locked or unlocked)");
        Console.WriteLine("  1  Invalid usage / options");
        Console.WriteLine("  2  Lock file exists but is invalid / unparseable");
    }

    private static void PrintLockAcquireUsage()
    {
        Console.WriteLine("Usage: aos lock acquire [--root <path>]");
        Console.WriteLine();
        Console.WriteLine("Acquires and holds the exclusive workspace lock by writing:");
        Console.WriteLine($"  - {AosPathRouter.WorkspaceLockContractPath}");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --root <path>   Repository root path (defaults to auto-detected root from current directory)");
        Console.WriteLine("                 (also accepted: --root=<path>)");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid usage / options");
        Console.WriteLine("  2  Failed to acquire (already locked / workspace missing)");
    }

    private static void PrintLockReleaseUsage()
    {
        Console.WriteLine("Usage: aos lock release [--root <path>] [--force]");
        Console.WriteLine();
        Console.WriteLine("Releases the exclusive workspace lock by deleting:");
        Console.WriteLine($"  - {AosPathRouter.WorkspaceLockContractPath}");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --root <path>   Repository root path (defaults to auto-detected root from current directory)");
        Console.WriteLine("                 (also accepted: --root=<path>)");
        Console.WriteLine("  --force         Delete the lock even if it cannot be parsed");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid usage / options");
        Console.WriteLine("  2  Failed to release (invalid lock file unless --force)");
    }

    private static void PrintSecretUsage()
    {
        Console.WriteLine("Usage: aos secret <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  set    Store a secret in the secure secret store");
        Console.WriteLine("  get    Retrieve a secret from the secret store (output masked)");
        Console.WriteLine("  list   List all secret names (not values)");
        Console.WriteLine("  delete Delete a secret from the secret store");
        Console.WriteLine();
        Console.WriteLine("Run 'aos secret <command> --help' for command-specific help.");
    }

    private static void PrintSecretSetUsage()
    {
        Console.WriteLine("Usage: aos secret set <name> <value>");
        Console.WriteLine();
        Console.WriteLine("Stores a secret in the secure secret store (Windows Credential Manager on Windows).");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <name>   Secret name (e.g., 'openai-key')");
        Console.WriteLine("  <value>  Secret value (will be stored securely)");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid usage / options");
        Console.WriteLine("  2  Failed to set secret");
    }

    private static void PrintSecretGetUsage()
    {
        Console.WriteLine("Usage: aos secret get <name>");
        Console.WriteLine();
        Console.WriteLine("Retrieves a secret from the secret store. The value is masked in output.");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <name>  Secret name to retrieve");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid usage / options");
        Console.WriteLine("  2  Secret not found or failed to retrieve");
    }

    private static void PrintSecretListUsage()
    {
        Console.WriteLine("Usage: aos secret list");
        Console.WriteLine();
        Console.WriteLine("Lists all secret names stored in the secret store (values are not shown).");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid usage / options");
        Console.WriteLine("  2  Failed to list secrets");
    }

    private static void PrintSecretDeleteUsage()
    {
        Console.WriteLine("Usage: aos secret delete <name>");
        Console.WriteLine();
        Console.WriteLine("Deletes a secret from the secret store.");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <name>  Secret name to delete");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid usage / options");
        Console.WriteLine("  2  Secret not found or failed to delete");
    }

    private static void PrintRepairUsage()
    {
        Console.WriteLine("Usage: aos repair <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  indexes  Rebuild spec catalog indexes and run index deterministically from disk state");
        Console.WriteLine();
        Console.WriteLine("Run 'aos repair <command> --help' for command-specific help.");
    }

    private static void PrintRepairIndexesUsage()
    {
        Console.WriteLine("Usage: aos repair indexes [--root <path>]");
        Console.WriteLine();
        Console.WriteLine("Rebuilds deterministic indexes from on-disk contract locations:");
        Console.WriteLine("  - spec catalog indexes under '.aos/spec/**/index.json'");
        Console.WriteLine("  - run index under '.aos/evidence/runs/index.json' (from '.aos/evidence/runs/<run-id>/artifacts/run.json')");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --root <path>   Repository root path (defaults to auto-detected root from current directory)");
        Console.WriteLine("                 (also accepted: --root=<path>)");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid usage / options");
        Console.WriteLine("  2  Repair failed (workspace inconsistent or invalid artifacts found)");
    }

    private static void PrintValidateSchemasUsage()
    {
        Console.WriteLine("Usage: aos validate schemas [--root <path>]");
        Console.WriteLine();
        Console.WriteLine("Validates the local schema pack under '.aos/schemas/**' created by 'aos init':");
        Console.WriteLine("  - '.aos/schemas/registry.json' exists and is well-formed");
        Console.WriteLine("  - registry 'schemas' is non-empty, canonical, and contains no duplicates");
        Console.WriteLine("  - each referenced schema file exists and is valid JSON");
        Console.WriteLine("  - each schema is minimally well-formed as JSON Schema");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --root <path>   Repository root path (defaults to auto-detected root from current directory)");
        Console.WriteLine("                 (also accepted: --root=<path>)");
    }

    private static void PrintValidateWorkspaceUsage()
    {
        Console.WriteLine("Usage: aos validate workspace [--layers <layers>] [--root <path>]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --layers <layers>  Comma-separated list from: spec,state,evidence,codebase,context,config (defaults to all layers)");
        Console.WriteLine("                    (also accepted: --layers=<layers>)");
        Console.WriteLine("  --root <path>      Repository root path (defaults to auto-detected root from current directory)");
    }

    private static void PrintInitUsage()
    {
        Console.WriteLine("Usage: aos init [--root <path>]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --root <path>   Repository root path (defaults to auto-detected root from current directory)");
    }

    private static void PrintExecutePlanUsage()
    {
        Console.WriteLine("Usage: aos execute-plan --plan <path>");
        Console.WriteLine();
        Console.WriteLine("Executes a persisted plan JSON file and records run evidence.");
        Console.WriteLine("On success, writes outputs under '.aos/evidence/runs/<run-id>/outputs/**' and prints the run id to STDOUT.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --plan <path>   Path to a persisted plan JSON file to execute");
        Console.WriteLine("                 (also accepted: --plan=<path>)");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid usage / options");
        Console.WriteLine("  2  Execution failed / invalid plan");
        Console.WriteLine("  3  Policy violation");
        Console.WriteLine("  4  Workspace locked (contention)");
        Console.WriteLine("  5  Unexpected internal error");
    }

    private static void PrintCheckpointUsage()
    {
        Console.WriteLine("Usage: aos checkpoint <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  create   Create a state checkpoint under '.aos/state/checkpoints/**'");
        Console.WriteLine("  restore  Restore a checkpoint into '.aos/state/state.json'");
        Console.WriteLine();
        Console.WriteLine("Run 'aos checkpoint <command> --help' for command-specific help.");
    }

    private static void PrintCheckpointCreateUsage()
    {
        Console.WriteLine("Usage: aos checkpoint create");
        Console.WriteLine();
        Console.WriteLine("Creates a new checkpoint folder under '.aos/state/checkpoints/**' containing:");
        Console.WriteLine("  - checkpoint.json  (metadata)");
        Console.WriteLine("  - state.json       (snapshot of '.aos/state/state.json')");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid usage / options");
        Console.WriteLine("  2  Failed (workspace invalid or state snapshot missing)");
        Console.WriteLine("  4  Workspace locked (contention)");
    }

    private static void PrintCheckpointRestoreUsage()
    {
        Console.WriteLine("Usage: aos checkpoint restore --checkpoint-id <id>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --checkpoint-id <id>   Checkpoint ID to restore (also accepted: --checkpoint-id=<id>)");
        Console.WriteLine();
        Console.WriteLine("Restores '.aos/state/state.json' from:");
        Console.WriteLine("  - '.aos/state/checkpoints/<id>/state.json'");
        Console.WriteLine("and appends an event describing the restore to '.aos/state/events.ndjson'.");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid usage / options");
        Console.WriteLine("  2  Failed (checkpoint missing or invalid)");
        Console.WriteLine("  4  Workspace locked (contention)");
    }

    private static string CreateCheckpoint(string aosRootPath)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));

        // Validate transition BEFORE any state/checkpoint writes.
        _ = Gmsd.Aos.Engine.StateTransitions.AosStateTransitionEngine.ValidateTransitionOrThrow(
            aosRootPath,
            Gmsd.Aos.Engine.StateTransitions.AosStateTransitionTable.Kinds.CheckpointCreated
        );

        // Validate current state snapshot is readable before allocating IDs / creating directories.
        var statePath = Path.Combine(aosRootPath, "state", "state.json");
        using var snapshotDoc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(statePath));

        var checkpointsRoot = Path.Combine(aosRootPath, "state", "checkpoints");
        Directory.CreateDirectory(checkpointsRoot);

        var checkpointId = AllocateNextCheckpointId(checkpointsRoot);
        var checkpointDir = Path.Combine(checkpointsRoot, checkpointId);
        Directory.CreateDirectory(checkpointDir);

        var metadata = new CheckpointMetadataDocument(
            SchemaVersion: 1,
            CheckpointId: checkpointId,
            SourceStateContractPath: AosStateStore.StateContractPath,
            SnapshotFile: "state.json"
        );

        var metadataPath = Path.Combine(checkpointDir, "checkpoint.json");
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            metadataPath,
            metadata,
            serializerOptions: new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                WriteIndented = true
            },
            writeIndented: true
        );

        var snapshotPath = Path.Combine(checkpointDir, "state.json");
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(snapshotPath, snapshotDoc.RootElement, writeIndented: true);

        // Record the create as a state event for auditability.
        var stateStore = new AosStateStore(aosRootPath);
        stateStore.AppendEvent(new CheckpointEventDocument(
            SchemaVersion: 1,
            Kind: Gmsd.Aos.Engine.StateTransitions.AosStateTransitionTable.Kinds.CheckpointCreated,
            CheckpointId: checkpointId,
            CheckpointContractPath: $".aos/state/checkpoints/{checkpointId}/checkpoint.json",
            SnapshotContractPath: $".aos/state/checkpoints/{checkpointId}/state.json",
            StateContractPath: AosStateStore.StateContractPath
        ));

        return checkpointId;
    }

    private static void RestoreCheckpoint(string aosRootPath, string checkpointId)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (string.IsNullOrWhiteSpace(checkpointId)) throw new ArgumentNullException(nameof(checkpointId));

        // Validate transition BEFORE any state writes.
        _ = Gmsd.Aos.Engine.StateTransitions.AosStateTransitionEngine.ValidateTransitionOrThrow(
            aosRootPath,
            Gmsd.Aos.Engine.StateTransitions.AosStateTransitionTable.Kinds.CheckpointRestored,
            checkpointId
        );

        var checkpointDir = Path.Combine(aosRootPath, "state", "checkpoints", checkpointId);
        var snapshotPath = Path.Combine(checkpointDir, "state.json");
        // Existence is validated above; keep the guard for clearer error text if called directly.
        if (!File.Exists(snapshotPath)) throw new FileNotFoundException($"Checkpoint snapshot not found: {snapshotPath}");

        using var snapshotDoc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(snapshotPath));
        var statePath = Path.Combine(aosRootPath, "state", "state.json");
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(statePath, snapshotDoc.RootElement, writeIndented: true);

        var stateStore = new AosStateStore(aosRootPath);
        stateStore.AppendEvent(new CheckpointEventDocument(
            SchemaVersion: 1,
            Kind: Gmsd.Aos.Engine.StateTransitions.AosStateTransitionTable.Kinds.CheckpointRestored,
            CheckpointId: checkpointId,
            CheckpointContractPath: $".aos/state/checkpoints/{checkpointId}/checkpoint.json",
            SnapshotContractPath: $".aos/state/checkpoints/{checkpointId}/state.json",
            StateContractPath: AosStateStore.StateContractPath
        ));
    }

    private static string AllocateNextCheckpointId(string checkpointsRootPath)
    {
        // Deterministic, monotonic ID allocation based on existing checkpoint folders.
        // Format chosen to satisfy schema: ^CHK-[A-Za-z0-9][A-Za-z0-9-]*$.
        var max = 0;
        if (Directory.Exists(checkpointsRootPath))
        {
            foreach (var dir in Directory.EnumerateDirectories(checkpointsRootPath, "CHK-*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(dir);
                if (name is null || !name.StartsWith("CHK-", StringComparison.Ordinal))
                {
                    continue;
                }

                var tail = name["CHK-".Length..];
                if (tail.Length == 0)
                {
                    continue;
                }

                if (int.TryParse(tail, out var n))
                {
                    max = Math.Max(max, n);
                }
            }
        }

        var next = max + 1;
        return $"CHK-{next:000000}";
    }

    private sealed record CheckpointMetadataDocument(
        int SchemaVersion,
        string CheckpointId,
        string SourceStateContractPath,
        string SnapshotFile);

    private sealed record CheckpointEventDocument(
        int SchemaVersion,
        string Kind,
        string CheckpointId,
        string CheckpointContractPath,
        string SnapshotContractPath,
        string StateContractPath);

    private static bool TryParseLayer(string value, out AosWorkspaceLayer layer)
    {
        layer = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "spec" => Set(AosWorkspaceLayer.Spec, out layer),
            "state" => Set(AosWorkspaceLayer.State, out layer),
            "evidence" => Set(AosWorkspaceLayer.Evidence, out layer),
            "codebase" => Set(AosWorkspaceLayer.Codebase, out layer),
            "context" => Set(AosWorkspaceLayer.Context, out layer),
            "config" => Set(AosWorkspaceLayer.Config, out layer),
            _ => false
        };
    }

    private static string ToLayerName(AosWorkspaceLayer layer) =>
        layer switch
        {
            AosWorkspaceLayer.Spec => "spec",
            AosWorkspaceLayer.State => "state",
            AosWorkspaceLayer.Evidence => "evidence",
            AosWorkspaceLayer.Codebase => "codebase",
            AosWorkspaceLayer.Context => "context",
            AosWorkspaceLayer.Config => "config",
            _ => layer.ToString()
        };

    private static bool Set(AosWorkspaceLayer value, out AosWorkspaceLayer layer)
    {
        layer = value;
        return true;
    }

    private static bool TryDiscoverRepositoryRoot(out string repositoryRootPath)
    {
        try
        {
            repositoryRootPath = AosRepositoryRootDiscovery.DiscoverOrThrow(Directory.GetCurrentDirectory());
            return true;
        }
        catch (AosRepositoryRootNotFoundException ex)
        {
            // Keep human-friendly message stable.
            Console.Error.WriteLine(ex.Message);
            WriteAosErrorLine(AosErrorMapper.Map(ex));
            repositoryRootPath = "";
            return false;
        }
    }

    private static bool EnsureWorkspaceValid(string repositoryRootPath, out string aosRootPath)
    {
        var report = AosWorkspaceValidator.Validate(repositoryRootPath);
        aosRootPath = report.AosRootPath;

        if (report.Issues.Count == 0)
        {
            return true;
        }

        Console.Error.WriteLine($"AOS workspace validation failed at {aosRootPath}.");
        foreach (var issue in report.Issues)
        {
            var layerLabel = issue.Layer is null ? "unknown" : ToLayerName(issue.Layer.Value);
            if (!string.IsNullOrWhiteSpace(issue.SchemaId))
            {
                var loc = issue.InstanceLocation ?? "";
                if (!string.IsNullOrWhiteSpace(loc))
                {
                    Console.Error.WriteLine($"FAIL [{layerLabel}] {issue.ContractPath} - ({issue.SchemaId} @ {loc}) {issue.Message}");
                }
                else
                {
                    Console.Error.WriteLine($"FAIL [{layerLabel}] {issue.ContractPath} - ({issue.SchemaId}) {issue.Message}");
                }
            }
            else
            {
                Console.Error.WriteLine($"FAIL [{layerLabel}] {issue.ContractPath} - {issue.Message}");
            }
        }

        // Actionable hint for the most common failure mode.
        if (report.Issues.Any(i => i.ContractPath == ".aos/" && i.Message.Contains("Missing", StringComparison.OrdinalIgnoreCase)))
        {
            Console.Error.WriteLine("Run 'aos init' to initialize the workspace, then re-run the command.");
        }

        WriteAosErrorLine(AosErrorMapper.FromWorkspaceValidationReport(report));
        return false;
    }

    private static IReadOnlyList<string> BuildValidateWorkspaceHints(AosWorkspaceValidationReport report)
    {
        var hints = new List<string>();

        var initHint = $"Run 'aos init --root {report.RepositoryRootPath}' to seed missing baseline artifacts.";
        var repairIndexesHint = $"Run 'aos repair indexes --root {report.RepositoryRootPath}' to rebuild deterministic indexes.";

        if (report.Issues.Any(i =>
                i.ContractPath == ".aos/" &&
                i.Message.Contains("Missing", StringComparison.OrdinalIgnoreCase)))
        {
            hints.Add(initHint);
        }

        // Any missing/malformed index is best addressed via deterministic repair.
        if (report.Issues.Any(i =>
                (i.ContractPath.StartsWith(".aos/spec/", StringComparison.Ordinal) &&
                 i.ContractPath.EndsWith("/index.json", StringComparison.Ordinal)) ||
                i.ContractPath == ".aos/evidence/runs/index.json"))
        {
            hints.Add(repairIndexesHint);
        }

        // For baseline state/evidence logs, init can re-seed missing stubs (it will not overwrite existing files).
        if (report.Issues.Any(i =>
                i.Message.Contains("Missing required", StringComparison.OrdinalIgnoreCase) &&
                (i.ContractPath.StartsWith(".aos/state/", StringComparison.Ordinal) ||
                 i.ContractPath == ".aos/evidence/logs/commands.json")))
        {
            hints.Add(initHint);
        }

        // If the events log is present but invalid, init/repair cannot fix it automatically.
        if (report.Issues.Any(i =>
                i.ContractPath == ".aos/state/events.ndjson" &&
                i.Message.StartsWith("Invalid", StringComparison.OrdinalIgnoreCase)))
        {
            hints.Add("Fix or remove the invalid non-empty line(s) in '.aos/state/events.ndjson' so each non-empty line is valid JSON.");
        }

        // Avoid duplicate hints while preserving stable ordering.
        return hints
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static LockAcquireResult AcquireWorkspaceLockOrNull(
        string aosRootPath,
        string command,
        bool createDirectories)
    {
        var result = AosWorkspaceLockManager.TryAcquireExclusive(
            aosRootPath,
            command,
            createDirectories
        );

        if (result.Acquired)
        {
            return LockAcquireResult.Acquired(result.Handle!);
        }

        // Actionable lock contention output (exit code stabilized in a later task).
        Console.Error.WriteLine("Workspace is locked.");
        Console.Error.WriteLine($"Lock path: {AosPathRouter.WorkspaceLockContractPath}");

        var existing = result.ExistingLock;
        if (existing is not null)
        {
            Console.Error.WriteLine($"Held since: {existing.AcquiredAtUtc}");
            Console.Error.WriteLine($"Holder: {existing.Holder.User}@{existing.Holder.Machine} pid={existing.Holder.Pid}");
            if (!string.IsNullOrWhiteSpace(existing.Holder.ProcessName))
            {
                Console.Error.WriteLine($"Process: {existing.Holder.ProcessName}");
            }
            if (!string.IsNullOrWhiteSpace(existing.Holder.Command))
            {
                Console.Error.WriteLine($"Command: {existing.Holder.Command}");
            }
            if (!string.IsNullOrWhiteSpace(existing.Holder.WorkingDirectory))
            {
                Console.Error.WriteLine($"Working directory: {existing.Holder.WorkingDirectory}");
            }
            if (!string.IsNullOrWhiteSpace(existing.ReleaseHint))
            {
                Console.Error.WriteLine($"Hint: {existing.ReleaseHint}");
            }
        }
        else
        {
            Console.Error.WriteLine("The existing lock file could not be parsed.");
            Console.Error.WriteLine($"If you believe it is stale, delete '{AosPathRouter.WorkspaceLockContractPath}'.");
        }

        var lockFileExists = File.Exists(result.LockPath);
        var exitCode = lockFileExists ? ExitCodeLockContention : ExitCodeKnownFailure;

        // Extra next-step hints for orchestration friendliness.
        Console.Error.WriteLine("Next steps:");
        Console.Error.WriteLine("  - Wait for the other process to finish, then retry.");
        Console.Error.WriteLine("  - Run 'aos lock status' to inspect the lock holder.");
        Console.Error.WriteLine("  - If the lock is stale, run 'aos lock release --force' (or delete the lock file).");

        WriteAosErrorLine(new AosErrorEnvelope(
            Code: AosErrorCodes.LockContended,
            Message: "Workspace is locked.",
            Details: new { LockContractPath = AosPathRouter.WorkspaceLockContractPath, command }
        ));

        return LockAcquireResult.NotAcquired(exitCode);
    }

    private sealed record LockAcquireResult(AosWorkspaceLockHandle? Handle, int ExitCode)
    {
        public static LockAcquireResult Acquired(AosWorkspaceLockHandle handle) =>
            new(handle, ExitCodeSuccess);

        public static LockAcquireResult NotAcquired(int exitCode) =>
            new(null, exitCode);
    }

    private static void WriteAosErrorLine(AosErrorEnvelope envelope)
    {
        if (envelope is null) throw new ArgumentNullException(nameof(envelope));

        // A single-line, machine-readable JSON envelope on STDERR.
        //
        // NOTE: some CLI snapshot tests normalize '\' to '/', so we also normalize
        // envelope strings to avoid emitting JSON escape backslashes.
        var code = (envelope.Code ?? "").Replace('\\', '/');
        var message = (envelope.Message ?? "").Replace('\\', '/');

        var buffer = new ArrayBufferWriter<byte>();
        var writerOptions = new JsonWriterOptions
        {
            Indented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            SkipValidation = false
        };

        using (var writer = new Utf8JsonWriter(buffer, writerOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("code", code);
            writer.WriteString("message", message);
            writer.WriteEndObject();
            writer.Flush();
        }

        var json = Encoding.UTF8.GetString(buffer.WrittenSpan);
        Console.Error.WriteLine($"AOS_ERROR: {json}");
    }
}

