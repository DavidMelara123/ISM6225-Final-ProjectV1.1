using AffiliateDataChat.Models;
using Microsoft.Data.Sqlite;
namespace AffiliateDataChat.Data;

public sealed class SqliteCourseRepository
{
    private readonly string _connectionString; public SqliteCourseRepository(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var path = DatabasePath.Resolve(configuration, environment);
        _connectionString = $"Data Source={path};Mode=ReadWriteCreate;Cache=Shared";
    }
    public async Task<IReadOnlyList<Course>> SearchAsync(
    string? searchText,
    string? department,
    string? deliveryMode,
    int? minimumEnrollment,
    CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
SELECT Id, Code, Title, Department, Enrollment, AverageGrade, DeliveryMode
FROM Courses
WHERE ($search = '' OR Code LIKE $pattern OR Title LIKE $pattern)
AND ($department = '' OR Department = $department COLLATE NOCASE)
AND ($mode = '' OR DeliveryMode = $mode COLLATE NOCASE)
AND ($minimum IS NULL OR Enrollment >= $minimum)
ORDER BY Department, Code
LIMIT 20;
""";
        var cleanSearch = searchText?.Trim() ?? "";
        command.Parameters.AddWithValue("$search", cleanSearch);
        command.Parameters.AddWithValue("$pattern", $"%{cleanSearch}%");
        command.Parameters.AddWithValue("$department", department?.Trim() ?? "");
        command.Parameters.AddWithValue("$mode", deliveryMode?.Trim() ?? "");
        command.Parameters.AddWithValue("$minimum", minimumEnrollment is null ? DBNull.Value : minimumEnrollment.Value);
        return await ReadCoursesAsync(command, cancellationToken);
    }
    public async Task<Course?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
SELECT Id, Code, Title, Department, Enrollment, AverageGrade, DeliveryMode
FROM Courses WHERE Code = $code COLLATE NOCASE LIMIT 1;
""";
        command.Parameters.AddWithValue("$code", code.Trim());
        return (await ReadCoursesAsync(command, cancellationToken)).SingleOrDefault();
    }
    public async Task<IReadOnlyList<Course>> TopByEnrollmentAsync(int count, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
SELECT Id, Code, Title, Department, Enrollment, AverageGrade, DeliveryMode
FROM Courses ORDER BY Enrollment DESC, Code LIMIT $count;
""";
        command.Parameters.AddWithValue("$count", Math.Clamp(count, 1, 10));
        return await ReadCoursesAsync(command, cancellationToken);
    }
    public async Task<object> SummarizeAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
SELECT COUNT(*), SUM(Enrollment), AVG(Enrollment), AVG(AverageGrade)
FROM Courses;
""";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new
        {
            CourseCount = reader.GetInt32(0),
            TotalEnrollment = reader.GetInt32(1),
            AverageClassSize = Math.Round(reader.GetDouble(2), 1),
            AverageGrade = Math.Round(reader.GetDouble(3), 1)
        };
    }
    private static async Task<IReadOnlyList<Course>> ReadCoursesAsync(
    SqliteCommand command,
    CancellationToken cancellationToken)
    {
        var courses = new List<Course>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            courses.Add(new Course(
            reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetInt32(4), reader.GetDouble(5), reader.GetString(6)));
        }
        return courses;
    }
}