using DragonSpark.AutoCert.Stores;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DragonSpark.AutoCert.UnitTests;

#pragma warning disable xUnit1051 // Do not use TestContext.Current.CancellationToken

public class FileSystemLockProviderTests : IDisposable
{
    private readonly Mock<ILogger<FileSystemLockProvider>> _loggerMock;
    private readonly IOptions<AutoCertOptions> _options;
    private readonly FileSystemLockProvider _provider;
    private readonly string _testDirectory;

    public FileSystemLockProviderTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        _options = Options.Create(new AutoCertOptions { CertificatePath = _testDirectory });
        _loggerMock = new Mock<ILogger<FileSystemLockProvider>>();
        _provider = new FileSystemLockProvider(_options, _loggerMock.Object);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task AcquireLockAsync_CreatesLockFile()
    {
        // Arrange
        const string key = "test-lock";

        // Act
        var lockObj = await _provider.AcquireLockAsync(key, CancellationToken.None);

        // Assert
        var expectedPath = Path.Combine(Directory.GetCurrentDirectory(), _testDirectory, ".locks", $"{key}.lock");
        Assert.True(File.Exists(expectedPath));

        await lockObj.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_ReleasesLockNotFile()
    {
        // Arrange
        const string key = "test-lock-2";
        var lockObj = await _provider.AcquireLockAsync(key, CancellationToken.None);
        var expectedPath = Path.Combine(Directory.GetCurrentDirectory(), _testDirectory, ".locks", $"{key}.lock");

        Assert.True(File.Exists(expectedPath));

        // Act
        await lockObj.DisposeAsync();

        // Assert - The file should be deleted on close due to FileOptions.DeleteOnClose
        Assert.False(File.Exists(expectedPath));
    }

    [Fact]
    public async Task AcquireLockAsync_RetriesWhenFileExists()
    {
        // Arrange
        const string key = "retry-lock";
        var lockObj1 = await _provider.AcquireLockAsync(key, CancellationToken.None);

        // Act
        // Run AcquireLockAsync in a separate task
        var task = Task.Run(async () =>
        {
            var lockObj2 = await _provider.AcquireLockAsync(key, CancellationToken.None);
            return lockObj2;
        });

        // Wait a bit to ensure it hits the retry loop
        await Task.Delay(500);

        // Release first lock
        await lockObj1.DisposeAsync();

        // Await the second lock acquisition
        var lockObj2 = await task;

        // Assert
        Assert.NotNull(lockObj2);
        await lockObj2.DisposeAsync();
    }

    [Fact]
    public async Task AcquireLockAsync_RespectsCancellation()
    {
        // Arrange
        const string key = "cancel-lock";
        var lockObj1 = await _provider.AcquireLockAsync(key, CancellationToken.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act & Assert
        // This should throw TaskCanceledException/OperationCanceledException after retrying for 500ms
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _provider.AcquireLockAsync(key, cts.Token);
        });

        await lockObj1.DisposeAsync();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        if (!Directory.Exists(_testDirectory)) return;

        try
        {
            Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}