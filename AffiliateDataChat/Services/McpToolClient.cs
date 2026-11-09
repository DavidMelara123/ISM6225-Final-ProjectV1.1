using ModelContextProtocol.Client;

namespace AffiliateDataChat.Services;

public sealed class McpToolClient(
    IConfiguration configuration,
    ILogger<McpToolClient> logger)
{
    public async Task<TResult> WithDiscoveredToolsAsync<TResult>(
        Func<IList<McpClientTool>, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        var endpoint = configuration["Mcp:Endpoint"];
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            throw new InvalidOperationException("Mcp:Endpoint must be an absolute URI.");
        }

        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = endpointUri,
            EnableStandaloneGetStream = false
        });
        await using var client = await ModelContextProtocol.Client.McpClient.CreateAsync(
            transport,
            cancellationToken: cancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
        logger.LogInformation(
            "Discovered {ToolCount} MCP tools from {Endpoint}: {ToolNames}",
            tools.Count,
            endpointUri,
            string.Join(", ", tools.Select(tool => tool.Name)));
        return await operation(tools);
    }
}
