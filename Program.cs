using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SureStacks.O365Logs2LA;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services => {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.AddHttpClient();
        services.ConfigureFunctionsApplicationInsights();
        services.AddSingleton<IManagedIdentityTokenService, ManagedIdentityTokenService>();
        services.AddSingleton<IOffice365ManagementApiService, Office365ManagementApiService>();
        services.AddSingleton<ILogAnalyticsService, LogAnalyticsService>();
    }).ConfigureLogging(logging =>
    {
        logging.AddFilter("Azure.Core", LogLevel.Warning);
    })
    .Build();

host.Run();
