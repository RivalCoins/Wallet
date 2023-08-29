using FsCheck;
using stellar_dotnet_sdk;

namespace RivalCoins.Airdrop.Test.Common.Generators;

public static class KeyPairGenerator
{
    public static Arbitrary<KeyPair> Generate() => CommonGenerator.KeyPair.ToArbitrary();
}