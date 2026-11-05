using System.Text.Json;
using AffiliateDataChat.Models;
using AffiliateDataChat.Services;
using Microsoft.AspNetCore.Mvc;
namespace AffiliateDataChat.Controllers;

public sealed class ChatController(ChatOrchestrator orchestrator, ILogger<ChatController> logger) : Controller
{
    private const string SessionKey = "chat-turns";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    [HttpGet]
    public IActionResult Index() => View(BuildViewModel());
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask(ChatViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Turns = ReadTurns();
            model.ProviderLabel = orchestrator.ProviderLabel;
            return View("Index", model);
        }
        var turns = ReadTurns().ToList();
        turns.Add(new ChatTurn("You", model.Question.Trim(), DateTimeOffset.UtcNow));
        try
        {
            var answer = await orchestrator.AskAsync(model.Question.Trim(), cancellationToken);
            turns.Add(new ChatTurn("Assistant", answer, DateTimeOffset.UtcNow));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The chat request failed.");
            turns.Add(new ChatTurn("Assistant",
            "I could not reach the MCP data tools. Check the MCP endpoint, application logs, and AI configuration, then try again.",
            DateTimeOffset.UtcNow));
        }
        SaveTurns(turns.TakeLast(12));
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Clear()
    {
        HttpContext.Session.Remove(SessionKey);
        return RedirectToAction(nameof(Index));
    }
    private ChatViewModel BuildViewModel() => new()
    {
        Turns = ReadTurns(),
        ProviderLabel = orchestrator.ProviderLabel
    };
    private IReadOnlyList<ChatTurn> ReadTurns()
    {
        var json = HttpContext.Session.GetString(SessionKey);
        return json is null
        ? []
        : JsonSerializer.Deserialize<List<ChatTurn>>(json, JsonOptions) ?? [];
    }
    private void SaveTurns(IEnumerable<ChatTurn> turns) =>
    HttpContext.Session.SetString(SessionKey, JsonSerializer.Serialize(turns, JsonOptions));
}