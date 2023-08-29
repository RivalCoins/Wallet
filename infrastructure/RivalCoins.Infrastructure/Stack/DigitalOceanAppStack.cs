using System;
using Pulumi;
using Pulumi.Kubernetes.Storage.V1;
using Provider = Pulumi.Kubernetes.Provider;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace RivalCoins.Infrastructure.Stack;

public abstract class DigitalOceanAppStack : AppStack
{
    protected DigitalOceanAppStack(StorageClass persistentStorageClass, Provider provider)
    : base(persistentStorageClass, provider)
    {
    }

    public List<(Service Service, Output<string> Url)> GetIngressServices(Namespace ingress, Provider provider) => new()
    {
        (ExternalService(this.Horizon, ingress, provider), this.HorizonUrl),
        (ExternalService(this.ApiService, ingress, provider), this.AssetServerUrl),
        (ExternalService(this.Wallet, ingress, provider), this.WalletUrl),
    };

    protected static Service ExternalService(Service service, Namespace ns, Provider provider)
    {
        return new Service(
            $"{service.GetResourceName()}-external",
            new ServiceArgs()
            {
                Metadata = new ObjectMetaArgs()
                {
                    Name = Output.Format($"{service.Metadata.Apply(m => m.Name)}-{service.Metadata.Apply(m => m.Namespace)}-{ns.Metadata.Apply(m => m.Name)}"),
                    Namespace = ns.Metadata.Apply(m => m.Name),
                },
                Spec = new ServiceSpecArgs
                {
                    Type = "ExternalName",
                    ExternalName = Output.Format($"{service.Metadata.Apply(m => m.Name)}.{service.Metadata.Apply(m => m.Namespace)}.svc.cluster.local"),
                    Ports = new ServicePortArgs() { Port = service.Spec.Apply(s => s.Ports[0].Port) },
                }
            },
            new()
            {
                CustomTimeouts = new() { Create = TimeSpan.FromSeconds(20) },
                Provider = provider,
            });
    }
}
