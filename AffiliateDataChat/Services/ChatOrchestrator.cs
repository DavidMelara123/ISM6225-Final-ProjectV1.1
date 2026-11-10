using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using AffiliateDataChat.Services.Chat;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;

namespace AffiliateDataChat.Services;

public sealed class ChatOrchestrator(
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

    public Task<OrchestrationResult> AskAsync(string question, CancellationToken cancellationToken) =>
        mcpToolClient.WithDiscoveredToolsAsync(async tools =>
        {
            return IsModelConfigured
                ? await AskModelAsync(question, tools, cancellationToken)
                : await AskDemoAsync(question, tools, cancellationToken);
        }, cancellationToken);

    private async Task<OrchestrationResult> AskModelAsync(
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
            "Use MCP tools for factual answers. Use affiliate_lookup for one affiliate, campaign_participants_without_sales for participants without sales, " +
            "campaign_summary for one campaign's totals, affiliate_ranking for affiliate rankings by net revenue, affiliate_sales_ranking for affiliate rankings by recorded sale count, and campaign_ranking for campaign sales rankings. " +
            "For requests to rank campaigns or show the top campaigns by sales count, use campaign_ranking and honor the requested top-N limit. " +
            "For requests asking which affiliate made the most sales, use affiliate_sales_ranking with limit 1; honor explicit top-N requests for sales rankings. " +
            "Rank affiliates by net revenue, not revenue after commission. For a singular 'which affiliate' or 'who generated the most' net revenue question, request limit 1. " +
            "Never return raw JSON. Present tool results in readable sentences or numbered lists, preserving the returned values. " +
            "Never invent rows or run arbitrary SQL; state briefly when no result is found.";

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

        logger.LogInformation(
            "Model response completed using {ToolCount} discovered MCP tools and model {Model}.",
            tools.Count,
            model);
        return new OrchestrationResult(response.Text, toolCalls);
    }

    private async Task<OrchestrationResult> AskDemoAsync(
        string question,
        IList<McpClientTool> tools,
        CancellationToken cancellationToken)
    {
        var lower = question.ToLowerInvariant();
        var campaignId = Regex.Match(question, @"\bcampaign(?:\s+id)?\s*#?\s*(\d+)\b", RegexOptions.IgnoreCase);
        var affiliateId = Regex.Match(question, @"\b(?:affiliate|id)\s*#?\s*(\d+)\b", RegexOptions.IgnoreCase);
        McpClientTool tool;
        IReadOnlyDictionary<string, object?>? arguments;

        if (lower.Contains("list campaigns") || lower.Contains("which campaigns") ||
            lower.Contains("what campaigns") || lower.Contains("available campaigns"))
        {
            tool = FindTool(tools, "list_campaigns");
            arguments = null;
        }
        else if (lower.Contains("without sales") || lower.Contains("no sales") ||
                 lower.Contains("zero sales") || lower.Contains("extra support") ||
                 lower.Contains("need support") || lower.Contains("might need support"))
        {
            tool = FindTool(tools, "campaign_participants_without_sales");
            arguments = CampaignArguments(lower, campaignId);
        }
        else if (lower.Contains("campaign") &&
                 (lower.Contains("most sales") || lower.Contains("highest sales") ||
                  lower.Contains("most number of sales") || lower.Contains("sales count") ||
                  ((lower.Contains("top") || lower.Contains("rank")) && lower.Contains("sales"))))
        {
            tool = FindTool(tools, "campaign_ranking");
            arguments = new Dictionary<string, object?> { ["limit"] = ExtractCount(question, 1) };
        }
        else if (lower.Contains("affiliate") &&
                 (lower.Contains("most sales") || lower.Contains("highest sales") ||
                  lower.Contains("sales count") ||
                  ((lower.Contains("top") || lower.Contains("rank")) && lower.Contains("sales"))))
        {
            tool = FindTool(tools, "affiliate_sales_ranking");
            var rankingArguments = new Dictionary<string, object?>
            {
                ["limit"] = ExtractCount(question, 1)
            };
            if (campaignId.Success)
                rankingArguments["campaignId"] = int.Parse(campaignId.Groups[1].Value);
            arguments = rankingArguments;
        }
        else if (lower.Contains("affiliate") &&
                 (lower.Contains("rank") || lower.Contains("top") || lower.Contains("most net revenue") ||
                  lower.Contains("highest net revenue") || lower.Contains("generated the most") ||
                  lower.Contains("best affiliate")))
        {
            tool = FindTool(tools, "affiliate_ranking");
            var rankingArguments = new Dictionary<string, object?>
            {
                ["limit"] = ExtractCount(question, IsSingularWinnerQuestion(question) ? 1 : 5)
            };
            if (campaignId.Success)
                rankingArguments["campaignId"] = int.Parse(campaignId.Groups[1].Value);
            arguments = rankingArguments;
        }
        else if (affiliateId.Success && (lower.Contains("history") || lower.Contains("by campaign") ||
                                         lower.Contains("each campaign")))
        {
            tool = FindTool(tools, "get_affiliate_campaign_history");
            arguments = new Dictionary<string, object?> { ["affiliateId"] = int.Parse(affiliateId.Groups[1].Value) };
        }
        else if (lower.Contains("campaign") &&
                 (lower.Contains("summary") || lower.Contains("summarize") || lower.Contains("overview") ||
                  lower.Contains("total") || lower.Contains("revenue") || lower.Contains("commission") ||
                  lower.Contains("how much")))
        {
            if (!campaignId.Success)
                return new OrchestrationResult(
                    "Please specify a campaign ID so I can summarize the correct campaign.",
                    []);

            tool = FindTool(tools, "campaign_summary");
            arguments = new Dictionary<string, object?> { ["campaignId"] = int.Parse(campaignId.Groups[1].Value) };
        }
        else if (affiliateId.Success)
        {
            tool = FindTool(tools, "affiliate_lookup");
            arguments = new Dictionary<string, object?> { ["affiliateId"] = int.Parse(affiliateId.Groups[1].Value) };
        }
        else
        {
            return new OrchestrationResult(
                "I can list campaigns, look up an affiliate, show campaign history, find participants without sales, " +
                "summarize campaigns, and rank affiliates or campaigns. Try one of the suggested questions.",
                []);
        }

        logger.LogInformation("Guided demo selected MCP tool {ToolName}.", tool.Name);
        var started = Stopwatch.GetTimestamp();
        var result = await tool.CallAsync(arguments, cancellationToken: cancellationToken);
        var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        logger.LogInformation(
            "MCP tool {ToolName} completed in {ElapsedMs} ms with {ContentBlocks} content blocks.",
            tool.Name,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            result.Content.Count);

        var explanation = ExplainResult(tool.Name, text, arguments);
        var explanationSection = string.IsNullOrEmpty(explanation) ? string.Empty : $"{explanation}\n\n";
        return new OrchestrationResult(
            $"Guided demo mode used the {tool.Name} tool.\n\n{explanationSection}{ToolOutputFormatter.Format(text)}\n\n" +
            "Configure an AI model to receive a conversational interpretation.",
            [
                new ToolInvocation(
                    tool.Name,
                    JsonSerializer.Serialize(arguments),
                    text,
                    result.IsError != true,
                    (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds)
            ]);
    }

    private static string ExplainResult(
        string toolName,
        string resultText,
        IReadOnlyDictionary<string, object?>? arguments)
    {
        if (toolName is not ("campaign_participants_without_sales" or "affiliate_ranking"))
            return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(resultText);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return string.Empty;

            var count = document.RootElement.GetArrayLength();
            var campaignId = arguments is not null &&
                arguments.TryGetValue("campaignId", out var campaignValue) &&
                campaignValue is int id
                    ? $" in campaign {id}"
                    : string.Empty;

            if (toolName == "campaign_participants_without_sales" && count == 0)
                return $"No participants without recorded sales matched{campaignId}. A fully refunded sale still counts as a recorded sale.";

            if (toolName == "affiliate_ranking")
            {
                var limit = arguments is not null &&
                    arguments.TryGetValue("limit", out var limitValue) &&
                    limitValue is int requestedLimit
                        ? requestedLimit
                        : 5;

                if (count < limit)
                    return $"Found {count} affiliate(s) with recorded sales matching{campaignId}; all matching affiliates are listed.";
            }
        }
        catch (JsonException)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static Dictionary<string, object?> CampaignArguments(string lower, Match campaignId)
    {
        if (campaignId.Success)
            return new Dictionary<string, object?> { ["campaignId"] = int.Parse(campaignId.Groups[1].Value) };

        var campaignName = lower.Contains("winter") ? "Winter Kickoff"
            : lower.Contains("spring") ? "Spring Launch"
            : lower.Contains("summer") ? "Summer Splash"
            : lower.Contains("school") ? "Back to School"
            : null;
        return campaignName is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?> { ["campaignName"] = campaignName };
    }

    private static int ExtractCount(string question, int defaultCount)
    {
        var match = Regex.Match(question, @"\btop\s+([1-9]|1\d|2[0-5])\b", RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value) : defaultCount;
    }

    private static bool IsSingularWinnerQuestion(string question) =>
        Regex.IsMatch(question, @"\b(which|what)\s+affiliate\b|\bwho\b.*\b(most|highest)\b",
            RegexOptions.IgnoreCase);

    private static McpClientTool FindTool(IEnumerable<McpClientTool> tools, string name) =>
        tools.Single(tool => string.Equals(tool.Name, name, StringComparison.Ordinal));

    private static string SummarizeResult(object? result)
    {
        return result is string value ? value : JsonSerializer.Serialize(result);
    }
}
