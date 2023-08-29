using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Pulumi;
using Pulumi.DigitalOcean;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Helm.V3;
using Pulumi.Kubernetes.Networking.V1;
using Pulumi.Kubernetes.Storage.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using Pulumi.Kubernetes.Types.Inputs.Networking.V1;
using RivalCoins.Infrastructure.Resource;
using RivalCoins.Infrastructure.Stack.App;
using Provider = Pulumi.Kubernetes.Provider;

namespace RivalCoins.Infrastructure.Stack.Cluster;

public class DigitalOceanStack : StackBase
{
    public DigitalOceanStack()
    {
        var devStack = new DevelopmentStack(this.PersistentStorageClass, this.Provider);
        var testStack = new TestStack(this.PersistentStorageClass, this.Provider);
        var productionStack = new ProductionStack(this.PersistentStorageClass, this.Provider);

        var ingressServices = testStack.GetIngressServices(this.IngressNamespace, this.Provider);
        ingressServices.AddRange(productionStack.GetIngressServices(this.IngressNamespace, this.Provider));
        ingressServices.AddRange(devStack.GetIngressServices(this.IngressNamespace, this.Provider));
        var clusterIssuer = CreateClusterIssuer(this.CertManager, this.Provider);
        Ingress(ingressServices, clusterIssuer, this.Provider, this.IngressNamespace);
    }

    [Output]
    public override Output<ImmutableArray<string>> ManualInstructions { get; set; }
    [Output]
    public override Output<string> KubeConfig { get; set; }

    #region Ingress

