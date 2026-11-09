using System.Threading.RateLimiting;
using AffiliateDataChat.Controllers;
using AffiliateDataChat.Data;
using AffiliateDataChat.Models;
using AffiliateDataChat.Services;
using AffiliateDataChat.Services.Chat;
using AffiliateDataChat.Services.Conversation;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// ---- MVC + anti-forgery (Joel) ------------------------------------------------------
builder.Services.AddControllersWithViews(options =>
{
    // Every MVC POST needs a valid anti-forgery token, even if an action forgets the attribute.
    // (This filter applies to MVC controllers only, not to the /mcp endpoint.)
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "adc.antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.Configure<FormOptions>(options => options.ValueLengthLimit = 4 * 1024);

// ---- Conversation history in session (Joel) -----------------------------------------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "adc.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.Configure<ChatUiOptions>(builder.Configuration.GetSection(ChatUiOptions.SectionName));
builder.Services.AddScoped<IConversationStore, SessionConversationStore>();

// ---- Data, chatbot and MCP server (Morgan / Ignatious) ------------------------------
builder.Services.AddSingleton<SqliteCourseRepository>();
builder.Services.AddSingleton<SqliteAffiliateRepository>();
builder.Services.AddScoped<McpToolClient>();
builder.Services.AddScoped<ChatOrchestrator>();
// The MVC layer talks to IChatbotService; this adapter connects it to ChatOrchestrator.
builder.Services.AddScoped<IChatbotService, OrchestratorChatbotService>();
builder.Services.AddMcpServer()
.WithHttpTransport(options =>
{
    options.SessionMode = HttpServerSessionMode.Stateless;
})
.WithToolsFromAssembly();

// ---- Rate limit on asking questions (Joel) ------------------------------------------
var requestsPerMinute = builder.Configuration.GetValue($"{ChatUiOptions.SectionName}:RequestsPerMinute", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ChatController.RateLimitPolicy, http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = requestsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Friendly 400/404/429 pages for the website — but not for /mcp, whose clients expect raw MCP responses.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/mcp"),
    branch => branch.UseStatusCodePagesWithReExecute("/Home/Status/{0}"));

// Basic security headers. The CSP only allows this site's own scripts and styles,
// so views must not contain inline <script> blocks or style="" attributes.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; " +
        "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseAuthorization();
app.MapMcp("/mcp");
app.MapControllerRoute(
name: "default",
pattern: "{controller=Chat}/{action=Index}/{id?}");
await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration, app.Environment);
app.Run();
public partial class Program;
