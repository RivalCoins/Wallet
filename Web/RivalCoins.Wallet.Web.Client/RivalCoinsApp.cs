using Blazored.LocalStorage;
using Microsoft.JSInterop;
using RivalCoins.Sdk;
using RivalCoins.Sdk.Grpc;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using static System.Net.WebRequestMethods;
using Newtonsoft.Json;

namespace RivalCoins.Wallet.Web.Client;

// Cannot use stellar_dotnet_sdk.KeyPair because it requires
// System.Security.Cryptography.Csp, which is not supported on Blazor WebAssembly
public record KeyPairBasic(string PublicKey, string PrivateKey);

public record RivalCoin(string Name, AssetTypeCreditAlphaNum Asset, string Description, double Quantity, string IconUri);

public class RivalCoinsApp : IRivalCoinsApp
{
    private readonly ILocalStorageService _localStorage;
    private readonly RivalCoinsService.RivalCoinsServiceClient _serverClient;
    private readonly string _l2HorizonUrl;
    private readonly HttpClient _http;
    private readonly AssetTypeCreditAlphaNum _wrappedAsset;

    private ((Sdk.Wallet Wallet, KeyPairBasic Account) L1, (Sdk.Wallet Wallet, KeyPairBasic Account) L2) _wallet;

    public RivalCoinsApp(
        ILocalStorageService localStorage,
        IJSRuntime javaScriptRuntime,
        RivalCoinsService.RivalCoinsServiceClient serverClient,
        IConfiguration config,
        HttpClient http)
    {
        _localStorage = localStorage;
        _js = javaScriptRuntime;
        _serverClient = serverClient;
        this.L1HorizonUrl = config.GetValue<string>("L1_HORIZON_URL");
        HomeDomain = config.GetValue<string>("RIVALCOINS_HOME_DOMAIN");
        _l2HorizonUrl = config.GetValue<string>("L2_HORIZON_URL");
        _wrappedAsset = (AssetTypeCreditAlphaNum) Asset.Create(config.GetValue<string>("WRAPPED_ASSET"));
        _http = http;
    }

    protected IJSRuntime _js { get; }

    public string L1NetworkPassphrase => _wallet.L1.Wallet.NetworkInfo!.NetworkPassphrase;
    public string L1HorizonUrl { get; }
    public string HomeDomain { get; }

    #region Disposal

    public void Dispose()
    {
        // Dispose of unmanaged resources.
        Dispose(true);

        // Suppress finalization.
        GC.SuppressFinalize(this);
    }

    private bool _disposed = false;
    protected void Dispose(bool deterministicDisposal)
    {
        if (_disposed)
        {
            return;
        }

        // disposed managed objects
        if (deterministicDisposal)
        {
            if (_wallet.L1.Wallet != null)
            {
                _wallet.L1.Wallet.Dispose();
            }

            if (_wallet.L2.Wallet != null)
            {
                _wallet.L2.Wallet.Dispose();
            }

        }

        _disposed = true;
    }

    #endregion Disposal

    private async Task CreateWalletAsync(string password)
    {
        _wallet.L1 = await RestoreWalletInternalAsync(await _js.InvokeAsync<string>("createKeyPair"), L1HorizonUrl, HomeDomain);
        _wallet.L1 = await RestoreWalletInternalAsync(await _js.InvokeAsync<string>("createKeyPair"), L1HorizonUrl, HomeDomain);

        // store encrypted secret seed in local storage
        var encryptedSecretSeed = await _js.InvokeAsync<string>("aesGcmEncrypt", _wallet.L1.Account.PrivateKey, password);
        await _localStorage.SetItemAsStringAsync("SecretSeed", encryptedSecretSeed);
    }

    public async Task<IEnumerable<RivalCoin>> GetSwappableCoinsAsync()
    {
        try
        {
            var rivalCoins = await Sdk.Wallet.GetRivalCoinsAsync(HomeDomain);

            return rivalCoins.Select(rivalCoin => new RivalCoin(rivalCoin.Name, rivalCoin.Asset, rivalCoin.Description, 0.0, rivalCoin.ImageUri)).ToList();
        }
        catch (Exception ex)
        {
            ;
        }

        return null;
    }

    public static async Task<List<(string Name, AssetTypeCreditAlphaNum Asset, string Description, string ImageUri)>> GetRivalCoinsAsync(string companyHomeDomain)
    {
        using var http = new HttpClient();
        await using var rivalCoinsToml = await http.GetStreamAsync($"{companyHomeDomain}/.well-known/stellar.toml");
        using var rivalCoinsTomlStream = new StreamReader(rivalCoinsToml);
        var rivalCoins = new List<(string Name, AssetTypeCreditAlphaNum Asset, string Description, string ImageUri)>();

        return rivalCoins;
    }

