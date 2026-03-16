$filePath = 'c:\Users\James Lestler\Desktop\Projects\nirmata\nirmata.Agents\Configuration\nirmataAgentsServiceCollectionExtensions.cs'
$content = Get-Content $filePath -Raw

$oldPattern = @'
        // Register observability services
        services.AddSingleton<ICorrelationIdProvider, RunCorrelationIdProvider>();

        // Register TaskExecutor services
        services.AddSingleton<ITaskExecutor>(sp =>
        {
            var runLifecycleManager = sp.GetRequiredService<IRunLifecycleManager>();
            var toolCallingLoop = sp.GetRequiredService<IToolCallingLoop>();
            var toolRegistry = sp.GetRequiredService<IToolRegistry>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            var stateStore = sp.GetRequiredService<IStateStore>();
            var logger = sp.GetRequiredService<ILogger<TaskExecutor>>();
            return new TaskExecutor(runLifecycleManager, toolCallingLoop, toolRegistry, workspace, stateStore, logger);
        });

        // Register TaskExecutorHandler
        services.AddSingleton<TaskExecutorHandler>();

        // Register AtomicGitCommitter services
        services.AddScoped<IAtomicGitCommitter, AtomicGitCommitter>();
        services.AddSingleton<AtomicGitCommitterHandler>();

        // Register UAT Verifier services
'@

$newPattern = @'
        // Register observability services
        services.AddSingleton<ICorrelationIdProvider, RunCorrelationIdProvider>();

        // Register Concurrency Limiter services (Task 4.1-4.8)
        services.AddSingleton<IConcurrencyLimiter>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            var logger = sp.GetRequiredService<ILogger<ConcurrencyLimiter>>();
            var configLoader = new ConcurrencyConfigurationLoader(workspace, sp.GetRequiredService<ILogger<ConcurrencyConfigurationLoader>>());
            var options = configLoader.Load();
            return new ConcurrencyLimiter(options, logger);
        });

        // Register TaskExecutor services with concurrency limiting
        services.AddSingleton<ITaskExecutor>(sp =>
        {
            var runLifecycleManager = sp.GetRequiredService<IRunLifecycleManager>();
            var toolCallingLoop = sp.GetRequiredService<IToolCallingLoop>();
            var toolRegistry = sp.GetRequiredService<IToolRegistry>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            var stateStore = sp.GetRequiredService<IStateStore>();
            var logger = sp.GetRequiredService<ILogger<TaskExecutor>>();
            var innerExecutor = new TaskExecutor(runLifecycleManager, toolCallingLoop, toolRegistry, workspace, stateStore, logger);
            var concurrencyLimiter = sp.GetRequiredService<IConcurrencyLimiter>();
            var limitedLogger = sp.GetRequiredService<ILogger<ConcurrencyLimiterTaskExecutor>>();
            return new ConcurrencyLimiterTaskExecutor(innerExecutor, concurrencyLimiter, limitedLogger);
        });

        // Register TaskExecutorHandler
        services.AddSingleton<TaskExecutorHandler>();

        // Register AtomicGitCommitter services
        services.AddScoped<IAtomicGitCommitter, AtomicGitCommitter>();
        services.AddSingleton<AtomicGitCommitterHandler>();

        // Register UAT Verifier services
'@

$newContent = $content -replace [regex]::Escape($oldPattern), $newPattern
Set-Content -Path $filePath -Value $newContent -NoNewline
Write-Host 'File updated successfully'
