namespace AffiliateDataChat.Services.Chat;

// -----------------------------------------------------------------------------
// SHARED CONTRACT between the MVC layer (Joel) and the chatbot service (Morgan).
//
// The controllers only ever talk to IChatbotService. Today OrchestratorChatbotService
// adapts the existing ChatOrchestrator to it. Agree on any change to these types
// as a team before editing them.
// -----------------------------------------------------------------------------

/// <summary>How an answer was produced. Shown to the user as a badge.</summary>
public enum ChatMode
{
    /// <summary>A language model chose and called the MCP tools.</summary>
    Model,

    /// <summary>No model configured or available, so rules-based routing called the tools.</summary>
    Fallback
}

/// <summary>One earlier turn of the conversation, passed to the service as context.</summary>
/// <param name="Role">"user" or "assistant".</param>
/// <param name="Content">The text of that turn.</param>
public sealed record ChatTurn(string Role, string Content);

/// <summary>Evidence of one MCP tool call, displayed under the answer.</summary>
/// <param name="ToolName">e.g. lookup_affiliate, summarize_campaign.</param>
/// <param name="ArgumentsJson">Arguments sent to the tool, as JSON.</param>
/// <param name="ResultSummary">Short, human-readable summary of what the tool returned.</param>
/// <param name="Succeeded">False if the tool returned an error or failed validation.</param>
/// <param name="DurationMs">How long the call took.</param>
public sealed record ToolInvocation(
    string ToolName,
    string ArgumentsJson,
    string ResultSummary,
    bool Succeeded,
    long DurationMs);

/// <summary>What the chatbot service returns for one question.</summary>
/// <param name="Answer">Plain-text answer. The UI encodes it and keeps line breaks; it does not render HTML or Markdown.</param>
/// <param name="Mode">Model or Fallback.</param>
/// <param name="ModelName">Model used, if any (null in fallback mode).</param>
/// <param name="ToolCalls">Every MCP tool call made while answering, in order.</param>
public sealed record ChatbotReply(
    string Answer,
    ChatMode Mode,
    string? ModelName,
    IReadOnlyList<ToolInvocation> ToolCalls);

/// <summary>Current state of the chatbot, shown on the chat page and the About page.</summary>
/// <param name="Mode">The mode the next answer is expected to use.</param>
/// <param name="ModelName">Configured model, if any.</param>
/// <param name="DiscoveredTools">Tool names found through MCP runtime discovery.</param>
/// <param name="StatusMessage">Optional note, e.g. "Model key missing - using fallback".</param>
public sealed record ChatbotStatus(
    ChatMode Mode,
    string? ModelName,
    IReadOnlyList<string> DiscoveredTools,
    string? StatusMessage = null);

public interface IChatbotService
{
    /// <summary>Answers one question. May throw; the controller turns exceptions into a friendly message.</summary>
    Task<ChatbotReply> AskAsync(string question, IReadOnlyList<ChatTurn> history, CancellationToken cancellationToken);

    /// <summary>Reports mode, model and discovered tools. Should not throw; report problems in StatusMessage.</summary>
    Task<ChatbotStatus> GetStatusAsync(CancellationToken cancellationToken);
}
