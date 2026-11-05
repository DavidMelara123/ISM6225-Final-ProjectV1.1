using AffiliateDataChat.Data;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace AffiliateDataChat.Mcp;

[McpServerToolType]
public sealed class CourseTools(SqliteCourseRepository repository)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    [McpServerTool(Name = "search_courses"), Description("Search courses by words in the code/title and optional department, delivery mode, or minimum enrollment. Returns at most 20 matching rows.")]
public async Task<string> SearchCoursesAsync(
    [Description("Optional words from a course code or title.")] string? searchText = null,
    [Description("Optional exact department, such as AI or Business Analytics.")] string? department = null,
    [Description("Optional exact mode: Online, Hybrid, or In Person.")] string? deliveryMode = null,
    [Description("Optional minimum enrollment. Must be zero or greater.")] int? minimumEnrollment = null,
    CancellationToken cancellationToken = default)
    {
        if (minimumEnrollment < 0) return "minimumEnrollment must be zero or greater.";
        var courses = await repository.SearchAsync(searchText, department, deliveryMode, minimumEnrollment, cancellationToken);
        return JsonSerializer.Serialize(courses, JsonOptions);
    }
    [McpServerTool(Name = "get_course_by_code"), Description("Get one course by its exact catalog code, for example AI-601.")]
    public async Task<string> GetCourseByCodeAsync(
    [Description("Exact course code.")] string code,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return "A course code is required.";
        var course = await repository.GetByCodeAsync(code, cancellationToken);
        return course is null
        ? $"No course was found for code {code}."
        : JsonSerializer.Serialize(course, JsonOptions);
    }
    [McpServerTool(Name = "top_courses_by_enrollment"), Description("Return the courses with the highest enrollment, from highest to lowest.")]
    public async Task<string> TopCoursesByEnrollmentAsync(
[Description("Number of courses to return, from 1 through 10.")] int count = 5,
CancellationToken cancellationToken = default)
    {
        var courses = await repository.TopByEnrollmentAsync(Math.Clamp(count, 1, 10), cancellationToken);
        return JsonSerializer.Serialize(courses, JsonOptions);
    }
    [McpServerTool(Name = "summarize_enrollment"), Description("Return overall course count, total enrollment, average class size, and average grade.")]
    public async Task<string> SummarizeEnrollmentAsync(CancellationToken cancellationToken = default)
    {
        var summary = await repository.SummarizeAsync(cancellationToken);
        return JsonSerializer.Serialize(summary, JsonOptions);
    }
}