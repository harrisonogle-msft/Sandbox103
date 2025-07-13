using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Sandbox103.V2;

public sealed class SdkStyleConversionHostedService : IHostedService
{
    private readonly ILogger<SdkStyleConversionHostedService> _logger;
    private readonly SdkStyleConversionOptions _options;
    private readonly ILogDropReader _logDropReader;

    public SdkStyleConversionHostedService(
        ILogger<SdkStyleConversionHostedService> logger,
        IOptions<SdkStyleConversionOptions> options,
        ILogDropReader logDropReader)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logDropReader);

        _logger = logger;
        _options = options.Value;
        _logDropReader = logDropReader;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StartAsync begin.");
        long t0 = Stopwatch.GetTimestamp();
        try
        {
            await _logDropReader.ReadAsync(new LogDropReaderOptions { Path = _options.LogDropPath }, cancellationToken);
        }
        finally
        {
            _logger.LogInformation($"StartAsync finished. ({Stopwatch.GetElapsedTime(t0)})");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
