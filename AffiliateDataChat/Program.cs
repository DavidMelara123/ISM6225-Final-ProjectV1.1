using AffiliateDataChat.Data;
using AffiliateDataChat.Services;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});
builder.Services.AddSingleton<SqliteCourseRepository>();
builder.Services.AddSingleton<SqliteAffiliateRepository>();
builder.Services.AddScoped<ChatOrchestrator>();
builder.Services.AddMcpServer()
.WithHttpTransport(options =>
{
    options.SessionMode = HttpServerSessionMode.Stateless;
})
.WithToolsFromAssembly();
var app = builder.Build(); 
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();
app.MapMcp("/mcp");
app.MapControllerRoute(
name: "default",
pattern: "{controller=Home}/{action=Index}/{id?}");
await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration, app.Environment);
app.Run();
public partial class Program;