    private static void Ingress(
        List<(Service Service, Output<string> Url)> ingressServices,
        ClusterIssuer issuer,
        Provider provider,
        Namespace ns)
    {
        _ = new Ingress(
            "ingress",
            new()
            {
                Metadata = new ObjectMetaArgs()
                {
                    Name = "ingress",
                    Namespace = ns.Metadata.Apply(m => m.Name),
                    Annotations = new()
                    {
                        { "kubernetes.io/ingress.class", "nginx" },
                        { "cert-manager.io/cluster-issuer", issuer.MetaData.Apply(m => m.Name) },
                        { "acme.cert-manager.io/http01-edit-in-place", true.ToString() },
                    }
                },
                Spec = new IngressSpecArgs()
                {
                    IngressClassName = "nginx",
                    Tls = ingressServices.Select(ingressService => 
                        new IngressTLSArgs()
                        {
                            Hosts = ingressService.Url.Apply(url => url.Replace("https://", string.Empty)),
                            SecretName = Output.Format($"{ingressService.Url.Apply(url => url.Replace("https://", string.Empty).Replace(".", "-"))}-tls")
                        }
                    ).ToList(),
                    Rules = ingressServices.Select(ingressService => 
                        new IngressRuleArgs()
                        {
                            Host = ingressService.Url.Apply(url => url.Replace("https://", string.Empty)),
                            Http = new HTTPIngressRuleValueArgs()
                            {
                                Paths = new HTTPIngressPathArgs()
                                {
                                    Path = "/",
                                    PathType = "Prefix",
                                    Backend = new IngressBackendArgs()
                                    {
                                        Service = new IngressServiceBackendArgs()
                                        {
                                            Name = ingressService.Service.Metadata.Apply(m => m.Name),
                                            Port = new ServiceBackendPortArgs() { Number = ingressService.Service.Spec.Apply(s => s.Ports[0].Port) }
                                        }
                                    }
                                }
                            }
                        }
                    ).ToList(),
                },
            },
            new()
            {
                Provider = provider,
            });
    }

    private ClusterIssuer CreateClusterIssuer(Release certManager, Provider provider)
    {
        return new ClusterIssuer(
            "letsencrypt",
            new ClusterIssuerResourceArgs()
            {
                Metadata = new ObjectMetaArgs()
                {
                    Name = Output.Format($"letsencrypt-{certManager.Namespace}"),
                    Namespace = certManager.Namespace,
                },
                Spec = new ClusterIssuerSpecArgs()
                {
                    Acme = new AcmeResourceArgs()
                    {
                        Server = "https://acme-v02.api.letsencrypt.org/directory",
                        Email = "admin@rivalcoins.money",
                        PrivateKeySecretRef = new InputMap<string>()
                        {
                            { "name", Output.Format($"letsencrypt-{certManager.Namespace}-private-key") }
                        },
                        Solvers = new List<SolverResourceArgs>()
                        {
                            new()
                            {
                                Http01 =
                                    new Http01ResourceArgs()
                                    {
                                        Ingress = new IngressResourceArgs() { Class = "nginx" }
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

    #endregion Ingress

    protected override StorageClass CreatePersistentStorageClass(Provider provider) =>
        new Pulumi.Kubernetes.Storage.V1.StorageClass("do-block-storage", new()
        {
            AllowVolumeExpansion = true,
            ApiVersion = "storage.k8s.io/v1",
            Kind = "StorageClass",
            Metadata = new Pulumi.Kubernetes.Types.Inputs.Meta.V1.ObjectMetaArgs
            {
                Annotations =
                    {
                        { "storageclass.kubernetes.io/is-default-class", "true" },
                    },
                Labels =
                    {
                        { "c3.doks.digitalocean.com/component", "csi-controller-service" },
                        { "c3.doks.digitalocean.com/plane", "data" },
                        { "doks.digitalocean.com/managed", "true" },
                    },
                Name = "do-block-storage",
            },
            Provisioner = "dobs.csi.digitalocean.com",
            ReclaimPolicy = "Delete",
            VolumeBindingMode = "Immediate",
        }, new CustomResourceOptions
        {
            Protect = true,
            Provider = provider,
        });

    protected override (KubernetesCluster Cluster, Provider Provider, Output<string> KubeConfig, Output<ImmutableArray<string>> NodeNames) CreateCluster()
    {
        var cluster = new Pulumi.DigitalOcean.KubernetesCluster(
            "do-cluster",
            new()
            {
                Region = Pulumi.DigitalOcean.Region.NYC1,
                Version = "1.27.4-do.0",
                RegistryIntegration = true,                
                NodePool = new Pulumi.DigitalOcean.Inputs.KubernetesClusterNodePoolArgs()
                {
                    Name = "default",
                    NodeCount = 3,
                    Size = Pulumi.DigitalOcean.DropletSlug.DropletS2VCPU2GB.ToString()
                }
            },
            new() { CustomTimeouts = new() { Create = TimeSpan.FromMinutes(6) } });

        var kubeConfig = cluster.Status.Apply(s =>
        {
            if (s == "running")
            {
                var clusterDataSource = cluster.Name.Apply(n => Pulumi.DigitalOcean.GetKubernetesCluster.InvokeAsync(new() { Name = n }));
                return clusterDataSource.Apply(c => c.KubeConfigs[0].RawConfig!);
            }
            else
            {
                return cluster.KubeConfigs.Apply(c => c[0].RawConfig!);
            }
        });

        var provider = new Provider("do", new() { KubeConfig = kubeConfig });
        Output<ImmutableArray<string>> nodeNames = cluster.NodePool.Apply(p => p.Nodes.Select(n => n.Name).Cast<string>().ToImmutableArray());

        return (cluster, provider, kubeConfig, nodeNames);
    }

    protected override StorageClass CreateLocalNodeStorageClass(Namespace ns, Provider provider)
    => new StorageClass(
        "local-storage",
        new()
        {
            Metadata = new ObjectMetaArgs()
            {
                Name = "local-storage",
                Namespace = ns.Metadata.Apply(m => m.Name),
            },
            Provisioner = "kubernetes.io/no-provisioner",
            VolumeBindingMode = "WaitForFirstConsumer"
        },
        new() { Provider = provider });
}
