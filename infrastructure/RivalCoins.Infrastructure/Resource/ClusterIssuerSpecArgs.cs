using Pulumi;

namespace RivalCoins.Infrastructure.Resource;

public class ClusterIssuerSpecArgs : ResourceArgs
{
    [Input("acme")]
    public Input<AcmeResourceArgs> Acme { get; set; }
}