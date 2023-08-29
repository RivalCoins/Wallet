using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace RivalCoins.Infrastructure.Resource;

public class ClusterIssuer : CustomResource
{
    internal ClusterIssuer(string name, ClusterIssuerResourceArgs args, CustomResourceOptions options)
        : base("kubernetes:cert-manager.io/v1:ClusterIssuer", name, args, options)
    {
        this.MetaData = args.Metadata!;
    }

    public Output<ObjectMetaArgs> MetaData { get; }
}