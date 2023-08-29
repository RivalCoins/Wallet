using Microsoft.Extensions.Hosting;
using Pulumi;
using System.Threading.Tasks;
using RivalCoins.Infrastructure.Stack;
using RivalCoins.Infrastructure.Stack.Cluster;

Task<int> deployment = null!;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((hostingContext, configuration) =>
    {
        IHostEnvironment env = hostingContext.HostingEnvironment;

        if (env.IsDevelopment())
        {
            deployment = Deployment.RunAsync<MinikubeStack>();
        }
        else
        {
            deployment = Deployment.RunAsync<DigitalOceanStack>();
        }
    })
    .Build();

await deployment;
