using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.Extensions.Configuration;
using stellar_dotnet_sdk;
using Tommy;
using static System.Net.Mime.MediaTypeNames;

const string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
        b =>
        {
            b
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});

builder.Configuration.AddEnvironmentVariables();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseCors();

//app.UseHttpsRedirection();

string GetRivalCoin(AssetTypeCreditAlphaNum wrapped, AssetTypeCreditAlphaNum wrapper)
{
    var toml = new TomlTable();
    var rivalCoins = new TomlArray() { IsTableArray = true };
    var rivalCoin = new TomlTable();
    
    toml.Add("CURRENCIES", rivalCoins);
    rivalCoins.Add(rivalCoin);

    rivalCoin.Add("code", new TomlString() { Value = wrapper.Code });
    rivalCoin.Add("issuer", new TomlString() { Value = wrapper.Issuer });
    rivalCoin.Add("display_decimals", new TomlInteger() { Value = 7 });
    rivalCoin.Add("name", new TomlString() { Value = $"{wrapper.Code} ({wrapped.Code})" });
    rivalCoin.Add("desc", new TomlString() { Value = $"My name is {wrapper.Code}" });
    rivalCoin.Add("is_asset_anchored", new TomlBoolean() { Value = false });
    rivalCoin.Add("image", new TomlString() { Value = "https://rivalcoins.money/wp-content/uploads/2021/06/logo-500x500-1.png" });

    using var sw = new StringWriter();
    toml.WriteTo(sw);

    return sw.ToString();
}

Console.WriteLine($"Fake USA Issuer: {app.Configuration.GetValue<string>("FAKE_USA_ISSUER_SEED")}");
Console.WriteLine($"Fake USA Issuer Length: {app.Configuration.GetValue<string>("FAKE_USA_ISSUER_SEED")!.Length}");
Console.WriteLine($"Fake USA Wrapper Issuer: {app.Configuration.GetValue<string>("FAKE_USA_WRAPPER_ISSUER_SEED")}");
var fakeUsaIssuer = KeyPair.FromSecretSeed(app.Configuration.GetValue<string>("FAKE_USA_ISSUER_SEED")!);
var fakeUsa = Asset.CreateNonNativeAsset("FakeUSA", fakeUsaIssuer.AccountId);
var fakeUsaWrapperIssuer = KeyPair.FromSecretSeed(app.Configuration.GetValue<string>("FAKE_USA_WRAPPER_ISSUER_SEED")!);
var rivalCoins = new[] { "SantaClaus", "ToothFairy", "EasterBunny" };
var rivalCoinDescriptions =
@$"[[CURRENCIES]]
code = ""FakeUSA""
issuer = ""{fakeUsaIssuer.AccountId}""
display_decimals = 7
name = ""Fake USA""
desc = ""Fake, pretend, 'Monopoly' money used for teaching about the US economy.""
is_asset_anchored = false
image = ""https://rivalcoins.money/wp-content/uploads/2021/06/logo-500x500-1.png""


"
    +
    rivalCoins
        .Select(rivalCoin => GetRivalCoin(fakeUsa, Asset.CreateNonNativeAsset(rivalCoin, fakeUsaWrapperIssuer.AccountId)))
        .Aggregate((accumulated, next) => $"{accumulated}{Environment.NewLine}{Environment.NewLine}{next}");

app.MapGet("/.well-known/stellar.toml", () => TypedResults.Text(rivalCoinDescriptions))
.RequireCors(MyAllowSpecificOrigins);

app.Run();