    public async Task<bool> RestoreWalletAsync(string password)
    {
        if (await _localStorage.ContainKeyAsync("SecretSeed"))
        {
            var encryptedSecretSeed = await _localStorage.GetItemAsStringAsync("SecretSeed");
            var decryptedSecretSeed = await _js.InvokeAsync<string>("aesGcmDecrypt", encryptedSecretSeed, password);

            _wallet.L1 = await RestoreWalletInternalAsync(await _js.InvokeAsync<string>("createKeyPairFromSeed", decryptedSecretSeed), L1HorizonUrl, HomeDomain);

            return _wallet.L1.Wallet.IsInitialized;
        }

        return true;
    }

    public async Task<bool> LoginUserAsync(string password)
    {
        // log in user if they are not already logged in and a wallet can be restored
        if (await _localStorage.ContainKeyAsync("SecretSeed"))
        {
            var secretSeed = await _localStorage.GetItemAsStringAsync("SecretSeed");
            var decryptedSecretSeed = await _js.InvokeAsync<string>("aesGcmDecrypt", secretSeed, password);

            _wallet.L1 = await RestoreWalletInternalAsync(await _js.InvokeAsync<string>("createKeyPairFromSeed", decryptedSecretSeed), L1HorizonUrl, HomeDomain);

            return true;
        }
        else
        {
            await this.CreateWalletAsync(password);
            return true;
        }

        return false;
    }

    private async Task TrustAssetAsync(Asset asset, string horizonUrl, string networkPassphrase) =>
        await _js.InvokeVoidAsync(
            "trustAsset",
            this.GetPublicAddress(),
            asset.Code(),
            asset.Issuer(),
            horizonUrl,
            networkPassphrase);

