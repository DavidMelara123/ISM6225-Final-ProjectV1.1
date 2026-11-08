using System.ComponentModel.DataAnnotations;
using AffiliateDataChat.Services.Chat;

namespace AffiliateDataChat.Models;

/// <summary>The question a user submits. Validated on the server; the same rules run in the browser.</summary>
public sealed class ChatInputModel
{
    public const int MinLength = 3;
    public const int MaxLength = 500;

    [Required(ErrorMessage = "Please type a question before sending.")]
    [StringLength(MaxLength, MinimumLength = MinLength,
        ErrorMessage = "Questions must be between {2} and {1} characters.")]
    [Display(Name = "Your question")]
    public string? Question { get; set; }
}

/// <summary>One message in the stored conversation.</summary>
public sealed class ChatMessage
{
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";

    public required string Role { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Assistant messages only: how the answer was produced.</summary>
    public ChatMode? Mode { get; init; }
    public string? ModelName { get; init; }
    public List<ToolInvocation> ToolCalls { get; init; } = [];

    /// <summary>True when this is a friendly error message rather than a real answer.</summary>
    public bool IsError { get; init; }

    public bool IsUser => Role == UserRole;
}

/// <summary>Settings for the chat page, from the "ChatUi" section of appsettings.json.</summary>
public sealed class ChatUiOptions
{
    public const string SectionName = "ChatUi";

    public int TimeoutSeconds { get; set; } = 45;
    public int MaxStoredMessages { get; set; } = 30;
    public int HistoryTurnsSentToService { get; set; } = 8;
    public int RequestsPerMinute { get; set; } = 10;
    public List<string> SuggestedQuestions { get; set; } = [];
}
