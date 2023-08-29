using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using Pulumi;
using System;
using System.Collections.Generic;
using System.Linq;
using Pulumi.Kubernetes.Types.Inputs.Apps.V1;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Storage.V1;
using Pulumi.Kubernetes.Apps.V1;
using DeploymentArgs = Pulumi.Kubernetes.Types.Inputs.Apps.V1.DeploymentArgs;
using DeploymentSpecArgs = Pulumi.Kubernetes.Types.Inputs.Apps.V1.DeploymentSpecArgs;
using Provider = Pulumi.Kubernetes.Provider;
using VolumeArgs = Pulumi.Kubernetes.Types.Inputs.Core.V1.VolumeArgs;

namespace RivalCoins.Infrastructure.Stack.App;

public abstract class AppStack
{
    protected static readonly (int secure, int unsecure) PortType = (443, 80);
    protected static readonly TimeSpan DefaultTimeOut = TimeSpan.FromSeconds(20);

    private const int StellarQuickStartHorizonPort = 8000;

    protected AppStack(StorageClass persistentStorageClass, Provider provider)
    {
        var appNamespace = CreateAppNamespace(provider);

        var horizonLabels = new InputMap<string>
        {
            { "app", "horizon" }
        };
        var horizonProxyLabels = new InputMap<string>
        {
            { "app", "horizon-proxy" }
        };

        var horizon = CreateHorizonApp(
            horizonLabels,
            persistentStorageClass,
            provider,
            appNamespace);

        Horizon = ClusterIP(
            horizon,
            StellarQuickStartHorizonPort,
            horizonLabels,
            provider);
        if (false)
        {
            var l1HorizonProxy = HorizonProxy("l1", Horizon, horizonProxyLabels, provider);
            ClusterIP(l1HorizonProxy, 8000, horizonProxyLabels, provider);
            LoadBalancer(
                "horizon-proxy-l1-lb",
                (l1HorizonProxy, nameof(PortType.secure)),
                8001,
                horizonProxyLabels);
        }

        if (false && this is DevelopmentStack)
        {
            return;
        }

        ApiService = Api(Horizon, appNamespace, provider).Unsecure;
        Wallet = CreateWallet(appNamespace, provider);

        if (false)
        {
            var stellarEphemeral = HorizonEphemeral(
                "ephemeral",
                InputMap<string>.Merge(horizonLabels, new() { { "mode", "ephemeral" } }),
                provider,
                appNamespace,
                persistentStorageClass);
            ClusterIPEphermeral(
                stellarEphemeral,
                8000,
                InputMap<string>.Merge(horizonLabels, new() { { "mode", "ephemeral" } }),
                provider);
        }
    }

    protected Service Horizon { get; }
    protected Service L2HorizonProxy { get; }
    protected Service ApiService { get; }
    protected Service Wallet { get; }

    protected abstract Namespace CreateAppNamespace(Provider provider);
    protected abstract Output<string> HorizonUrl { get; }
    protected abstract Output<string> AssetServerUrl { get; }
    protected abstract Output<string> DappUrl { get; }
    protected abstract Output<string> WalletUrl { get; }
    protected abstract Output<string> FakeUsaIssuerAccount { get; }
    protected abstract Output<string> FakeUsaWrapperIssuerAccount { get; }

    #region Horizon Proxy

