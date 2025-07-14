// One of the MSBuild libraries defines an ILogger that we never want to use,
// despite pulling in the MSBuild namespace frequently.
global using ILogger = Microsoft.Extensions.Logging.ILogger;

// Let's sort out namespaces later, when the code is ported to wherever it should live.
global using Sandbox103;
global using Sandbox103.Extensions;
global using Sandbox103.Helpers;
global using Sandbox103.Options;
global using Sandbox103.V1;
global using Sandbox103.V1.BuildDrops;
global using Sandbox103.V1.LogDrops;
global using Sandbox103.V1.Repos;
global using Sandbox103.V2;
global using Sandbox103.V2.Abstractions;
global using Sandbox103.V2.Events;
global using Sandbox103.V2.Notifications;
