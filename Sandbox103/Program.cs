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
using Sandbox103.V2.Notifications;
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
        options.RepositoryPath = commandLineOptions.RepoPath;
        options.LogDropPath = commandLineOptions.LogDropPath;
        options.BuildDropPath = commandLineOptions.BuildDropPath;
    });
}

//builder.Services.AddHostedService<SdkStyleConversionHostedService>();
builder.Services.TryAddSingleton<ILogDropReader, LogDropReader>();
builder.Services.TryAddSingleton<IBinaryLogReader, BinaryLogReader>();
builder.Services.TryAddSingleton<IArchiveFileIndex, ArchiveFileIndex>();
builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventSourceSubscriber, ProjectImportEventSourceSubscriber>());
builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventSourceSubscriber, ProjectFileXmlEventSourceSubscriber>());
builder.Services.TryAddSingleton<ISourceRepositoryReader, SourceRepositoryReader>();
builder.Services.TryAddSingleton<IProjectFileEvaluator, ProjectFileEvaluator>();
builder.Services.TryAddSingleton<IProjectFileTransformer, ProjectFileTransformer>();
builder.Services.TryAddSingleton<ISourceRepositoryTransformer, SourceRepositoryTransformer>();

// If we want to do hundreds of these, use the event bus. For a console app designed to do just one, it's not necessary.
//builder.Services.TryAddSingleton<EventBus>();
//builder.Services.TryAddSingleton<IEventBus>(static sp => sp.GetRequiredService<EventBus>());
//builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, EventBus>(static sp => sp.GetRequiredService<EventBus>()));
//builder.Services.TryAddSingleton<IDispatcher, Dispatcher>();
//builder.Services.AddOptions<EventBusOptions>().BindConfiguration("EventBus");
//builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ISubscriber<SdkStyleConversionNotification>, LogDropPublisher>());
//builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ISubscriber<LogDropNotification>, LogDropSubscriber>());

IHost host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var options = host.Services.GetRequiredService<IOptions<SdkStyleConversionOptions>>().Value;

using var cancellation = new CancellationTokenSource(commandLineOptions?.Timeout ?? CommandLineOptions.DefaultTimeout);
CancellationToken cancellationToken = cancellation.Token;

await host.StartAsync(cancellationToken);

// Read all `.binlog` files in the log drop and index them.
var logDropReader = host.Services.GetRequiredService<ILogDropReader>();
ILogDrop logDrop = await logDropReader.ReadAsync(new LogDropReaderOptions { Path = options.LogDropPath }, cancellationToken);

// Associate each indexed binlog project file with a project file in the local source repository.
var repoReader = host.Services.GetRequiredService<ISourceRepositoryReader>();
ISourceRepository repoIndex = await repoReader.ReadAsync(options.RepositoryPath, logDrop, cancellationToken);

// Transform the repo, including conversion of each project file to SDK-style.
var repoTransformer = host.Services.GetRequiredService<ISourceRepositoryTransformer>();
await repoTransformer.TransformAsync(repoIndex, cancellationToken);

using (var stopCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
{
    await host.StopAsync(stopCancellation.Token).WaitAsync(stopCancellation.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
}

Console.WriteLine("\nDone.");
