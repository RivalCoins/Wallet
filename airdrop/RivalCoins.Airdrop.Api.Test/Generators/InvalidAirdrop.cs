using FsCheck;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Test.Common;
using RivalCoins.Sdk;
using stellar_dotnet_sdk;
using Constants = RivalCoins.Airdrop.Common.Constants;

namespace RivalCoins.Airdrop.Api.Test.Generators;

public static class InvalidAirdrop
{
    public static Arbitrary<Airdrop.Common.Repository.Queue.Model.Airdrop> Generate()
    {
        const double MaxLimit = 10.0;

        var asset = Arb.Default.Bool().Generator.Select(airdropForSupportedAsset =>
        {
            var airdrop = new Airdrop.Common.Repository.Queue.Model.Airdrop();

            airdrop.Asset = airdropForSupportedAsset ? Constants.USA.CanonicalName() : $"NotSupported:{KeyPair.Random().AccountId}";

            return airdrop;
        });

        var assetAndQuantity = asset.Zip(Arb.Default.Bool().Generator).Select(airdropWithoutQuantity =>
        {
            airdropWithoutQuantity.Item1.Quantity = airdropWithoutQuantity.Item2 ? MaxLimit : MaxLimit + 1.0;

            return airdropWithoutQuantity.Item1;
        });

        var assetAndQuantityAndPayDate = assetAndQuantity.Zip(Gen.Choose(-1, 1)).Select(airdropWithoutPayDate =>
        {
            airdropWithoutPayDate.Item1.PayDate = new DateTime(DateTime.Now.Year, 1, 1).AddDays(airdropWithoutPayDate.Item2);

            return airdropWithoutPayDate.Item1;
        });

        var airdrop = assetAndQuantityAndPayDate.Zip(Arb.Default.Bool().Generator).Select(airdropWithoutAccount =>
        {
            Wallet? wallet = null;

            if (airdropWithoutAccount.Item2)
            {
                airdropWithoutAccount.Item1.StellarAccoutId = KeyPair.Random().AccountId;
            }
            else
            {
                wallet = new Wallet("https://localhost:8001", KeyPair.Random().SecretSeed, "https://localhost:7777");
                Wallet.CreateAccountAsync(KeyPair.FromSecretSeed(wallet.AccountSecretSeed), wallet.NetworkUrl).Wait();
                wallet.InitializeAsync().Wait();
            }

            return (Airdrop: airdropWithoutAccount.Item1, Wallet: wallet);
        })
        .Zip(Arb.Default.Bool().Generator).Select(airdropWithoutQuantity =>
        {
            airdropWithoutQuantity.Item1.Airdrop.Quantity = airdropWithoutQuantity.Item2 ? MaxLimit : MaxLimit + 1.0;

            return airdropWithoutQuantity.Item1;
        })
        .Zip(Arb.Default.Bool().Generator).Select(airdropWithoutTrustline =>
        {
            if (airdropWithoutTrustline.Item1.Wallet != null && airdropWithoutTrustline.Item1.Airdrop.Asset == Constants.USA.CanonicalName())
            {
                Helper.CreateTrustlineAsync(
                    (AssetTypeCreditAlphaNum)Asset.Create(airdropWithoutTrustline.Item1.Airdrop.Asset),
                    airdropWithoutTrustline.Item1.Wallet,
                    MaxLimit).Wait();
            }

            return airdropWithoutTrustline.Item1.Airdrop;
        });

        return airdrop.ToArbitrary();
    }
}