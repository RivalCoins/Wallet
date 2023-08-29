using Pulumi;
using Pulumi.Kubernetes;
using Pulumi.Kubernetes.Helm.V3;
using Pulumi.Kubernetes.Storage.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using Pulumi.KubernetesCertManager;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pulumi.DigitalOcean;
using Pulumi.Kubernetes.Core.V1;
using Provider = Pulumi.Kubernetes.Provider;
using Pulumi.Experimental.Provider;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;

namespace RivalCoins.Infrastructure.Stack;

public class MinikubeStack : StackBase
{
    public MinikubeStack()
    {
        var cluster = CreateCluster();

        _ = new DevelopmentStack(this.PersistentStorageClass);
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
