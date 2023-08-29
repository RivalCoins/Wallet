using Pulumi;

namespace RivalCoins.Infrastructure.Resource;

public class AcmeResourceArgs : ResourceArgs
{
    [Input("server")]
    public Input<string> Server { get; set; }
    [Input("email")]
    public Input<string> Email { get; set; }
    [Input("privateKeySecretRef")]
    public InputMap<string> PrivateKeySecretRef { get; set; }
    [Input("solvers")]
    public InputList<SolverResourceArgs> Solvers { get; set; }
}