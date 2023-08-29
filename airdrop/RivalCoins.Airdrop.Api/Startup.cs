using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using RestSharp;
using RivalCoins.Airdrop.Api;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Common.Config;
using RivalCoins.Sdk;
using stellar_dotnet_sdk;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using RivalCoins.Airdrop.Common.Repository.Cosmos.Model;
using Constants = RivalCoins.Airdrop.Common.Constants;

[assembly: FunctionsStartup(typeof(Startup))]

namespace RivalCoins.Airdrop.Api;

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.Services.RegisterConfig<IPinwheelConfig, PinwheelConfig>();
        builder.Services.RegisterConfig<IAirdropConfig, AirdropConfig>();

        builder.Services.AddSingleton(s =>
        {
            return new RestClient($"{s.GetService<IPinwheelConfig>().EnvironmentUrl}/v1");
        });


        builder.Services.AddHttpClient();

        builder.Services.AddSingleton((s) =>
        {
            //var wallet = Wallet.Default[Network.Local] with { AccountSecretSeed = s.GetRequiredService<IAirdropConfig>().AirdropAccountSeed };
            var wallet = new Sdk.Wallet("https://localhost:8001", " ", "https://localhost:7777");

            //Wallet.CreateAccountAsync(KeyPair.FromSecretSeed(wallet.AccountSecretSeed).AccountId, wallet.Network).Wait();
            //wallet.InitializeAsync().Wait();
            
            return wallet;
        });

        builder.Services.AddSingleton((s) =>
        {
            var airdropWallet = s.GetRequiredService<Wallet>();

            return airdropWallet.Server;
        });

        builder.Services.AddCosmosRepository(
            options =>
            {
                options.CosmosConnectionString = "AccountEndpoint=https://cosmos-rivalcoins-dev-airdrop.documents.azure.com:443/;AccountKey=DZqafMpUKy7OOKVeEstMblQITExeZcEvXXB75su0SH3BRk6jQgT71YkIHyWuSVvZACPN1yWZJK3Qlz9bqLeucw==;";
                options.DatabaseId = "sql-rivalcoins-dev-airdrop";
                options.ContainerPerItemType = true;

                options.ContainerBuilder.Configure<AirdropParticipant>(containerOptions => containerOptions
                    .WithContainer(Constants.AirdropParticipantContainer));

                options.ContainerBuilder.Configure<AirdropRun>(containerOptions => containerOptions
                    .WithContainer(Constants.AirdropRunContainer));

                options.ContainerBuilder.Configure<RivalCoinUser>(containerOptions => containerOptions
                    .WithContainer(Constants.RivalCoinUserContainer));
            });

        builder.Services.AddSingleton<IPayStubReader, PinwheelPayStubReader>();

        builder.Services.AddScoped(s =>
        {
            var options = new ServiceBusClientOptions { EnableCrossEntityTransactions = true };

            return new ServiceBusClient(s.GetService<IConfiguration>().GetConnectionString(Constants.ServiceBusConnection), options);
        });

        //builder.Services.AddSingleton(s =>
        //{
        //    return new MemoryCache("Rival Coins Api");
        //});
    }
}