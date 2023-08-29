using Pulumi;

namespace RivalCoins.Infrastructure.Resource;

public class IngressResourceArgs : ResourceArgs
{
    [Input("class")]
    public Input<string> Class { get; set; }
}