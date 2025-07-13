using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sandbox103.Extensions;
using Sandbox103.Helpers;
using Sandbox103.Options;
using Sandbox103.V1.BuildDrops;
using Sandbox103.V1.LogDrops;
using Sandbox103.V1.Repos;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Xml;

// Parse command line options.
CommandLineOptions? commandLineOptions = Parser.Default.ParseArguments<CommandLineOptions>(args).WithNotParsed(HandleCommandLineError).Value;
static void HandleCommandLineError(IEnumerable<Error> errors)
{
    bool debugMode = bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_DEBUG_MODE"), out bool parsed) && parsed;
    if (!debugMode)
    {
        Console.Error.WriteLine("Invalid argument(s).");
        Environment.Exit(1);
    }
}

// Set up .NET generic host for logging, configuration, DI, and handling of CTRL+C.

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

OptionsBuilder<SdkStyleConversionOptions> optionsBuilder = builder.Services.AddOptions<SdkStyleConversionOptions>().BindConfiguration(nameof(SdkStyleConversionOptions));
if (commandLineOptions is not null)
{
    // Override the values with the ones from command line, if present.
    optionsBuilder.Configure(options =>
    {
        options.RepoPath = commandLineOptions.RepoPath;
        options.LogDropPath = commandLineOptions.LogDropPath;
        options.BuildDropPath = commandLineOptions.BuildDropPath;
    });
}

builder.Services.AddHostedService<SdkStyleConversionHostedService>();
builder.Services.TryAddSingleton<ILogDropReader, LogDropReader>();
builder.Services.TryAddSingleton<IBinaryLogReader, BinaryLogReader>();
builder.Services.TryAddSingleton<IArchiveFileIndex, ArchiveFileIndex>();
builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventSourceSubscriber, ProjectImportEventSourceSubscriber>());
builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventSourceSubscriber, ProjectFileXmlEventSourceSubscriber>());

IHost host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var options = host.Services.GetRequiredService<IOptions<SdkStyleConversionOptions>>().Value;

using (var startupCancellation = new CancellationTokenSource(commandLineOptions?.Timeout ?? CommandLineOptions.DefaultTimeout))
{
    long t0 = Stopwatch.GetTimestamp();
    try
    {
        logger.LogInformation("Starting the host.");
        await host.StartAsync(startupCancellation.Token);
        logger.LogInformation("Host start sequence completed. ({0})", Stopwatch.GetElapsedTime(t0));
    }
    catch (OperationCanceledException)
    {
        logger.LogError("Host start sequence timed out. ({0})", Stopwatch.GetElapsedTime(t0));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Host start sequence failed. ({0})", Stopwatch.GetElapsedTime(t0));
    }
}

using (var stopCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
{
    await host.StopAsync(stopCancellation.Token);
}

Console.WriteLine("\nDone.");
