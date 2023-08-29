using RivalCoins.Sdk;
using stellar_dotnet_sdk.responses;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RivalCoins.Wallet.Web.Client;

public interface IRivalCoinsApp
{
    Task<IEnumerable<RivalCoin>> GetSwappableCoinsAsync();
    Task<bool> RestoreWalletAsync(string password);
    Task<bool> LoginUserAsync(string password);
    Task<bool> AirDropAsync(RivalCoin rivalCoin);
    string GetPublicAddress();
    Task<bool> SwapAysnc(RivalCoin swapOut, RivalCoin swapIn, double quantity);
    Task<Balance[]> GetBalancesAsync(string accountId);
    Task<(bool Success, string Message)> GetTaxContributionHonorAsync(Stream receipt);
    Task<string> Initialize();
    Task SyncWithL1NetworkAsync();

    string L1NetworkPassphrase { get; }
    string L1HorizonUrl { get; }
    string HomeDomain { get; }
}
