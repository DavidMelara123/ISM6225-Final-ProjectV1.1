using AffiliateDataChat.Data;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace AffiliateDataChat.Mcp;

public sealed class CourseTools(SqliteCourseRepository repository)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
public async Task<string> SearchCoursesAsync(
    string? searchText = null,
    string? department = null,
    string? deliveryMode = null,
    int? minimumEnrollment = null,
    CancellationToken cancellationToken = default)
    {
        if (minimumEnrollment < 0) return "minimumEnrollment must be zero or greater.";
        var courses = await repository.SearchAsync(searchText, department, deliveryMode, minimumEnrollment, cancellationToken);
        return JsonSerializer.Serialize(courses, JsonOptions);
    }
    public async Task<string> GetCourseByCodeAsync(
    string code,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return "A course code is required.";
        var course = await repository.GetByCodeAsync(code, cancellationToken);
        return course is null
        ? $"No course was found for code {code}."
        : JsonSerializer.Serialize(course, JsonOptions);
    }
    public async Task<string> TopCoursesByEnrollmentAsync(
int count = 5,
CancellationToken cancellationToken = default)
    {
        var courses = await repository.TopByEnrollmentAsync(Math.Clamp(count, 1, 10), cancellationToken);
        return JsonSerializer.Serialize(courses, JsonOptions);
    }
    public async Task<string> SummarizeEnrollmentAsync(CancellationToken cancellationToken = default)
    {
        var summary = await repository.SummarizeAsync(cancellationToken);
        return JsonSerializer.Serialize(summary, JsonOptions);
    }
}