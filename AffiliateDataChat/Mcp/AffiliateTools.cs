using System.ComponentModel;
using System.Text.Json;
using AffiliateDataChat.Data;
using ModelContextProtocol.Server;

namespace AffiliateDataChat.Mcp;

[McpServerToolType]
public sealed class AffiliateTools(SqliteAffiliateRepository repository)
{
    private const int MaxResults = 25;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [McpServerTool(Name = "list_campaigns"), Description("List available campaigns with dates, participation counts, and recorded sales counts.")]
    public async Task<string> ListCampaignsAsync(CancellationToken cancellationToken = default)
    {
        var campaigns = await repository.ListCampaignsAsync(cancellationToken);
        return JsonSerializer.Serialize(campaigns, JsonOptions);
    }

    [McpServerTool(Name = "affiliate_lookup"), Description("Look up one affiliate by numeric affiliate ID and return their campaign and revenue totals.")]
    public async Task<string> AffiliateLookupAsync(
        [Description("Numeric affiliate ID, such as 101.")] int affiliateId,
        CancellationToken cancellationToken = default)
    {
        if (affiliateId < 1)
            return "affiliateId must be greater than zero.";

        var affiliate = await repository.GetAffiliateAsync(affiliateId, cancellationToken);
        return affiliate is null
            ? $"No affiliate was found for ID {affiliateId}."
            : JsonSerializer.Serialize(affiliate, JsonOptions);
    }

    [McpServerTool(Name = "get_affiliate_campaign_history"), Description("Show an affiliate's participation, sales, net revenue, and commission for each campaign.")]
    public async Task<string> GetAffiliateCampaignHistoryAsync(
        [Description("Numeric affiliate ID to look up.")] int affiliateId,
        CancellationToken cancellationToken = default)
    {
        if (affiliateId < 1)
            return "affiliateId must be greater than zero.";

        var affiliate = await repository.GetAffiliateAsync(affiliateId, cancellationToken);
        if (affiliate is null)
            return $"No affiliate was found for ID {affiliateId}.";

        var history = await repository.GetAffiliateCampaignHistoryAsync(affiliateId, cancellationToken);
        return JsonSerializer.Serialize(history, JsonOptions);
    }

    [McpServerTool(Name = "campaign_participants_without_sales"), Description("List campaign participants with no recorded sales, optionally filtered by campaign ID or name.")]
    public async Task<string> CampaignParticipantsWithoutSalesAsync(
        [Description("Optional numeric campaign ID.")] int? campaignId = null,
        [Description("Optional campaign name fragment, such as Summer Splash.")] string? campaignName = null,
        CancellationToken cancellationToken = default)
    {
        if (campaignId is <= 0)
            return "campaignId must be greater than zero.";

        var participants = await repository.FindParticipantsWithoutSalesAsync(
            campaignId, campaignName, cancellationToken);
        return JsonSerializer.Serialize(participants, JsonOptions);
    }

    [McpServerTool(Name = "campaign_summary"), Description("Return participant, sales, gross revenue, discount, refund, net revenue, and commission totals for one campaign.")]
    public async Task<string> CampaignSummaryAsync(
        [Description("Numeric campaign ID, such as 2.")] int campaignId,
        CancellationToken cancellationToken = default)
    {
        if (campaignId < 1)
            return "campaignId must be greater than zero.";

        var summary = await repository.SummarizeCampaignAsync(campaignId, cancellationToken);
        return summary is null
            ? $"No campaign was found for ID {campaignId}."
            : JsonSerializer.Serialize(summary, JsonOptions);
    }

    [McpServerTool(Name = "campaign_ranking"), Description("Rank campaigns by recorded sales count, highest first.")]
    public async Task<string> CampaignRankingAsync(
        [Description("Maximum number of campaigns to return, from 1 through 25.")] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxResults)
            return $"limit must be from 1 through {MaxResults}.";

        var rankings = await repository.RankCampaignsAsync(limit, cancellationToken);
        return JsonSerializer.Serialize(rankings, JsonOptions);
    }

    [McpServerTool(Name = "affiliate_ranking"), Description("Rank affiliates with sales by net revenue, highest first, optionally filtered by campaign or date range.")]
    public async Task<string> AffiliateRankingAsync(
        [Description("Optional numeric campaign ID.")] int? campaignId = null,
        [Description("Optional inclusive start date in yyyy-MM-dd format.")] DateOnly? from = null,
        [Description("Optional inclusive end date in yyyy-MM-dd format.")] DateOnly? to = null,
        [Description("Maximum number of affiliates to return, from 1 through 25.")] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (campaignId is <= 0)
            return "campaignId must be greater than zero.";
        if (limit is < 1 or > MaxResults)
            return $"limit must be from 1 through {MaxResults}.";

        var rankings = await repository.RankAffiliatesAsync(
            campaignId, from, to, limit, cancellationToken);
        return JsonSerializer.Serialize(rankings, JsonOptions);
    }
}
