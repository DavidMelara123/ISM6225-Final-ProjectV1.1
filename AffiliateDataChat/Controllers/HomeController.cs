using System.Diagnostics;
using AffiliateDataChat.Services.Chat;
using AffiliateDataChat.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AffiliateDataChat.Controllers;

public sealed class HomeController(IChatbotService chatbot, ILogger<HomeController> logger) : Controller
{
    private static readonly PlannedTool[] PlannedTools =
    [
        new("lookup_affiliate", "Exact lookup of an affiliate by ID.", "Show me affiliate 104."),
        new("find_affiliates_without_sales", "Campaign participants with no recorded sales, optionally for one campaign.",
            "Which affiliates participated in the summer campaign but made no sales?"),
        new("summarize_campaign", "Campaign revenue, commission expense and sales counts.",
            "How much revenue and commission expense did campaign 3 generate?"),
        new("rank_affiliates", "Affiliates ranked by net revenue for a campaign or date range, with a result limit.",
            "Which affiliate generated the most net revenue?")
    ];

    [HttpGet]
    public IActionResult Index() => RedirectToAction("Index", "Chat");

    [HttpGet]
    public async Task<IActionResult> About(CancellationToken cancellationToken) => View(new AboutViewModel
    {
        Status = await ChatController.SafeStatusAsync(chatbot, logger, cancellationToken),
        PlannedTools = PlannedTools
    });

    // Error pages are re-executed with the ORIGINAL request's method (often POST), so they
    // must accept any method and must not require an anti-forgery token themselves.
    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View("Status", new StatusPageViewModel
    {
        StatusCode = 500,
        Title = "Something went wrong",
        Message = "The page hit an unexpected problem. Please go back and try again. If it keeps happening, let the team know the reference below.",
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
    });

    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [Route("Home/Status/{code:int}")]
    public IActionResult Status(int code)
    {
        var (title, message) = code switch
        {
            400 => ("Your form couldn't be sent",
                    "The page may have been open too long, or the request was not sent from this site. Reload the chat page and try again."),
            404 => ("Page not found", "That page doesn't exist. Use the menu to get back to the chat."),
            429 => ("Slow down a little", "You've sent a lot of questions in a short time. Wait a minute, then try again."),
            _ => ("Something went wrong", "Please go back and try again.")
        };

        Response.StatusCode = code;
        return View("Status", new StatusPageViewModel
        {
            StatusCode = code,
            Title = title,
            Message = message,
            RequestId = HttpContext.TraceIdentifier
        });
    }
}
