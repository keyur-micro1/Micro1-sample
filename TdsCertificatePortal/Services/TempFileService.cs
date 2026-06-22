namespace TdsCertificatePortal.Services;

public sealed class TempFileService
{
    private readonly ILogger<TempFileService> _logger;

    public TempFileService(IConfiguration configuration, ILogger<TempFileService> logger)
    {
        _logger = logger;
        var configuredRoot = configuration["TempStorage:RootPath"];
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            configuredRoot = Path.Combine(Path.GetTempPath(), "TdsCertificatePortal");
        }

        RootPath = configuredRoot;
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public string CreateSessionFolder(string sessionId)
    {
        var safeSessionId = string.Concat(sessionId.Where(char.IsLetterOrDigit));
        if (string.IsNullOrWhiteSpace(safeSessionId))
        {
            safeSessionId = Guid.NewGuid().ToString("N");
        }

        var folder = Path.Combine(RootPath, safeSessionId);
        Directory.CreateDirectory(folder);
        return folder;
    }

    public async Task<string> SaveUploadAsync(IFormFile file, string folder, string expectedExtension, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(folder);
        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Only {expectedExtension} files are supported for this upload.");
        }

        var targetPath = Path.Combine(folder, $"{Guid.NewGuid():N}{expectedExtension.ToLowerInvariant()}");
        await using var stream = File.Create(targetPath);
        await file.CopyToAsync(stream, cancellationToken);
        return targetPath;
    }

    public void DeleteSessionFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        var fullRoot = Path.GetFullPath(RootPath);
        var fullFolder = Path.GetFullPath(folder);
        if (!fullFolder.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Skipped temp cleanup for path outside root: {TempFolder}", folder);
            return;
        }

        if (Directory.Exists(fullFolder))
        {
            Directory.Delete(fullFolder, recursive: true);
        }
    }

    public void CleanStaleFolders(TimeSpan maxAge)
    {
        Directory.CreateDirectory(RootPath);
        foreach (var directory in Directory.EnumerateDirectories(RootPath))
        {
            try
            {
                var info = new DirectoryInfo(directory);
                if (DateTime.UtcNow - info.LastWriteTimeUtc > maxAge)
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to remove stale temp directory {TempDirectory}", directory);
            }
        }
    }

    public (bool Exists, int FileCount, long Bytes) InspectFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return (false, 0, 0);
        }

        var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToList();
        return (true, files.Count, files.Sum(f => new FileInfo(f).Length));
    }
}
