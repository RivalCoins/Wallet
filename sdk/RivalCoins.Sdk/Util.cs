using Newtonsoft.Json;
using Serilog;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using stellar_dotnet_sdk.responses.effects;
using Tommy;
using stellar_dotnet_sdk.responses.page;


namespace RivalCoins.Sdk;

public static class Util
{
    public static readonly long MaxTrustlineLimit = long.Parse(ChangeTrustOperation.MaxLimit[..ChangeTrustOperation.MaxLimit.IndexOf('.')]);

    private const double BaseReserve = 0.5;
    private const double AccountOperationalBuffer = 3.0;

    public static string OutputFolder
    {
        get
        {
            var outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RivalCoins", "Util");

            Directory.CreateDirectory(outputFolder);

            return outputFolder;
        }
    }

    public static async Task<List<TResponse>> GetAllResultsAsync<TResponse>(Task<Page<TResponse>> query)
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

    public static async Task CreateRivalCoinsAsync(
        AssetTypeCreditAlphaNum wrappedAsset,
        KeyPair wrappedAssetFunder,
        double liquidityVolume,
        (KeyPair WrapperAssetIssuer, KeyPair WrapperAssetDistribution, Wallet XlmFunder) accounts,
        string[] rivalCoinAssetCodes)
    {
        foreach (var rivalCoinAssetCode in rivalCoinAssetCodes)
        {
            await CreateRivalCoinInternalAsync(wrappedAsset, wrappedAssetFunder, liquidityVolume, rivalCoinAssetCode, accounts);
        }
    }

    public static async Task CreateRivalCoinAsync(
        AssetTypeCreditAlphaNum wrappedAsset,
        KeyPair wrappedAssetFunder,
        double liquidityVolume,
        string wrapperAssetCode,
        (KeyPair Issuing, KeyPair Distribution) wrapperAccounts,
        Wallet wallet)
    {
        await CreateRivalCoinInternalAsync(wrappedAsset, wrappedAssetFunder, liquidityVolume, wrapperAssetCode, (wrapperAccounts.Issuing, wrapperAccounts.Distribution, wallet));
    }

    private static async Task CreateRivalCoinInternalAsync(
        Asset wrappedAsset,
        KeyPair wrappedAssetFunder,
        double liquidityVolume,
        string wrapperAssetCode,
        (KeyPair WrapperAssetIssuing, KeyPair WrapperAssetDistribution, Wallet XlmFunder) accounts)
    {
        var transactionBuilder = new TransactionBuilder(accounts.XlmFunder.Account.Info);

        // create Rival Coin
        AddRivalCoinCreation(
            transactionBuilder,
            wrappedAsset,
            wrappedAssetFunder,
            liquidityVolume.ToString(),
            (accounts.WrapperAssetIssuing, accounts.WrapperAssetDistribution, wrapperAssetCode));

        var response = await accounts.XlmFunder.SubmitTransactionAsync(transactionBuilder.Build(), true, $"(1) Create Rival Coins, (2) fund {wrappedAsset.Code()}, (3) fund Rival Coins, (4) create orders");
        if (response?.IsSuccess() is null or false)
        {
            throw new Exception("failed");
        }

        var toml = new TomlTable();
        var rivalCoins = new TomlArray() { IsTableArray = true };
        var rivalCoin = new TomlTable();

        toml.Add("CURRENCIES", rivalCoins);
        rivalCoins.Add(rivalCoin);

        rivalCoin.Add("code", new TomlString() { Value = wrapperAssetCode });
        rivalCoin.Add("issuer", new TomlString() { Value = accounts.WrapperAssetIssuing.AccountId });
        rivalCoin.Add("display_decimals", new TomlInteger() { Value = 7 });
        rivalCoin.Add("name", new TomlString() { Value = $"<CHANGE ME> ({wrappedAsset.Code()})" });
        rivalCoin.Add("desc", new TomlString() { Value = "<CHANGE ME>" });
        rivalCoin.Add("is_asset_anchored", new TomlBoolean() { Value = false });
        rivalCoin.Add("image", new TomlString() { Value = "<CHANGE ME>" });

        using var sw = new StringWriter();
        toml.WriteTo(sw);

        Console.WriteLine(sw.ToString());

        const string StellarMainnetUrl = "https://horizon.stellar.org";
        var rivalCoinStatsSite = $"https://stellar.expert/explorer/{(accounts.XlmFunder.NetworkUrl == StellarMainnetUrl ? "public" : "testnet")}/asset/{wrapperAssetCode}-{accounts.WrapperAssetIssuing.AccountId}";
        Console.WriteLine($@"<a href=""{rivalCoinStatsSite}"" target=_blank>Nerd Stats for <CHANGE ME></a>");
    }

