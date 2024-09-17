using System;
using DFC.EventGridSubscriptions.Services.Interface;
using DFC.EventGridSubscriptions.Services.Sources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace DFC.EventGridSubscriptions.Services.Extensions
{
    public static class ConfigurationExtensions
    {
        public static IConfigurationBuilder AddKeyVaultConfigurationProvider(
            this IConfigurationBuilder configuration, List<string> keyVaultKeys, IServiceProvider serviceProvider)
        {
            var keyVaultService = serviceProvider.GetRequiredService<IKeyVaultService>();
            configuration.Add(new KeyVaultSource(keyVaultKeys, keyVaultService));
            return configuration;
        }
    }
}
