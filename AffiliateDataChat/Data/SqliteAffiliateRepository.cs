using System.Globalization;
using AffiliateDataChat.Models;
using Microsoft.Data.Sqlite;
namespace AffiliateDataChat.Data;

/// <summary>
/// Read-only queries behind the four MCP tools. Every user-supplied value is bound as a SQLite
/// parameter ($name); nothing is concatenated into SQL. Money is stored as cents and returned as decimal.
/// </summary>
public sealed class SqliteAffiliateRepository
{
    private const int MaxRows = 25;
    private readonly string _connectionString;

    public SqliteAffiliateRepository(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var path = DatabasePath.Resolve(configuration, environment);
        _connectionString = $"Data Source={path};Mode=ReadWriteCreate;Cache=Shared";
    }

    // Tool: lookup_affiliate. Returns null when the id does not exist.
    public async Task<AffiliateProfile?> GetAffiliateAsync(int affiliateId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
SELECT a.AffiliateId, a.Name, a.ContentCategory, a.JoinDate,
(SELECT COUNT(*) FROM AffiliateCampaigns ac WHERE ac.AffiliateId = a.AffiliateId),
(SELECT COUNT(*) FROM AffiliateSales s
   JOIN AffiliateCampaigns ac ON ac.AffiliateCampaignId = s.AffiliateCampaignId
   WHERE ac.AffiliateId = a.AffiliateId),
(SELECT COALESCE(SUM(s.GrossRevenueCents - s.DiscountCents - s.RefundCents), 0) FROM AffiliateSales s
   JOIN AffiliateCampaigns ac ON ac.AffiliateCampaignId = s.AffiliateCampaignId
   WHERE ac.AffiliateId = a.AffiliateId),
(SELECT COALESCE(SUM(s.CommissionCents), 0) FROM AffiliateSales s
   JOIN AffiliateCampaigns ac ON ac.AffiliateCampaignId = s.AffiliateCampaignId
   WHERE ac.AffiliateId = a.AffiliateId)
FROM Affiliates a
WHERE a.AffiliateId = $id;
""";
        command.Parameters.AddWithValue("$id", affiliateId);
        var rows = await ReadAsync(command, r => new AffiliateProfile(
            r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3),
            r.GetInt32(4), r.GetInt32(5), Money(r.GetInt64(6)), Money(r.GetInt64(7))), cancellationToken);
        return rows.SingleOrDefault();
    }

    // Tool: find_affiliates_without_sales. Participants with zero AffiliateSales rows, optionally for one campaign (id or name text).
    public async Task<IReadOnlyList<ParticipantWithoutSales>> FindParticipantsWithoutSalesAsync(
        int? campaignId, string? campaignName, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
SELECT a.AffiliateId, a.Name, c.CampaignId, c.CampaignName, ac.CommissionRate
FROM AffiliateCampaigns ac
JOIN Affiliates a ON a.AffiliateId = ac.AffiliateId
JOIN Campaigns c ON c.CampaignId = ac.CampaignId
WHERE NOT EXISTS (SELECT 1 FROM AffiliateSales s WHERE s.AffiliateCampaignId = ac.AffiliateCampaignId)
AND ($campaignId IS NULL OR c.CampaignId = $campaignId)
AND ($name = '' OR c.CampaignName LIKE $pattern ESCAPE '\')
ORDER BY c.CampaignId, a.AffiliateId
LIMIT $limit;
""";
        var cleanName = campaignName?.Trim() ?? "";
        var escaped = cleanName.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        command.Parameters.AddWithValue("$campaignId", campaignId is null ? DBNull.Value : campaignId.Value);
        command.Parameters.AddWithValue("$name", cleanName);
        command.Parameters.AddWithValue("$pattern", $"%{escaped}%");
        command.Parameters.AddWithValue("$limit", MaxRows);
        return await ReadAsync(command, r => new ParticipantWithoutSales(
            r.GetInt32(0), r.GetString(1), r.GetInt32(2), r.GetString(3), r.GetDouble(4)), cancellationToken);
    }

