using Pulumi;
using Pulumi.Kubernetes;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Storage.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace RivalCoins.Infrastructure.Stack.App;

public class TestStack : DigitalOceanAppStack
{
    public TestStack(StorageClass persistentStorageClass, Provider provider)
        : base(persistentStorageClass, provider)
    {
    }

    protected override Namespace CreateAppNamespace(Provider provider) => new("test", new() { Metadata = new ObjectMetaArgs() { Name = "test" } }, new() { Provider = provider });

    protected override Output<string> RivalCoinsHomeDomain => Output.Create("https://test.rivalcoins.money");
    protected override Output<string> HorizonUrl { get; } = Output.Create("https://horizon-test.rivalcoins.money");
    protected override Output<string> AssetServerUrl { get; } = Output.Create("https://api-test.rivalcoins.money");
    protected override Output<string> DappUrl { get; } = Output.Create("https://test.rivalcoins.money/competitor/jeromebellsr/");
    protected override Output<string> WalletUrl { get; } = Output.Create("https://wallet-test.rivalcoins.money");
    protected override Output<string> FakeUsaIssuerAccount { get; } = Output.Create("GCZ5YWZFCBCJBQADBDSD7BQYEPRYGMGSACEPRYEXMHEWIUCN3WXSUJDD");
    protected override Output<string> FakeUsaWrapperIssuerAccount { get; } = Output.Create("GAAAHF6UIOPRBYG6YYGC4RDEVUGQPA2YELTBYQ4GXPLW45I6AQWZGIC4");
}
