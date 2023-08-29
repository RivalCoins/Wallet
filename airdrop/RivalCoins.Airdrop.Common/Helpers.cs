using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;

namespace RivalCoins.Airdrop.Common;

public class Helpers
{
    public static async Task<bool> IsValidAirdropAsync(Repository.Queue.Model.Airdrop airdrop, Server server)
    {
        var validAirdrop = false;
        var balance = await GetBalanceAsync(
            Asset.Create(airdrop.Asset),
            KeyPair.FromAccountId(airdrop.StellarAccoutId),
            server);

        if (balance != null)
        {
            // sufficient trustline limit
            validAirdrop = (double.Parse(balance.BalanceString) + airdrop.Quantity) <= double.Parse(balance.Limit);
        }

        return validAirdrop;
    }

    public static async Task<bool> TrustlineExistsAsync(Asset asset, KeyPair account, Server server) =>
        await GetBalanceAsync(asset, account, server) != null;

    public static async Task<Balance?> GetBalanceAsync(Asset asset, KeyPair account, Server server)
    {
        var foundAccount = await GetAccountAsync(account.AccountId, server);

        // account exists
        if (foundAccount != null)
        {
            return foundAccount.Balances.FirstOrDefault(b => b.Asset.CanonicalName() == asset.CanonicalName());
        }

        return null;
    }

    public static async Task<bool> AccountExistsAsync(string accountId, Server server) =>
        await GetAccountAsync(accountId, server) == null;

    public static async Task<AccountResponse?> GetAccountAsync(string accountId, Server server)
    {
        AccountResponse? account = null;

        try
        {
            account = await server.Accounts.Account(accountId);

        }
        catch (Exception)
        {
        }

        return account;
    }
}