using FsCheck;
using Microsoft.Extensions.Primitives;
using RivalCoins.Sdk;
using stellar_dotnet_sdk;

namespace RivalCoins.Airdrop.Test.Common.Generators;

public static class CommonGenerator
{
    public static Gen<KeyPair> KeyPair =>
        Gen.ListOf(32, Arb.Default.Byte().Generator)
            .Select(seed => stellar_dotnet_sdk.KeyPair.FromSecretSeed(seed.ToArray()));

    public static Gen<Wallet> Wallet =>
        KeyPair.Select(account => new Sdk.Wallet("https://localhost:8001", account.SecretSeed, "https://localhost:7777"));

    public static Gen<Wallet> InitializedWallet =>
        Wallet.Select(wallet =>
        {
            Sdk.Wallet.CreateAccountAsync(
                stellar_dotnet_sdk.KeyPair.FromSecretSeed(wallet.AccountSecretSeed),
                wallet.NetworkUrl).Wait();

            wallet.InitializeAsync().Wait();

            return wallet;
        });

    public static Gen<string?> MalformedStellarAccountId =>
        Gen.OneOf(
            Gen.Constant(null as string),
            Gen.Constant((string?)string.Empty),
            Gen.Constant((string?)" "),
            Gen.Constant((string?)"_DXO2ZEKAZ23Z2CK74FHLY2O7JRJVABMYQNLWE5ANGY4XZRNBSN24GIY"));

    public static Gen<KeyPair> GetAccount(this Gen<Wallet> wallet) =>
        wallet.Select(w => stellar_dotnet_sdk.KeyPair.FromSecretSeed(w.AccountSecretSeed));

    public static Gen<string> GetAccountId(this Gen<Wallet> wallet) =>
        wallet.Select(w => stellar_dotnet_sdk.KeyPair.FromSecretSeed(w.AccountSecretSeed).AccountId);

    public static Gen<T?> Nullable<T>(this Gen<T> g) where T : class
        => g.Select(value => (T?)value);

    public static Gen<string?> Nullable(this Gen<string> g) => g.Select(value => (string?)value);

    public static Gen<StringValues> StringValues(this Gen<string> g) => g.Select(value => new StringValues(value));

    public static Gen<StringValues> StringValuesFromNullable(this Gen<string?> g) => g.Select(value => new StringValues(value));

    public static Gen<Wallet> NoTrustline(this Gen<Wallet> wallet, AssetTypeCreditAlphaNum asset) =>
        wallet
            .Zip(Gen.Constant(asset))
            .Apply(
                Gen.Constant((Tuple<Wallet, AssetTypeCreditAlphaNum> trustlineRequest) =>
                {
                    var assetBalance = trustlineRequest.Item1.Account.Info.Balances.FirstOrDefault(b => b.Asset.CanonicalName() == trustlineRequest.Item2.CanonicalName());
                    if (assetBalance != null)
                    {
                        Helper.CreateTrustlineAsync(trustlineRequest.Item2, trustlineRequest.Item1, 0.0).Wait();
                    }

                    return trustlineRequest.Item1;
                }));

    public static Gen<Wallet> WithTrusline(this Gen<Wallet> wallet, AssetTypeCreditAlphaNum asset) =>
        wallet
            .Zip(Gen.Constant(asset))
            .Apply(
                Gen.Constant((Tuple<Wallet, AssetTypeCreditAlphaNum> trustlineRequest) =>
                {
                    Helper.CreateTrustlineAsync(trustlineRequest.Item2, trustlineRequest.Item1).Wait();

                    return trustlineRequest.Item1;
                }));

}