    // Tool: summarize_campaign. Returns null when the campaign does not exist.
    public async Task<CampaignSummary?> SummarizeCampaignAsync(int campaignId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
SELECT c.CampaignId, c.CampaignName, c.StartDate, c.EndDate,
(SELECT COUNT(*) FROM AffiliateCampaigns p WHERE p.CampaignId = c.CampaignId),
(SELECT COUNT(*) FROM AffiliateCampaigns p WHERE p.CampaignId = c.CampaignId
   AND NOT EXISTS (SELECT 1 FROM AffiliateSales x WHERE x.AffiliateCampaignId = p.AffiliateCampaignId)),
COUNT(s.SaleId),
COALESCE(SUM(s.GrossRevenueCents), 0), COALESCE(SUM(s.DiscountCents), 0),
COALESCE(SUM(s.RefundCents), 0), COALESCE(SUM(s.CommissionCents), 0)
FROM Campaigns c
LEFT JOIN AffiliateCampaigns ac ON ac.CampaignId = c.CampaignId
LEFT JOIN AffiliateSales s ON s.AffiliateCampaignId = ac.AffiliateCampaignId
WHERE c.CampaignId = $id
GROUP BY c.CampaignId;
""";
        command.Parameters.AddWithValue("$id", campaignId);
        var rows = await ReadAsync(command, r =>
        {
            var gross = Money(r.GetInt64(7));
            var discount = Money(r.GetInt64(8));
            var refund = Money(r.GetInt64(9));
            var commission = Money(r.GetInt64(10));
            var net = gross - discount - refund;
            return new CampaignSummary(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3),
                r.GetInt32(4), r.GetInt32(5), r.GetInt32(6), gross, discount, refund, net, commission, net - commission);
        }, cancellationToken);
        return rows.SingleOrDefault();
    }

    // Tool: rank_campaigns. Ranks campaigns by sales count (ties: lower CampaignId first).
    public async Task<IReadOnlyList<CampaignRank>> RankCampaignsAsync(
        int limit, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
SELECT c.CampaignId, c.CampaignName, COUNT(s.SaleId),
COALESCE(SUM(s.GrossRevenueCents - s.DiscountCents - s.RefundCents), 0),
COALESCE(SUM(s.CommissionCents), 0)
FROM Campaigns c
LEFT JOIN AffiliateCampaigns ac ON ac.CampaignId = c.CampaignId
LEFT JOIN AffiliateSales s ON s.AffiliateCampaignId = ac.AffiliateCampaignId
GROUP BY c.CampaignId, c.CampaignName
ORDER BY COUNT(s.SaleId) DESC, c.CampaignId
LIMIT $limit;
""";
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, MaxRows));
        var rows = await ReadAsync(command, r =>
        {
            var net = Money(r.GetInt64(3));
            var commission = Money(r.GetInt64(4));
            return (Id: r.GetInt32(0), Name: r.GetString(1), Sales: r.GetInt32(2),
                Net: net, Commission: commission);
        }, cancellationToken);
        return rows.Select((x, i) => new CampaignRank(
            i + 1, x.Id, x.Name, x.Sales, x.Net, x.Commission, x.Net - x.Commission)).ToList();
    }

    // Tool: rank_affiliates. Ranks affiliates that have at least one sale by net revenue (ties: lower AffiliateId first).
    public async Task<IReadOnlyList<AffiliateRank>> RankAffiliatesAsync(
        int? campaignId, DateOnly? from, DateOnly? to, int limit, CancellationToken cancellationToken)
    {
        if (from is not null && to is not null && from > to)
            throw new ArgumentException("The start date must not be after the end date.");

        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
SELECT a.AffiliateId, a.Name, COUNT(s.SaleId),
SUM(s.GrossRevenueCents - s.DiscountCents - s.RefundCents) AS NetCents,
SUM(s.CommissionCents)
FROM AffiliateSales s
JOIN AffiliateCampaigns ac ON ac.AffiliateCampaignId = s.AffiliateCampaignId
JOIN Affiliates a ON a.AffiliateId = ac.AffiliateId
WHERE ($campaignId IS NULL OR ac.CampaignId = $campaignId)
AND ($from IS NULL OR s.SaleDate >= $from)
AND ($to IS NULL OR s.SaleDate <= $to)
GROUP BY a.AffiliateId, a.Name
ORDER BY NetCents DESC, a.AffiliateId
LIMIT $limit;
""";
        command.Parameters.AddWithValue("$campaignId", campaignId is null ? DBNull.Value : campaignId.Value);
        command.Parameters.AddWithValue("$from", from is null ? DBNull.Value : from.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$to", to is null ? DBNull.Value : to.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, MaxRows));
        var rows = await ReadAsync(command, r => (
            Id: r.GetInt32(0), Name: r.GetString(1), Sales: r.GetInt32(2),
            Net: Money(r.GetInt64(3)), Commission: Money(r.GetInt64(4))), cancellationToken);
        return rows.Select((x, i) => new AffiliateRank(i + 1, x.Id, x.Name, x.Sales, x.Net, x.Commission, x.Net - x.Commission)).ToList();
    }

    // For testing: row counts per table (expected 8, 4, 14, 20).
    public async Task<IReadOnlyDictionary<string, int>> GetTableCountsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var counts = new Dictionary<string, int>();
        foreach (var table in new[] { "Affiliates", "Campaigns", "AffiliateCampaigns", "AffiliateSales" }) // fixed names, not user input
        {
            var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table};";
            counts[table] = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }
        return counts;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static decimal Money(long cents) => cents / 100m;

    private static async Task<IReadOnlyList<T>> ReadAsync<T>(
        SqliteCommand command, Func<SqliteDataReader, T> map, CancellationToken cancellationToken)
    {
        var results = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            results.Add(map(reader));
        return results;
    }
}
