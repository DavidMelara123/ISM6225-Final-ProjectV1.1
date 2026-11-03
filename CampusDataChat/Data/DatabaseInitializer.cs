using Microsoft.Data.Sqlite;
namespace CampusDataChat.Data;

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
CREATE TABLE IF NOT EXISTS Courses (
Id INTEGER PRIMARY KEY AUTOINCREMENT,
Code TEXT NOT NULL UNIQUE,
Title TEXT NOT NULL,
Department TEXT NOT NULL,
Enrollment INTEGER NOT NULL CHECK (Enrollment >= 0),
AverageGrade REAL NOT NULL CHECK (AverageGrade BETWEEN 0 AND 100),
DeliveryMode TEXT NOT NULL
);
""";
        await create.ExecuteNonQueryAsync();
        var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM Courses;";
        var existing = Convert.ToInt32(await count.ExecuteScalarAsync());
        if (existing > 0) return;
        var seedRows = new (string Code, string Title, string Department, int Enrollment, double Grade, string Mode)[]
        {
("AI-601", "Applied Machine Learning", "AI", 42, 88.4, "Hybrid"),
("AI-615", "Responsible Generative AI", "AI", 36, 91.2, "In Person"),
("AI-630", "Natural Language Systems", "AI", 31, 87.9, "Online"),
("BA-510", "Data Visualization for Decisions", "Business Analytics", 55, 86.7, "Hybrid"),
("BA-525", "Predictive Business Analytics", "Business Analytics", 47, 89.1, "In Person"),
("BA-540", "Optimization and Simulation", "Business Analytics", 28, 84.6, "Online"),
("IS-505", "Database Systems", "Information Systems", 61, 85.8, "Hybrid"),
("IS-550", "Cloud Application Development", "Information Systems", 39, 90.3, "Online")
        };
        await using var transaction = await connection.BeginTransactionAsync();
        foreach (var row in seedRows)
        {
            var insert = connection.CreateCommand();
            insert.Transaction = (SqliteTransaction)transaction;
            insert.CommandText = """
INSERT INTO Courses(Code, Title, Department, Enrollment, AverageGrade, DeliveryMode)
VALUES ($code, $title, $department, $enrollment, $grade, $mode);
""";
            insert.Parameters.AddWithValue("$code", row.Code);
            insert.Parameters.AddWithValue("$title", row.Title);
            insert.Parameters.AddWithValue("$department", row.Department);
            insert.Parameters.AddWithValue("$enrollment", row.Enrollment);
            insert.Parameters.AddWithValue("$grade", row.Grade);
            insert.Parameters.AddWithValue("$mode", row.Mode);
            await insert.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }
}
