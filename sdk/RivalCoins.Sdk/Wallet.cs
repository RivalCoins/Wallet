using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.requests;
using stellar_dotnet_sdk.responses;
using stellar_dotnet_sdk.xdr;
using Tommy;
using Asset = stellar_dotnet_sdk.Asset;
using Transaction = stellar_dotnet_sdk.Transaction;

namespace RivalCoins.Sdk;

public class ServerSubmissionException : Exception
{
    public ServerSubmissionException()
        : base("Failed to submit transaction")
    {
    }

    public SubmitTransactionResponse Response { get; init; }
}

public record Wallet(
    string NetworkUrl,
    string? AccountSecretSeed,
    string HomeDomain) : IDisposable
{
    private readonly HttpClient _httpClient = new();

    public stellar_dotnet_sdk.Network? NetworkInfo { get; private set; }

    public bool IsInitialized { get; private set; }

    private Server _server;
    public Server Server
    {
        get
        {
            if (_server == null)
            {
                _server = new Server(NetworkUrl, _httpClient);
            }
            
            return _server;
        }
    }

    public (AccountResponse Info, KeyPair? Signer) Account { get; private set; }

    public void Dispose()
    {
        // Dispose of unmanaged resources.
        Dispose(true);

        // Suppress finalization.
        GC.SuppressFinalize(this);
    }

    private bool _disposed;
    protected void Dispose(bool deterministicDisposal)
    {
        if (_disposed)
        {
            return;
        }

        // disposed managed objects
        if (deterministicDisposal)
        {
            if (Server != null)
            {
                Server.Dispose();
            }

            _httpClient.Dispose();
        }

        _disposed = true;
    }

    public static void AutoSign(Transaction transaction, Wallet submittingAccount)
    {
        var requiredSignatures = 
            transaction.Operations.Select(o => o.SourceAccount)
            .Union(new [] { transaction.SourceAccount })
            .Where(account => account != null)
            .Select(account => account.AccountId)
            .Distinct()
            .ToList();

        var availableSigners =
            transaction.Operations.Select(o => o.SourceAccount)
                .Union(new[] { transaction.SourceAccount, GetSigner(submittingAccount.AccountSecretSeed) })
                .Where(account => account != null && account.SigningKey.CanSign())
                .DistinctBy(account => account.AccountId)
                .ToList();

        foreach (var requiredSignature in requiredSignatures)
        {
            var availableSigner = availableSigners.FirstOrDefault(signer => signer.AccountId == requiredSignature);
            if (availableSigner != null)
            {
                transaction.Sign(availableSigner, submittingAccount.NetworkInfo);
            }
        }
    }

    private static async Task AutoSignAsync(Transaction transaction, Network network, string networkUrl, HttpClient httpClient)
    {
        var requiredSignatures =
            transaction.Operations.Select(o => o.SourceAccount)
                .Union(new[] { transaction.SourceAccount })
                .Where(account => account != null)
                .Select(account => account.AccountId)
                .Distinct()
                .ToList();

        var availableSigners =
            transaction.Operations.Select(o => o.SourceAccount)
                .Union(new[] { transaction.SourceAccount })
                .Where(account => account != null && account.SigningKey.CanSign())
                .DistinctBy(account => account.AccountId)
                .ToList();

        var responseHandler = new ResponseHandler<RootResponse>();
        using var response = await httpClient.GetAsync(networkUrl);
        var rootResponse = await responseHandler.HandleResponse(response);
        var networkInfo = new stellar_dotnet_sdk.Network(rootResponse.NetworkPassphrase);

        foreach (var requiredSignature in requiredSignatures)
        {
            var availableSigner = availableSigners.FirstOrDefault(signer => signer.AccountId == requiredSignature);
            if (availableSigner != null)
            {
                transaction.Sign(availableSigner, networkInfo);
            }
        }
    }

    private static KeyPair? GetSigner(string seed)
    {
        KeyPair? keyPair = null;

        try
        {
            keyPair = KeyPair.FromSecretSeed(seed);
        }
        catch (Exception)
        {
        }

        return keyPair;
    }

    public static async Task<SubmitTransactionResponse?> SubmitTransactionAsync(
        Transaction transaction,
        bool autoSign,
        Network network,
        string networkUrl,
        string logDescription)
    {
        try
        {
            using var httpClient = new HttpClient();

            // sign transaction
            if (autoSign)
            {
                await AutoSignAsync(transaction, network, networkUrl, httpClient);
            }

            // submit transaction
            var server = new Server(networkUrl, httpClient);
            var response = await server.SubmitTransaction(transaction, new SubmitTransactionOptions { EnsureSuccess = true });
            if (response.IsSuccess())
            {
                Console.WriteLine($"{logDescription} - Success");
            }
            else
            {
                throw new ServerSubmissionException() { Response = response };
            }

            return response;
        }
        catch (ServerSubmissionException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"{logDescription} - Exception: {exception.Message + Environment.NewLine + exception.StackTrace}");
            throw;
        }
    }

    public async Task<SubmitTransactionResponse?> SubmitTransactionAsync(Transaction transaction, bool autoSign, string logDescription)
    {
        try
        {
            // sign transaction
            if (autoSign)
            {
                AutoSign(transaction, this);
            }

            // submit transaction
            var response = await this.Server.SubmitTransaction(transaction, new SubmitTransactionOptions { EnsureSuccess = true });
            if (response.IsSuccess())
            {
                Console.WriteLine($"{logDescription} - Success");
            }
            else
            {
                throw new ServerSubmissionException() { Response = response };
            }

            return response;
        }
        catch (ServerSubmissionException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"{logDescription} - Exception: {exception.Message + Environment.NewLine + exception.StackTrace}");
            throw;
        }
    }
    
    private async Task<string> InitializeInternalAsync(string accountId)
    {
        if (this.IsInitialized)
        {
            return accountId;
        }

        // KeyPair uses NSec.Cryptography.Sodium.InitializeCore, which is not supported on Blazor WebAssembly.
        // We will initialize as much as we can while avoiding this library.
        var canUseCryptography = CanUseCryptography();

        // get account info
        this.Account = 
            (
                await this.Server.Accounts.Account(canUseCryptography && !string.IsNullOrWhiteSpace(this.AccountSecretSeed) ? KeyPair.FromSecretSeed(AccountSecretSeed).AccountId : accountId),
                !string.IsNullOrWhiteSpace(this.AccountSecretSeed) ? KeyPair.FromSecretSeed(AccountSecretSeed) : null
            );

        // get network info
        //      We can't use stellar_dotnet_sdk.Server.Root() due to a design flaw in its implementation.
        //      The Root() method invokes Task.Result which is incompatible in this async method
        var responseHandler = new ResponseHandler<RootResponse>();
        using var response = await _httpClient.GetAsync(NetworkUrl);
        var rootResponse = await responseHandler.HandleResponse(response);
        this.NetworkInfo = new stellar_dotnet_sdk.Network(rootResponse.NetworkPassphrase);

        // set home domain
        if(this.Account.Info.HomeDomain != this.HomeDomain)
        {
            var setHomeDoamin = new TransactionBuilder(this.Account.Info);
            Util.SetHomeDomain(setHomeDoamin, this.Account.Info.KeyPair, this);
            var result = await this.SubmitTransactionAsync(setHomeDoamin.Build(), true, $"set home domain for {this.Account.Info.AccountId}");
            if (result?.IsSuccess() is null or false)
            {
                throw new Exception($"failed to set home domain for {this.Account.Info.AccountId}");
            }
        }

        this.IsInitialized = true;

        return accountId;
    }

    public Task<string> InitializeAsync(string accountId) => this.InitializeInternalAsync(accountId);

    public async Task<string> InitializeAsync()
    {
        if (!CanUseCryptography())
        {
            throw new Exception($"This platform does not support cryptography eg 'NSec.Cryptography.Sodium.InitializeCore', please use {nameof(InitializeAsync)}(string) which does not use cryptography functions.");
        }

        return await this.InitializeInternalAsync(string.Empty);
    }

    public static Task CreateAccountAsync(KeyPair account, string networkUrl, HttpClient? httpClient = null)
        => CreateAccountAsync(account.Address, networkUrl, httpClient);

    public static async Task CreateAccountAsync(string account, string networkUrl, HttpClient? httpClient = null)
    {
        const string StellarTestnet = "https://horizon-testnet.stellar.org";

        var disposeHttpClient = false;

        if(httpClient == null)
        {
            httpClient = new HttpClient();
            disposeHttpClient = true;
        }

        using var server = new Server(networkUrl, httpClient);

        try
        {
            // do nothing if account already exists
            _ = await server.Accounts.Account(account);
        }
        catch (Exception)
        {
            // exception thrown so account must not exist

            // if we don't assign to a value, we will not wait for the result.
            var discard = await httpClient.GetAsync($"{(networkUrl == StellarTestnet ? "https://friendbot.stellar.org" : $"{networkUrl}/friendbot")}?addr={account}");
        }

        if (disposeHttpClient)
        {
            httpClient.Dispose();
        }
    }

    public static double GetMinimumBalance(double baseReserve, int numEntries, int numSponsoringEntries, int numSponsoredEntries)
        => (2 + numEntries + numSponsoringEntries - numSponsoredEntries) * baseReserve;

    /// <summary>
    ///     Gets Stellar assets issued by an individual or organization, as defined by <paramref name="homeDomain"/>/.well-known/stellar.toml
    /// </summary>
    /// <param name="homeDomain">The domain of a Stellar asset issuer</param>
    /// <returns></returns>
    public static async Task<List<(string Name, AssetTypeCreditAlphaNum Asset, string Description, string ImageUri)>> GetRivalCoinsAsync(string homeDomain)
    {
        //using var http = new HttpClient();
        var rivalCoins = new List<(string Name, AssetTypeCreditAlphaNum Asset, string Description, string ImageUri)>();
        using var httpClient = new HttpClient();
        using var reader = new StringReader(await httpClient.GetStringAsync($"{homeDomain}/.well-known/stellar.toml"));
        var table = TOML.Parse(reader);

        foreach (TomlNode currency in table["CURRENCIES"])
        {
            var rivalCoin = (
                currency["name"],
                (AssetTypeCreditAlphaNum)Asset.CreateNonNativeAsset(currency["code"], currency["issuer"]),
                currency["desc"],
                currency["image"]);

            rivalCoins.Add(rivalCoin);
        }

        return rivalCoins;
    }

    private static bool CanUseCryptography()
    {
        var canUseCryptography = false;

        try
        {
            _ = KeyPair.Random();
            canUseCryptography = true;
        }
        catch (Exception) { }

        return canUseCryptography;
    }
}