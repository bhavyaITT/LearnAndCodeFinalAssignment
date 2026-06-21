using Microsoft.Extensions.Configuration;
using PRM.Client.Composition;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

await new AppHost(configuration).RunAsync(CancellationToken.None);
