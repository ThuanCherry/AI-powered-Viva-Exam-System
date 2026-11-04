using MySqlConnector;
using AIVES.Api.Integrations;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Text.Json;
using System.Text;

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
            var dbName = await command.ExecuteScalarAsync();
            Console.WriteLine($"MySQL Connected: {dbName}");

            await using var countTablesCmd = new MySqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE()", connection);
            var tableCount = await countTablesCmd.ExecuteScalarAsync();
            Console.WriteLine($"Total Tables: {tableCount}");

            await using var countUsersCmd = new MySqlCommand("SELECT COUNT(*) FROM users", connection);
            var userCount = await countUsersCmd.ExecuteScalarAsync();
            Console.WriteLine($"Total Users: {userCount}");

            await using var countRolesCmd = new MySqlCommand("SELECT COUNT(*) FROM roles", connection);
            var roleCount = await countRolesCmd.ExecuteScalarAsync();
            Console.WriteLine($"Total Roles: {roleCount}");
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

    public static async Task SeedAdmin(IConfiguration configuration)
    {
        await SeedUsers(configuration);
    }

    public static async Task SeedUsers(IConfiguration configuration)
    {
        var settings = new MySqlConnectionStringBuilder(configuration.GetConnectionString("DefaultConnection") ?? "")
            { ConnectionTimeout = 10 };
        Console.WriteLine($"MySQL server={settings.Server}; database={settings.Database}");
        try
        {
            await using var connection = new MySqlConnection(settings.ConnectionString);
            await connection.OpenAsync();

            await using (var roleCmd = new MySqlCommand(@"
                INSERT IGNORE INTO roles (role_name, description) VALUES 
                ('Admin', 'Quản trị viên hệ thống'),
                ('Lecturer', 'Giảng viên'),
                ('Student', 'Sinh viên');", connection))
            {
                await roleCmd.ExecuteNonQueryAsync();
            }

            var roles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            await using (var getRolesCmd = new MySqlCommand("SELECT role_id, role_name FROM roles;", connection))
            await using (var reader = await getRolesCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    roles[reader.GetString("role_name")] = reader.GetInt32("role_id");
                }
            }

            var seedList = new[]
            {
                new { FullName = "Quản trị viên", Email = "admin@aives.vn", Password = "Admin@123456", Role = "Admin" },
                new { FullName = "TS. Nguyễn Văn An", Email = "lecturer@aives.vn", Password = "Lecturer@123456", Role = "Lecturer" },
                new { FullName = "ThS. Trần Thị Bình", Email = "lecturer2@aives.vn", Password = "Lecturer@123456", Role = "Lecturer" },
                new { FullName = "Lê Văn Cường", Email = "student1@aives.vn", Password = "Student@123456", Role = "Student" },
                new { FullName = "Phạm Thị Dung", Email = "student2@aives.vn", Password = "Student@123456", Role = "Student" },
                new { FullName = "Hoàng Văn Em", Email = "student3@aives.vn", Password = "Student@123456", Role = "Student" },
                new { FullName = "Đỗ Thị Giang", Email = "student4@aives.vn", Password = "Student@123456", Role = "Student" },
                new { FullName = "Vũ Văn Hùng", Email = "student5@aives.vn", Password = "Student@123456", Role = "Student" }
            };

            foreach (var item in seedList)
            {
                if (!roles.TryGetValue(item.Role, out var roleId))
                {
                    Console.WriteLine($"[SKIPPED] Role '{item.Role}' not found for '{item.Email}'.");
                    continue;
                }

                var hash = BCrypt.Net.BCrypt.HashPassword(item.Password);

                await using var checkCmd = new MySqlCommand("SELECT user_id FROM users WHERE email = @email;", connection);
                checkCmd.Parameters.AddWithValue("@email", item.Email);
                var existingId = await checkCmd.ExecuteScalarAsync();

                if (existingId != null)
                {
                    await using var updateCmd = new MySqlCommand(
                        "UPDATE users SET full_name = @name, role_id = @roleId, status = 'Active', password_hash = @hash WHERE user_id = @id;", connection);
                    updateCmd.Parameters.AddWithValue("@name", item.FullName);
                    updateCmd.Parameters.AddWithValue("@roleId", roleId);
                    updateCmd.Parameters.AddWithValue("@hash", hash);
                    updateCmd.Parameters.AddWithValue("@id", existingId);
                    await updateCmd.ExecuteNonQueryAsync();
                    Console.WriteLine($"[UPDATED] {item.Role.PadRight(8)}: {item.Email} ({item.FullName})");
                }
                else
                {
                    await using var insertCmd = new MySqlCommand(@"
                        INSERT INTO users (full_name, email, password_hash, role_id, status, created_at)
                        VALUES (@name, @email, @hash, @roleId, 'Active', NOW());", connection);
                    insertCmd.Parameters.AddWithValue("@name", item.FullName);
                    insertCmd.Parameters.AddWithValue("@email", item.Email);
                    insertCmd.Parameters.AddWithValue("@hash", hash);
                    insertCmd.Parameters.AddWithValue("@roleId", roleId);
                    await insertCmd.ExecuteNonQueryAsync();
                    Console.WriteLine($"[CREATED] {item.Role.PadRight(8)}: {item.Email} ({item.FullName})");
                }
            }

            Console.WriteLine("\n=== Current Users in Database ===");
            await using (var listCmd = new MySqlCommand(@"
                SELECT u.user_id, u.full_name, u.email, r.role_name, u.status 
                FROM users u 
                LEFT JOIN roles r ON u.role_id = r.role_id 
                ORDER BY r.role_name, u.user_id;", connection))
            await using (var reader = await listCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32("user_id");
                    var name = reader.GetString("full_name");
                    var email = reader.GetString("email");
                    var role = reader.IsDBNull(reader.GetOrdinal("role_name")) ? "Unknown" : reader.GetString("role_name");
                    var status = reader.GetString("status");
                    Console.WriteLine($"ID: {id,2} | Role: {role,-9} | Status: {status,-7} | Email: {email,-20} | Name: {name}");
                }
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Error seeding users: {exception.Message}");
            Environment.ExitCode = 1;
        }
    }

    public static async Task ProbeAi(IConfiguration configuration)
    {
        Console.WriteLine("--- Testing Gemini LLM & TTS live with current configuration ---");
        var status = new ProviderStatus(configuration);
        var services = new ServiceCollection();
        services.AddHttpClient("providers");
        services.AddSingleton<ProviderHttp>();
        var sp = services.BuildServiceProvider();
        var http = sp.GetRequiredService<ProviderHttp>();

        // 1. Test LLM Adaptive Follow-up
        Console.WriteLine("\n[1/2] Calling Gemini LLM Decide (Adaptive Follow-up evaluation)...");
        var llmSw = Stopwatch.StartNew();
        try
        {
            var llm = new GeminiViva(configuration, status, http);
            var mainQ = "Nêu nguyên lý Dependency Injection và lợi ích của nó trong phát triển phần mềm?";
            var expected = "DI là kỹ thuật tách biệt việc khởi tạo đối tượng khỏi nơi sử dụng. Lợi ích: giảm phụ thuộc chặt (loose coupling), dễ viết Unit Test và bảo trì code.";
            var studentAns = "Dạ, Dependency Injection là việc đưa đối tượng vào từ bên ngoài ạ.";
            var decision = await llm.Decide(mainQ, expected, mainQ, studentAns, "", CancellationToken.None);
            llmSw.Stop();
            Console.WriteLine($"[LLM SUCCESS in {llmSw.ElapsedMilliseconds}ms]");
            Console.WriteLine($" - Model: {configuration["AI:Model"]}");
            Console.WriteLine($" - NeedsFollowUp: {decision.NeedsFollowUp}");
            Console.WriteLine($" - Follow-up Question: {decision.Question}");
            Console.WriteLine($" - Reason: {decision.Reason}");
        }
        catch (Exception ex)
        {
            llmSw.Stop();
            Console.WriteLine($"[LLM ERROR in {llmSw.ElapsedMilliseconds}ms]: {ex.Message}");
        }

        // 2. Test TTS (Text-to-Speech)
        Console.WriteLine("\n[2/2] Calling Gemini TTS (Text-to-Speech)...");
        var ttsSw = Stopwatch.StartNew();
        try
        {
            var speech = new GeminiVivaSpeech(configuration, status, http, new GeminiLiveTranscriber(configuration, status, new GeminiSocketFactory()));
            var wav = await speech.Speak("Chào bạn, mời bạn trả lời câu hỏi vấn đáp.", CancellationToken.None);
            ttsSw.Stop();
            Console.WriteLine($"[TTS SUCCESS in {ttsSw.ElapsedMilliseconds}ms]");
            Console.WriteLine($" - Model: {status.TtsModel}");
            Console.WriteLine($" - Voice: {status.Voice}");
            Console.WriteLine($" - Generated Audio: {wav.Length} bytes ({BitConverter.ToInt32(wav, 24)} Hz PCM WAV)");
        }
        catch (Exception ex)
        {
            ttsSw.Stop();
            Console.WriteLine($"[TTS ERROR in {ttsSw.ElapsedMilliseconds}ms]: {ex.Message}");
        }
    }
}
