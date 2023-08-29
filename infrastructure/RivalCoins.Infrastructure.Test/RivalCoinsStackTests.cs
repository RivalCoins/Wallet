using FsCheck;
using NUnit.Framework;
using Pulumi.Testing;
using RivalCoins.Sdk.Test.Core;
using System.Collections.Immutable;
using RivalCoins.Infrastructure.Stack;
using RivalCoins.Infrastructure.Stack.Cluster;

namespace RivalCoins.Infrastructure.Test;

[TestFixture]
public class RivalCoinsStackTests : TestClassBase
{
    private const string StellarCoreImageName = "registry.digitalocean.com/rivalcoins/stellar-quickstart:v0.1.5";
    private const string HorizonProxyImageName = "horizonproxy:dev";

    private class Mocks : IMocks
    {
        public Task<object> CallAsync(MockCallArgs args)
        {
            return Task.FromResult((object)args.Args);
        }

        public Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args)
        {
            return Task.FromResult((args.Id, (object)args.Inputs));
        }
    }

    private static Pulumi.Kubernetes.Apps.V1.Deployment[] PodsRunningImage(string imageName, Pulumi.Kubernetes.Apps.V1.Deployment[] pods)
        => pods.Where(d => d.Spec.Value().Template.Spec.Containers.Any(c => c.Image == imageName)).ToArray();

    private static Pulumi.Kubernetes.Apps.V1.Deployment[] PodsWithName(string podName, Pulumi.Kubernetes.Apps.V1.Deployment[] pods)
        => pods.Where(d => d.Spec.Value().Template.Spec.Containers.Any(c => c.Name == podName)).ToArray();

    private static Pulumi.Kubernetes.Apps.V1.Deployment[] PodsWithNameStartingWith(string podName, Pulumi.Kubernetes.Apps.V1.Deployment[] pods)
        => pods.Where(d => d.Spec.Value().Template.Spec.Containers.Any(c => c.Name == podName)).ToArray();

    private static Pulumi.Kubernetes.Apps.V1.Deployment[] PodsWithLabel(KeyValuePair<string, string> label, Pulumi.Kubernetes.Apps.V1.Deployment[] pods)
      => pods.Where(d => d.Spec.Value().Template.Metadata.Labels.Contains(label)).ToArray();

    private static Pulumi.Kubernetes.Apps.V1.Deployment[] PodsWithLabels(Pulumi.Kubernetes.Apps.V1.Deployment[] pods, params KeyValuePair<string, string>[] labels)
      => pods.Where(d => labels.All(l => d.Spec.Value().Template.Metadata.Labels.Contains(l))).ToArray();

    private static Pulumi.Kubernetes.Core.V1.Service[] ServicesWithSelectorLabels(Pulumi.Kubernetes.Core.V1.Service[] services, params KeyValuePair<string, string>[] labels)
      => services.Where(d => labels.All(l => d.Spec.Value().Selector.Contains(l))).ToArray();

    private static Property ContainerHasEnvironmentVariables(Pulumi.Kubernetes.Types.Outputs.Core.V1.Container container, string labelPrefix, params KeyValuePair<string, string>[] environmentVariables)
    {
        var containerHasEnvironmentVariables = true.ToProperty();

        foreach(var environmentVariable in environmentVariables)
        {
            containerHasEnvironmentVariables = containerHasEnvironmentVariables
                .And((container.Env.Any(envVar => envVar.Name == environmentVariable.Key && envVar.Value == environmentVariable.Value))
                .Label($"{labelPrefix} container has environmnet variable {environmentVariable.Key}={environmentVariable.Value}"));
        }

        return containerHasEnvironmentVariables;
    }

    private static Property ServiceMapsToPodExclusively(
        Pulumi.Kubernetes.Core.V1.Service[] services,
        Pulumi.Kubernetes.Apps.V1.Deployment[] pods,
        KeyValuePair<string, string>[] appLabels, 
        string labelPrefix)
    {
        return
            (PodsWithLabels(pods, appLabels).Length == 1)
                .Label($"{labelPrefix} pod labels for identification")

            .And((ServicesWithSelectorLabels(services, appLabels).Length == 1)
                .Label($"{labelPrefix} service is the only service selecting pod: " + ServicesWithSelectorLabels(services, appLabels).Length))

            .And((ServicesWithSelectorLabels(services, appLabels)[0].Spec.Value().Selector.Count == appLabels.Length)
                .Label($"{labelPrefix} service only selects pod"))

            .And((ServicesWithSelectorLabels(services, appLabels)[0].Spec.Value().Ports.Length == 1)
                .Label($"{labelPrefix} service number of ports"))

            .And((ServicesWithSelectorLabels(services, appLabels)[0].Spec.Value().Ports[0].TargetPort.AsT0 == PodsWithLabels(pods, appLabels)[0].Spec.Value().Template.Spec.Containers[0].Ports[0].ContainerPortValue)
                .Label($"{labelPrefix} service targets pod port"))
            ;
    }

    private static Property PodWithOneContainer(
        Pulumi.Kubernetes.Apps.V1.Deployment[] pods,
        KeyValuePair<string, string>[] appLabels,
        string labelPrefix,
        (string Image, int Port) expectedContainerInfo)
        =>
        (PodsWithLabels(pods, appLabels).Length == 1)
            .Label($"{labelPrefix} pod labels for identification")

        .And(() => PodsWithLabels(pods, appLabels)[0].Spec.Value().Template.Spec.Containers.Length == 1,
            $"{labelPrefix} pod's container count")

        .And(() => PodsWithLabels(pods, appLabels)[0].Spec.Value().Template.Spec.Containers[0].Image == expectedContainerInfo.Image,
            $"{labelPrefix} pod container image")

        .And(() => PodsWithLabels(pods, appLabels)[0].Spec.Value().Template.Spec.Containers[0].Ports.Length == 1,
            $"{labelPrefix} pod container - number of ports")

        .And(() => PodsWithLabels(pods, appLabels)[0].Spec.Value().Template.Spec.Containers[0].Ports[0].ContainerPortValue == expectedContainerInfo.Port,
            $"{labelPrefix} pod container port value")
        ;

    private static Property NetworkLayerValidations(string networkLayer, ImmutableArray<Pulumi.Resource> results)
    {
        const int StellarQuickStartHorizonPortDefault = 8000;

        KeyValuePair<string, string>[] stellarCoreLabels = { new("app", "stellar-core"), new("network-layer", networkLayer) };
        KeyValuePair<string, string>[] horizonProxyLabels = { new("app", "horizon-proxy"), new("network-layer", networkLayer) };
        KeyValuePair<string, string>[] horizonProxyEnvironmentVariables = 
        {
            new("PROXIED_URL", $"http://stellar-core-{networkLayer}:8000"),
            new("ASPNETCORE_URLS", "http://+"),
        };

        var pods = results.OfType<Pulumi.Kubernetes.Apps.V1.Deployment>().ToArray();
        var services = results.OfType<Pulumi.Kubernetes.Core.V1.Service>().ToArray();
        var clusterIps = services.Where(s => s.Spec.Value().Type == "ClusterIP").ToArray();

        return
            (PodsWithName($"stellar-core-{networkLayer}", pods).Length == 1)
                .Label($"{networkLayer} Number of pods with name 'stellar-core-{networkLayer}'")

            .And((PodsWithLabels(pods, stellarCoreLabels).Length == 1)
                .Label($"{networkLayer} Stellar Core pod labels for identification"))

            .And((PodsWithName($"stellar-core-{networkLayer}", pods).SequenceEqual(PodsWithLabels(pods, stellarCoreLabels)))
                .Label($"{networkLayer} Stellar Core pod labels"))

            .And((PodsWithName($"stellar-core-{networkLayer}", pods)[0].Spec.Value().Template.Spec.Containers.Length == 1)
                .Label($"{networkLayer} Stellar Core pod's container count"))

            .And((PodsWithName($"stellar-core-{networkLayer}", pods)[0].Spec.Value().Template.Spec.Containers[0].Image == StellarCoreImageName)
                .Label($"{networkLayer} Stellar Core pod container image"))

            .And((PodsWithName($"stellar-core-{networkLayer}", pods)[0].Spec.Value().Template.Spec.Containers[0].Ports.Length == 1)
                .Label($"{networkLayer} Stellar Core pod container - number of ports"))

            .And((PodsWithName($"stellar-core-{networkLayer}", pods)[0].Spec.Value().Template.Spec.Containers[0].Ports[0].ContainerPortValue == StellarQuickStartHorizonPortDefault)
                .Label($"{networkLayer} Stellar Core pod container port value"))

            .And((ServicesWithSelectorLabels(clusterIps, stellarCoreLabels).Length == 1)
                .Label($"{networkLayer} Stellar Core service is the only service selecting Stellar Core pod"))

            .And((ServicesWithSelectorLabels(clusterIps, stellarCoreLabels)[0].Spec.Value().Selector.Count == stellarCoreLabels.Length)
                .Label($"{networkLayer} Stellar Core service only selects Stellar Core pod"))

            .And((ServicesWithSelectorLabels(clusterIps, stellarCoreLabels)[0].Spec.Value().Ports.Length == 1)
                .Label($"{networkLayer} Stellar Core service number of ports"))

            .And((ServicesWithSelectorLabels(clusterIps, stellarCoreLabels)[0].Spec.Value().Ports[0].TargetPort.AsT0 == PodsWithName($"stellar-core-{networkLayer}", pods)[0].Spec.Value().Template.Spec.Containers[0].Ports[0].ContainerPortValue)
                .Label($"{networkLayer} Stellar Core service targets Stellar Core pod port"))

            .And((PodsWithName($"horizon-proxy-{networkLayer}", pods).Length == 1)
                .Label($"{networkLayer} Number of pods with name 'horizon-proxy-{networkLayer}'"))

            .And((PodsWithLabels(pods, horizonProxyLabels).Length == 1)
                .Label($"{networkLayer} Horizon Proxy pod labels for identification"))

            .And((PodsWithName($"horizon-proxy-{networkLayer}", pods)[0].Spec.Value().Template.Spec.Containers[0].Image == HorizonProxyImageName)
                .Label($"{networkLayer} Horizon Proxy pod container image"))

            .And(ContainerHasEnvironmentVariables(
                PodsWithName($"horizon-proxy-{networkLayer}", pods)[0].Spec.Value().Template.Spec.Containers[0],
                $"horizon-proxy-{networkLayer}",
                horizonProxyEnvironmentVariables))

            .And(ServiceMapsToPodExclusively(services, pods, horizonProxyLabels, $"horizon-proxy-{networkLayer}"))

            .And((ServicesWithSelectorLabels(services, horizonProxyLabels)[0].Spec.Value().Type == "LoadBalancer")
                .Label($"{networkLayer} Horizon Proxy service is a load balancer"))

            .And((ServicesWithSelectorLabels(services, horizonProxyLabels)[0].Metadata.Value().Name == $"horizon-proxy-{networkLayer}")
                .Label($"{networkLayer} Horizon Proxy service has a deterministic name"))

        ;
    }

    [FsCheck.NUnit.Property(MaxTest = 1)]
    public Property Test1()
    {
        const int NumProxiesForStellarCore = 2;
        const int NumProxiesForWallet = 1;

        KeyValuePair<string, string>[] apiLabels = { new("app", "api") };
        KeyValuePair<string, string>[] walletLabels = { new("app", "wallet") };

        var results = Pulumi.Deployment.TestAsync<DigitalOceanStack>(new Mocks()).Result;
        var pods = results.OfType<Pulumi.Kubernetes.Apps.V1.Deployment>().ToArray();
        var services = results.OfType<Pulumi.Kubernetes.Core.V1.Service>().ToArray();
        var clusterIps = services.Where(s => s.Spec.Value().Type == "ClusterIP").ToArray();
        var loadBalancers = services.Where(s => s.Spec.Value().Type == "LoadBalancer").ToArray();
        var loadBalancerPortsAscending = loadBalancers.SelectMany(lb => lb.Spec.Value().Ports.Select(p => p.Port)).OrderBy(_ => _);
        var loadBalancerUniquePortsAscending = loadBalancers.SelectMany(lb => lb.Spec.Value().Ports.Select(p => p.Port)).Distinct().OrderBy(_ => _);

        return
            (PodsRunningImage(StellarCoreImageName, pods).Length == 2)
                .Label($"Number of Stellar Core instances")
                
            //.And((PodsRunningImage(HorizonProxyImageName, pods).Length == NumProxiesForStellarCore + NumProxiesForWallet)
            //    .Label($"Number of Horizon Proxy instances"))

            //.And((clusterIps.Length == NumProxiesForStellarCore + NumProxiesForWallet)
            //    .Label("Total number of services with interal IPs"))

            .And(loadBalancerPortsAscending.SequenceEqual(loadBalancerUniquePortsAscending)
                .Label("Unique load balancer ports"))

            .And(NetworkLayerValidations("l1", results))

            .And(NetworkLayerValidations("l2", results))

            // API
            .And(PodWithOneContainer(pods, apiLabels, "API", ("rivalcoins-api:dev", 80)))
            .And(ServiceMapsToPodExclusively(services, pods, apiLabels, "API"))
            .And((PodsWithLabels(pods, apiLabels)[0].Spec.Value().Template.Spec.Containers[0].Env.Count(env => env.Name == "RIVALCOINS_HOME_DOMAIN" && env.Value == "https://rivalcoins.money") == 1)
                .Label("API: home domain"))
            .And((PodsWithLabels(pods, apiLabels)[0].Spec.Value().Template.Spec.Containers[0].Env.Count(env => env.Name == "L1_HORIZON_URL" && env.Value == "https://horizon-l1.rivalcoins.money") == 1)
                .Label("API: L1 Horizon URL"))
            .And((PodsWithLabels(pods, apiLabels)[0].Spec.Value().Template.Spec.Containers[0].Env.Count(env => env.Name == "L2_HORIZON_URL" && env.Value == "https://horizon-l2.rivalcoins.money") == 1)
                .Label("API: L2 Horizon URL"))

            // Wallet
            //.And(PodWithOneContainer(pods, walletLabels, "Wallet", ("wallet:dev", 80)))
            //.And(ServiceMapsToPodExclusively(services, pods, walletLabels, "Wallet"))
            //.And((ServicesWithSelectorLabels(services, new KeyValuePair<string, string>("app", "wallet")).Length == 1)
            //    .Label("Wallet: Number of proxies"))
            //.And((ServicesWithSelectorLabels(services, new KeyValuePair<string, string>("app", "wallet-proxy")).Length == 1)
            //    .Label($"Wallet: Horizon Proxy service is a load balancer"))
            //.And((PodsWithLabels(pods, walletLabels)[0].Spec.Value().Template.Spec.Containers[0].Env.Count(env => env.Name == "L1_HORIZON_URL") == 1)
            //    .Label("Wallet: L1 Horizon URL"))
            //.And((PodsWithLabels(pods, walletLabels)[0].Spec.Value().Template.Spec.Containers[0].Env.Count(env => env.Name == "L2_HORIZON_URL") == 1)
            //    .Label("Wallet: L2 Horizon URL"))
            //.And((PodsWithLabels(pods, walletLabels)[0].Spec.Value().Template.Spec.Containers[0].Env.Count(env => env.Name == "API_URL") == 1)
            //    .Label("Wallet: API URL"))
            //.And((PodsWithLabels(pods, walletLabels)[0].Spec.Value().Template.Spec.Containers[0].Env.Count(env => env.Name == "RIVALCOINS_HOME_DOMAIN") == 1)
            //    .Label("Wallet: Rival Coins home domain URL"))

                ;
    }
}