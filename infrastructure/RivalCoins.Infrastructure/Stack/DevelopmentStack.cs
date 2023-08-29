using Google.Protobuf.WellKnownTypes;
using Pulumi;
using Pulumi.Kubernetes;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Storage.V1;
using Pulumi.Kubernetes.Types.Inputs.Apps.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Server.IIS.Core;

namespace RivalCoins.Infrastructure.Stack;

public class DevelopmentStack : AppStack
{
    private const string MinikubeProfileName = "profile4";

    private Service? _mockCompanySite;
    private StorageClass _storageClass;
    //private Node[] _nodes;

    public DevelopmentStack(StorageClass persistentStorageClass) : base(persistentStorageClass, null!)
    {
    }

    protected override Namespace CreateAppNamespace(Provider provider) => new("dev", new() { Metadata = new ObjectMetaArgs() { Name = "dev" } });

    protected override Output<string> HorizonUrl { get; } = Output.Create("https://localhost:8001");
    protected override Output<string> AssetServerUrl => throw new NotImplementedException();
    protected override Output<string> DappUrl => throw new NotImplementedException();
    protected override Output<string> WalletUrl => throw new NotImplementedException();
    protected override Output<string> FakeUsaIssuerAccount => throw new NotImplementedException();
    protected override Output<string> FakeUsaWrapperIssuerAccount => throw new NotImplementedException();

