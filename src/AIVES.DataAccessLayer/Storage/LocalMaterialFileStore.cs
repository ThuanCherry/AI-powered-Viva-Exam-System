namespace AIVES.DataAccessLayer.Storage;

public interface IMaterialFileStore
{
    Task<string> SaveMaterialAsync(string fileName, Stream contentStream, string subDirectory = "materials");
    Task<Stream?> GetMaterialStreamAsync(string relativePath);
    Task<bool> DeleteMaterialAsync(string relativePath);
    string GetFullPath(string relativePath);
}

/// <summary>
/// Quản lý lưu trữ file tài liệu & audio trong Storage/materials
/// như trong sơ đồ kiến trúc Data Access Layer.
/// </summary>
public class LocalMaterialFileStore : IMaterialFileStore
{
    private readonly string _baseStoragePath;

    public LocalMaterialFileStore(string? baseStoragePath = null)
    {
        _baseStoragePath = string.IsNullOrWhiteSpace(baseStoragePath)
            ? Path.Combine(AppContext.BaseDirectory, "Storage", "materials")
            : baseStoragePath;

        if (!Directory.Exists(_baseStoragePath))
        {
            Directory.CreateDirectory(_baseStoragePath);
        }
    }

    public async Task<string> SaveMaterialAsync(string fileName, Stream contentStream, string subDirectory = "materials")
    {
        var targetDir = Path.Combine(_baseStoragePath, subDirectory);
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var uniqueFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
        var fullPath = Path.Combine(targetDir, uniqueFileName);

        using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await contentStream.CopyToAsync(fileStream);
        }

        return Path.Combine("Storage", subDirectory, uniqueFileName).Replace('\\', '/');
    }

    public Task<Stream?> GetMaterialStreamAsync(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> DeleteMaterialAsync(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public string GetFullPath(string relativePath)
    {
        var cleaned = relativePath.TrimStart('/', '\\');
        if (cleaned.StartsWith("Storage/", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned.Substring("Storage/".Length);
        }
        return Path.Combine(_baseStoragePath, cleaned);
    }
}
