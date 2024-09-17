using DFC.Compui.Cosmos;
using DFC.Compui.Cosmos.Contracts;
using DFC.EventGridSubscriptions.Data;
using DFC.EventGridSubscriptions.Services.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DFC.EventGridSubscriptions.ApiFunction
{
    [SuppressMessage("Maintainability", "S3928:The parameter name is not declared in the argument list", Justification = "Reviewed")]
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWebApplication()
                .ConfigureServices(services =>
                {
                    services.AddApplicationInsightsTelemetryWorkerService();
                    services.ConfigureFunctionsApplicationInsights();

                    var configBuilder = new ConfigurationBuilder()
                        .SetBasePath(GetCustomSettingsPath())
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables();

                    var config = configBuilder.Build();

                    services.AddKeyVaultClient($"https://{config["keyvault_name"]}.vault.azure.net");

                    var keyVaultKeys = config.GetSection("KeyVaultOptions:ApplicationKeyVaultKeys").Get<List<string>>()
                                       ?? throw new ArgumentNullException("ApplicationKeyVaultKeys not found");

                    config = configBuilder.AddKeyVaultConfigurationProvider(keyVaultKeys, services.BuildServiceProvider()).Build();

                    services.AddSingleton<IConfiguration>(config);

                    services.Configure<EventGridSubscriptionClientOptions>(config.GetSection("EventGridSubscriptionClientOptions"));
                    services.Configure<AdvancedFilterOptions>(config.GetSection("AdvancedFilterOptions"));

                    services.AddEventGridManagementClient();

                    var cosmosDbConnectionEventGridSubscriptions = config
                        .GetSection("Configuration:CosmosDbConnections:EventGridSubscriptions")
                        .Get<CosmosDbConnection>() ?? throw new ArgumentNullException("CosmosDbConnection not found");

                    services.AddDocumentServices<SubscriptionModel>(
                        cosmosDbConnectionEventGridSubscriptions,
                        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.ToUpperInvariant() == "DEVELOPMENT");

                    services.Configure<LoggerFilterOptions>(options =>
                    {
                        // The Application Insights SDK adds a default logging filter that instructs ILogger to capture only Warning and more severe logs. Application Insights requires an explicit override.
                        // Log levels can also be configured using appsettings.json. For more information, see https://learn.microsoft.com/en-us/azure/azure-monitor/app/worker-service#ilogger-logs
                        LoggerFilterRule? toRemove = options.Rules.FirstOrDefault(rule => rule.ProviderName
                            == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");

                        if (toRemove is not null)
                        {
                            options.Rules.Remove(toRemove);
                        }
                    });
                })
                .Build();

            await host.RunAsync();
        }

        private static string GetCustomSettingsPath()
        {
            var home = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
            var path = Path.Combine(home, "site", "wwwroot");

            if (Directory.Exists(path))
            {
                return path;
            }

            path = new Uri(Assembly.GetExecutingAssembly().Location).LocalPath;

            if (string.IsNullOrEmpty(path))
            {
                return path ?? throw new ArgumentNullException("Path for settings could not be determined");
            }

            path = Path.GetDirectoryName(path) ?? string.Empty;
            var parentDir = Directory.GetParent(path);
            path = parentDir?.FullName;

            return path ?? throw new ArgumentNullException("Path for settings could not be determined");
        }
    }
}