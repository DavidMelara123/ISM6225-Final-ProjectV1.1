using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.ClientModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;
using AffiliateDataChat.Services.Chat;
namespace AffiliateDataChat.Services;

public sealed partial class ChatOrchestrator(
    IConfiguration configuration,
    McpToolClient mcpToolClient,
    ILogger<ChatOrchestrator> logger,
    ILoggerFactory loggerFactory)
{
    public string ProviderLabel => IsModelConfigured
        ? $"{configuration["Ai:Provider"]} · {configuration["Ai:Model"]}"
        : "Guided demo · Affiliate MCP tools";
    private bool IsOllamaConfigured =>
        string.Equals(configuration["Ai:Provider"], "Ollama", StringComparison.OrdinalIgnoreCase);
    private bool IsOpenAiConfigured =>
    string.Equals(configuration["Ai:Provider"], "OpenAI", StringComparison.OrdinalIgnoreCase)
    && !string.IsNullOrWhiteSpace(configuration["Ai:ApiKey"]);
    private bool IsModelConfigured => IsOpenAiConfigured || IsOllamaConfigured;
    public async Task<OrchestrationResult> AskAsync(string question, CancellationToken cancellationToken)
    {
        return await mcpToolClient.WithDiscoveredToolsAsync(async tools =>
        {
            if (IsModelConfigured)
            {
                return await AskOpenAiAsync(question, tools, cancellationToken);
            }
            return await AskDemoAsync(question, tools, cancellationToken);
        }, cancellationToken);
    }
    private async Task<OrchestrationResult> AskOpenAiAsync(
    string question,
    IList<McpClientTool> tools,
    CancellationToken cancellationToken)
    {
        var toolCalls = new List<ToolInvocation>();
        var apiKey = IsOllamaConfigured ? "ollama" : configuration["Ai:ApiKey"]!;
        var model = configuration["Ai:Model"] ?? "gpt-5-mini";
        var endpoint = configuration["Ai:BaseUrl"] ?? "https://api.openai.com/v1";
        IChatClient client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
        {
            Endpoint = new Uri(endpoint)
        })
        .GetChatClient(model)
        .AsIChatClient()
         .AsBuilder()
         .UseFunctionInvocation(loggerFactory, functionClient =>
         {
             functionClient.FunctionInvoker = async (context, token) =>
             {
                 var started = Stopwatch.GetTimestamp();
                 var argumentsJson = JsonSerializer.Serialize(context.Arguments);
                 try
                 {
                     var result = await context.Function.InvokeAsync(context.Arguments, token);
                     toolCalls.Add(new ToolInvocation(
                         context.Function.Name,
                         argumentsJson,
                         SummarizeResult(result),
                         true,
                         (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds));
                     return result;
                 }
                 catch (Exception ex)
                 {
                     toolCalls.Add(new ToolInvocation(
                         context.Function.Name,
                         argumentsJson,
                         ex.Message,
                         false,
                         (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds));
                     throw;
                 }
             };
         })
        .Build();
        var systemPrompt = configuration["Ai:SystemPrompt"]
            ?? "You are a concise affiliate and campaign-data assistant. Answer the user's exact question directly in no more than 2 short sentences. " +
            "Do not describe your reasoning, repeat the question, ask for information that the available tools can provide, or offer optional follow-up help. " +
            "Use affiliate_lookup for one affiliate, campaign_participants_without_sales for participants with no sales, campaign_summary for one campaign's totals, " +
            "and affiliate_ranking for ranking affiliates. Interpret 'most sales' or 'highest number of sales' for an affiliate as SalesCount and use affiliate_ranking with limit 1. " +
            "For which campaign has the most sales, use campaign_ranking. For which campaign made the most money, revenue, or profit, use campaign_summary for campaigns 1 through 4 and compare the returned totals. " +
            "Never invent rows, infer missing data, or run arbitrary SQL. State briefly when no result is found.";
        var messages = new List<ChatMessage>
{
 new(ChatRole.System, systemPrompt),
new(ChatRole.User, question)
};
        var response = await client.GetResponseAsync(messages, new ChatOptions
        {
            Tools = [.. tools],
            MaxOutputTokens = 250
        }, cancellationToken);
        logger.LogInformation("OpenAI response completed using {ToolCount} discovered MCP tools and model {Model}.", tools.Count, model);
         return new OrchestrationResult(response.Text, toolCalls);
    }
    private async Task<OrchestrationResult> AskDemoAsync(
    string question,
    IList<McpClientTool> tools,
    CancellationToken cancellationToken)
    {
        var lower = question.ToLowerInvariant();
        McpClientTool tool;
        IReadOnlyDictionary<string, object?>? arguments;
        var campaignId = CampaignIdRegex().Match(question);
        if (lower.Contains("without sales") || lower.Contains("no sales") || lower.Contains("zero sales"))
        {
            tool = FindTool(tools, "campaign_participants_without_sales");
            arguments = new Dictionary<string, object?> { ["campaignId"] = campaignId.Success ? int.Parse(campaignId.Groups[1].Value) : 2 };
        }
        else if (lower.Contains("campaign") && (lower.Contains("most sales") || lower.Contains("highest sales") || lower.Contains("most number of sales")))
        {
            tool = FindTool(tools, "campaign_ranking");
            arguments = new Dictionary<string, object?> { ["limit"] = ExtractCount(question) };
        }
        else if ((lower.Contains("campaign") || lower.Contains("affiliate")) && (lower.Contains("summary") || lower.Contains("overview") || lower.Contains("total")))
        {
            tool = FindTool(tools, "campaign_summary");
            arguments = new Dictionary<string, object?> { ["campaignId"] = campaignId.Success ? int.Parse(campaignId.Groups[1].Value) : 2 };
        }
        else if (lower.Contains("affiliate") &&
            (lower.Contains("rank")
                || lower.Contains("top affiliate")
                || lower.Contains("best affiliate")
                || lower.Contains("top performing")
                || lower.Contains("most net revenue")
                || lower.Contains("highest net revenue")))
        {
            tool = FindTool(tools, "affiliate_ranking");
            arguments = new Dictionary<string, object?>
            {
                ["campaignId"] = campaignId.Success ? int.Parse(campaignId.Groups[1].Value) : null,
                ["limit"] = ExtractCount(question)
            };
        }
        else if (lower.Contains("affiliate") || lower.Contains("partner"))
        {
            tool = FindTool(tools, "affiliate_lookup");
            var affiliateId = AffiliateIdRegex().Match(question);
            arguments = new Dictionary<string, object?>
            {
                ["affiliateId"] = affiliateId.Success ? int.Parse(affiliateId.Groups[1].Value) : 101
            };
        }
        else
        {
            tool = FindTool(tools, "affiliate_lookup");
            var affiliateId = AffiliateIdRegex().Match(question);
            arguments = new Dictionary<string, object?>
            {
                ["affiliateId"] = affiliateId.Success ? int.Parse(affiliateId.Groups[1].Value) : 101
            };
        }
        logger.LogInformation("Demo mode selected MCP tool {ToolName}.", tool.Name);
        var started = Stopwatch.GetTimestamp();
        var result = await tool.CallAsync(arguments, cancellationToken: cancellationToken);
        var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        logger.LogInformation("MCP tool {ToolName} completed in {ElapsedMs} ms with {ContentBlocks} content blocks.",
            tool.Name,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            result.Content.Count);
        return new OrchestrationResult(
            $"Demo mode used the MCP tool `{tool.Name}`.\n\n{text}\n\nConfigure an OpenAI key to receive a natural-language interpretation.",
            [new ToolInvocation(
                tool.Name,
                JsonSerializer.Serialize(arguments),
                text.Length > 500 ? text[..500] : text,
                result.IsError != true,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds)]);
    }

    private static string SummarizeResult(object? result)
    {
        var text = result is string value ? value : JsonSerializer.Serialize(result);
        return text.Length > 500 ? text[..500] : text;
    }
    private static McpClientTool FindTool(IEnumerable<McpClientTool> tools, string name) =>
    tools.Single(tool => string.Equals(tool.Name, name, StringComparison.Ordinal));
    private static int ExtractCount(string question)
    {
        var match = Regex.Match(question, @"\b([1-9]|10)\b");
        return match.Success ? int.Parse(match.Value) : 5;
    }
    private static Regex CampaignIdRegex() => new(@"\bcampaign(?:\s+id)?\s*(\d+)\b", RegexOptions.IgnoreCase);
    private static Regex AffiliateIdRegex() => new(@"\b(?:affiliate|id)\s*(\d+)\b", RegexOptions.IgnoreCase);
}