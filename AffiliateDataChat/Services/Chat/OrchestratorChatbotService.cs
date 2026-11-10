using Microsoft.Extensions.Caching.Memory;
using ModelContextProtocol.Client;

namespace AffiliateDataChat.Services.Chat;

/// <summary>
/// Connects the MVC layer to the existing <see cref="ChatOrchestrator"/>.
///
/// The orchestrator currently returns only the answer text, so this class:
///   - works out the mode (Model vs Fallback) from the same Ai:* settings the orchestrator uses;
///   - lists the MCP tools through runtime discovery, for the status badge and the About page.
///
/// Per-answer tool evidence needs the orchestrator to report which tools it called.
/// Once it does, this class can pass those calls through (or ChatOrchestrator can
/// implement IChatbotService directly and this adapter can be deleted).
/// </summary>
public sealed class OrchestratorChatbotService(
    ChatOrchestrator orchestrator,
    IConfiguration configuration,
    IMemoryCache cache,
    ILogger<OrchestratorChatbotService> logger) : IChatbotService
{
    private const string ToolsCacheKey = "mcp.discovered-tools";
    private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DiscoveryCacheTime = TimeSpan.FromMinutes(5);

    private bool IsModelConfigured =>
        string.Equals(configuration["Ai:Provider"], "Ollama", StringComparison.OrdinalIgnoreCase)
        || (string.Equals(configuration["Ai:Provider"], "OpenAI", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(configuration["Ai:ApiKey"]));

    private ChatMode Mode => IsModelConfigured ? ChatMode.Model : ChatMode.Fallback;

    private string? ModelName => IsModelConfigured ? configuration["Ai:Model"] : null;

    public async Task<ChatbotReply> AskAsync(string question, IReadOnlyList<ChatTurn> history, CancellationToken cancellationToken)
    {
        var result = await orchestrator.AskAsync(question, cancellationToken);
        return new ChatbotReply(result.Answer, Mode, ModelName, result.ToolCalls);
    }

    public async Task<ChatbotStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var tools = await DiscoverToolsAsync(cancellationToken);
        var note = tools.Count == 0
            ? "Could not list the MCP tools. Check Mcp:Endpoint and the application log."
            : null;
        return new ChatbotStatus(Mode, ModelName, tools, note);
    }

    /// <summary>Asks the MCP server which tools it offers. Cached briefly so every page load doesn't reconnect.</summary>
    private async Task<IReadOnlyList<string>> DiscoverToolsAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(ToolsCacheKey, out IReadOnlyList<string>? cached) && cached is not null)
            return cached;

        try
        {
            var endpoint = configuration["Mcp:Endpoint"]
                ?? throw new InvalidOperationException("Mcp:Endpoint is not configured.");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(DiscoveryTimeout);

            var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(endpoint),
                EnableStandaloneGetStream = false
            });
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);
            var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);

            IReadOnlyList<string> names = tools.Select(tool => tool.Name).ToList();
            logger.LogInformation("MCP discovery found {Count} tools: {Tools}", names.Count, string.Join(", ", names));

            cache.Set(ToolsCacheKey, names, DiscoveryCacheTime); // failures are not cached
            return names;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "MCP tool discovery failed");
            return [];
        }
    }
}
