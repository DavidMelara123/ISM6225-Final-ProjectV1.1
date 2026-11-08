using System.Text.Json;
using AffiliateDataChat.Models;
using Microsoft.Extensions.Options;

namespace AffiliateDataChat.Services.Conversation;

/// <summary>Keeps one visitor's conversation history.</summary>
public interface IConversationStore
{
    IReadOnlyList<ChatMessage> GetMessages();
    void Add(ChatMessage message);
    void Clear();
}

/// <summary>
/// Stores the conversation in the ASP.NET Core session (server-side memory, keyed by a
/// session cookie). History is per browser, is capped, and is lost when the app restarts —
/// which is fine: the affiliate data lives in SQLite, not here.
/// </summary>
public sealed class SessionConversationStore(IHttpContextAccessor accessor, IOptions<ChatUiOptions> options)
    : IConversationStore
{
    private const string SessionKey = "chat.history";

    private ISession Session =>
        accessor.HttpContext?.Session ?? throw new InvalidOperationException("No active HTTP session.");

    public IReadOnlyList<ChatMessage> GetMessages()
    {
        var json = Session.GetString(SessionKey);
        if (string.IsNullOrEmpty(json)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? [];
        }
        catch (JsonException)
        {
            // Corrupt or outdated session data: start a fresh conversation rather than failing.
            Session.Remove(SessionKey);
            return [];
        }
    }

    public void Add(ChatMessage message)
    {
        var messages = GetMessages().ToList();
        messages.Add(message);

        var max = Math.Max(2, options.Value.MaxStoredMessages);
        if (messages.Count > max)
            messages = messages.Skip(messages.Count - max).ToList();

        Session.SetString(SessionKey, JsonSerializer.Serialize(messages));
    }

    public void Clear() => Session.Remove(SessionKey);
}
