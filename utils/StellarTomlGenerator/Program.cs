using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RivalCoins.Sdk;
using Tommy;

using IHost host = Host.CreateDefaultBuilder(args).Build();
var config = host.Services.GetService<IConfiguration>()!;

var toml = new TomlTable();
using var sw = new StringWriter();

// version
toml.Add("Version", new TomlString() { Value = "2.0.0" });

// accounts
var accounts = new TomlArray();
toml.Add("Accounts", accounts);
accounts.Add(config["FAKE_USA_ISSUER"]);
accounts.Add(config["FAKE_USA_WRAPPER_ISSUER"]);
accounts.Add(config["FAKE_USA_WRAPPER_DISTRIBUTOR"]);

// URI request signing key
toml.Add("URI_REQUEST_SIGNING_KEY", new TomlString() { Value = config["URI_REQUEST_SIGNING_KEY"] });

// documentation
var documentation = new TomlTable
{
    { "ORG_NAME", "Rival Coins" },
    { "ORG_DBA", "Rival Coins" },
    { "ORG_URL", config["RIVALCOINS_HOME_DOMAIN"] },
    { "ORG_LOGO", $"{config["RIVALCOINS_HOME_DOMAIN"]}/wp-content/uploads/2021/06/logo-500x500-1.png" },
    { "ORG_DESCRIPTION", "Rival Coins is putting YOU on the face of money, if Benjamin Franklin can be on a $100 bill, then why can’t you?" },
    { "ORG_PHYSICAL_ADDRESS", "Chicago, IL" },
    { "ORG_TWITTER", "RivalCoins" },
    { "ORG_OFFICIAL_EMAIL", "hello@rivalcoins.money" },
};
toml.Add("DOCUMENTATION", documentation);

// principals
var principals = new TomlTable
{
    { "name", "Jerome Bell" },
    { "email", "jerome.bell@rivalcoins.money" },
    { "twitter", "jeromebelljr" },
    { "github", "heteroculturalism" },
};
toml.Add("PRINCPALS", principals);

// assets
var rivalCoins = new TomlArray() { IsTableArray = true };

// wrapped asset
var wrapped = new TomlTable
{
    { "code", new TomlString() { Value = "FakeUSA" } },
    { "issuer", new TomlString() { Value = config["FAKE_USA_ISSUER"] } },
    { "display_decimals", new TomlInteger() { Value = 7 } },
    { "name", new TomlString() { Value = "Fake USA" } },
    { "desc", new TomlString() { Value = "Fake, pretend, 'Monopoly' money used for teaching about the US economy." } },
    { "is_asset_anchored", new TomlBoolean() { Value = false } },
    { "image", new TomlString() { Value = $"{config["RIVALCOINS_HOME_DOMAIN"]}/wp-content/uploads/2021/06/logo-500x500-1.png" } }
};
rivalCoins.Add(wrapped);

// Rival Coins
foreach (var rivalCoinConfig in  config.GetSection("RIVAL_COINS").GetChildren())
{
    var rivalCoin = new TomlTable
    {
        { "code", new TomlString() { Value = rivalCoinConfig["code"] } },
        { "issuer", new TomlString() { Value = config["FAKE_USA_WRAPPER_ISSUER"] } },
        { "display_decimals", new TomlInteger() { Value = 7 } },
        { "name", new TomlString() { Value = rivalCoinConfig["name"] } },
        { "desc", new TomlString() { Value = rivalCoinConfig["desc"] } },
        { "is_asset_anchored", new TomlBoolean() { Value = false } },
        { "image", new TomlString() { Value = $"{config["RIVALCOINS_HOME_DOMAIN"]}{rivalCoinConfig["image"]}" } }
    };

    rivalCoins.Add(rivalCoin);
}

toml.WriteTo(sw);
sw.WriteLine();
rivalCoins.WriteTo(sw, "CURRENCIES");

await File.WriteAllTextAsync($"{Util.OutputFolder}/stellar-{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")}.toml", sw.ToString());
