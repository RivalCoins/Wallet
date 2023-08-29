using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pulumi;
using RivalCoins.Infrastructure.Stack;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