    private static void AddRivalCoinCreation(
        TransactionBuilder transactionBuilder,
        Asset wrappedAsset,
        KeyPair wrappedAssetFunder,
        string liquidityVolume,
        (KeyPair Issuing, KeyPair Distribution, string AssetCode) wrapperAssetAccount)
    {
        var wrapperAsset = Asset.CreateNonNativeAsset(wrapperAssetAccount.AssetCode, wrapperAssetAccount.Issuing.AccountId);

        // fund distribution account with wrapped asset
        if(false)
        {
            SendPayment(
                wrappedAsset,
                liquidityVolume,
                wrappedAssetFunder,
                wrapperAssetAccount.Distribution,
                transactionBuilder);
        }

        // fund distribution account with wrapper asset
        SendPayment(
            wrapperAsset,
            liquidityVolume,
            wrapperAssetAccount.Issuing,
            wrapperAssetAccount.Distribution,
            transactionBuilder);

        // sell wrapper asset
        transactionBuilder
            .AddOperation(new ChangeTrustOperation.Builder(new ChangeTrustAsset.Wrapper(wrappedAsset))
                .SetSourceAccount(wrapperAssetAccount.Distribution)
                .Build())
            .AddOperation(new CreatePassiveSellOfferOperation.Builder(wrapperAsset, wrappedAsset, liquidityVolume, "1.0")
                .SetSourceAccount(wrapperAssetAccount.Distribution)
                .Build());

        // buy back wrapper asset
        if(false)
        {
            transactionBuilder.AddOperation(
                new CreatePassiveSellOfferOperation.Builder(wrappedAsset, wrapperAsset, liquidityVolume, "1.0")
                    .SetSourceAccount(wrapperAssetAccount.Distribution)
                    .Build());
        }
    }

    public static async Task<((KeyPair Issuing, List<KeyPair> Distributions) USA, (KeyPair Issuing, List<KeyPair> Distributions) GovFundRewards)>
        CreateGovFundRewardsEnvironmentAsync(string networkUrl, string homeDomain)
    {
        var wallet = new Wallet(networkUrl, KeyPair.Random().SecretSeed, homeDomain);
        await Wallet.CreateAccountAsync(KeyPair.FromSecretSeed(wallet.AccountSecretSeed), networkUrl);
        await wallet.InitializeAsync();

        // create USA
        var usaAccounts = await CreateCurrencySystemAsync(
            "USA",
            30L * 1000L * 1000L * 1000L * 1000L,
            MaxTrustlineLimit,
            wallet);

        // create Gov Fund Rewards subscription token
        var govFundRewardsAccounts = await CreateCurrencySystemAsync(
            "GFRewards",
            1L,
            1L,
            wallet);

        // create airdrop participant
        var nonExistent = KeyPair.Random();
        var exists = KeyPair.Random();
        var usaOnly = KeyPair.Random();
        var subscribed = KeyPair.Random();
        var airdropParticipant = KeyPair.Random();
        await Wallet.CreateAccountAsync(exists, wallet.NetworkUrl);
        await Wallet.CreateAccountAsync(usaOnly, wallet.NetworkUrl);
        await Wallet.CreateAccountAsync(subscribed, wallet.NetworkUrl);
        await Wallet.CreateAccountAsync(airdropParticipant, wallet.NetworkUrl);

        var tx = new TransactionBuilder(wallet.Account.Info);

        // valid airdrop participant
        CreateTrustline(
            Asset.CreateNonNativeAsset("USA", usaAccounts.Issuing.AccountId),
            airdropParticipant,
            tx);
        CreateTrustline(
            Asset.CreateNonNativeAsset("GFRewards", govFundRewardsAccounts.Issuing.AccountId),
            airdropParticipant,
            tx);

        // unsubscribed but accepts USA
        CreateTrustline(
            Asset.CreateNonNativeAsset("USA", usaAccounts.Issuing.AccountId),
            usaOnly,
            tx);

        // subscribed but does not accept USA
        CreateTrustline(
            Asset.CreateNonNativeAsset("GFRewards", govFundRewardsAccounts.Issuing.AccountId),
            subscribed,
            tx);

        await wallet.SubmitTransactionAsync(tx.Build(), true, "Create airdrop participant");

        Log.Information($"USA Issuing - {usaAccounts.Issuing.AccountId}:{usaAccounts.Issuing.SecretSeed}");
        Log.Information($"USA Distribution - {usaAccounts.Distributions.First().AccountId}:{usaAccounts.Distributions.First().SecretSeed}");
        Log.Information($"Gov Fund Rewards Issuing - {govFundRewardsAccounts.Issuing.AccountId}:{govFundRewardsAccounts.Issuing.SecretSeed}");
        Log.Information($"Gov Fund Rewards Distribution - {govFundRewardsAccounts.Distributions.First().AccountId}:{govFundRewardsAccounts.Distributions.First().SecretSeed}");
        Log.Information($"Non-existent - {nonExistent.AccountId}:{nonExistent.SecretSeed}");
        Log.Information($"Exists - {exists.AccountId}:{exists.SecretSeed}");
        Log.Information($"USA only - {usaOnly.AccountId}:{usaOnly.SecretSeed}");
        Log.Information($"Subscribed - {subscribed.AccountId}:{subscribed.SecretSeed}");
        Log.Information($"Airdrop Participant - {airdropParticipant.AccountId}:{airdropParticipant.SecretSeed}");

        return (usaAccounts, govFundRewardsAccounts);
    }

