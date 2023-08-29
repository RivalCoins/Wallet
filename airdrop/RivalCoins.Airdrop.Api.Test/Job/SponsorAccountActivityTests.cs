using FsCheck;
using RivalCoins.Airdrop.Test.Common.Generators;
using RivalCoins.Sdk;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Test.Common;
using stellar_dotnet_sdk.responses;
using Constants = RivalCoins.Airdrop.Common.Constants;

namespace RivalCoins.Airdrop.Api.Test.Job;

public static class PropertyExtensions
{
    public static Property And(this Property property, bool? condition) =>
        property.And(condition != null && condition.Value);
}

public class SponsorAccountActivityTests : TestClassBase
{
    private Server _server;

    #region Setup

    protected override void OnSetup()
    {
        base.OnSetup();

        _server = new Server("https://localhost:8001");
    }

    #endregion Setup

    #region Tests

    public static class WalletGenerator
    {
        public static Arbitrary<Wallet> Generator() => CommonGenerator.InitializedWallet.ToArbitrary();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(WalletGenerator), typeof(KeyPairGenerator) })]
    public Property Test1(Wallet sponsor, KeyPair accountToSponsor)
    {
        // Arrange

        // Act

        // Assert

        var actualAccountToSponsor = _server.Accounts.AccountAsync(accountToSponsor.AccountId).Result;
        var tx = new TransactionBuilder(actualAccountToSponsor);

        return 
            (actualAccountToSponsor != null)
                .Label("Sponsored account exists")
            
            .And(actualAccountToSponsor?.Balances.Any(b => b.Asset.CanonicalName() == Constants.USA.CanonicalName()))
                .Label("Accepts USA")

            .And(actualAccountToSponsor?.Balances.Any(b => b.Asset.CanonicalName() == Constants.GovFundRewards.CanonicalName()))
                .Label("Subscribed to Gov Fund Rewards")

            .And(actualAccountToSponsor?.Signers.Length > 1)
                .Label("Multi-signature account")

            .And(actualAccountToSponsor?.Signers.Any(signer => signer.Key == actualAccountToSponsor.AccountId))
                .Label("Sponsored account ")
            ;
    }

    #endregion Tests

}