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
                .ConfigureAppConfiguration(builder =>
                {
                    builder.SetBasePath(GetCustomSettingsPath())
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    var config = context.Configuration;

                    services.AddApplicationInsightsTelemetryWorkerService();
                    services.ConfigureFunctionsApplicationInsights();
                    services.AddLogging();

                    var keyVaultName = config["keyvault_name"];
                    if (string.IsNullOrEmpty(keyVaultName))
                    {
                        throw new ArgumentNullException("keyvault_name", "KeyVault name is not provided in configuration.");
                    }

                    services.AddKeyVaultClient($"https://{keyVaultName}.vault.azure.net");

                    var keyVaultKeys = config.GetSection("KeyVaultOptions:ApplicationKeyVaultKeys").Get<List<string>>()
                                       ?? throw new ArgumentNullException("ApplicationKeyVaultKeys not found");

                    config = new ConfigurationBuilder()
                        .AddConfiguration(config)
                        .AddKeyVaultConfigurationProvider(keyVaultKeys, services.BuildServiceProvider())
                        .Build();

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