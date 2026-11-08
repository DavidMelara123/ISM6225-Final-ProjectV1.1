using AffiliateDataChat.Models;
using AffiliateDataChat.Services.Chat;

namespace AffiliateDataChat.ViewModels;

public sealed class ChatPageViewModel
{
    public IReadOnlyList<ChatMessage> Messages { get; init; } = [];
    public ChatInputModel Input { get; init; } = new();
    public IReadOnlyList<string> SuggestedQuestions { get; init; } = [];
    public required ChatbotStatus Status { get; init; }
}

public sealed class AboutViewModel
{
    public required ChatbotStatus Status { get; init; }

    /// <summary>The four tools the team planned; compared against what was discovered at runtime.</summary>
    public IReadOnlyList<PlannedTool> PlannedTools { get; init; } = [];
}

public sealed record PlannedTool(string Name, string Purpose, string ExampleQuestion);

public sealed class StatusPageViewModel
{
    public int StatusCode { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public string? RequestId { get; init; }
}
