#pragma warning disable MAAI001
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace Chapter08.Memory.Demos;

public static class FoundryMemoryDemo
{
    public static async Task RunAsync(
        string? deploymentAlias = null,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default,
        bool useTemporaryScope = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(FoundryMemoryDemo).Assembly)
            .AddEnvironmentVariables()
            .Build();
        var settings = LoadSettings(configuration, deploymentAlias);
        var configuredMemoryScope = useTemporaryScope ? null : configuration["Foundry:MemoryScope"];
        if (string.IsNullOrWhiteSpace(configuredMemoryScope))
        {
            configuredMemoryScope = null;
        }

        var memoryScope = new FoundryMemoryProviderScope(
            configuredMemoryScope ?? $"chapter08-demo-{Guid.NewGuid():N}");
        var projectClient = new AIProjectClient(
            new Uri(settings.ProjectEndpoint),
            new DefaultAzureCredential());
        var memoryProvider = new FoundryMemoryProvider(
            projectClient,
            settings.MemoryStoreName,
            stateInitializer: _ => new(memoryScope));
        var agent = projectClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "Chapter08FoundryMemory",
                ChatOptions = new()
                {
                    ModelId = settings.ModelDeployment,
                    Instructions = """
                        You are a friendly travel assistant. Use memories about the user when they are relevant.
                        Do not invent details or claim a memory exists when it is not provided.
                        """
                },
                AIContextProviders = [memoryProvider]
            });

        writeOutput?.Invoke("--- Foundry-managed memory ---");
        writeOutput?.Invoke($"Model deployment: {settings.ModelDeployment}");
        writeOutput?.Invoke($"Memory store: {settings.MemoryStoreName}");
        writeOutput?.Invoke(configuredMemoryScope is null
            ? "Memory scope: temporary for this run"
            : "Memory scope: configured for reuse across runs");
        writeOutput?.Invoke("Creating or verifying the configured Foundry memory store.");
        await memoryProvider.EnsureMemoryStoreCreatedAsync(
            settings.ModelDeployment,
            settings.EmbeddingDeployment,
            "Chapter 08 memory demonstration",
            cancellationToken);

        AgentSession firstSession = await agent.CreateSessionAsync(cancellationToken);
        await RunTurnAsync(
            agent,
            firstSession,
            "My name is Sam, and I prefer window seats when I travel.",
            "Store personal context",
            writeOutput,
            cancellationToken);

        writeOutput?.Invoke("\nWaiting for Foundry memory updates...");
        await memoryProvider.WhenUpdatesCompletedAsync(cancellationToken: cancellationToken);

        AgentSession nextSession = await agent.CreateSessionAsync(cancellationToken);
        await RunTurnAsync(
            agent,
            nextSession,
            "What travel preference do you remember about me?",
            "Recall in a new session",
            writeOutput,
            cancellationToken);

        if (configuredMemoryScope is null)
        {
            writeOutput?.Invoke("\nCleaning up the temporary demo memory scope.");
            await memoryProvider.WhenUpdatesCompletedAsync(cancellationToken: cancellationToken);
            await memoryProvider.EnsureStoredMemoriesDeletedAsync(nextSession, cancellationToken);
        }
    }

    private static async Task RunTurnAsync(
        AIAgent agent,
        AgentSession session,
        string input,
        string heading,
        Action<string>? writeOutput,
        CancellationToken cancellationToken)
    {
        writeOutput?.Invoke($"\n--- {heading} ---");
        writeOutput?.Invoke($"User: {input}");
        var response = await agent.RunAsync(input, session, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(response.Text))
        {
            throw new InvalidOperationException($"The Foundry memory agent returned an empty response during '{heading}'.");
        }

        writeOutput?.Invoke($"Assistant: {response.Text}");
    }

    private static FoundryMemorySettings LoadSettings(
        IConfiguration configuration,
        string? deploymentAlias)
    {
        var projectEndpoint = configuration["Foundry:ProjectEndpoint"]
            ?? configuration["FOUNDRY_PROJECT_ENDPOINT"];
        var modelDeployment = ResolveDeployment(
            configuration,
            deploymentAlias,
            "Foundry:ModelDeployment",
            "AZURE_AI_MODEL_DEPLOYMENT_NAME");
        var embeddingDeployment = configuration["Foundry:ModelDeployments:embedding"]
            ?? configuration["AZURE_AI_EMBEDDING_DEPLOYMENT_NAME"];
        var memoryStoreName = configuration["Foundry:MemoryStoreName"]
            ?? configuration["AZURE_AI_MEMORY_STORE_ID"]
            ?? "chapter08-memory-demo";

        if (string.IsNullOrWhiteSpace(projectEndpoint))
        {
            throw new InvalidOperationException(
                "Configure Foundry:ProjectEndpoint or FOUNDRY_PROJECT_ENDPOINT for the Foundry memory variant.");
        }

        if (string.IsNullOrWhiteSpace(modelDeployment))
        {
            throw new InvalidOperationException(
                "Configure Foundry:ModelDeployment or AZURE_AI_MODEL_DEPLOYMENT_NAME for the Foundry memory variant.");
        }

        if (string.IsNullOrWhiteSpace(embeddingDeployment))
        {
            throw new InvalidOperationException(
                "Configure Foundry:ModelDeployments:embedding or AZURE_AI_EMBEDDING_DEPLOYMENT_NAME " +
                "with a deployed embedding model for the Foundry memory variant.");
        }

        return new FoundryMemorySettings(
            projectEndpoint,
            modelDeployment,
            embeddingDeployment,
            memoryStoreName);
    }

    private static string? ResolveDeployment(
        IConfiguration configuration,
        string? deploymentAlias,
        string defaultKey,
        string environmentKey)
    {
        if (string.IsNullOrWhiteSpace(deploymentAlias))
        {
            return configuration[defaultKey] ?? configuration[environmentKey];
        }

        var aliasDeployment = configuration[$"Foundry:ModelDeployments:{deploymentAlias}"];
        if (string.IsNullOrWhiteSpace(aliasDeployment))
        {
            throw new InvalidOperationException(
                $"Foundry model deployment alias '{deploymentAlias}' is not configured. " +
                $"Set Foundry:ModelDeployments:{deploymentAlias} or Foundry__ModelDeployments__{deploymentAlias}.");
        }

        return aliasDeployment;
    }

    private sealed record FoundryMemorySettings(
        string ProjectEndpoint,
        string ModelDeployment,
        string EmbeddingDeployment,
        string MemoryStoreName);
}
#pragma warning restore MAAI001
