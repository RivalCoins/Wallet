using NUnit.Framework;
using stellar_dotnet_sdk;
using System.Diagnostics;
using FsCheck;
using Serilog;
using Serilog.Events;

namespace RivalCoins.Sdk.Test.Core;

public class TestClassBase
{
    private readonly List<string> _containers = new();

    private (string ContainerName, string ContainerImage, (int Host, int Container) PortForward) L1Network { get; set; } = ("stellar-l1-test", "registry.digitalocean.com/rivalcoins/stellar-quickstart:v0.1.5", (8000, 8000));
    private (string ContainerName, string ContainerImage, (int Host, int Container) PortForward) L1HorizonProxy { get; set; } = ("horizonproxy-l1-test", "horizonproxy:test", (8001, 443));
    private (string ContainerName, string ContainerImage, (int Host, int Container) PortForward) L2Network { get; set; } = ("stellar-l2-test", "registry.digitalocean.com/rivalcoins/stellar-quickstart:v0.1.5", (9000, 8000));
    private (string ContainerName, string ContainerImage, (int Host, int Container) PortForward) L2HorizonProxy { get; set; } = ("horizonproxy-l2-test", "horizonproxy:test", (9001, 443));
    private Wallet Wallet { get; set; }

    #region Setup / Tear Down

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        Console.SetOut(TestContext.Progress);

        //Process.Start("docker", $"kill {this.L1Network.ContainerName}").WaitForExit();
        //Process.Start("docker", $"kill {this.L1HorizonProxy.ContainerName}").WaitForExit();
        //Process.Start("docker", $"kill {this.L2Network.ContainerName}").WaitForExit();
        //Process.Start("docker", $"kill {this.L2HorizonProxy.ContainerName}").WaitForExit();

        //var sslConfiguration =
        //    new List<KeyValuePair<string, string>>()
        //    {
        //        new("ASPNETCORE_URLS", "https://+;http://+"),
        //        new("ASPNETCORE_Kestrel__Certificates__Default__Password", "password1"),
        //        new("ASPNETCORE_Kestrel__Certificates__Default__Path", "/https/aspnetapp.pfx")
        //    }.ToArray();

        //// Horizon L1 proxy
        //this.StartContainer(
        //    this.L1HorizonProxy.ContainerName,
        //    this.L1HorizonProxy.ContainerImage,
        //    null,
        //    this.L1HorizonProxy.PortForward,
        //    sslConfiguration.Append(new("PROXIED_URL", $"http://host.docker.internal:{this.L1Network.PortForward.Host}")).ToList());

        //// Stellar L1 network
        //this.StartContainer(
        //    this.L1Network.ContainerName,
        //    this.L1Network.ContainerImage,
        //    "--standalone --enable-core-artificially-accelerate-time-for-testing",
        //    this.L1Network.PortForward,
        //    null);

        //// Horizon L2 proxy
        //this.StartContainer(
        //    this.L2HorizonProxy.ContainerName,
        //    this.L2HorizonProxy.ContainerImage,
        //    null,
        //    this.L2HorizonProxy.PortForward,
        //    sslConfiguration.Append(new("PROXIED_URL", $"http://host.docker.internal:{this.L2Network.PortForward.Host}")).ToList());

        //// Stellar L2 network
        //this.StartContainer(
        //    this.L2Network.ContainerName,
        //    this.L2Network.ContainerImage,
        //    "--standalone --enable-core-artificially-accelerate-time-for-testing",
        //    this.L2Network.PortForward,
        //    null);

        //Task.Delay(40 * 1000).Wait();

        this.OnOneTimeSetUp();
    }

    protected void RestartContainers()
    {
        Process.Start("docker", $"kill {this.L1Network.ContainerName}").WaitForExit();
        Process.Start("docker", $"kill {this.L1HorizonProxy.ContainerName}").WaitForExit();
        Process.Start("docker", $"kill {this.L2Network.ContainerName}").WaitForExit();
        Process.Start("docker", $"kill {this.L2HorizonProxy.ContainerName}").WaitForExit();

        var sslConfiguration =
            new List<KeyValuePair<string, string>>()
            {
                new("ASPNETCORE_URLS", "https://+;http://+"),
                new("ASPNETCORE_Kestrel__Certificates__Default__Password", "password1"),
                new("ASPNETCORE_Kestrel__Certificates__Default__Path", "/https/aspnetapp.pfx")
            }.ToArray();

        // Horizon L1 proxy
        this.StartContainer(
            this.L1HorizonProxy.ContainerName,
            this.L1HorizonProxy.ContainerImage,
            null,
            this.L1HorizonProxy.PortForward,
            sslConfiguration.Append(new("PROXIED_URL", $"http://host.docker.internal:{this.L1Network.PortForward.Host}")).ToList());

        // Stellar L1 network
        this.StartContainer(
            this.L1Network.ContainerName,
            this.L1Network.ContainerImage,
            "--standalone --enable-core-artificially-accelerate-time-for-testing",
            this.L1Network.PortForward,
            null);

        // Horizon L2 proxy
        this.StartContainer(
            this.L2HorizonProxy.ContainerName,
            this.L2HorizonProxy.ContainerImage,
            null,
            this.L2HorizonProxy.PortForward,
            sslConfiguration.Append(new("PROXIED_URL", $"http://host.docker.internal:{this.L2Network.PortForward.Host}")).ToList());

        // Stellar L2 network
        this.StartContainer(
            this.L2Network.ContainerName,
            this.L2Network.ContainerImage,
            "--standalone --enable-core-artificially-accelerate-time-for-testing",
            this.L2Network.PortForward,
            null);

        Task.Delay(40 * 1000).Wait();
    }
    protected virtual void OnOneTimeSetUp()
    {
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        //foreach (var container in _containers)
        //{
        //    Process.Start("docker", $"kill {container}").WaitForExit();
        //}

        this.OnOneTimeTearDown();
    }

    protected virtual void OnOneTimeTearDown()
    {
    }

    private void StartContainer(
        string containerName, 
        string containerImage,
        string? containerOptions,
        (int Host, int Container) portForward,
        List<KeyValuePair<string,string>>? environmentVariables)
    {
        var dockerContainer = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = @$"run --rm -it {environmentVariables?.Select(e => $"-e {e.Key}={e.Value}").Aggregate((aggregated, next) => $"{aggregated} {next}")} -p ""{portForward.Host}:{portForward.Container}"" -v {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aspnet\\https")}:/https/ --name {containerName} {containerImage} {containerOptions}",
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        dockerContainer.Start();

        Task.Run(() =>
        {
            while (dockerContainer.StandardOutput.ReadLine() is { } standardOutput)
            {
                Console.WriteLine(standardOutput);
            }
        });

        _containers.Add(containerName);
    }

    #endregion Setup / Tear Down
}