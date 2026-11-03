namespace CampusDataChat.Data;

public static class DatabasePath
{
    public static string Resolve(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["Database:Path"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }
        var azureHome = Environment.GetEnvironmentVariable("HOME");
        var root = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"))
        && !string.IsNullOrWhiteSpace(azureHome)
        ? Path.Combine(azureHome, "data", "CampusDataChat")
        : Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, "campus.db");
    }
}
