using Pulumi;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pulumi.Kubernetes;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Storage.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace RivalCoins.Infrastructure.Stack;

public class ProductionStack : DigitalOceanAppStack
{
    public ProductionStack(StorageClass persistentStorageClass, Provider provider)
        : base(persistentStorageClass, provider)
    {
    }

    protected override Namespace CreateAppNamespace(Provider provider) => new("prod", new() { Metadata = new ObjectMetaArgs() { Name = "prod" } }, new() { Provider = provider });
    protected override Output<string> RivalCoinsHomeDomain => Output.Create("https://rivalcoins.money");
    protected override Output<string> HorizonUrl { get; } = Output.Create("https://horizon.rivalcoins.money");
    protected override Output<string> AssetServerUrl { get; } = Output.Create("https://api.rivalcoins.money");
    protected override Output<string> DappUrl { get; } = Output.Create("https://rivalcoins.money");
    protected override Output<string> WalletUrl { get; } = Output.Create("https://wallet.rivalcoins.money");
    protected override Output<string> FakeUsaIssuerAccount { get; } = Output.Create("GAQRM5HGAW7SITWXIVHGV26NMW6RQZIFMMUTSTPFXGIWKSBTLQKGG7EO");
    protected override Output<string> FakeUsaWrapperIssuerAccount { get; } = Output.Create("GABAWEV2F7MAJWO5PEJ773B6LXXYWOOH3NOWO4YYHMDF5YXCWRFULCND");
}
