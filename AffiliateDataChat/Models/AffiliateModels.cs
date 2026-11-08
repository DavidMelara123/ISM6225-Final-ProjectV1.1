namespace AffiliateDataChat.Models;

public record AffiliateProfile(
    int AffiliateId, string Name, string ContentCategory, string JoinDate,
    int CampaignCount, int SalesCount, decimal NetRevenue, decimal CommissionExpense);

public record ParticipantWithoutSales(
    int AffiliateId, string AffiliateName, int CampaignId, string CampaignName, double CommissionRate);

public record CampaignSummary(
    int CampaignId, string CampaignName, string StartDate, string EndDate,
    int ParticipantCount, int ParticipantsWithoutSales, int SalesCount,
    decimal GrossRevenue, decimal DiscountAmount, decimal RefundAmount,
    decimal NetRevenue, decimal CommissionExpense, decimal RevenueAfterCommission);

public record AffiliateRank(
    int Rank, int AffiliateId, string Name, int SalesCount,
    decimal NetRevenue, decimal CommissionExpense, decimal RevenueAfterCommission);
