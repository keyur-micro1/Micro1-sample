namespace TdsCertificatePortal.Services;

public sealed class TempCleanupHostedService : IHostedService
{
    private readonly TempFileService _tempFileService;

    public TempCleanupHostedService(TempFileService tempFileService)
    {
        _tempFileService = tempFileService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _tempFileService.CleanStaleFolders(TimeSpan.FromHours(8));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
