using System.ComponentModel;
using System.Text.Json;
using AffiliateDataChat.Data;
using ModelContextProtocol.Server;

namespace AffiliateDataChat.Mcp;

[McpServerToolType]
public sealed class AffiliateTools(SqliteAffiliateRepository repository)
{
    private const int MaxRankingResults = 10;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [McpServerTool(Name = "affiliate_lookup"), Description("Look up one affiliate by its numeric affiliate ID and return its campaign and revenue totals.")]
    public async Task<string> AffiliateLookupAsync(
        [Description("Numeric affiliate ID, such as 101.")] int affiliateId,
        CancellationToken cancellationToken = default)
    {
        if (affiliateId < 1)
        {
            return "affiliateId must be greater than zero.";
        }

        var result = await repository.GetAffiliateAsync(affiliateId, cancellationToken);
        return result is null
            ? $"No affiliate was found for ID {affiliateId}."
            : JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "campaign_participants_without_sales"), Description("List campaign participants that have no sales, optionally filtered by campaign ID or name.")]
    public async Task<string> CampaignParticipantsWithoutSalesAsync(
        [Description("Optional numeric campaign ID.")] int? campaignId = null,
        [Description("Optional campaign name fragment, such as Spring Launch.")] string? campaignName = null,
        CancellationToken cancellationToken = default)
    {
        if (campaignId is <= 0)
        {
            return "campaignId must be greater than zero.";
        }

        var results = await repository.FindParticipantsWithoutSalesAsync(campaignId, campaignName, cancellationToken);
        return JsonSerializer.Serialize(results, JsonOptions);
    }

    [McpServerTool(Name = "campaign_summary"), Description("Return participant, sales, and revenue totals for one campaign.")]
    public async Task<string> CampaignSummaryAsync(
        [Description("Numeric campaign ID, such as 2.")] int campaignId,
        CancellationToken cancellationToken = default)
    {
        if (campaignId < 1)
        {
            return "campaignId must be greater than zero.";
        }

        var result = await repository.SummarizeCampaignAsync(campaignId, cancellationToken);
        return result is null
            ? $"No campaign was found for {campaignId}."
            : JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "campaign_ranking"), Description("Rank campaigns by number of sales, highest first.")]
    public async Task<string> CampaignRankingAsync(
        [Description("Maximum number of ranked campaigns to return, from 1 through 10.")] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxRankingResults)
        {
            return $"limit must be between 1 and {MaxRankingResults}.";
        }

        var results = await repository.RankCampaignsAsync(limit, cancellationToken);
        return JsonSerializer.Serialize(results, JsonOptions);
    }

    [McpServerTool(Name = "affiliate_ranking"), Description("Rank affiliates by net revenue, highest first, optionally for one campaign and date range.")]
    public async Task<string> AffiliateRankingAsync(
        [Description("Optional numeric campaign ID.")] int? campaignId = null,
        [Description("Optional inclusive start date in yyyy-MM-dd format.")] DateOnly? from = null,
        [Description("Optional inclusive end date in yyyy-MM-dd format.")] DateOnly? to = null,
        [Description("Maximum number of ranked affiliates to return, from 1 through 10.")] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (campaignId is <= 0)
        {
            return "campaignId must be greater than zero.";
        }

        if (limit is < 1 or > MaxRankingResults)
        {
            return $"limit must be between 1 and {MaxRankingResults}.";
        }

        var results = await repository.RankAffiliatesAsync(campaignId, from, to, limit, cancellationToken);
        return JsonSerializer.Serialize(results, JsonOptions);
    }
}
