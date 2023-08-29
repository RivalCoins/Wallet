using Pulumi;

namespace RivalCoins.Infrastructure.Resource;

public class ClusterIssuerResourceArgs : Pulumi.Kubernetes.ApiExtensions.CustomResourceArgs
{
    public ClusterIssuerResourceArgs() : base("cert-manager.io/v1", "ClusterIssuer")
    {
    }

    [Input("spec")]
    public Input<ClusterIssuerSpecArgs> Spec { get; set; }
}