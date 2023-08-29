using FsCheck;
using FsCheck.NUnit;
using RivalCoins.Airdrop.Test.Common;
using RivalCoins.Airdrop.Test.Common.Generators;
using RivalCoins.Sdk;
using stellar_dotnet_sdk;

namespace RivalCoins.Airdrop.Common.Tests;

public class HelperTests : TestClassBase
{
    #region Tests

    public static class WalletGenerator
    {
        public static Arbitrary<Wallet> Generator() => CommonGenerator.InitializedWallet.ToArbitrary();
    }

    [Property(Arbitrary = new[] { typeof(WalletGenerator), typeof(KeyPairGenerator) })]
    public Property Test1(Wallet sponsor, KeyPair nonExistentAccount)
    {
        // Arrange

        // Act

        // Assert
        return true.ToProperty();
    }

    #endregion Tests
}