    private static Service MockCompanySite(Provider provider)
    {
        const string L1FakeMoneySecretsFileName = "l1-fake-money-secrets.txt";
        const string L1FakeMoneySecretsPath = "secret/data/rivalcoins/l1/fake-money";
        const string L2FakeMoneySecretsFileName = "l2-fake-money-secrets.txt";
        const string L2FakeMoneySecretsPath = "secret/data/rivalcoins/l2/fake-money";
        const string ServiceAccountName = "mock-company-site";

        InputMap<string> appLabels = new() { { "app", "mock-company-site" } };

        var serviceAccount = new ServiceAccount(ServiceAccountName,
            new()
            {
                Metadata = new ObjectMetaArgs()
                {
                    Name = ServiceAccountName,
                    Labels = appLabels
                }
            });

        var mockCompanySite = new Pulumi.Kubernetes.Apps.V1.Deployment(
            "mock-company-site",
            new DeploymentArgs
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
                            Labels = appLabels,
                            Annotations = new()
                            {
                                { "vault.hashicorp.com/agent-inject", "true" },
                                { "vault.hashicorp.com/role", ServiceAccountName },
                                { $"vault.hashicorp.com/agent-inject-secret-{L1FakeMoneySecretsFileName}", L1FakeMoneySecretsPath },
                                {
                                    $"vault.hashicorp.com/agent-inject-template-{L1FakeMoneySecretsFileName}",
                                    @$"{{{{- with secret ""{L1FakeMoneySecretsPath}"" -}}}}"
                                        + @$"export FAKE_MONEY_ISSUER_SEED={{{{ .Data.data.issuer_seed }}}}"
                                        + @$" export FAKE_MONEY_DISTRIBUTOR_SEED={{{{ .Data.data.distributor_seed }}}}"
                                    + @$"{{{{- end }}}}"
                                },
                                { $"vault.hashicorp.com/agent-inject-secret-{L2FakeMoneySecretsFileName}", L2FakeMoneySecretsPath },
                                {
                                    $"vault.hashicorp.com/agent-inject-template-{L2FakeMoneySecretsFileName}",
                                    @$"{{{{- with secret ""{L2FakeMoneySecretsPath}"" -}}}}"
                                        + @$"export FAKE_MONEY_WRAPPER_ISSUER_SEED={{{{ .Data.data.wrapper_issuer_seed }}}}"
                                        + @$" export FAKE_MONEY_WRAPPER_DISTRIBUTOR_SEED={{{{ .Data.data.wrapper_distributor_seed }}}}"
                                        + @$" export BANK_SEED={{{{ .Data.data.bank_seed }}}}"
                                    + @$"{{{{- end }}}}"
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
                                    Name = "mock-company-site",
                                    Image = "mock-company-site:dev",
                                    Ports = DevWebServerContainerPorts(),
                                    VolumeMounts = DevWebServerVolumeMountArgs(),
                                    Env = DevWebServerEnvironmentVariables(),
                                    Command = "/bin/bash",
                                    Args = { "-c", $"source /vault/secrets/{L1FakeMoneySecretsFileName} && source /vault/secrets/{L2FakeMoneySecretsFileName} && dotnet /app/RivalCoins.MockCompanySite.dll" }
                                }
                            },
                            Volumes = DevWebServerVolumeArgs()
                        }
                    }
                }
            },
            new() { CustomTimeouts = new() { Create = TimeSpan.FromSeconds(20) } });

        var clusterIp = ClusterIP(mockCompanySite, 8010, appLabels, provider);
        var loadBalancer = LoadBalancer($"{mockCompanySite.GetResourceName()}-lb", (mockCompanySite, nameof(PortType.secure)), 8080, appLabels);

        return clusterIp;
    }

    private static List<EnvVarArgs> DevWebServerEnvironmentVariables()
        => new List<EnvVarArgs>()
        {
                new EnvVarArgs { Name = "ASPNETCORE_ENVIRONMENT", Value = "Development" },
                new EnvVarArgs { Name = "ASPNETCORE_URLS", Value = "https://+;http://+" },
                new EnvVarArgs { Name = "ASPNETCORE_Kestrel__Certificates__Default__Password", Value = "password1" },
                new EnvVarArgs { Name = "ASPNETCORE_Kestrel__Certificates__Default__Path", Value = "/https/aspnetapp.pfx" }
        };

    private static InputList<VolumeMountArgs> DevWebServerVolumeMountArgs()
        => new VolumeMountArgs()
        {
            Name = "https",
            MountPath = "/https"
        };

    private static InputList<VolumeArgs> DevWebServerVolumeArgs()
         => new VolumeArgs()
         {
             Name = "https",
             HostPath = new HostPathVolumeSourceArgs() { Path = "/https" }
         };

    private static InputList<ContainerPortArgs> DevWebServerContainerPorts()
        => new List<ContainerPortArgs>
        {
            new() { Name = nameof(PortType.unsecure), ContainerPortValue = PortType.unsecure },
            new() { Name = nameof(PortType.secure), ContainerPortValue = PortType.secure }
        };

    #region Wallet

    protected override InputList<ContainerPortArgs> WalletContainerPorts() =>
        new List<ContainerPortArgs>
        {
            new() { Name = nameof(PortType.unsecure), ContainerPortValue = PortType.unsecure },
            new() { Name = nameof(PortType.secure), ContainerPortValue = PortType.secure }
        };

    protected override InputList<VolumeMountArgs>? WalletVolumeMountArgs() => DevWebServerVolumeMountArgs();

    protected override InputList<VolumeArgs>? WalletVolumeArgs() => DevWebServerVolumeArgs();

    protected override List<EnvVarArgs> WalletEnvironmentVariables()
        => DevWebServerEnvironmentVariables().Union(new EnvVarArgs[] {
        new() { Name = "L1_HORIZON_URL", Value = Output.Format($"http://{this.Horizon.Metadata.Apply(m => m.Name)}:{this.Horizon.Spec.Apply(s => s.Ports[0].Port)}") },
        new() { Name = "L2_HORIZON_URL", Value = Output.Format($"http://{L2HorizonProxy.Metadata.Apply(m => m.Name)}:{L2HorizonProxy.Spec.Apply(s => s.Ports[0].Port)}") },
        new() { Name = "API_URL", Value = Output.Format($"http://{ApiService.Metadata.Apply(m => m.Name)}:{ApiService.Spec.Apply(s => s.Ports[0].Port)}") },
        new() { Name = "RIVALCOINS_HOME_DOMAIN", Value = Output.Format($"http://{_mockCompanySite!.Metadata.Apply(m => m.Name)}:{_mockCompanySite!.Spec.Apply(s => s.Ports[0].Port)}") },
        new() { Name = "WRAPPED_ASSET", Value = "FakeMONEY:GDWN72Y2FAXARUASIEN7WQNROJKKWIDWD7LIMG3IOZOTH3KBZ2PXLUKE" } }).ToList();

    #endregion Wallet

    #region Horizon Proxy

    protected override List<EnvVarArgs> HorizonProxyEnvironmentVariables(Service proxiedHorizonService)
        => new List<EnvVarArgs>(DevWebServerEnvironmentVariables())
        {
            new EnvVarArgs { Name = "PROXIED_URL", Value = Output.Format($"http://{proxiedHorizonService.Metadata.Apply(m => m.Name)}:{proxiedHorizonService.Spec.Apply(s => s.Ports[0].Port)}") },
        };

    protected override InputList<VolumeMountArgs>? HorizonProxyVolumeMountArgs() => DevWebServerVolumeMountArgs();

    protected override InputList<VolumeArgs>? HorizonProxyVolumeArgs() => DevWebServerVolumeArgs();

    protected override InputList<ContainerPortArgs> HorizonProxyContainerPorts() => DevWebServerContainerPorts();

    #endregion Horizon Proxy

    #region API

    protected override void OnApiLoading(Provider provider)
    {
        _mockCompanySite = MockCompanySite(provider);
    }

    protected override Output<string> RivalCoinsHomeDomain => Output.Format($"http://{_mockCompanySite!.Metadata.Apply(m => m.Name)}:{_mockCompanySite!.Spec.Apply(s => s.Ports[0].Port)}");

    protected override List<EnvVarArgs> ApiEnvironmentVariables(Service horizon)
    {
        var environmentVariables = DevWebServerEnvironmentVariables();
        environmentVariables.AddRange(base.ApiEnvironmentVariables(horizon));

        return environmentVariables;
    }

    protected override InputList<VolumeMountArgs>? ApiVolumeMountArgs() => DevWebServerVolumeMountArgs();

    protected override InputList<VolumeArgs>? ApiVolumeArgs() => DevWebServerVolumeArgs();

    protected override InputList<ContainerPortArgs> ApiContainerPorts() => DevWebServerContainerPorts();

    #endregion API
}