    public static void CreateTrustline(Asset asset, KeyPair account, TransactionBuilder transactionBuilder)
    {
        // create trustline
        transactionBuilder.AddOperation(
            new ChangeTrustOperation.Builder(new ChangeTrustAsset.Wrapper(asset), ChangeTrustOperation.MaxLimit)
                .SetSourceAccount(account)
                .Build());
    }

    public static async Task<(KeyPair Issuing, List<KeyPair> Distributions)> CreateCurrencySystemAsync(
        string currencyAssetCode,
        long currencySupply,
        long tradingQuantity,
        Wallet wallet)
    {
        var transactionBuilder = new TransactionBuilder(wallet.Account.Info);

        //var walletAccountBefore = await wallet.Server.Accounts.Account(wallet.Account.AccountId);

        // create accounts
        var currencyAccounts = await CreateCurrencyAccountsAsync(
            wallet,
            tradingQuantity.ToString(),
            currencyAssetCode,
            currencySupply);

        // lock down issuing account (1 operation)
        transactionBuilder.AddOperation(
            new SetOptionsOperation.Builder()
                .SetMasterKeyWeight(1)
                .SetLowThreshold(2)
                .SetMediumThreshold(2)
                .SetHighThreshold(2)
                .SetHomeDomain(wallet.HomeDomain)
                .SetSourceAccount(currencyAccounts.IssuingAccount)
                .SetSetFlags((int)AccountFlag.AuthImmutableFlag)
                .Build());

        var response = await wallet.SubmitTransactionAsync(transactionBuilder.Build(), true, $"Lock down {currencyAssetCode} issuing account");
        if (response?.IsSuccess() is null or false)
        {
            throw new Exception();
        }

        //var walletAccountAfter = await wallet.Server.Accounts.Account(wallet.Account.AccountId);
        //Log.Information($"Total XLM Used: {double.Parse(walletAccountBefore.Balances.First(b => b.AssetType == AssetTypeNative.JsonType).BalanceString) - double.Parse(walletAccountAfter.Balances.First(b => b.AssetType == AssetTypeNative.JsonType).BalanceString)}");

        // save currency system
        var currencySystemFile = $@"{OutputFolder}\{currencyAssetCode}-CurrencySystem.json";
        var currencySystem = new CurrencySystem() { AssetCode = currencyAssetCode };
        currencySystem.BaseIssuingAccount = $"{currencyAccounts.IssuingAccount.AccountId}:{currencyAccounts.IssuingAccount.SecretSeed}";
        currencySystem.BaseDistributionAccounts = currencyAccounts.DistributionAccounts.Select(a => $"{a.AccountId}:{a.SecretSeed}").ToArray();

        await File.WriteAllTextAsync(currencySystemFile, JsonConvert.SerializeObject(currencySystem));

        // save TOML file
        await SaveTomlFileAsync(currencySystem, wallet.HomeDomain);

        return currencyAccounts;
    }

