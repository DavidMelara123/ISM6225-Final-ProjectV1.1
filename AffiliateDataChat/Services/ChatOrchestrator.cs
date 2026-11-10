<<<<<<< HEAD
using System.Diagnostics;
=======
>>>>>>> e6f976d (fixed chatbot not answering different questions)
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
<<<<<<< HEAD
 new(ChatRole.System, systemPrompt),
=======
new(ChatRole.System,
"You are a concise affiliate campaign performance assistant. Use the available affiliate MCP tools for every factual data answer. " +
"Never invent rows or run arbitrary SQL. State when no result is found. Net revenue is gross revenue minus discounts and refunds. " +
"Revenue after commission is not profit because other costs are excluded."),
>>>>>>> e6f976d (fixed chatbot not answering different questions)
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
<<<<<<< HEAD
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
=======
        var affiliateId = Regex.Match(question, @"\baffiliate(?:\s+id)?\s*#?\s*(\d+)\b", RegexOptions.IgnoreCase);
        var campaignId = Regex.Match(question, @"\bcampaign\s*#?\s*(\d+)\b", RegexOptions.IgnoreCase);

        if (lower.Contains("list campaigns") || lower.Contains("which campaigns") ||
            lower.Contains("what campaigns") || lower.Contains("available campaigns"))
        {
            tool = FindTool(tools, "list_campaigns");
            arguments = null;
>>>>>>> e6f976d (fixed chatbot not answering different questions)
        }
        else if (affiliateId.Success && (lower.Contains("history") || lower.Contains("by campaign") ||
                                         lower.Contains("each campaign")))
        {
            tool = FindTool(tools, "get_affiliate_campaign_history");
            arguments = new Dictionary<string, object?> { ["affiliateId"] = int.Parse(affiliateId.Groups[1].Value) };
        }
        else if (affiliateId.Success)
        {
            tool = FindTool(tools, "lookup_affiliate");
            arguments = new Dictionary<string, object?> { ["affiliateId"] = int.Parse(affiliateId.Groups[1].Value) };
        }
        else if (lower.Contains("without sales") || lower.Contains("no sales") || lower.Contains("zero sales") ||
                 lower.Contains("extra support") || lower.Contains("need support"))
        {
            tool = FindTool(tools, "find_affiliates_without_sales");
            arguments = AffiliateCampaignArguments(lower, campaignId);
        }
        else if (lower.Contains("rank") || lower.Contains("top") || lower.Contains("highest net revenue") ||
                 lower.Contains("most net revenue") || lower.Contains("generated the most"))
        {
            tool = FindTool(tools, "rank_affiliates");
            var rankingArguments = new Dictionary<string, object?>();
            if (campaignId.Success)
                rankingArguments["campaignId"] = int.Parse(campaignId.Groups[1].Value);
            rankingArguments["limit"] = ExtractAffiliateRankCount(question);
            arguments = rankingArguments;
        }
        else if (campaignId.Success && (lower.Contains("summary") || lower.Contains("summarize") ||
                 lower.Contains("how much") || lower.Contains("revenue") || lower.Contains("commission")))
        {
            tool = FindTool(tools, "summarize_campaign");
            arguments = new Dictionary<string, object?> { ["campaignId"] = int.Parse(campaignId.Groups[1].Value) };
        }
        else if (lower.Contains("affiliate") || lower.Contains("campaign") ||
                 lower.Contains("commission") || lower.Contains("revenue"))
        {
            return "I can list campaigns, look up an affiliate, show an affiliate's campaign history, " +
                   "find participants with no sales, summarize a campaign, or rank affiliates by net revenue. " +
                   "Try one of the suggested questions.";
        }
        else
        {
<<<<<<< HEAD
            tool = FindTool(tools, "affiliate_lookup");
            var affiliateId = AffiliateIdRegex().Match(question);
            arguments = new Dictionary<string, object?>
            {
                ["affiliateId"] = affiliateId.Success ? int.Parse(affiliateId.Groups[1].Value) : 101
            };
=======
            return "I can answer questions about affiliates, campaigns, sales, and revenue. " +
                   "Try one of the suggested affiliate questions.";
>>>>>>> e6f976d (fixed chatbot not answering different questions)
        }
        logger.LogInformation("Demo mode selected MCP tool {ToolName}.", tool.Name);
        var started = Stopwatch.GetTimestamp();
        var result = await tool.CallAsync(arguments, cancellationToken: cancellationToken);
        var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
<<<<<<< HEAD
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
=======
        var explanation = ExplainResult(tool.Name, text, arguments);
        var explanationSection = string.IsNullOrEmpty(explanation) ? string.Empty : $"{explanation}\n\n";
        return $"Guided demo used the MCP tool `{tool.Name}`.\n\n{explanationSection}{text}\n\nConfigure an OpenAI key to receive a natural-language interpretation.";
>>>>>>> e6f976d (fixed chatbot not answering different questions)
    }

    private static string ExplainResult(
        string toolName,
        string resultText,
        IReadOnlyDictionary<string, object?>? arguments)
    {
        if (toolName is not ("find_affiliates_without_sales" or "rank_affiliates"))
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

            if (toolName == "find_affiliates_without_sales" && count == 0)
            {
                return $"No participants without recorded sales matched{campaignId}. A fully refunded sale still counts as a recorded sale.";
            }

            if (toolName == "rank_affiliates")
            {
                var limit = arguments is not null &&
                    arguments.TryGetValue("limit", out var limitValue) &&
                    limitValue is int requestedLimit
                        ? requestedLimit
                        : 10;

                if (count < limit)
                {
                    return $"Found {count} affiliate(s) with recorded sales matching{campaignId}; " +
                           $"the requested limit was {limit}, so all matching affiliates are listed.";
                }
            }
        }
        catch (JsonException)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static Dictionary<string, object?> AffiliateCampaignArguments(string lower, Match campaignId)
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

    private static int ExtractAffiliateRankCount(string question)
    {
        var match = Regex.Match(question, @"\btop\s+([1-9]|1\d|2[0-5])\b", RegexOptions.IgnoreCase);
        if (match.Success)
            return int.Parse(match.Groups[1].Value);

        return Regex.IsMatch(question,
            @"\b(which|what)\s+affiliate\b|\bwho\b.*\b(most|highest)\b",
            RegexOptions.IgnoreCase)
            ? 1
            : 10;
    }

    private static McpClientTool FindTool(IEnumerable<McpClientTool> tools, string name) =>
    tools.Single(tool => string.Equals(tool.Name, name, StringComparison.Ordinal));
<<<<<<< HEAD
    private static int ExtractCount(string question)
    {
        var match = Regex.Match(question, @"\b([1-9]|10)\b");
        return match.Success ? int.Parse(match.Value) : 5;
    }
    private static Regex CampaignIdRegex() => new(@"\bcampaign(?:\s+id)?\s*(\d+)\b", RegexOptions.IgnoreCase);
    private static Regex AffiliateIdRegex() => new(@"\b(?:affiliate|id)\s*(\d+)\b", RegexOptions.IgnoreCase);
}
=======
}











>>>>>>> e6f976d (fixed chatbot not answering different questions)
