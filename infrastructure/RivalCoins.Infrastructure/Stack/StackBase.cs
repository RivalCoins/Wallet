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
using Pulumi.Kubernetes.Networking.V1;
using Pulumi.Kubernetes.Types.Inputs.Apps.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Networking.V1;
using RivalCoins.Infrastructure.Resource;

namespace RivalCoins.Infrastructure.Stack;

public abstract class StackBase : Pulumi.Stack
{
    protected StackBase()
    {
        var cluster = CreateCluster();

        this.KubeConfig = cluster.KubeConfig;

        var vaultNamespace = new Namespace("vault", new() { Metadata = new ObjectMetaArgs() { Name = "vault" } }, new() { Provider = cluster.Provider });
        var localStorageClass = this.CreateLocalNodeStorageClass(vaultNamespace, cluster.Provider);

        var vaultName = Vault(
            cluster.NodeNames,
            localStorageClass,
            cluster.Provider,
            vaultNamespace);

        this.PersistentStorageClass = this.CreatePersistentStorageClass(cluster.Provider);
        this.Provider = cluster.Provider;

        Debug(cluster.Provider);

        var certManagerNamespace = new Namespace("cert-manager", new() { Metadata = new ObjectMetaArgs() { Name = "cert-manager" } }, new() { Provider = cluster.Provider });
        this.IngressNamespace = new Namespace("ingress", new() { Metadata = new ObjectMetaArgs() { Name = "ingress" } }, new() { Provider = cluster.Provider });

        this.CertManager = CreateCertManager(cluster.Provider, certManagerNamespace);
        IngressController(cluster.Provider, this.IngressNamespace);

        this.ManualInstructions = Output.All(
            Output.Format($"kubectl exec -it {vaultName}-0 -n {vaultNamespace.Metadata.Apply(m => m.Name)} -- vault operator init      # Vault - Write down all 5 unseal keys and the root token"),
            Output.Format($"kubectl exec -it {vaultName}-0 -n {vaultNamespace.Metadata.Apply(m => m.Name)} -- vault operator unseal    # Vault - Enter Key 1 of 3"),
            Output.Format($"kubectl exec -it {vaultName}-0 -n {vaultNamespace.Metadata.Apply(m => m.Name)} -- vault operator unseal    # Vault - Enter Key 2 of 3"),
            Output.Format($"kubectl exec -it {vaultName}-0 -n {vaultNamespace.Metadata.Apply(m => m.Name)} -- vault operator unseal    # Vault - Enter Key 3 of 3"),
            Output.Format($"kubectl exec -it {vaultName}-1 -n {vaultNamespace.Metadata.Apply(m => m.Name)} -- vault operator raft join http://{vaultName}-0.{vaultName}-internal:8200    # Vault - Sync with leader node"),
            Output.Format($"kubectl exec -it {vaultName}-1 -n {vaultNamespace.Metadata.Apply(m => m.Name)} -- vault operator unseal    # Vault - Enter Key 1 of 3"),
            Output.Format($"kubectl exec -it {vaultName}-1 -n {vaultNamespace.Metadata.Apply(m => m.Name)} -- vault operator unseal    # Vault - Enter Key 2 of 3"),
            Output.Format($"kubectl exec -it {vaultName}-1 -n {vaultNamespace.Metadata.Apply(m => m.Name)} -- vault operator unseal    # Vault - Enter Key 3 of 3"),
            Output.Format($"kubectl exec -it {vaultName}-2 -n {vaultNamespace.Metadata.Apply(m => m.Name)} -- vault operator raft join http://{vaultName}-0.{vaultName}-internal:8200    # Vault - Sync with leader node"),
            Output.Format($"kubectl exec -it {vaultName}-2 -n {vaultNamespace.Metadata.Apply(m => m.Name)} -- vault operator unseal    # Vault - Enter Key 1 of 3"),
            Output.Format($"kubectl exec -it {vaultName}-2 -n {vaultNamespace.Metadata.Apply(m => m.Name)} -- vault operator unseal    # Vault - Enter Key 2 of 3"),
            Output.Format($"kubectl exec -it {vaultName}-2 -n {vaultNamespace.Metadata.Apply(m => m.Name)} -- vault operator unseal    # Vault - Enter Key 3 of 3"),
            Output.Format($"kubectl exec -it {vaultName}-0 -n {vaultNamespace.Metadata.Apply(m => m.Name)} -- /bin/sh                  # Vault - Launch command prompt"),
            Output.Format($"vault login                                                     # Vault - Login")

        );
    }

    public abstract Output<ImmutableArray<string>> ManualInstructions { get; set; }
    public abstract Output<string> KubeConfig { get; set; }

    protected StorageClass PersistentStorageClass { get; }
    protected Provider Provider { get; }
    protected Release CertManager { get; }
    protected Namespace IngressNamespace { get; }

    protected abstract StorageClass CreatePersistentStorageClass(Provider provider);
    protected abstract (KubernetesCluster Cluster, Provider Provider, Output<string> KubeConfig, Output<ImmutableArray<string>> NodeNames) CreateCluster();
    protected abstract StorageClass CreateLocalNodeStorageClass(Namespace ns, Provider provider);

