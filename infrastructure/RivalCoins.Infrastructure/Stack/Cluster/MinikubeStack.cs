using System.Collections.Immutable;
using Pulumi;
using Pulumi.DigitalOcean;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Storage.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using RivalCoins.Infrastructure.Stack.App;
using Provider = Pulumi.Kubernetes.Provider;

namespace RivalCoins.Infrastructure.Stack.Cluster;

public class MinikubeStack : StackBase
{
    public MinikubeStack()
    {
        var cluster = CreateCluster();

        _ = new MinikubeAppStack((StorageClass)this.PersistentStorageClass);
    }

    [Output]
    public override Output<ImmutableArray<string>> ManualInstructions { get; set; }
    [Output]
    public override Output<string> KubeConfig { get; set; }

    protected override StorageClass CreatePersistentStorageClass(Provider provider) =>
        new StorageClass(
            "standard",
            new()
            {
                ApiVersion = "storage.k8s.io/v1",
                Kind = "StorageClass",
                Metadata = new ObjectMetaArgs
                {
                    Annotations =
                    {
                        { "storageclass.kubernetes.io/is-default-class", "true" },
                    },
                    Labels =
                    {
                        { "addonmanager.kubernetes.io/mode", "EnsureExists" },
                    },
                    Name = "standard",
                },
                Provisioner = "k8s.io/minikube-hostpath",
                ReclaimPolicy = "Delete",
                VolumeBindingMode = "Immediate",
            },
            new CustomResourceOptions
            {
                Protect = true,
            });

    protected override (KubernetesCluster Cluster, Provider Provider, Output<string> KubeConfig, Output<ImmutableArray<string>> NodeNames) CreateCluster()
    {
        const string MinikubeProfileName = "rivalcoins";
            
        var nodeNames = Output.Create(new [] { MinikubeProfileName, $"{MinikubeProfileName}-m02", $"{MinikubeProfileName}-m03", }.ToImmutableArray());

        return (default!, default!, default!, nodeNames);
    }

    protected override StorageClass CreateLocalNodeStorageClass(Namespace ns, Provider provider)
        => new StorageClass(
            "csi-hostpath-sc",
            new()
            {
                ApiVersion = "storage.k8s.io/v1",
                Kind = "StorageClass",
                Metadata = new ObjectMetaArgs
                {
                    Annotations = null,
                    Labels =
                    {
                        { "addonmanager.kubernetes.io/mode", "Reconcile" },
                    },
                    Name = "csi-hostpath-sc",
                },
                Provisioner = "hostpath.csi.k8s.io",
                ReclaimPolicy = "Delete",
                VolumeBindingMode = "Immediate",
            },
            new CustomResourceOptions
            {
                Protect = true,
            });
}
