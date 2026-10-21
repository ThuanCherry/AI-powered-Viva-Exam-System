using AIVES.Lecturer.Testing;

await using var sandbox = await LecturerSandbox.Create("http://localhost:5240", "artifacts/lecturer-sandbox/preview.sqlite");
Console.WriteLine("Lecturer sandbox: http://localhost:5240; SQLite test riêng; AI bị chặn hoàn toàn.");
Console.WriteLine("Test accounts: lecturer@lecturer-test.invalid, student1@lecturer-test.invalid, student2@lecturer-test.invalid. Password documented in docs/LECTURER_WORKFLOW.md; test data only.");
await sandbox.App.WaitForShutdownAsync();
