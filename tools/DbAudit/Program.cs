using System.Text.Json;
using MySqlConnector;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath("backend/AIVES.Api"))
    .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.local.json", true)
    .AddUserSecrets("AIVES-Workflow-Local", true).AddEnvironmentVariables().Build();
var connection = config.GetConnectionString("DefaultConnection");
try
{
    var builder = new MySqlConnectionStringBuilder(connection ?? "") { ConnectionTimeout = 5 };
    if (args.Contains("--list-databases")) builder.Database = "";
    if (args.Length == 2 && args[0] == "--database") builder.Database = args[1];
    await using var db = new MySqlConnection(builder.ConnectionString);
    await db.OpenAsync();
    foreach (var query in args.Contains("--list-databases") ? new[] { "SHOW DATABASES" } : new[] {
        "SELECT VERSION() version, DATABASE() db, UTC_TIMESTAMP() utc_now",
        "SELECT TABLE_NAME,COLUMN_NAME,COLUMN_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('courses','academic_classes','class_students','documents','questions','exam_sessions','exam_candidates','exam_viva_settings','exam_questions','answers','follow_up_questions') ORDER BY TABLE_NAME,ORDINAL_POSITION",
        "SELECT r.role_name, COUNT(*) count FROM users u JOIN roles r ON r.role_id=u.role_id GROUP BY r.role_name",
        "SELECT status,question_type,COUNT(*) count FROM questions GROUP BY status,question_type" })
    {
        await using var command = new MySqlCommand(query, db);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            Console.WriteLine(string.Join(" | ", Enumerable.Range(0, reader.FieldCount).Select(i => $"{reader.GetName(i)}={reader.GetValue(i)}")));
    }
}
catch (MySqlException ex)
{
    Console.WriteLine($"MySQL check failed. Code={ex.Number}; type={ex.GetType().Name}; {ex.Message}. No import was executed.");
    Environment.ExitCode = 1;
}
