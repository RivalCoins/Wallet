using FsCheck;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses.page;

namespace RivalCoins.Sdk.Test.Core;

public static class Validations
{
    public static Property AssetExists(AssetTypeCreditAlphaNum asset, Wallet wallet)
    {
        var assetInfo = wallet.Server.Assets.AssetCode(asset.Code).AssetIssuer(asset.Issuer).Execute().Result;

        return
           (assetInfo.Records.Count == 1)
                .Label("Asset exists");
    }

    public static Property AssetExists(this Property property, AssetTypeCreditAlphaNum asset, Wallet wallet) => property.And(AssetExists(asset, wallet));

    public static Property ImmutableAccount(KeyPair account, Wallet wallet)
    {
        var networkAccount = wallet.Server.Accounts.Account(account.AccountId).Result;

        return
            (networkAccount.Thresholds.LowThreshold > networkAccount.Signers.Sum(signer => signer.Weight))
                .Label("Disabled low threshold operations")

            .And(networkAccount.Thresholds.MedThreshold > networkAccount.Signers.Sum(signer => signer.Weight))
                .Label("Disabled medium threshold operations")

            .And(networkAccount.Thresholds.HighThreshold > networkAccount.Signers.Sum(signer => signer.Weight))
                .Label("Disabled high threshold operations");
    }

    public static Property ImmutableAccount(this Property property, KeyPair account, Wallet wallet) => property.And(ImmutableAccount(account, wallet));

    public static Property AssetHoldersHaveFullCustody(AssetTypeCreditAlphaNum asset, Wallet wallet)
    {
        var assetInfo = wallet.Server.Assets.AssetCode(asset.Code()).AssetIssuer(asset.Issuer).Execute().Result.Records.First();
        
        return
            AssetExists(asset, wallet)
                
            .And(!assetInfo.Flags.AuthRequired)
                .Label($"{asset.CanonicalName()} shall not require authorization to hold.")

            .And(!assetInfo.Flags.AuthRevocable)
                .Label($"{asset.CanonicalName()} shall not be freezeable.")

            .And(assetInfo.Flags.AuthImmutable)
                .Label($"{asset.CanonicalName()} authorization settings shall be immutable and the issuing account not be deletable.");
    }

    public static Property AssetHoldersHaveFullCustody(this Property property, AssetTypeCreditAlphaNum asset, Wallet wallet) => property.And(AssetHoldersHaveFullCustody(asset, wallet));

    public static Property HomeDomain(KeyPair account, Wallet wallet)
    {
        var networkAccount = wallet.Server.Accounts.Account(account.AccountId).Result;

        return
            (networkAccount.HomeDomain == wallet.HomeDomain)
                .Label("Home domain");
    }

    public static Property HomeDomain(this Property property, KeyPair account, Wallet wallet) => property.And(HomeDomain(account, wallet));

    public static Property AssetSupply(string assetCode, (KeyPair Issuing, List<KeyPair> Distributions) currency, Wallet wallet)
    {
        var asset = wallet.Server.Assets.AssetCode(assetCode).AssetIssuer(currency.Issuing.AccountId).Execute().Result.Records.First();
        var distributionAccounts = Task.WhenAll(currency.Distributions.Select(d => wallet.Server.Accounts.Account(d.AccountId))).Result;
        var assetBalances =
            from distributionAccount in distributionAccounts
            from balance in distributionAccount.Balances
            where balance.Asset.CanonicalName() == asset.Asset.CanonicalName()
            select balance;

        return
            (double.Parse(asset.Amount) == assetBalances.Sum(b => double.Parse(b.BalanceString) + double.Parse(b.SellingLiabilities)))
                .Label($"{asset.Asset.CanonicalName()} supply");
    }

    public static Property AssetSupply(this Property property, string assetCode, (KeyPair Issuing, List<KeyPair> Distributions) currency, Wallet wallet) => property.And(AssetSupply(assetCode, currency, wallet));

    public static Property AssetWrapping(AssetTypeCreditAlphaNum wrappedAsset, AssetTypeCreditAlphaNum wrapperAsset, KeyPair liquidityAccount, Wallet wallet)
    {
        var liquidityAccountOffers = GetAllResultsAsync(wallet.Server.Offers.ForAccount(liquidityAccount.AccountId).Execute()).Result;
        var orderForWrapping = liquidityAccountOffers.Where(offer => offer.Selling.CanonicalName() == wrapperAsset.CanonicalName() && offer.Buying.CanonicalName() == wrappedAsset.CanonicalName()).ToList();
        var orderForUnwrapping = liquidityAccountOffers.Where(offer => offer.Buying.CanonicalName() == wrapperAsset.CanonicalName() && offer.Selling.CanonicalName() == wrappedAsset.CanonicalName()).ToList();

        return
            (orderForWrapping.Count == 1)
                .Label("Only 1 order for wrapping asset")

            .And(orderForUnwrapping.Count == 1)
                .Label("Only 1 order for unwrapping asset")

            .And(double.Parse(orderForWrapping.First().Amount) == double.Parse(orderForUnwrapping.First().Amount))
                .Label("All wrapper assets can be redeemed for wrapped asset")

            .And(double.Parse(orderForWrapping.First().Price) > double.Parse(orderForUnwrapping.First().Price))
                .Label("Wrapper asset sold for a premium")

            .And(double.Parse(orderForUnwrapping.First().Price) == 1.0)
                .Label("Wrapped asset can be unwrapped with no fee")
            
            .AssetHoldersHaveFullCustody(wrapperAsset, wallet);
    }

    public static Property AssetWrapping(this Property property, AssetTypeCreditAlphaNum wrappedAsset, AssetTypeCreditAlphaNum wrapperAsset, KeyPair liquidityAccount, Wallet wallet)
        => property.And(AssetWrapping(wrappedAsset, wrapperAsset, liquidityAccount, wallet));

    public static Property CurrencySystem(string assetCode, KeyPair issuing, List<KeyPair> distributions, Wallet wallet)
    {
        return
            ImmutableAccount(issuing, wallet)
                .Label("Immutable supply")

            .AssetHoldersHaveFullCustody(
                (AssetTypeCreditAlphaNum)Asset.CreateNonNativeAsset(assetCode, issuing.AccountId), wallet)

            .HomeDomain(issuing, wallet)

            .And(distributions.Select(distribution => HomeDomain(distribution, wallet)).Aggregate((accumulated, next) => accumulated.And(next)))
                .Label("Home domain of distribution accounts")

            .AssetSupply(assetCode, (issuing, distributions), wallet);
    }

    public static Property CurrencySystem(this Property property, string assetCode, KeyPair issuing, List<KeyPair> distributions, Wallet wallet) => 
        property.And(CurrencySystem(assetCode, issuing, distributions, wallet));

    private static async Task<List<TResponse>> GetAllResultsAsync<TResponse>(Task<Page<TResponse>> query)
    {
        var allResults = new List<TResponse>();
        var currentQuery = await query;
        var keepQuerying = true;

        while (keepQuerying)
        {
            allResults.AddRange(currentQuery.Records);

            currentQuery = await currentQuery.NextPage();

            keepQuerying = currentQuery.Records.Any();
        }

        return allResults;
    }
}