using FsCheck;
using Grpc.Net.Client;
using LaunchDarkly.EventSource;
using NUnit.Framework;
using RivalCoins.Sdk.Grpc;
using RivalCoins.Sdk.Test.Core;
using Serilog;
using Serilog.Events;
using System.Net.Http;
using RivalCoins.Sdk;
using RivalCoins.Sdk.Test.Core.Generators;
using stellar_dotnet_sdk;
using Util = RivalCoins.Sdk.Util;
using stellar_dotnet_sdk.responses;
using Transaction = stellar_dotnet_sdk.Transaction;

namespace RivalCoins.Server.Test;

[TestFixture]
public class ServerTests : TestClassBase
{
    private RivalCoinsService.RivalCoinsServiceClient _client;
    private GrpcChannel _channel;

    protected override void OnOneTimeSetUp()
    {
        base.OnOneTimeSetUp();

        _channel = GrpcChannel.ForAddress("https://localhost:7123");
        _client = new RivalCoinsService.RivalCoinsServiceClient(_channel);
    }

    protected override void OnOneTimeTearDown()
    {
        base.OnOneTimeTearDown();

        _channel.Dispose();
    }

    private const string L1HorizonUrl = "https://localhost:8001";
    private const string L2HorizonUrl = "https://localhost:9001";

    [FsCheck.NUnit.Property(MaxTest = 1)]
    public Property SyncAccount()
    {
        var balances = 
            // L1 balance
            Gen.Choose(0, int.MaxValue / 2)
            
            // Difference between L1 & L2 balance (absolute value)
            .Select(l1Balance => (L1Balance: l1Balance, DifferenceAbsoluteValue: l1Balance / 2))

            // L2 balance
            .Zip(Gen.Elements(-1, 0, 1))
            .Select(_ => (L1Balance: _.Item1.L1Balance, L2Balance: _.Item1.L1Balance + (_.Item1.DifferenceAbsoluteValue * _.Item2)))
            ;

        return Prop.ForAll(balances.ToArbitrary(), KeyPairGenerator.Generate(), (b, recipient) =>
        {
            //Console.WriteLine($"L1: {b.L1Balance}, L2: {b.L2Balance}");
            Wallet.CreateAccountAsync(recipient, L1HorizonUrl).Wait();


            Wallet.CreateAccountAsync(recipient, L2HorizonUrl).Wait();



        });
        // return (success.Message == "This is a test").ToProperty();
    }

    [FsCheck.NUnit.Property(MaxTest = 1)]
    public Property Airdrop()
    {
        var balances =
                // L1 balance
                Gen.Choose(0, int.MaxValue / 2)

                    // Difference between L1 & L2 balance (absolute value)
                    .Select(l1Balance => (L1Balance: l1Balance, DifferenceAbsoluteValue: l1Balance / 2))

                    // L2 balance
                    .Zip(Gen.Elements(-1, 0, 1))
                    .Select(_ => (L1Balance: _.Item1.L1Balance, L2Balance: _.Item1.L1Balance + (_.Item1.DifferenceAbsoluteValue * _.Item2)))
            ;

        return Prop.ForAll(balances.ToArbitrary(), KeyPairGenerator.Generate(), (b, recipient) =>
        {
            // create L1 account
            Wallet.CreateAccountAsync(recipient, L1HorizonUrl).Wait();
            using var l1Recipient = new Wallet(L1HorizonUrl, recipient.SecretSeed, "https://dev.rivalcoins.money");
            l1Recipient.InitializeAsync().Wait();

            // create L2 account
            Wallet.CreateAccountAsync(recipient, L2HorizonUrl).Wait();
            using var l2Recipient = new Wallet(L2HorizonUrl, recipient.SecretSeed, "https://dev.rivalcoins.money");
            l2Recipient.InitializeAsync().Wait();

            // wrapped asset issuer is the same on L1 & L2 network
            var wrappedAsset = l1Recipient.Server.Assets.AssetCode("FakeMONEY").Execute().Result.Records.First();

            // trust wrapped asset
            var txL1TrustWrappedAsset = new TransactionBuilder(l1Recipient.Account.Info);
            Util.CreateTrustline(wrappedAsset.Asset, l1Recipient.Account.Signer!, txL1TrustWrappedAsset);
            var success = l1Recipient.SubmitTransactionAsync(txL1TrustWrappedAsset.Build(), true, $"{l1Recipient.Account.Signer!.Address} trust {wrappedAsset.Asset.CanonicalName()}").Result;
            if (success?.IsSuccess() is not true)
            {
                throw new Exception();
            }

            var txL2TrustWrappedAsset = new TransactionBuilder(l2Recipient.Account.Info);
            Util.CreateTrustline(wrappedAsset.Asset, l2Recipient.Account.Signer!, txL2TrustWrappedAsset);
            success = l2Recipient.SubmitTransactionAsync(txL2TrustWrappedAsset.Build(), true, $"{l2Recipient.Account.Signer!.Address} trust {wrappedAsset.Asset.CanonicalName()}").Result;
            if (success?.IsSuccess() is not true)
            {
                throw new Exception();
            }

            var airdropSuccess = _client.Airdrop(new() { RecipientAddress = recipient.Address, Asset = wrappedAsset.Asset.CanonicalName() });

            return airdropSuccess.Success_.Label($"Airdrop unsuccessful: {airdropSuccess.Message}");
        });
    }

    [Test]
    public void Restart()
    {
        this.RestartContainers();
    }

