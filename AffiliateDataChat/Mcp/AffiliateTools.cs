using System.ComponentModel;
using System.Text.Json;
using AffiliateDataChat.Data;
using ModelContextProtocol.Server;

namespace AffiliateDataChat.Mcp;

[McpServerToolType]
public sealed class AffiliateTools(SqliteAffiliateRepository repository)
{
<<<<<<< HEAD
    private const int MaxRankingResults = 10;
=======
>>>>>>> e6f976d (fixed chatbot not answering different questions)
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

<<<<<<< HEAD
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
=======
    [McpServerTool(Name = "list_campaigns"), Description("List available campaigns with their dates, participant counts, and recorded sales counts.")]
    public async Task<string> ListCampaignsAsync(CancellationToken cancellationToken = default)
    {
        var campaigns = await repository.ListCampaignsAsync(cancellationToken);
        return JsonSerializer.Serialize(campaigns, JsonOptions);
    }

    [McpServerTool(Name = "lookup_affiliate"), Description("Look up an affiliate by exact ID and return their profile and performance totals.")]
    public async Task<string> LookupAffiliateAsync(
        [Description("Affiliate ID to look up.")] int affiliateId,
        CancellationToken cancellationToken = default)
    {
        if (affiliateId <= 0)
            return "affiliateId must be greater than zero.";

        var affiliate = await repository.GetAffiliateAsync(affiliateId, cancellationToken);
        return affiliate is null
            ? $"No affiliate was found for ID {affiliateId}."
            : JsonSerializer.Serialize(affiliate, JsonOptions);
    }

    [McpServerTool(Name = "get_affiliate_campaign_history"), Description("Show an affiliate's participation and sales performance for each campaign.")]
    public async Task<string> GetAffiliateCampaignHistoryAsync(
        [Description("Affiliate ID to look up.")] int affiliateId,
        CancellationToken cancellationToken = default)
    {
        if (affiliateId <= 0)
            return "affiliateId must be greater than zero.";

        var affiliate = await repository.GetAffiliateAsync(affiliateId, cancellationToken);
        if (affiliate is null)
            return $"No affiliate was found for ID {affiliateId}.";

        var history = await repository.GetAffiliateCampaignHistoryAsync(affiliateId, cancellationToken);
        return JsonSerializer.Serialize(history, JsonOptions);
    }

    [McpServerTool(Name = "find_affiliates_without_sales"), Description("Find campaign participants with no recorded sales, optionally filtered by campaign ID or name. Returns at most 25 participants.")]
    public async Task<string> FindAffiliatesWithoutSalesAsync(
        [Description("Optional campaign ID.")] int? campaignId = null,
        [Description("Optional campaign name or part of a name.")] string? campaignName = null,
        CancellationToken cancellationToken = default)
    {
        if (campaignId is <= 0)
            return "campaignId must be greater than zero.";

        var participants = await repository.FindParticipantsWithoutSalesAsync(
            campaignId, campaignName, cancellationToken);
        return JsonSerializer.Serialize(participants, JsonOptions);
    }

    [McpServerTool(Name = "summarize_campaign"), Description("Summarize campaign participants, sales, gross revenue, discounts, refunds, net revenue, and commission expense.")]
    public async Task<string> SummarizeCampaignAsync(
        [Description("Campaign ID to summarize.")] int campaignId,
        CancellationToken cancellationToken = default)
    {
        if (campaignId <= 0)
            return "campaignId must be greater than zero.";

        var summary = await repository.SummarizeCampaignAsync(campaignId, cancellationToken);
        return summary is null
            ? $"No campaign was found for ID {campaignId}."
            : JsonSerializer.Serialize(summary, JsonOptions);
    }

    [McpServerTool(Name = "rank_affiliates"), Description("Rank affiliates with sales by net revenue, optionally filtered by campaign or date range. Returns at most 25 affiliates.")]
    public async Task<string> RankAffiliatesAsync(
        [Description("Optional campaign ID.")] int? campaignId = null,
        [Description("Optional inclusive start date in yyyy-MM-dd format.")] DateOnly? from = null,
        [Description("Optional inclusive end date in yyyy-MM-dd format.")] DateOnly? to = null,
        [Description("Maximum number of affiliates to return, from 1 through 25.")] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (campaignId is <= 0)
            return "campaignId must be greater than zero.";
        if (limit is < 1 or > 25)
            return "limit must be from 1 through 25.";

        var rankings = await repository.RankAffiliatesAsync(
            campaignId, from, to, limit, cancellationToken);
        return JsonSerializer.Serialize(rankings, JsonOptions);
>>>>>>> e6f976d (fixed chatbot not answering different questions)
    }
}
