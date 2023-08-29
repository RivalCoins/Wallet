using Blazored.LocalStorage;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using RestSharp;
using RivalCoins.Sdk;
using RivalCoins.Sdk.Grpc;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace RivalCoins.Wallet.Web.Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            Console.WriteLine("Hello World!");
            Console.WriteLine(builder.Configuration.GetDebugView());

            builder.Services.AddSingleton(services =>
            {
                var httpClient = new HttpClient(new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler()));
                var channel = GrpcChannel.ForAddress(services.GetService<IConfiguration>().GetValue<string>("API_URL"), new GrpcChannelOptions { HttpClient = httpClient });
                return new RivalCoinsService.RivalCoinsServiceClient(channel);
            });

            builder.Services.AddSingleton(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            builder.Services.AddBlazoredLocalStorage();

            builder.Services.AddScoped<IRivalCoinsApp, RivalCoinsApp>();

            builder.Services.AddMudServices();

            builder.Services.AddSingleton(sp =>
            {
                return new RestClient(sp.GetService<HttpClient>(), false, o => o.BaseUrl = new Uri("http://localhost:7071/api/"));
            });

            await builder.Build().RunAsync();
        }
    }
}
