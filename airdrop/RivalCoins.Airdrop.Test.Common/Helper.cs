using RivalCoins.Sdk;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;

namespace RivalCoins.Airdrop.Test.Common;

public class Helper
{
    public static async Task CreateTrustlineAsync(AssetTypeCreditAlphaNum asset, Wallet accountWallet, double? limit = null)
    {
        if (!accountWallet.IsInitialized)
        {
            await accountWallet.InitializeAsync();
        }

        var createTrustlineTx = new TransactionBuilder(accountWallet.Account.Info);

        if (limit == null)
        {
            createTrustlineTx.AddOperation(new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(asset)).Build());
        }
        else
        {
            createTrustlineTx.AddOperation(new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(asset), limit.ToString()).Build());
        }

        var signedTx = createTrustlineTx.Build();

        var response = await accountWallet.SubmitTransactionAsync(signedTx, true, $"Trust {asset.CanonicalName()}");
        if (response?.IsSuccess() is null or false)
        {
            throw new Exception($"failed to create trustline for {asset.CanonicalName()}");
        }
    }
}