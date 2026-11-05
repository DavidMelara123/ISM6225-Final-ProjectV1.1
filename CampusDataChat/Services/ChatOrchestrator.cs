using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;
namespace CampusDataChat.Services;

public sealed partial class ChatOrchestrator(IConfiguration configuration, ILogger<ChatOrchestrator> logger)
{
    public string ProviderLabel => IsOpenAiConfigured ? $"OpenAI · {configuration["Ai:Model"]}" : "Guided demo · MCP tools";
    private bool IsOpenAiConfigured =>
    string.Equals(configuration["Ai:Provider"], "OpenAI", StringComparison.OrdinalIgnoreCase)
    && !string.IsNullOrWhiteSpace(configuration["Ai:ApiKey"]);
    public async Task<string> AskAsync(string question, CancellationToken cancellationToken)
    {
        var endpoint = configuration["Mcp:Endpoint"]
        ?? throw new InvalidOperationException("Mcp:Endpoint is not configured.");
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(endpoint),
            EnableStandaloneGetStream = false
        });
        await using var mcpClient = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
        var tools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
        if (IsOpenAiConfigured)
        {
            return await AskOpenAiAsync(question, tools, cancellationToken);
        }
        return await AskDemoAsync(question, tools, cancellationToken);
    }
    private async Task<string> AskOpenAiAsync(
    string question,
    IList<McpClientTool> tools,
    CancellationToken cancellationToken)
    {
        var apiKey = configuration["Ai:ApiKey"]!;
        var model = configuration["Ai:Model"] ?? "gpt-5-mini";
        IChatClient client = new OpenAIClient(apiKey)
        .GetChatClient(model)
        .AsIChatClient()
        .AsBuilder()
        .UseFunctionInvocation()
        .Build();
        var messages = new List<ChatMessage>
{
new(ChatRole.System,
"You are a concise university course-data assistant. Use the MCP tools for every factual data answer. " +
"Never invent rows or run arbitrary SQL. State when no result is found. Mention course codes when available."),
new(ChatRole.User, question)
};
        var response = await client.GetResponseAsync(messages, new ChatOptions
        {
            Tools = [.. tools],
            MaxOutputTokens = 700
        }, cancellationToken);
        return response.Text;
    }
    private async Task<string> AskDemoAsync(
    string question,
    IList<McpClientTool> tools,
    CancellationToken cancellationToken)
    {
        var lower = question.ToLowerInvariant();
        McpClientTool tool;
        IReadOnlyDictionary<string, object?>? arguments;
        var code = CourseCodeRegex().Match(question);
        if (code.Success)
        {
            tool = FindTool(tools, "get_course_by_code");
            arguments = new Dictionary<string, object?> { ["code"] = code.Value.ToUpperInvariant() };
        }
        else if (lower.Contains("top") || lower.Contains("largest") || lower.Contains("highest enrollment"))
        {
            tool = FindTool(tools, "top_courses_by_enrollment");
            arguments = new Dictionary<string, object?> { ["count"] = ExtractCount(question) };
        }
        else if (lower.Contains("summary") || lower.Contains("total") || lower.Contains("average"))
        {
            tool = FindTool(tools, "summarize_enrollment");
            arguments = null;
        }
        else
        {
            tool = FindTool(tools, "search_courses");
            arguments = new Dictionary<string, object?>
            {
                ["department"] = lower.Contains("business analytics") ? "Business Analytics"
            : lower.Contains("information systems") ? "Information Systems"
            : lower.Contains(" ai ") || lower.StartsWith("ai ") || lower.Contains("artificial intelligence") ? "AI"
            : null,
                ["deliveryMode"] = lower.Contains("online") ? "Online"
                : lower.Contains("hybrid") ? "Hybrid"
: lower.Contains("in person") ? "In Person"
: null,
                ["minimumEnrollment"] = ExtractMinimumEnrollment(question)
            };
        }
        logger.LogInformation("Demo mode selected MCP tool {ToolName}.", tool.Name);
        var result = await tool.CallAsync(arguments, cancellationToken: cancellationToken);
        var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        return $"Demo mode used the MCP tool `{tool.Name}`.\n\n{text}\n\nConfigure an OpenAI key to receive a natural-language interpretation.";
    }
    private static McpClientTool FindTool(IEnumerable<McpClientTool> tools, string name) =>
    tools.Single(tool => string.Equals(tool.Name, name, StringComparison.Ordinal));
    private static int ExtractCount(string question)
    {
        var match = Regex.Match(question, @"\b([1-9]|10)\b");
        return match.Success ? int.Parse(match.Value) : 5;
    }
    private static int? ExtractMinimumEnrollment(string question)
    {
        var match = Regex.Match(question, @"(?:at least|minimum|more than)\s+(\d+)", RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }
    [GeneratedRegex(@"\b(?:AI|BA|IS)-\d{3}\b", RegexOptions.IgnoreCase)]
    private static partial Regex CourseCodeRegex();
}