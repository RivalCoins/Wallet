using Pulumi;

namespace RivalCoins.Infrastructure.Resource;

public class SolverResourceArgs : ResourceArgs
{
    [Input("http01")]
    public Input<Http01ResourceArgs> Http01 { get; set; }
}