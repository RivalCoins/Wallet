using Pulumi;
using Pulumi.Kubernetes;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Storage.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace RivalCoins.Infrastructure.Stack.App;

public class DevelopmentStack : EphemeralStackBase
{
    public DevelopmentStack(StorageClass persistentStorageClass, Provider provider)
        : base(persistentStorageClass, provider)
    {
    }

    protected override Namespace CreateAppNamespace(Provider provider) => new("dev", new() { Metadata = new ObjectMetaArgs() { Name = "dev" } }, new() { Provider = provider });

    protected override Output<string> RivalCoinsHomeDomain => Output.Create("https://dev.rivalcoins.money");
    protected override Output<string> HorizonUrl { get; } = Output.Create("https://horizon-dev.rivalcoins.money");
    protected override Output<string> AssetServerUrl { get; } = Output.Create("https://api-dev.rivalcoins.money");
    protected override Output<string> DappUrl { get; } = Output.Create("https://dev.rivalcoins.money");
    protected override Output<string> WalletUrl { get; } = Output.Create("https://wallet-dev.rivalcoins.money");
    protected override Output<string> FakeUsaIssuerAccount { get; } = Output.Create("GCUOZX76D5GYMVSBRC5IC3WFZXFTETBNLI4H67BKUJHGAOSOJMZGKUOV");
    protected override Output<string> FakeUsaWrapperIssuerAccount { get; } = Output.Create("GCJNRPDP3EQCC7BNM3C4ZIZAX3NHLB5STFIWUNZTLYPYWQ7XZMZXXNF4");
}
