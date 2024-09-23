using DFC.Compui.Cosmos;
using DFC.Compui.Cosmos.Contracts;
using DFC.EventGridSubscriptions.Data;
using DFC.EventGridSubscriptions.Services;
using DFC.EventGridSubscriptions.Services.Extensions;
using DFC.EventGridSubscriptions.Services.Interface;
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

                    services.AddOptions<EventGridSubscriptionClientOptions>()
                        .Configure<IConfiguration>((settings, configuration) => { configuration.GetSection("EventGridSubscriptionClientOptions").Bind(settings); });

                    services.AddOptions<AdvancedFilterOptions>()
                        .Configure<IConfiguration>((settings, configuration) => { configuration.GetSection("AdvancedFilterOptions").Bind(settings); });

                    services.AddKeyVaultClient($"https://{config["keyvault_name"]}.vault.azure.net");
                    var keyVaultKeys = config.GetSection("KeyVaultOptions:ApplicationKeyVaultKeys").Get<List<string>>() ?? throw new ArgumentNullException();
                    config = configBuilder.AddKeyVaultConfigurationProvider(keyVaultKeys, services.BuildServiceProvider()).Build();

                    services.AddSingleton<IConfiguration>(config);
                    services.AddTransient<ISubscriptionService, SubscriptionService>();
                    services.AddEventGridManagementClient();

                    var cosmosDbConnectionEventGridSubscriptions = config.GetSection("Configuration:CosmosDbConnections:EventGridSubscriptions").Get<CosmosDbConnection>() ?? throw new ArgumentNullException();
                    services.AddDocumentServices<SubscriptionModel>(cosmosDbConnectionEventGridSubscriptions, Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.ToUpperInvariant() == "DEVELOPMENT");

                    services.Configure<LoggerFilterOptions>(options =>
                    {
                        // Remove the default logging filter for Application Insights
                        var toRemove = options.Rules.FirstOrDefault(rule => rule.ProviderName
                            == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");

                        if (toRemove != null)
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
            string? path = Path.Combine(home, "site", "wwwroot");

            if (Directory.Exists(path))
            {
                return path;
            }

            path = new Uri(Assembly.GetExecutingAssembly().Location!).LocalPath;

            if (string.IsNullOrEmpty(path))
            {
                return path ?? throw new ArgumentNullException(path);
            }

            path = Path.GetDirectoryName(path) ?? string.Empty;
            DirectoryInfo? parentDir = Directory.GetParent(path);
            path = parentDir?.FullName;

            return path ?? throw new ArgumentNullException(path);
        }
    }
}