    private static async Task SaveTomlFileAsync(CurrencySystem currencySystem, string rivalCoinsCompanyUrl)
    {
        var toml = new TomlTable();
        var sw = new StringWriter();

        toml.Add("VERSION", new TomlString() { Value = "2.0.0" });

        var rivalCoinsAccounts = new TomlArray();
        toml.Add("ACCOUNTS", rivalCoinsAccounts);

        // issuing account
        rivalCoinsAccounts.Add(new TomlString() { Value = currencySystem.BaseIssuingAccount.Split(':')[0], Comment = $"{currencySystem.AssetCode} Issuing Account" });

        // distribution accounts
        var processedFirstDistributionAccount = false;
        foreach (var dollarDistributionAccount in currencySystem.BaseDistributionAccounts)
        {
            if (processedFirstDistributionAccount)
            {
                rivalCoinsAccounts.Add(new TomlString() { Value = dollarDistributionAccount.Split(':')[0] });
            }
            else
            {
                rivalCoinsAccounts.Add(new TomlString() { Value = dollarDistributionAccount.Split(':')[0], Comment = $"{currencySystem.AssetCode} Distribution Accounts" });
                processedFirstDistributionAccount = true;
            }
        }

        // documentation
        var documentation = new TomlTable();
        toml.Add("DOCUMENTATION", documentation);

        documentation.Add("ORG_NAME", new TomlString() { Value = "Rival Coins" });
        documentation.Add("ORG_DBA", new TomlString() { Value = "Rival Coins" });
        documentation.Add("ORG_URL", new TomlString() { Value = rivalCoinsCompanyUrl });
        documentation.Add("ORG_LOGO", new TomlString() { Value = "https://rivalcoins.money/wp-content/uploads/2021/06/logo-500x500-1.png" });
        documentation.Add("ORG_DESCRIPTION", new TomlString() { Value = "Custom branded money, competing in a global popularity contest." });
        documentation.Add("ORG_PHYSICAL_ADDRESS", new TomlString() { Value = "Chicago, IL" });
        documentation.Add("ORG_TWITTER", new TomlString() { Value = "RivalCoins" });
        documentation.Add("ORG_OFFICIAL_EMAIL", new TomlString() { Value = "info@rivalcoins.money" });

        // principals
        var principals = new TomlTable();
        toml.Add("PRINCIPALS", principals);

        principals.Add("name", new TomlString() { Value = "Jerome Bell" });
        principals.Add("email", new TomlString() { Value = "jerome.bell@rivalcoins.money" });
        principals.Add("twitter", new TomlString() { Value = "jeromebelljr" });
        principals.Add("github", new TomlString() { Value = "heteroculturalism" });

        toml.WriteTo(sw);

        // currencies
        var currenciesToml = new TomlTable();
        var currencies = new TomlArray() { IsTableArray = true };

        currenciesToml.Add("CURRENCIES", currencies);

        var currency = new TomlTable();
        currencies.Add(currency);
        currency.Add("code", new TomlString() { Value = currencySystem.AssetCode });
        currency.Add("issuer", new TomlString() { Value = currencySystem.BaseIssuingAccount.Split(':')[0] });
        currency.Add("display_decimals", new TomlInteger() { Value = 7 });
        currency.Add("name", new TomlString() { Value = "<CHANGE ME>" });
        currency.Add("desc", new TomlString() { Value = "<CHANGE ME>" });
        currency.Add("is_asset_anchored", new TomlBoolean() { Value = false });
        currency.Add("image", new TomlString() { Value = "https://rivalcoins.money/wp-content/uploads/2021/06/logo-500x500-1.png" });

        sw.WriteLine();
        currenciesToml.WriteTo(sw);

        var foo = sw.ToString().Replace("\",", "\"," + Environment.NewLine);

        await File.WriteAllTextAsync($@"{OutputFolder}\{currencySystem.AssetCode}-stellar.toml", foo);
    }

