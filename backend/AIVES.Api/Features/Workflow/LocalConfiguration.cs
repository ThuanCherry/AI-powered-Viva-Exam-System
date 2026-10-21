namespace AIVES.Api.Features.Workflow;

public static class LocalConfiguration
{
    public const string SecretsId = "AIVES-Workflow-Local";
    public static void AddLocalSecrets(ConfigurationManager configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment()) return;
        // An explicit AI key in appsettings takes precedence for local demo configuration.
        var appFile = new ConfigurationBuilder().SetBasePath(environment.ContentRootPath).AddJsonFile("appsettings.json", optional: true).Build();
        var aiSettings = !string.IsNullOrWhiteSpace(appFile["AI:ApiKey"])
            ? appFile.GetSection("AI").AsEnumerable().Concat(appFile.GetSection("Speech").AsEnumerable())
                .Where(p => p.Value != null).ToDictionary(p => p.Key, p => p.Value)
            : null;
        (appFile as IDisposable)?.Dispose();
        configuration.AddUserSecrets(SecretsId, reloadOnChange: true);
        // Git Bash / shell APPDATA overrides must not hide the Windows account's own secrets.
        if (OperatingSystem.IsWindows() && string.IsNullOrWhiteSpace(configuration["AI:ApiKey"]))
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(profile))
            {
                var path = Path.Combine(profile, "AppData", "Roaming", "Microsoft", "UserSecrets", SecretsId, "secrets.json");
                if (File.Exists(path)) configuration.AddJsonFile(path, optional: true, reloadOnChange: true);
            }
        }
        if (aiSettings != null) configuration.AddInMemoryCollection(aiSettings);
    }
}
