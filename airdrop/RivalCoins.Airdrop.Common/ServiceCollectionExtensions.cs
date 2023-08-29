using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RivalCoins.Airdrop.Common;

public static class ServiceCollectionExtensions
{
    public static void RegisterConfig<TInterface, TClass>(this IServiceCollection serviceCollection)
        where TInterface : class
        where TClass : class, TInterface, new()
    {
        serviceCollection.AddOptions<TClass>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration.GetSection(typeof(TClass).Name).Bind(settings);
            });

        serviceCollection.AddSingleton<TInterface, TClass>(s =>
        {
            var config = s.GetRequiredService<IOptions<TClass>>().Value;

            return config;
        });
    }
}