    private async Task<(Wallet L1, Wallet L2)> CreateUserAccountAsync(KeyPair user)
    {
        // create L1 account
        await Wallet.CreateAccountAsync(user, L1HorizonUrl);
        var l1User = new Wallet(L1HorizonUrl, user.SecretSeed, "https://dev.rivalcoins.money");
        await l1User.InitializeAsync();

        // create L2 account
        await Wallet.CreateAccountAsync(user, L2HorizonUrl);
        var l2User = new Wallet(L2HorizonUrl, user.SecretSeed, "https://dev.rivalcoins.money");
        await l2User.InitializeAsync();

        return (l1User, l2User);
    }

    private async Task TrustAssetAsync(Wallet recipient, Asset asset)
    {
        var txL2TrustWrappedAsset = new TransactionBuilder(recipient.Account.Info);
        Util.CreateTrustline(asset, recipient.Account.Signer!, txL2TrustWrappedAsset);
        var success = await recipient.SubmitTransactionAsync(txL2TrustWrappedAsset.Build(), true, $"{recipient.Account.Signer!.Address} trust {asset.CanonicalName()}");
        if (success?.IsSuccess() is not true)
        {
            throw new Exception();
        }
    }

    [FsCheck.NUnit.Property(MaxTest = 1)]
    public Property Swap()
    {
        var recipient = this.CreateUserAccountAsync(KeyPair.Random()).Result;

        // airdrop the wrapped asset
        var wrappedAsset = recipient.L1.Server.Assets.AssetCode("FakeMONEY").Execute().Result.Records.First().Asset;
        this.TrustAssetAsync(recipient.L1, wrappedAsset).Wait();
        this.TrustAssetAsync(recipient.L2, wrappedAsset).Wait();
        _client.Airdrop(new() { RecipientAddress = recipient.L1.Account.Info.AccountId, Asset = wrappedAsset.CanonicalName() });
        var wrappedAssetBalance = recipient.L1.Server.Accounts.Account(recipient.L1.Account.Info.AccountId).Result;

        // trust the wrapper asset
        var wrapperAsset = recipient.L2.Server.Assets.AssetCode("JeromeBellSr").Execute().Result.Records.First().Asset;
        this.TrustAssetAsync(recipient.L2, wrapperAsset).Wait();

        var quantity = double.Parse(wrappedAssetBalance.Balances.First(b => b.Asset.CanonicalName() == wrappedAsset.CanonicalName()).BalanceString) / 2;

        // Act
        var result = _client.Swap(new()
        {
            SwapOut = wrappedAsset.CanonicalName(), 
            SwapIn = wrapperAsset.CanonicalName(), 
            Quantity = quantity.ToStellarQuantityString(),
            User = recipient.L2.Account.Info.AccountId
        });
        var txSwap = Transaction.FromEnvelopeXdr(result.SignedXdr);
        var txResult = recipient.L2.SubmitTransactionAsync(txSwap, true, "Swap").Result;

        var l2Balances = recipient.L2.Server.Accounts.Account(recipient.L2.Account.Info.AccountId).Result;
        var wrapperAssetBalance = l2Balances.Balances.First(b => b.Asset.CanonicalName() == wrapperAsset.CanonicalName());

        // Assert
        return
            (result.Success)
                .Label($"Failed: {result.Message}")

            .And(txResult?.IsSuccess() is true)
                .Label("Transaction successfully submitted")

            .And(double.Parse(wrapperAssetBalance.BalanceString).ToStroops() == quantity.ToStroops())
                .Label("Wrapper asset balance");
    }

    [FsCheck.NUnit.Property(MaxTest = 1)]
    public Property Sync()
    {
        var recipient = this.CreateUserAccountAsync(KeyPair.Random()).Result;

        // airdrop the wrapped asset
        var wrappedAsset = recipient.L1.Server.Assets.AssetCode("FakeMONEY").Execute().Result.Records.First().Asset;
        this.TrustAssetAsync(recipient.L1, wrappedAsset).Wait();
        this.TrustAssetAsync(recipient.L2, wrappedAsset).Wait();
        _client.Airdrop(new() { RecipientAddress = recipient.L1.Account.Info.AccountId, Asset = wrappedAsset.CanonicalName() });
        var wrappedAssetBalance = recipient.L1.Server.Accounts.Account(recipient.L1.Account.Info.AccountId).Result;

        // trust the wrapper asset
        var wrapperAsset = recipient.L2.Server.Assets.AssetCode("JeromeBellSr").Execute().Result.Records.First().Asset;
        this.TrustAssetAsync(recipient.L2, wrapperAsset).Wait();

        var quantity = double.Parse(wrappedAssetBalance.Balances.First(b => b.Asset.CanonicalName() == wrappedAsset.CanonicalName()).BalanceString) / 2;

        // Act
        var result = _client.Swap(new()
        {
            SwapOut = wrappedAsset.CanonicalName(),
            SwapIn = wrapperAsset.CanonicalName(),
            Quantity = quantity.ToStellarQuantityString(),
            User = recipient.L2.Account.Info.AccountId
        });
        var txSwap = Transaction.FromEnvelopeXdr(result.SignedXdr);
        var txResult = recipient.L2.SubmitTransactionAsync(txSwap, true, "Swap").Result;

        var l2Balances = recipient.L2.Server.Accounts.Account(recipient.L2.Account.Info.AccountId).Result;
        var wrapperAssetBalance = l2Balances.Balances.First(b => b.Asset.CanonicalName() == wrapperAsset.CanonicalName());

        // Assert
        return
            (result.Success)
                .Label($"Failed: {result.Message}")

            .And(txResult?.IsSuccess() is true)
                .Label("Transaction successfully submitted")

            .And(double.Parse(wrapperAssetBalance.BalanceString).ToStroops() == quantity.ToStroops())
                .Label("Wrapper asset balance");
    }
}