    private Pulumi.Kubernetes.Apps.V1.Deployment HorizonProxy(
        string networkLayer,
        Service stellarCoreService,
        InputMap<string> appLabels,
        Provider provider)
    {
        var deploymentArgs = new DeploymentArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = $"horizon-proxy-{networkLayer}"
            },
            Spec = new DeploymentSpecArgs
            {
                Selector = new LabelSelectorArgs
                {
                    MatchLabels = appLabels
                },
                Replicas = 1,
                Template = new PodTemplateSpecArgs
                {
                    Metadata = new ObjectMetaArgs
                    {
                        Labels = appLabels
                    },
                    Spec = new PodSpecArgs
                    {
                        Containers =
                        {
                            new ContainerArgs
                            {
                                Name = $"horizon-proxy-{networkLayer}",
                                Image = "registry.digitalocean.com/rivalcoins/horizonproxy:dev",
                                Ports = HorizonProxyContainerPorts(),
                                Env = HorizonProxyEnvironmentVariables(stellarCoreService),
                                VolumeMounts = HorizonProxyVolumeMountArgs()!
                            }
                        },
                        Volumes = HorizonProxyVolumeArgs()!
                    }
                }
            }
        };

        return new($"horizon-proxy-{networkLayer}", deploymentArgs, new() { Provider = provider });
    }

    protected virtual List<EnvVarArgs> HorizonProxyEnvironmentVariables(Service proxiedHorizonService)
        => new List<EnvVarArgs>()
        {
            new EnvVarArgs { Name = "PROXIED_URL", Value = Output.Format($"http://{proxiedHorizonService.Metadata.Apply(m => m.Name)}:{proxiedHorizonService.Spec.Apply(s => s.Ports[0].Port)}") },
            new EnvVarArgs { Name = "ASPNETCORE_URLS", Value = "http://+" }
        };

    protected virtual InputList<VolumeMountArgs>? HorizonProxyVolumeMountArgs() => null;
    protected virtual InputList<VolumeMountArgs>? HorizonVolumeMountArgs(Input<string> volumeName) =>
        new VolumeMountArgs()
        {
            Name = volumeName,
            MountPath = "/opt/stellar"
        };

    protected virtual InputList<VolumeArgs>? HorizonProxyVolumeArgs() => null;

    protected virtual InputList<ContainerPortArgs> HorizonProxyContainerPorts() =>
        new List<ContainerPortArgs> { new() { Name = nameof(PortType.unsecure), ContainerPortValue = PortType.unsecure } };

    #endregion Horizon Proxy

    #region Wallet

    private Service CreateWallet(Namespace ns, Provider provider)
    {
        InputMap<string> appLabels = new() { { "app", "wallet" } };

        var wallet = new Pulumi.Kubernetes.Apps.V1.Deployment(
            $"wallet-{ns.GetResourceName()}",
            new DeploymentArgs
            {
                Metadata = new ObjectMetaArgs()
                {
                    Namespace = ns.Metadata.Apply(m => m.Name),
                },
                Spec = new DeploymentSpecArgs
                {
                    Selector = new LabelSelectorArgs
                    {
                        MatchLabels = appLabels
                    },
                    Replicas = 1,
                    Template = new PodTemplateSpecArgs
                    {
                        Metadata = new ObjectMetaArgs
                        {
                            Labels = appLabels,
                            Namespace = ns.Metadata.Apply(m => m.Name),
                        },
                        Spec = new PodSpecArgs
                        {
                            Containers =
                            {
                                new ContainerArgs
                                {
                                    Name = "wallet",
                                    Image = "registry.digitalocean.com/rivalcoins/rabet-mobile:test8",
                                    Ports = new ContainerPortArgs() { Name = nameof(PortType.unsecure), ContainerPortValue = 3000 },
                                    Env = new List<EnvVarArgs>
                                    {
                                        new() { Name = "ASSET_SERVER_URL", Value = Output.Format($"{AssetServerUrl}/assetDetails") },
                                        new() { Name = "HORIZON_URL", Value = HorizonUrl },
                                        new() { Name = "DAPP_URL", Value = DappUrl },
                                        new() { Name = "FAKE_USA_ISSUER", Value = FakeUsaIssuerAccount },
                                        new() { Name = "FAKE_USA_WRAPPER_ISSUER", Value = FakeUsaWrapperIssuerAccount },
                                    }
                                }
                            },
                        }
                    }
                }
            },
            new()
            {
                CustomTimeouts = new() { Create = TimeSpan.FromSeconds(20) },
                Provider = provider,
            });

        var clusterIp = ClusterIP(wallet, 9010, appLabels, provider);

        return clusterIp;
    }

    private void WalletLegacy()
    {
        InputMap<string> appLabels = new() { { "app", "wallet" } };

        var wallet = new Pulumi.Kubernetes.Apps.V1.Deployment("wallet", new DeploymentArgs
        {
            Spec = new DeploymentSpecArgs
            {
                Selector = new LabelSelectorArgs
                {
                    MatchLabels = appLabels
                },
                Replicas = 1,
                Template = new PodTemplateSpecArgs
                {
                    Metadata = new ObjectMetaArgs
                    {
                        Labels = appLabels
                    },
                    Spec = new PodSpecArgs
                    {
                        Containers =
                        {
                            new ContainerArgs
                            {
                                Name = "wallet",
                                Image = "wallet:dev",
                                Ports = WalletContainerPorts(),
                                VolumeMounts = WalletVolumeMountArgs()!,
                                Env = WalletEnvironmentVariables()
                            }
                        },
                        Volumes = WalletVolumeArgs()!
                    }
                }
            }
        });

        //var walletService = ClusterIP(wallet, 8888, appLabels);
        //var walletProxy = HorizonProxy("wallet", walletService, new() { { "app", "wallet-proxy" } });
        var loadBalancer = LoadBalancer($"{wallet.GetResourceName()}-lb", (wallet, nameof(PortType.secure)), 7777, appLabels);
    }

    protected virtual List<EnvVarArgs> WalletEnvironmentVariables()
    => new()
    {
        new() { Name = "L1_HORIZON_URL", Value = "https://horizon-l1.rivalcoins.money" },
        new() { Name = "L2_HORIZON_URL", Value = "https://horizon-l2.rivalcoins.money" },
        new() { Name = "API_URL", Value = "https://api.rivalcoins.money" },
        new() { Name = "RIVALCOINS_HOME_DOMAIN", Value = "https://rivalcoins.money" },
        new() { Name = "WRAPPED_ASSET", Value = "FakeMONEY:GDWN72Y2FAXARUASIEN7WQNROJKKWIDWD7LIMG3IOZOTH3KBZ2PXLUKE" }
    };

    protected virtual InputList<ContainerPortArgs> WalletContainerPorts() =>
        new List<ContainerPortArgs> { new() { Name = nameof(PortType.unsecure), ContainerPortValue = PortType.unsecure } };

    protected virtual InputList<VolumeMountArgs>? WalletVolumeMountArgs() => null;

    protected virtual InputList<VolumeArgs>? WalletVolumeArgs() => null;

    #endregion Wallet

    #region API

    protected virtual void OnApiLoading(Provider provider)
    {
    }

    private (Service Secure, Service Unsecure) Api(Service horizon, Namespace ns, Provider provider)
    {
        OnApiLoading(provider);

        const string FakeUsaSecretsFileName = "fake-usa-secrets.txt";
        const string L1FakeUsaSecretsPath = "secret/data/rivalcoins/l1/fake-usa";
        const string ServiceAccountName = "api";

        var secretPath = Output.Format($"secret/data/rivalcoins/{ns.Metadata.Apply(m => m.Name)}/fake-usa");

        InputMap<string> appLabels = new() { { "app", "api" } };

        var serviceAccount = new ServiceAccount(
            $"{ServiceAccountName}-{ns.GetResourceName()}",
            new()
            {
                Metadata = new ObjectMetaArgs()
                {
                    Name = ServiceAccountName,
                    Namespace = horizon.Metadata.Apply(m => m.Namespace),
                    Labels = appLabels,
                },
                ImagePullSecrets = new LocalObjectReferenceArgs() { Name = "rivalcoins" }
            },
            new() { Provider = provider });

        var api = new Pulumi.Kubernetes.Apps.V1.Deployment(
            $"api-{ns.GetResourceName()}",
            new DeploymentArgs
            {
                Metadata = new ObjectMetaArgs()
                {
                    Namespace = horizon.Metadata.Apply(m => m.Namespace),
                },
                Spec = new DeploymentSpecArgs
                {
                    Selector = new LabelSelectorArgs
                    {
                        MatchLabels = appLabels
                    },
                    Replicas = 1,
                    Template = new PodTemplateSpecArgs
                    {
                        Metadata = new ObjectMetaArgs
                        {
                            Labels = appLabels,
                            Annotations = new()
                            {
                                { "vault.hashicorp.com/agent-inject", "true" },
                                { "vault.hashicorp.com/role", Output.Format($"{ServiceAccountName}-{ns.Metadata.Apply(m => m.Name)}") },
                                { $"vault.hashicorp.com/agent-inject-secret-{FakeUsaSecretsFileName}", secretPath },
                                {
                                    $"vault.hashicorp.com/agent-inject-template-{FakeUsaSecretsFileName}",
                                    Output.Format(@$"{{{{- with secret ""{secretPath}"" -}}}} export FAKE_USA_ISSUER_SEED={{{{ .Data.data.issuer_seed }}}} export FAKE_USA_DISTRIBUTOR_SEED={{{{ .Data.data.distributor_seed }}}} export FAKE_USA_WRAPPER_ISSUER_SEED={{{{ .Data.data.wrapper_issuer_seed }}}} export FAKE_USA_WRAPPER_DISTRIBUTOR_SEED={{{{ .Data.data.wrapper_distributor_seed }}}} {{{{- end }}}}")
                                },
                            }
                        },
                        Spec = new PodSpecArgs
                        {
                            ServiceAccountName = serviceAccount.Metadata.Apply(m => m.Name),
                            Containers =
                            {
                                new ContainerArgs
                                {
                                    Name = "rivalcoins-api",
                                    Image = "registry.digitalocean.com/rivalcoins/api:dev9",
                                    Ports = ApiContainerPorts(),
                                    VolumeMounts = ApiVolumeMountArgs()!,
                                    Env = ApiEnvironmentVariables(horizon),
                                    Command = "/bin/bash",
                                    Args = { "-c", $"source /vault/secrets/{FakeUsaSecretsFileName} && dotnet /app/RivalCoins.Server.dll" }
                                }
                            },
                            Volumes = ApiVolumeArgs()!,
                            ImagePullSecrets = new LocalObjectReferenceArgs() { Name = "rivalcoins" }
                        }
                    }
                }
            },
            new()
            {
                CustomTimeouts = new() { Create = TimeSpan.FromSeconds(30) },
                Provider = provider,
            });

        var clusterIp = ClusterIP(api, 8888, appLabels, provider);
        if (false)
        {
            var loadBalancer = LoadBalancer($"{api.GetResourceName()}-lb", (api, nameof(PortType.secure)), 8888, appLabels);
        }

        return (null!, clusterIp);
    }

    protected abstract Output<string> RivalCoinsHomeDomain { get; }

    protected virtual List<EnvVarArgs> ApiEnvironmentVariables(Service horizon)
        => new List<EnvVarArgs>()
        {
            new EnvVarArgs { Name = "RIVALCOINS_HOME_DOMAIN", Value = RivalCoinsHomeDomain },
            new EnvVarArgs { Name = "L1_HORIZON_URL", Value = Output.Format($"http://{horizon.Metadata.Apply(m => m.Name)}:{horizon.Spec.Apply(s => s.Ports[0].Port)}") },
            new EnvVarArgs { Name = "L2_HORIZON_URL", Value = Output.Format($"http://{horizon.Metadata.Apply(m => m.Name)}:{horizon.Spec.Apply(s => s.Ports[0].Port)}") },
            new EnvVarArgs { Name = "ASPNETCORE_URLS", Value = "http://+" }
        };

    protected virtual InputList<VolumeMountArgs>? ApiVolumeMountArgs() => null;

    protected virtual InputList<VolumeArgs>? ApiVolumeArgs() => null;

    protected virtual InputList<ContainerPortArgs> ApiContainerPorts()
        => new List<ContainerPortArgs> { new() { Name = nameof(PortType.unsecure), ContainerPortValue = 5001 } };

    #endregion API

    protected static Service LoadBalancer(
        string serviceName,
        (Pulumi.Kubernetes.Apps.V1.Deployment Pod, string ContainerPortName) pod,
        int externalPort,
        InputMap<string> selector)
    {
        Console.WriteLine("*******************************" + serviceName);

        return
            new(serviceName,
                new ServiceArgs()
                {
                    Spec = new ServiceSpecArgs
                    {
                        Type = "LoadBalancer",
                        Selector = selector,
                        Ports = new ServicePortArgs
                        {
                            Port = externalPort,
                            TargetPort = pod.Pod.Spec.Apply(s => s.Template.Spec.Containers[0].Ports.First(p => p.Name == pod.ContainerPortName).ContainerPortValue)
                        }
                    }
                },
                new() { CustomTimeouts = new() { Create = TimeSpan.FromMinutes(5) } });
    }

    private StatefulSet CreateHorizonApp(
        InputMap<string> appLabels,
        StorageClass persistentStorageClass,
        Provider provider,
        Namespace ns)
    {
        const string HorizonVolumeName = "horizon";

        return new(
            $"horizon-{ns.GetResourceName()}",
            new StatefulSetArgs
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = "horizon",
                    Namespace = ns.Metadata.Apply(m => m.Name),
                },
                Spec = new StatefulSetSpecArgs
                {
                    Selector = new LabelSelectorArgs
                    {
                        MatchLabels = appLabels
                    },
                    ServiceName = "horizon",
                    Replicas = 1,
                    Template = new PodTemplateSpecArgs
                    {
                        Metadata = new ObjectMetaArgs
                        {
                            Labels = appLabels
                        },
                        Spec = new PodSpecArgs
                        {
                            Containers =
                            {
                                new ContainerArgs
                                {
                                    Name = "horizon",
                                    Image = "registry.digitalocean.com/rivalcoins/stellar-quickstart:dev6",
                                    Stdin = true,
                                    Command = "/start --standalone --enable-core-artificially-accelerate-time-for-testing".Split(' ').ToList(),
                                    Ports =
                                    {
                                        new ContainerPortArgs
                                        {
                                            Name = nameof(PortType.unsecure),
                                            ContainerPortValue = 8000,
                                        }
                                    },
                                    VolumeMounts = HorizonVolumeMountArgs(HorizonVolumeName)!,
                                }
                            }
                        },

                    },
                    VolumeClaimTemplates = new PersistentVolumeClaimArgs()
                    {
                        Metadata = new ObjectMetaArgs()
                        {
                            Name = HorizonVolumeName,
                            Labels = appLabels,
                            Namespace = ns.Metadata.Apply(m => m.Name),
                        },
                        Spec = new PersistentVolumeClaimSpecArgs()
                        {
                            AccessModes = "ReadWriteOnce",
                            Resources = new ResourceRequirementsArgs()
                            {
                                Claims = new ResourceClaimArgs() { Name = HorizonVolumeName },
                                Requests = { { "storage", "100Gi" } }
                            },
                            StorageClassName = persistentStorageClass.Metadata.Apply(m => m.Name)
                        }
                    }
                }
            },
            new()
            {
                CustomTimeouts = new() { Create = TimeSpan.FromMinutes(7) },
                Provider = provider,
            });
    }

    private static StatefulSet HorizonEphemeral(
        string networkLayer,
        InputMap<string> appLabels,
        Provider provider,
        Namespace ns,
        StorageClass persistentStorageClass)
    {
        const string PodNamePrefix = "horizon";

        string StellarCoreVolumeName() => $"{PodNamePrefix}-{networkLayer}";

        return new(
            $"{PodNamePrefix}-{networkLayer}",
            new StatefulSetArgs
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = $"{PodNamePrefix}-{networkLayer}",
                    Namespace = ns.Metadata.Apply(m => m.Name),
                },
                Spec = new StatefulSetSpecArgs
                {
                    Selector = new LabelSelectorArgs
                    {
                        MatchLabels = appLabels
                    },
                    ServiceName = $"{PodNamePrefix}-{networkLayer}",
                    Replicas = 1,
                    Template = new PodTemplateSpecArgs
                    {
                        Metadata = new ObjectMetaArgs
                        {
                            Labels = appLabels
                        },
                        Spec = new PodSpecArgs
                        {
                            Containers =
                            {
                                new ContainerArgs
                                {
                                    Name = $"{PodNamePrefix}-{networkLayer}",
                                    Image = "registry.digitalocean.com/rivalcoins/stellar-quickstart:dev5",
                                    Stdin = true,
                                    Command = "/start --standalone --enable-core-artificially-accelerate-time-for-testing".Split(' ').ToList(),
                                    Ports =
                                    {
                                        new ContainerPortArgs
                                        {
                                            Name = nameof(PortType.unsecure),
                                            ContainerPortValue = 8000,
                                        }
                                    },
                                    //VolumeMounts = new VolumeMountArgs()
                                    //{
                                    //    Name = StellarCoreVolumeName(),
                                    //    MountPath = "/opt/stellar"
                                    //}
                                },
                            }
                        },
                    },
                    VolumeClaimTemplates = new PersistentVolumeClaimArgs()
                    {
                        Metadata = new ObjectMetaArgs() { Name = StellarCoreVolumeName(), Labels = appLabels },
                        Spec = new PersistentVolumeClaimSpecArgs()
                        {
                            AccessModes = "ReadWriteOnce",
                            Resources = new ResourceRequirementsArgs()
                            {
                                Claims = new ResourceClaimArgs() { Name = StellarCoreVolumeName() },
                                Requests = { { "storage", "1Gi" } }
                            },
                            StorageClassName = persistentStorageClass.Metadata.Apply(m => m.Name)
                        }
                    }
                }
            },
            new()
            {
                CustomTimeouts = new() { Create = TimeSpan.FromMinutes(7) },
                Provider = provider,
            });
    }

    protected static Service ClusterIP(Pulumi.Kubernetes.Apps.V1.Deployment pod, int servicePort, InputMap<string> selector, Provider provider) =>
        new(pod.GetResourceName(),
            new ServiceArgs()
            {
                Metadata = new ObjectMetaArgs()
                {
                    Name = pod.GetResourceName(),
                    Namespace = pod.Metadata.Apply(m => m.Namespace),
                },
                Spec = new ServiceSpecArgs
                {
                    Type = "ClusterIP",
                    Selector = selector,
                    Ports = new ServicePortArgs
                    {
                        Name = nameof(PortType.unsecure),
                        TargetPort = pod.Spec.Apply(s => s.Template.Spec.Containers[0].Ports.First(p => p.Name == nameof(PortType.unsecure)).ContainerPortValue),
                        Port = servicePort,
                    }
                }
            },
            new()
            {
                CustomTimeouts = new() { Create = TimeSpan.FromSeconds(20) },
                Provider = provider,
            });

    protected static Service ClusterIP(StatefulSet pod,
        int servicePort,
        InputMap<string> selector,
        Provider provider) =>
        new(
            pod.GetResourceName(),
            new ServiceArgs()
            {
                Metadata = new ObjectMetaArgs()
                {
                    Name = pod.Metadata.Apply(m => m.Name),
                    Namespace = pod.Metadata.Apply(m => m.Namespace),
                },
                Spec = new ServiceSpecArgs
                {
                    Type = "ClusterIP",
                    Selector = selector,
                    Ports = new ServicePortArgs { TargetPort = pod.Spec.Apply(s => s.Template.Spec.Containers[0].Ports.First(p => p.Name == nameof(PortType.unsecure)).ContainerPortValue), Port = servicePort }
                }
            },
            new()
            {
                CustomTimeouts = new() { Create = TimeSpan.FromSeconds(20) },
                Provider = provider,
            });

    protected static Service ClusterIPEphermeral(
        StatefulSet pod,
        int servicePort,
        InputMap<string> selector,
        Provider provider) =>
        new(pod.GetResourceName(),
            new ServiceArgs()
            {
                Metadata = new ObjectMetaArgs()
                {
                    Name = pod.GetResourceName()
                },
                Spec = new ServiceSpecArgs
                {
                    Type = "ClusterIP",
                    Selector = selector,
                    Ports = new ServicePortArgs { TargetPort = pod.Spec.Apply(s => s.Template.Spec.Containers[0].Ports.First(p => p.Name == nameof(PortType.unsecure)).ContainerPortValue), Port = servicePort }
                }
            },
            new()
            {
                CustomTimeouts = new() { Create = TimeSpan.FromSeconds(20) },
                Provider = provider,
            });
}
