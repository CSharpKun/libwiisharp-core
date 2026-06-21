using Dameng.Logging.TUnit;
using Messerli.TempDirectory;
using Microsoft.Extensions.Logging;
using TUnit.Core.Interfaces;

namespace LibWiiSharpCore.Tests;

public sealed class NusClientFixture : IAsyncInitializer, IAsyncDisposable
{
    private HttpClient _client = null!;
    private ILogger<LibWiiSharpCore.NusClient> _logger = null!;

    public TempSubdirectory TestingTempDirectory { get; private set; } = null!;
    public TempSubdirectory OriginalTempDirectory { get; private set; } = null!;
    public LibWiiSharpCore.NusClient TestingNusClient { get; private set; } = null!;
    public libWiiSharp.NusClient OriginalNusClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _client = new() { Timeout = TimeSpan.FromMinutes(1) };
        var loggerFactory = TestContext.Current!.GetLoggerFactory();
        _logger = loggerFactory.CreateLogger<LibWiiSharpCore.NusClient>();
        TestingNusClient = new(_client, _logger);
        OriginalNusClient = new();
        TestingTempDirectory = TempSubdirectory.Create();
        OriginalTempDirectory = TempSubdirectory.Create();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        TestingTempDirectory.Dispose();
        OriginalTempDirectory.Dispose();
    }
}
