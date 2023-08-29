using Pulumi;

namespace RivalCoins.Infrastructure.Resource;

public class Http01ResourceArgs : ResourceArgs
{
    [Input("ingress")]
    public Input<IngressResourceArgs> Ingress { get; set; }
}