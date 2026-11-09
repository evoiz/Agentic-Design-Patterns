using System.Diagnostics;

namespace AgenticDesignPatterns.Foundry.SmokeTests;

internal static class DotnetDemoRunner
{
    private static readonly string RepositoryRoot = ResolveRepositoryRoot();

    public static Task<DemoRunResult> RunChapter01SpecsAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "PromptChaining", "PromptChaining.csproj"),
            ["specs"]);

    public static Task<DemoRunResult> RunChapter02TravelAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "Routing", "Routing.csproj"),
            ["travel"]);

    public static Task<DemoRunResult> RunChapter03TopicAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "Parallelization", "Parallelization.csproj"),
            ["topic"]);

    public static Task<DemoRunResult> RunChapter04FactCheckAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "Reflection", "Reflection.csproj"),
            ["fact-check"]);

    public static Task<DemoRunResult> RunChapter05SearchAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "ToolUse", "ToolUse.csproj"),
            ["search"]);

    public static Task<DemoRunResult> RunChapter06PlanWriteAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "Planning", "Planning.csproj"),
            ["plan-write"]);

    public static Task<DemoRunResult> RunChapter07CrewAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "MultiAgent", "MultiAgent.csproj"),
            ["crew"]);

    public static Task<DemoRunResult> RunChapter08SessionMemoryAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "Memory", "Memory.csproj"),
            ["session"]);

    public static Task<DemoRunResult> RunChapter09LearningAdaptationAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "LearningAdaptation", "LearningAdaptation.csproj"),
            ["maf"]);

    public static Task<DemoRunResult> RunChapter21ExplorationAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "ExplorationDiscovery", "ExplorationDiscovery.csproj"),
            ["explore"]);

    public static Task<DemoRunResult> RunChapter17ReasoningAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "Reasoning", "Reasoning.csproj"),
            ["all"]);

    public static async Task<DemoRunResult> RunChapter11GoalSettingMonitoringAsync()
    {
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            $"chapter11-goal-setting-{Guid.NewGuid():N}");
        try
        {
            return await RunAsync(
                Path.Combine(RepositoryRoot, "dotnet", "src", "GoalSettingMonitoring", "GoalSettingMonitoring.csproj"),
                ["--iterations", "1", "--output-dir", outputDirectory]);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    public static Task<DemoRunResult> RunChapter12RecoveryAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "ExceptionHandling", "ExceptionHandling.csproj"),
            ["all"]);

    public static Task<DemoRunResult> RunChapter14RagAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "RAG", "RAG.csproj"),
            ["rag", "What", "did", "the", "president", "say", "about", "Justice", "Breyer?"]);

    public static Task<DemoRunResult> RunChapter19EvaluationAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "Evaluation", "Evaluation.csproj"),
            ["all"]);

    public static Task<DemoRunResult> RunChapter16ComplexityAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "ResourceOptimization", "ResourceOptimization.csproj"),
            ["complexity"]);

    public static Task<DemoRunResult> RunChapter18PolicyAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "Guardrails", "Guardrails.csproj"),
            ["policy", "--text", "Explain the benefits of unit testing."]);

    private static async Task<DemoRunResult> RunAsync(string projectPath, string[] demoArguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepositoryRoot
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--");

        foreach (var argument in demoArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var deploymentAlias = Environment.GetEnvironmentVariable("FOUNDRY_TEST_DEPLOYMENT_ALIAS");
        if (!string.IsNullOrWhiteSpace(deploymentAlias))
        {
            startInfo.ArgumentList.Add("--deployment");
            startInfo.ArgumentList.Add(deploymentAlias);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet process for Foundry smoke test.");

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            throw new TimeoutException($"Timed out while running demo project '{projectPath}'.");
        }

        return new DemoRunResult(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask);
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "dotnet", "AgenticDesignPatterns.slnx");
            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root for Foundry smoke tests.");
    }

    internal sealed record DemoRunResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => $"""
            STDOUT:
            {StandardOutput}

            STDERR:
            {StandardError}
            """;
    }
}
