using Microsoft.Data.Sqlite;
namespace AffiliateDataChat.Data;

/// <summary>
/// Creates the four affiliate tables and seeds 46 synthetic records
/// (8 affiliates, 4 campaigns, 14 participations, 20 sales). Safe to run on every startup.
/// Money is stored as whole cents (INTEGER) so SQL sums are exact.
/// Commission policy: CommissionCents = round((Gross - Discount - Refund) x CommissionRate), so refunds claw back commission.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var path = DatabasePath.Resolve(configuration, environment);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();

        var create = connection.CreateCommand();
        create.CommandText = """
PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS Affiliates (
AffiliateId INTEGER PRIMARY KEY,
Name TEXT NOT NULL,
ContentCategory TEXT NOT NULL,
JoinDate TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS Campaigns (
CampaignId INTEGER PRIMARY KEY,
CampaignName TEXT NOT NULL UNIQUE,
StartDate TEXT NOT NULL,
EndDate TEXT NOT NULL,
CHECK (EndDate >= StartDate)
);
CREATE TABLE IF NOT EXISTS AffiliateCampaigns (
AffiliateCampaignId INTEGER PRIMARY KEY,
AffiliateId INTEGER NOT NULL REFERENCES Affiliates(AffiliateId),
CampaignId INTEGER NOT NULL REFERENCES Campaigns(CampaignId),
CommissionRate REAL NOT NULL CHECK (CommissionRate >= 0 AND CommissionRate <= 1),
UNIQUE (AffiliateId, CampaignId)
);
CREATE TABLE IF NOT EXISTS AffiliateSales (
SaleId INTEGER PRIMARY KEY,
AffiliateCampaignId INTEGER NOT NULL REFERENCES AffiliateCampaigns(AffiliateCampaignId),
SaleDate TEXT NOT NULL,
GrossRevenueCents INTEGER NOT NULL CHECK (GrossRevenueCents >= 0),
DiscountCents INTEGER NOT NULL CHECK (DiscountCents >= 0),
RefundCents INTEGER NOT NULL CHECK (RefundCents >= 0),
CommissionCents INTEGER NOT NULL CHECK (CommissionCents >= 0),
CHECK (DiscountCents + RefundCents <= GrossRevenueCents)
);
CREATE INDEX IF NOT EXISTS IX_AffiliateSales_AffiliateCampaignId ON AffiliateSales(AffiliateCampaignId);
""";
        await create.ExecuteNonQueryAsync();

        var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM Affiliates;";
        if (Convert.ToInt32(await count.ExecuteScalarAsync()) > 0) return;

        var affiliates = new (int Id, string Name, string Category, string Joined)[]
        {
            (101, "Maya Chen", "Fitness", "2024-02-10"),
            (102, "Luis Ortega", "Tech Reviews", "2023-11-05"),
            (103, "Priya Nair", "Lifestyle", "2024-06-18"),
            (104, "Jordan Blake", "Travel", "2024-03-22"),
            (105, "Sofia Rossi", "Food", "2025-01-14"),
            (106, "Ethan Park", "Gaming", "2024-09-01"),
            (107, "Amara Okafor", "Fashion", "2025-02-27"),
            (108, "Noah Fischer", "Fitness", "2025-04-09")
        };
        var campaigns = new (int Id, string Name, string Start, string End)[]
        {
            (1, "Winter Kickoff", "2026-01-05", "2026-02-15"),
            (2, "Spring Launch", "2026-03-01", "2026-04-15"),
            (3, "Summer Splash", "2026-06-01", "2026-07-31"),
            (4, "Back to School", "2026-08-10", "2026-09-20")
        };
        var participations = new (int Id, int AffiliateId, int CampaignId, double Rate)[]
        {
            (1, 101, 1, 0.10),
            (2, 101, 3, 0.12),
            (3, 102, 1, 0.08),
            (4, 102, 3, 0.08),
            (5, 102, 4, 0.10),
            (6, 103, 2, 0.15),
            (7, 103, 3, 0.15),
            (8, 104, 3, 0.10),
            (9, 105, 2, 0.12),
            (10, 105, 4, 0.12),
            (11, 106, 3, 0.05),
            (12, 107, 4, 0.10),
            (13, 108, 3, 0.10),
            (14, 104, 1, 0.10)
        };
        var sales = new (int Id, int ParticipationId, string Date, long Gross, long Discount, long Refund, long Commission)[]
        {
            (1, 1, "2026-01-12", 20000, 2000, 0, 1800),
            (2, 1, "2026-01-25", 15000, 0, 0, 1500),
            (3, 1, "2026-02-03", 30000, 3000, 5000, 2200),
            (4, 3, "2026-01-20", 40000, 4000, 0, 2880),
            (5, 3, "2026-02-10", 25000, 0, 0, 2000),
            (6, 14, "2026-01-30", 12000, 0, 12000, 0),
            (7, 6, "2026-03-12", 50000, 5000, 0, 6750),
            (8, 6, "2026-04-02", 35000, 0, 7000, 4200),
            (9, 9, "2026-03-20", 22000, 2200, 0, 2376),
            (10, 2, "2026-06-10", 60000, 6000, 0, 6480),
            (11, 2, "2026-07-01", 18000, 0, 0, 2160),
            (12, 4, "2026-06-15", 75000, 7500, 10000, 4600),
            (13, 4, "2026-07-12", 30000, 0, 0, 2400),
            (14, 7, "2026-06-20", 90000, 9000, 0, 12150),
            (15, 7, "2026-07-15", 45000, 0, 15000, 4500),
            (16, 8, "2026-06-25", 26000, 2600, 0, 2340),
            (17, 13, "2026-07-05", 14000, 0, 0, 1400),
            (18, 5, "2026-08-20", 38000, 0, 0, 3800),
            (19, 5, "2026-09-02", 21000, 2100, 0, 1890),
            (20, 10, "2026-08-25", 32000, 0, 0, 3840)
        };

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        foreach (var a in affiliates)
            await RunAsync(connection, transaction,
                "INSERT INTO Affiliates(AffiliateId, Name, ContentCategory, JoinDate) VALUES ($id, $name, $category, $joined);",
                ("$id", a.Id), ("$name", a.Name), ("$category", a.Category), ("$joined", a.Joined));
        foreach (var c in campaigns)
            await RunAsync(connection, transaction,
                "INSERT INTO Campaigns(CampaignId, CampaignName, StartDate, EndDate) VALUES ($id, $name, $start, $end);",
                ("$id", c.Id), ("$name", c.Name), ("$start", c.Start), ("$end", c.End));
        foreach (var p in participations)
            await RunAsync(connection, transaction,
                "INSERT INTO AffiliateCampaigns(AffiliateCampaignId, AffiliateId, CampaignId, CommissionRate) VALUES ($id, $affiliate, $campaign, $rate);",
                ("$id", p.Id), ("$affiliate", p.AffiliateId), ("$campaign", p.CampaignId), ("$rate", p.Rate));
        foreach (var s in sales)
            await RunAsync(connection, transaction,
                """
INSERT INTO AffiliateSales(SaleId, AffiliateCampaignId, SaleDate, GrossRevenueCents, DiscountCents, RefundCents, CommissionCents)
VALUES ($id, $participation, $date, $gross, $discount, $refund, $commission);
""",
                ("$id", s.Id), ("$participation", s.ParticipationId), ("$date", s.Date), ("$gross", s.Gross),
                ("$discount", s.Discount), ("$refund", s.Refund), ("$commission", s.Commission));
        await transaction.CommitAsync();
    }

    private static async Task RunAsync(SqliteConnection connection, SqliteTransaction transaction,
        string sql, params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }
}