    protected static void Debug(Provider provider)
    {
        _ = new Pulumi.Kubernetes.Apps.V1.Deployment(
            $"debug",
            new DeploymentArgs
            {
                Metadata = new ObjectMetaArgs()
                {
                    Namespace = "ingress",
                },
                Spec = new DeploymentSpecArgs
                {
                    Selector = new LabelSelectorArgs
                    {
                        MatchLabels = new InputMap<string> { { "app", "debug" } }
                    },
                    Replicas = 1,
                    Template = new PodTemplateSpecArgs
                    {
                        Metadata = new ObjectMetaArgs
                        {
                            Name = "debug",
                            Labels = new() { { "app", "debug" } },
                        },
                        Spec = new PodSpecArgs
                        {
                            Containers =
                            {
                                new ContainerArgs
                                {
                                    Name = "debug",
                                    Image = "ubuntu",
                                    Command = "sleep",
                                    Args = { "infinity" }
                                }
                            }
                        }
                    }
                }
            },
            new()
            {
                Provider = provider,
            });
    }

    private static void IngressController(Provider provider, Namespace ns)
    {
        _ = new Release(
            "ingress-nginx",
            new()
            {
                Chart = "ingress-nginx",
                Version = "v4.7.1",
                RepositoryOpts = new Pulumi.Kubernetes.Types.Inputs.Helm.V3.RepositoryOptsArgs
                {
                    Repo = "https://kubernetes.github.io/ingress-nginx"
                },
                Namespace = ns.Metadata.Apply(m => m.Name),
                Values =
                {
                    ["installCRDs"] = true,
                    ["service.beta.kubernetes.io"] = false,
                    ["service.beta.kubernetes.io/do-loadbalancer-tls-passthrough"] = false,
                    ["controller.publishService.enabled"] = true,
                }
            },
            new()
            {
                Provider = provider,
                CustomTimeouts = new()
                {
                    Create = TimeSpan.FromSeconds(20)
                }
            });
    }

    #region Cert Manager

    private static Release CreateCertManager(Provider provider, Namespace ns)
    {
        return new Release(
            "cert-manager",
            new()
            {
                Chart = "cert-manager",
                Version = "v1.12.3",
                RepositoryOpts = new Pulumi.Kubernetes.Types.Inputs.Helm.V3.RepositoryOptsArgs
                {
                    Repo = "https://charts.jetstack.io"
                },
                Namespace = ns.Metadata.Apply(m => m.Name),
                Values =
                {
                    ["installCRDs"] = true,
                }
            },
            new()
            {
                Provider = provider,
                CustomTimeouts = new()
                {
                    Create = TimeSpan.FromSeconds(20)
                }
            });
    }

    #endregion Cert Manager

    #region Vault

    private static Output<string> Vault(
        Output<ImmutableArray<string>> nodeNames,
        StorageClass localStorageClass,
        Provider provider,
        Namespace ns)
    {
        const int NumberOfNodes = 3;

        for (var i = 0; i < NumberOfNodes; i++)
        {
            _ = new PersistentVolume(
           $"local-node-{i}",
           new()
           {
               Metadata = new ObjectMetaArgs()
               {
                   Name = nodeNames.GetAt(i),
                   Namespace = ns.Metadata.Apply(m => m.Name),
               },
               Spec = new PersistentVolumeSpecArgs()
               {
                   Capacity = new() { { "storage", "1Gi" } },
                   VolumeMode = "Filesystem",
                   AccessModes = new[] { "ReadWriteOnce" },
                   PersistentVolumeReclaimPolicy = "Retain",
                   StorageClassName = localStorageClass.Metadata.Apply(m => m.Name),
                   Local = new LocalVolumeSourceArgs() { Path = "/var/tmp" },
                   NodeAffinity = new VolumeNodeAffinityArgs()
                   {
                       Required = new NodeSelectorArgs()
                       {
                           NodeSelectorTerms = new NodeSelectorTermArgs[]
                           {
                                new()
                                {
                                    MatchExpressions =  new NodeSelectorRequirementArgs()
                                    {
                                        Key = "kubernetes.io/hostname",
                                        Operator = "In",
                                        Values = nodeNames.GetAt(i)
                                    }
                                }
                           }
                       }
                   }
               }
           },
           new() { Provider = provider }
           );
        }

        var vault = new Release(
            "vault",
            new()
            {
                Chart = "vault",
                Version = "v0.25.0",
                RepositoryOpts = new Pulumi.Kubernetes.Types.Inputs.Helm.V3.RepositoryOptsArgs
                {
                    Repo = "https://helm.releases.hashicorp.com"
                },
                Namespace = ns.Metadata.Apply(m => m.Name),
                Values =
                {
                    ["server"] = new Dictionary<string, object>()
                    {
                        ["dataStorage"] = new InputMap<object>
                        {
                            ["enabled"] = true,
                            ["size"] = "500Mi",
                            ["storageClass"] = localStorageClass.Metadata.Apply(m => m.Name)
                        }.ToOutput(),
                        ["ha"] = new InputMap<object>
                        {
                            ["enabled"] = true,
                            ["raft"] = new Dictionary<string, object>()
                            {
                                ["enabled"] = true
                            }
                        }.ToOutput()
                    }
                }
            },
            new()
            {
                Provider = provider,
                CustomTimeouts = new()
                {
                    Create = TimeSpan.FromSeconds(20)
                }
            });

        return vault.Name;
    }

    #endregion Vault
}
