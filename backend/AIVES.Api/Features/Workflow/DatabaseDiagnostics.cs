using MySqlConnector;

namespace AIVES.Api.Features.Workflow;

public static class DatabaseDiagnostics
{
    public static async Task Check(IConfiguration configuration)
    {
        var settings = new MySqlConnectionStringBuilder(configuration.GetConnectionString("DefaultConnection") ?? "")
            { ConnectionTimeout = 5 };
        Console.WriteLine($"MySQL server={settings.Server}; port={settings.Port}; database={settings.Database}; SSL={settings.SslMode}");
        try
        {
            await using var connection = new MySqlConnection(settings.ConnectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand("SELECT DATABASE()", connection);
            Console.WriteLine($"MySQL Connected: {await command.ExecuteScalarAsync()}");
        }
        catch (Exception exception)
        {
            for (Exception? error = exception; error != null; error = error.InnerException)
            {
                var message = string.IsNullOrEmpty(settings.Password) ? error.Message : error.Message.Replace(settings.Password, "***");
                Console.WriteLine($"{error.GetType().Name}: {message}");
            }
            Environment.ExitCode = 1;
        }
    }
}