    private static async Task<(KeyPair IssuingAccount, List<KeyPair> DistributionAccounts)> CreateCurrencyAccountsAsync(
        Wallet wallet,
        string tradingQuantity,
        string currencyAssetCode,
        long currencySupply)
    {
        async Task<KeyPair> CreateIssuingAccountAsync()
        {
            var issuingAccountMinimumBalance = Wallet.GetMinimumBalance(BaseReserve, 0, 0, 0);
            var transactionBuilder = new TransactionBuilder(wallet.Account.Info);
            var issuingAccount = KeyPair.Random();

            transactionBuilder
                .AddOperation(new CreateAccountOperation.Builder(issuingAccount, (issuingAccountMinimumBalance + AccountOperationalBuffer).ToString()).Build())
                .AddOperation(new SetOptionsOperation.Builder().SetHomeDomain(wallet.HomeDomain).Build());

            var response = await wallet.SubmitTransactionAsync(transactionBuilder.Build(), true, "Create issuing account");
            if (response?.IsSuccess() is null or false)
            {
                throw new Exception();
            }
            Log.Information($"{currencyAssetCode} Issuing Account: {issuingAccount.AccountId}:{issuingAccount.SecretSeed}");

            return issuingAccount;
        }

        async Task CreateDistributionAccountsAsync(Asset currency, KeyPair issuingAccount, List<(KeyPair DistributionAccount, long TradingQuantity)> distributionAccounts)
        {
            if (distributionAccounts.Any())
            {
                var transactionBuilder = new TransactionBuilder(wallet.Account.Info);

                foreach (var distributionAccount in distributionAccounts)
                {
                    SetHomeDomain(transactionBuilder, distributionAccount.DistributionAccount, wallet);
                    SendPayment(currency, distributionAccount.TradingQuantity.ToString(), issuingAccount, distributionAccount.DistributionAccount, transactionBuilder);
                }
                var response = await wallet.SubmitTransactionAsync(transactionBuilder.Build(), true, $"(1) Fund {currencyAssetCode} distribution accounts");
                if (response?.IsSuccess() is null or false)
                {
                    throw new Exception();
                }
            }
        }

        // create distribution accounts
        var issuingAccount = await CreateIssuingAccountAsync();
        var distributionAccounts = await CreateCurrencyDistributionAccountsAsync(wallet, long.Parse(tradingQuantity), currencySupply);
        var currency = Asset.CreateNonNativeAsset(currencyAssetCode, issuingAccount.AccountId);
        var transactionBuilder = new TransactionBuilder(wallet.Account.Info);

        foreach (var distributionAccount in distributionAccounts)
        {
            Log.Information($"{currencyAssetCode} Distribution Account: {distributionAccount.DistributionAccount.AccountId}:{distributionAccount.DistributionAccount.SecretSeed} - {distributionAccount.TradingQuantity}");
        }

        // fund distribution accounts
        const int NumOperationsPerDistributionAccountCreation = 3;

        foreach(var batch in distributionAccounts.Chunk(Constants.MaxNumberOfOperationPerTransaction / NumOperationsPerDistributionAccountCreation / 2))
        {
            await CreateDistributionAccountsAsync(currency, issuingAccount, batch.ToList());
        }

        return (issuingAccount, distributionAccounts.Select(distributionAccount => distributionAccount.DistributionAccount).ToList());
    }

    public static void SetHomeDomain(TransactionBuilder transactionBuilder, KeyPair account, Wallet wallet)
    {
        transactionBuilder.AddOperation(
            new SetOptionsOperation.Builder()
                .SetHomeDomain(wallet.HomeDomain)
                .SetSourceAccount(account)
                .Build());
    }

    public static void SendPayment(
        Asset asset,
        string amount,
        KeyPair sender,
        KeyPair recipient,
        TransactionBuilder transactionBuilder)
    {
        // create asset trust line
        transactionBuilder.AddOperation(
        new ChangeTrustOperation.Builder(new ChangeTrustAsset.Wrapper(asset))
                .SetSourceAccount(recipient)
                .Build());

        // send payment
        transactionBuilder.AddOperation(
        new PaymentOperation.Builder(recipient, asset, amount)
                .SetSourceAccount(sender)
                .Build());
    }

    private static async Task<List<(KeyPair DistributionAccount, long TradingQuantity)>> CreateCurrencyDistributionAccountsAsync(
        Wallet wallet,
        long distributionAccountTradingQuantity,
        long currencySupply)
    {
        var supplyDistribution = Math.DivRem(currencySupply, distributionAccountTradingQuantity);
        var numDistributionAccounts = supplyDistribution.Quotient + (supplyDistribution.Remainder != 0 ? 1 : 0);
        var distributionAccounts = new List<(KeyPair DistributionAccount, long TradingQuantity)>();

        // create distribution accounts
        var distributionAccountMinimumBalance = Wallet.GetMinimumBalance(BaseReserve, 3, 0, 0);
        var lastDistributionAccountIndex = numDistributionAccounts - 1;

        for (var i = 0; i < numDistributionAccounts; i++)
        {
            distributionAccounts.Add((KeyPair.Random(), i == lastDistributionAccountIndex && supplyDistribution.Remainder != 0 ? supplyDistribution.Remainder : distributionAccountTradingQuantity));
        }

        foreach (var accountCreationTransaction in distributionAccounts.Chunk(Constants.MaxNumberOfOperationPerTransaction))
        {
            var transactionBuilder = new TransactionBuilder(wallet.Account.Info);

            for (var i = 0; i < accountCreationTransaction.Length; i++)
            {
                transactionBuilder.AddOperation(
                    new CreateAccountOperation.Builder(accountCreationTransaction[i].DistributionAccount, (distributionAccountMinimumBalance + AccountOperationalBuffer).ToString()).Build());
            }

            var response = await wallet.SubmitTransactionAsync(transactionBuilder.Build(), true, "Create currency distribution accounts");
            if (response?.IsSuccess() is null or false)
            {
                throw new Exception();
            }
        }

        return distributionAccounts;
    }
}