    public async Task<bool> AirDropAsync(RivalCoin rivalCoin)
    {
        try
        {
            if (true)
            {
                // trust L1 asset
                await this.TrustAssetAsync(_wrappedAsset, _wallet.L1.Wallet.NetworkUrl, _wallet.L1.Wallet.NetworkInfo!.NetworkPassphrase);

                // trust L2 assets
                await this.TrustAssetAsync(_wrappedAsset, _wallet.L2.Wallet.NetworkUrl, _wallet.L2.Wallet.NetworkInfo!.NetworkPassphrase);
                await this.TrustAssetAsync(rivalCoin.Asset, _wallet.L2.Wallet.NetworkUrl, _wallet.L2.Wallet.NetworkInfo!.NetworkPassphrase);

                var result = await _serverClient.AirdropAsync(new()
                {
                    RecipientAddress = this.GetPublicAddress(),
                    Asset = rivalCoin.Asset.CanonicalName()
                });
                if (!result.Success_)
                {
                    throw new Exception(result.Message);
                }
            }
            else
            {
                var tx = await _serverClient.CreateAirDropTransactionAsync(new AirDropRequest() { RecipientAddress = _wallet.L1.Account.PublicKey });

                var signedTx = await this.SignTransactionAsync(tx.UnsignedXdr, _wallet.L1.Wallet);

                await this.SubmitTransactionAsync(signedTx, _wallet.L1.Wallet);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{nameof(AirDropAsync)} - {ex.Message}");
        }

        return false;
    }

    public async Task<string> SignTransactionAsync(string xdr, Sdk.Wallet wallet) => await _js.InvokeAsync<string>("signTransaction", xdr, wallet.NetworkInfo.NetworkPassphrase);

    public async Task SubmitTransactionAsync(string xdr, Sdk.Wallet wallet) => await _js.InvokeVoidAsync("submitTransaction", xdr, wallet.NetworkInfo.NetworkPassphrase, wallet.NetworkUrl);
 
    public string GetPublicAddress() => _wallet.L1.Account?.PublicKey;

    public async Task SyncWithL1NetworkAsync()
    {
        var response = await _http.GetAsync($"{_wallet.L2.Wallet.NetworkUrl}/syncAccount?account={_wallet.L1.Account.PublicKey}");
        var responsePayload = JsonConvert.DeserializeObject<StellarTransaction>(await response.Content.ReadAsStringAsync());
        if (responsePayload?.Success is true)
        {
            var txSigned = await this.SignTransactionAsync(responsePayload.L1TransactionXdr, _wallet.L2.Wallet);

            await this.SubmitTransactionAsync(txSigned, _wallet.L2.Wallet);
        }        
    }

    public virtual async Task<bool> SwapAysnc(RivalCoin swapOut, RivalCoin swapIn, double quantity)
    {
        if (true)
        {
            await this.TrustAssetAsync(swapIn.Asset, _wallet.L2.Wallet.NetworkUrl, _wallet.L2.Wallet.NetworkInfo!.NetworkPassphrase);

            var result = await _serverClient.SwapAsync(new()
            {
                User = this.GetPublicAddress(),
                SwapOut = swapOut.Asset.CanonicalName(),
                SwapIn = swapIn.Asset.CanonicalName(),
                Quantity = quantity.ToStellarQuantityString()
            });
            if (!result.Success)
            {
                throw new Exception(result.Message);
            }

            var signedSwapTx = await this.SignTransactionAsync(result.SignedXdr, _wallet.L2.Wallet);

            await this.SubmitTransactionAsync(signedSwapTx, _wallet.L2.Wallet);
        }
        else
        {
            var paths = await _wallet.L2.Wallet.Server.PathStrictReceive
                .SourceAssets(new[] { swapOut.Asset })
                .DestinationAsset(swapIn.Asset)
                .DestinationAmount(quantity.ToString())
                .Execute();

            var pathAsset = paths.Records.First().Path.FirstOrDefault() ?? swapIn.Asset;

            var swapXdr = await _js.InvokeAsync<string>(
                "getSwapTx",
                swapOut.Asset.Code(),
                swapOut.Asset.Issuer(),
                swapIn.Asset.Code(),
                swapIn.Asset.Issuer(),
                quantity.ToString(),
                pathAsset.Code(),
                pathAsset.Issuer(),
                _wallet.L2.Wallet.NetworkInfo.NetworkPassphrase,
                _wallet.L2.Account.PublicKey,
                _wallet.L2.Wallet.NetworkUrl);
        }

        return true;
    }

    public async Task<Balance[]> GetBalancesAsync(string accountId)
    {
        var account = await _wallet.L2.Wallet.Server.Accounts.Account(accountId);

        return account.Balances;
    }

    public async Task<string?> Initialize()
    {
        var accountId = await _js.InvokeAsync<string>("connectToRabet");
        if(accountId != null)
        {
            // initialize layer 1 network wallet
            await Sdk.Wallet.CreateAccountAsync(accountId, this.L1HorizonUrl);
            _wallet.L1 = (new Sdk.Wallet(this.L1HorizonUrl, " ", this.HomeDomain), new KeyPairBasic(accountId, string.Empty));
            await _wallet.L1.Wallet.InitializeAsync(accountId);

            // initialize layer 2 network wallet
            await Sdk.Wallet.CreateAccountAsync(accountId, _l2HorizonUrl);
            _wallet.L2 = (new Sdk.Wallet(_l2HorizonUrl, " ", this.HomeDomain), new KeyPairBasic(accountId, string.Empty));
            await _wallet.L2.Wallet.InitializeAsync(accountId);

            //var success = await _serverClient.AirdropAsync(new AirDropRequest() { RecipientAddress = this.GetPublicAddress(), Asset = "my_asset" });
        }

        return accountId;
    }

    private static async Task<(Sdk.Wallet Wallet, KeyPairBasic Account)> RestoreWalletInternalAsync(
        string walletKeyPairInfo,
        string networkUrl,
        string companyHomeDomain)
    {
        const int PublicKeyIndex = 1;
        const int SecretSeedIndex = 0;

        var walletKeyPair = new KeyPairBasic(walletKeyPairInfo.Split(':')[PublicKeyIndex], walletKeyPairInfo.Split(':')[SecretSeedIndex]);
        var wallet = new Sdk.Wallet(networkUrl, walletKeyPair.PrivateKey, companyHomeDomain);
        var accountExists = false;

        // if the account does not exit on the system, create it
        try
        {
            _ = await wallet.Server.Accounts.Account(walletKeyPair.PublicKey);
            accountExists = true;
        }
        catch (Exception)
        {
        }

        // initialize wallet
        if(accountExists)
        {
            await wallet.InitializeAsync(walletKeyPair.PublicKey);
        }

        return (wallet, walletKeyPair);
    }

    public async Task<(bool Success, string Message)> GetTaxContributionHonorAsync(Stream receipt)
    {
        var result = await _serverClient.ReceiveTaxContributionHonorAsync(new TaxContributionInfo()
        {
            AccountId = _wallet.L1.Account.PublicKey,
            Receipt = await Google.Protobuf.ByteString.FromStreamAsync(receipt)
        });

        await this.SubmitTransactionAsync(result.SignedXdr, _wallet.L1.Wallet);

        return (result.Success.Success_, result.Success.Message);
    }
}
