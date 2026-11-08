using AffiliateDataChat.Models;
using AffiliateDataChat.Services.Chat;
using AffiliateDataChat.Services.Conversation;
using AffiliateDataChat.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AffiliateDataChat.Controllers;

public sealed class ChatController(
    IChatbotService chatbot,
    IConversationStore conversation,
    IOptions<ChatUiOptions> options,
    ILogger<ChatController> logger) : Controller
{
    public const string RateLimitPolicy = "chat";

    private ChatUiOptions Settings => options.Value;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await BuildPageAsync(new ChatInputModel(), cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<IActionResult> Ask([Bind(Prefix = "Input")] ChatInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            // Re-show the page with the validation message and the user's text kept in the box.
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(nameof(Index), await BuildPageAsync(input, cancellationToken));
        }

        var question = input.Question!.Trim();

        // Context for the service: recent real turns only (no error messages), oldest first.
        var history = conversation.GetMessages()
            .Where(m => !m.IsError)
            .TakeLast(Settings.HistoryTurnsSentToService)
            .Select(m => new ChatTurn(m.Role, m.Content))
            .ToList();

        conversation.Add(new ChatMessage { Role = ChatMessage.UserRole, Content = question });
        conversation.Add(await GetAnswerAsync(question, history, cancellationToken));

        // Post/Redirect/Get: refreshing the page will not resend the question.
        return Redirect(Url.Action(nameof(Index))! + "#latest");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Clear()
    {
        conversation.Clear();
        return RedirectToAction(nameof(Index));
    }

    private async Task<ChatMessage> GetAnswerAsync(string question, IReadOnlyList<ChatTurn> history, CancellationToken requestAborted)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(Settings.TimeoutSeconds));

        try
        {
            var reply = await chatbot.AskAsync(question, history, timeout.Token);
            return new ChatMessage
            {
                Role = ChatMessage.AssistantRole,
                Content = string.IsNullOrWhiteSpace(reply.Answer)
                    ? "The assistant returned an empty answer. Please try rephrasing your question."
                    : reply.Answer.Trim(),
                Mode = reply.Mode,
                ModelName = reply.ModelName,
                ToolCalls = reply.ToolCalls.ToList()
            };
        }
        catch (OperationCanceledException) when (!requestAborted.IsCancellationRequested)
        {
            logger.LogWarning("Chatbot timed out after {Seconds}s", Settings.TimeoutSeconds);
            return ErrorMessage($"That took longer than {Settings.TimeoutSeconds} seconds, so I stopped waiting. " +
                                "Please try again, or ask a narrower question (for example, one campaign).");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Full details go to the log for the team; the user gets a plain message with no internals.
            logger.LogError(ex, "Chatbot service failed for a question of {Length} characters", question.Length);
            return ErrorMessage("Sorry — I couldn't get an answer right now. Your question was not lost; " +
                                "please try again in a moment.");
        }
    }

    private static ChatMessage ErrorMessage(string text) =>
        new() { Role = ChatMessage.AssistantRole, Content = text, IsError = true };

    private async Task<ChatPageViewModel> BuildPageAsync(ChatInputModel input, CancellationToken cancellationToken) => new()
    {
        Messages = conversation.GetMessages(),
        Input = input,
        SuggestedQuestions = Settings.SuggestedQuestions,
        Status = await SafeStatusAsync(chatbot, logger, cancellationToken)
    };

    /// <summary>Status is informational: if it fails, the page still loads.</summary>
    internal static async Task<ChatbotStatus> SafeStatusAsync(IChatbotService chatbot, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            return await chatbot.GetStatusAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read chatbot status");
            return new ChatbotStatus(ChatMode.Fallback, null, [], "Status unavailable — the chatbot service did not respond.");
        }
    }
}
