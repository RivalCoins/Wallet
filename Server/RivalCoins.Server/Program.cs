using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.Mvc;
using RivalCoins.Sdk;
using RivalCoins.Server.Model;
using RivalCoins.Server.Services;
using stellar_dotnet_sdk;

namespace RivalCoins.Server;

public class Program
{
    private const string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
    private const long UsaSupply = 100L * 1000L * 1000L * 1000L * 1000L;

    private static string _rivalCoinsHomeDomain = null!;
    private static int _transactionNum = 1;
    private static int _airdropNum = 1;
    private static Wallet _airdropFaucet = null!;

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Additional configuration is required to successfully run gRPC on macOS.
        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

        // Add services to the container.
        builder.Services.AddGrpc();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(name: MyAllowSpecificOrigins,
                                builder =>
                                {
                                    builder
                                    .AllowAnyOrigin()
                                    .AllowAnyHeader()
                                    .AllowAnyMethod()
                                    .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding"); ;
                                });
        });

        _rivalCoinsHomeDomain = builder.Configuration.GetValue<string>("RIVALCOINS_HOME_DOMAIN");

        if(false)
        {
            var moneyAccounts = await InitalizeRivalCoinAccountsAsync(
                (builder.Configuration.GetValue<string>("FAKE_MONEY_ISSUER_SEED"), builder.Configuration.GetValue<string>("FAKE_MONEY_DISTRIBUTOR_SEED")),
                (builder.Configuration.GetValue<string>("FAKE_MONEY_WRAPPER_ISSUER_SEED"), builder.Configuration.GetValue<string>("FAKE_MONEY_WRAPPER_DISTRIBUTOR_SEED")),
                (builder.Configuration.GetValue<string>("L1_HORIZON_URL"), builder.Configuration.GetValue<string>("L2_HORIZON_URL")),
                "FakeMONEY");
        }

        var usaAccounts = await InitalizeRivalCoinAccountsAsync(
            (builder.Configuration.GetValue<string>("FAKE_USA_ISSUER_SEED"), builder.Configuration.GetValue<string>("FAKE_USA_DISTRIBUTOR_SEED")),
            (builder.Configuration.GetValue<string>("FAKE_USA_WRAPPER_ISSUER_SEED"), builder.Configuration.GetValue<string>("FAKE_USA_WRAPPER_DISTRIBUTOR_SEED")),
            (builder.Configuration.GetValue<string>("L1_HORIZON_URL"), builder.Configuration.GetValue<string>("L2_HORIZON_URL")),
            "FakeUSA");

        builder.Services.AddSingleton(_ => usaAccounts);

        var app = builder.Build();

        app.UseGrpcWeb();
        app.UseCors();

        // Configure the HTTP request pipeline.

        app.MapGrpcService<RivalCoinsService>()
            .EnableGrpcWeb()
            .RequireCors(MyAllowSpecificOrigins);

        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

        app.MapMethods("/assetDetails", new[] { "OPTIONS" }, (HttpRequest request, string requestUrl) =>
        {
            return Microsoft.AspNetCore.Http.Results.Ok();
        })
        .RequireCors(MyAllowSpecificOrigins);

        app.MapPost("/assetDetails", async (List<AssetId> assets) =>
        {
            var rivalCoins = await Wallet.GetRivalCoinsAsync(_rivalCoinsHomeDomain);

            var matchingRivalCoins =
                from rivalCoin in rivalCoins
                from asset in assets
                where $"{asset.Code}:{asset.Issuer}" == rivalCoin.Asset.CanonicalName()
                select rivalCoin;

            return Results.Json(matchingRivalCoins.Select(matchingRivalCoin =>
                new AssetDetail()
                {
                    Code = matchingRivalCoin.Asset.Code,
                    Issuer = matchingRivalCoin.Asset.Issuer,
                    Logo = matchingRivalCoin.ImageUri,
                    Name = matchingRivalCoin.Name,
                    HomeDomain = _rivalCoinsHomeDomain,
                    Description = matchingRivalCoin.Description,
                }
                ).ToList(),
                new() { });
        })
        .RequireCors(MyAllowSpecificOrigins);

        _ = UpdateRivalCoinLiquidityAsync(
                Asset.CreateNonNativeAsset("FakeUSA", usaAccounts.L1.Issuer.Account.Info.AccountId),
                usaAccounts.Wrapper.Distributor);

        // current Rival Coin standings for https://rivalcoins.money
        var circulatingSupplies = new List<CirculatingSupply>();

        _ = RivalCoinCirculatingSupplyServerAsync(
            usaAccounts.Wrapper.Distributor.Account.Info.KeyPair,
            usaAccounts.L1.Distributor,            
            new Progress<List<CirculatingSupply>>(currentCirculatingSupplies => circulatingSupplies = currentCirculatingSupplies));

        app.MapGet("/rivalcoincirculatingsupplies", () =>
        {
            return circulatingSupplies;
        })
        .WithName("GetRivalCoinCirculatingSupplies")
        .RequireCors(MyAllowSpecificOrigins);

        // "faucet" airdrop
        if (false)
        {
            const int MaxOperationsPerTransaction = 100;
            const int StellarLedgerTimeSeconds = 5;

            _airdropFaucet = usaAccounts.L1.Distributor;

            var airdropBuffer = CreateBuffer<(string Destination, AssetTypeCreditAlphaNum Asset)>(TimeSpan.FromSeconds(StellarLedgerTimeSeconds), MaxOperationsPerTransaction);
            var airdropExecution = new ActionBlock<IList<(string Destination, AssetTypeCreditAlphaNum Asset)>>(AirdropAsync);

            app.MapGet("/airdrop", async ([FromQuery(Name = "destination")] string destination, [FromQuery(Name = "asset")] string asset) =>
            {
                var success = false;

                try
                {
                    var account = await usaAccounts.L1.Distributor.Server.Accounts.Account(destination);
                    var assetCannonicalName = asset.Replace('-', ':');
                    var acceptsAsset = account.Balances.Any(b => b.Asset != null && b.Asset.CanonicalName() == assetCannonicalName);

                    if (acceptsAsset)
                    {
                        success = airdropBuffer.Post((destination, (AssetTypeCreditAlphaNum)(Asset.Create(assetCannonicalName))));
                    }

                }
                catch (Exception) { }

                return success ? "success" : "failure";
            })
            .RequireCors(MyAllowSpecificOrigins);
        }

        app.Run();
    }

    private static async Task UpdateRivalCoinLiquidityAsync(AssetTypeCreditAlphaNum wrapped, Wallet wrapperDistribution)
    {
        while (true)
        {
            var timeInterval = Task.Delay(5 * 1000);
            var wrapperBalances = (await wrapperDistribution.Server.Accounts.Account(wrapperDistribution.Account.Info.AccountId))
                .Balances.Where(b => b.Asset.Type != AssetTypeNative.RestApiType && b.Asset.CanonicalName() != wrapped.CanonicalName());

            foreach (var wrapperBalance in wrapperBalances)
            {
                var distributionAccount = await wrapperDistribution.Server.Accounts.Account(wrapperDistribution.Account.Info.AccountId);
                var orders = await Sdk.Util.GetAllResultsAsync(wrapperDistribution.Server.Offers.ForAccount(distributionAccount.AccountId).Execute());
                var wrappedBalance = distributionAccount.Balances.First(b => b.Asset.CanonicalName() == wrapped.CanonicalName());
                var wrapper = Asset.CreateNonNativeAsset(wrapperBalance.Asset.Code(), wrapperBalance.Asset.Issuer());
                var targetSellQuantity = double.Parse(wrapperBalance.BalanceString);
                //var targetBuyQuantity = Math.Min(targetSellQuantity, double.Parse(wrappedBalance.BalanceString));
                var wrapperAssetInfo = (await wrapperDistribution.Server.Assets.AssetCode(wrapper.Code).AssetIssuer(wrapper.Issuer).Execute()).Records[0];
                var wrapperQuantityInCirculation = double.Parse(wrapperAssetInfo.Amount) - double.Parse(wrapperBalance.BalanceString);

                // update Rival Coin sale
                var sale = orders.FirstOrDefault(o => o.Selling.CanonicalName() == wrapper.CanonicalName() && o.Buying.CanonicalName() == wrapped.CanonicalName());
                if (sale == null || double.Parse(sale.Amount) != targetSellQuantity)
                {
                    var wrapperSellOrder = new TransactionBuilder(wrapperDistribution.Account.Info);
                    wrapperSellOrder.AddOperation(new ManageSellOfferOperation.Builder(
                        wrapper,
                        wrapped,
                        targetSellQuantity.ToString(),
                        "1.0")
                        .SetOfferId(long.Parse(sale.Id))
                        .Build());

                    var response = await wrapperDistribution.SubmitTransactionAsync(wrapperSellOrder.Build(), true, $"update {wrapper.CanonicalName()} sale");
                    if (response?.IsSuccess() is null or false)
                    {
                        Console.Error.WriteLine($"failed update {wrapper.CanonicalName()} sale");
                    }
                }

                // update Rival Coin buy back
                if(sale != null && wrapperQuantityInCirculation > 0.0)
                {
                    var buyBack = orders.FirstOrDefault(o => o.Selling.CanonicalName() == wrapped.CanonicalName() && o.Buying.CanonicalName() == wrapper.CanonicalName());
                    //var wrappedAvailableForSale = double.Parse(wrappedBalance.BalanceString) - double.Parse(wrappedBalance.SellingLiabilities) > 0.0;

                    var wrappedSellOrder = new TransactionBuilder(wrapperDistribution.Account.Info);
                    if (buyBack == null)
                    {
                        wrappedSellOrder.AddOperation(
                            new CreatePassiveSellOfferOperation.Builder(wrapped, wrapper, wrapperQuantityInCirculation.ToString(), "1.0")
                                .Build());
                    }
                    else
                    {
                        wrappedSellOrder.AddOperation(new ManageSellOfferOperation.Builder(
                                wrapped,
                                wrapper,
                                wrapperQuantityInCirculation.ToString(),
                                "1.0")
                            .SetOfferId(long.Parse(buyBack.Id))
                            .Build());
                    }

                    try
                    {
                        var response = await wrapperDistribution.SubmitTransactionAsync(wrappedSellOrder.Build(), true, $"update {wrapper.CanonicalName()} buy back");
                        if (response?.IsSuccess() is null or false)
                        {
                            Console.Error.WriteLine($"failed update {wrapper.CanonicalName()} buy back");
                        }
                    }
                    catch (Exception ex)
                    {
                        ;
                    }
                }
            }

            await timeInterval;
        }
    }

    private static async Task<RivalCoinAccounts> InitalizeRivalCoinAccountsAsync(
        (string Issuer, string Distributor) wrappedAccountSeed,
        (string Issuer, string Distributor) wrapperAccountSeed,
        (string L1, string L2) horizonUrl,
        string assetCode)
    {
        // initialize wrapped asset accounts
        var l1Wrapped = await InitializeWrappedAssetAccountsAsync(
            assetCode,
            (KeyPair.FromSecretSeed(wrappedAccountSeed.Issuer), KeyPair.FromSecretSeed(wrappedAccountSeed.Distributor)),
            horizonUrl.L1);
        var l2Wrapped = await InitializeWrappedAssetAccountsAsync(
            assetCode,
            (KeyPair.FromSecretSeed(wrappedAccountSeed.Issuer), KeyPair.FromSecretSeed(wrappedAccountSeed.Distributor)),
            horizonUrl.L2);
        var wrappedAsset = Asset.CreateNonNativeAsset(assetCode, l1Wrapped.Issuer.Account.Info.AccountId);

        // create demo Rival Coins
        await Wallet.CreateAccountAsync(KeyPair.FromSecretSeed(wrapperAccountSeed.Issuer), horizonUrl.L1);
        var wrapperIssuer = new Wallet(horizonUrl.L1, wrapperAccountSeed.Issuer, _rivalCoinsHomeDomain);
        await wrapperIssuer.InitializeAsync();

        await Wallet.CreateAccountAsync(KeyPair.FromSecretSeed(wrapperAccountSeed.Distributor), horizonUrl.L1);
        var wrapperDistributor = new Wallet(horizonUrl.L1, wrapperAccountSeed.Distributor, _rivalCoinsHomeDomain);
        await wrapperDistributor.InitializeAsync();

        var rivalCoinAssets =
            (await Wallet.GetRivalCoinsAsync(_rivalCoinsHomeDomain))
            .Where(r => r.Asset.Issuer == wrapperIssuer.Account.Info.AccountId)
            .Select(r => r.Asset.CanonicalName())
            .ToList();

        var existingRivalCoins =
            wrapperDistributor.Account.Info.Balances
            .Where(b => b.AssetIssuer == wrapperIssuer.Account.Info.AccountId)
            .Select(b => b.Asset.CanonicalName())
            .ToList();

        var rivalCoinsToCreate = rivalCoinAssets.Except(existingRivalCoins).ToList();

        if (rivalCoinsToCreate.Any())
        {
            const long LiquidityVolume = 100L * 1000L;

            await Sdk.Util.CreateRivalCoinsAsync(
                wrappedAsset,
                l1Wrapped.Distributor.Account.Signer!,
                LiquidityVolume,
                (wrapperIssuer.Account.Signer!, wrapperDistributor.Account.Signer!, wrapperIssuer),
                rivalCoinsToCreate.Select(r => r.Split(':')[0]).ToArray());

            if(false)
            {
                var bank = default(Wallet);
                var txBankSetup = new TransactionBuilder(bank.Account.Info);

                // fund wrapped asset
                txBankSetup
                    .AddOperation(new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(wrappedAsset)).Build())
                    .AddOperation(new PaymentOperation.Builder(
                        bank.Account.Info.KeyPair,
                        wrappedAsset,
                        (LiquidityVolume * rivalCoinsToCreate.Count).ToString())
                    .SetSourceAccount(l2Wrapped.Distributor.Account.Signer!)
                    .Build());

                // fund wrapper assets (Rival Coins)
                foreach (var rivalCoinToCreate in rivalCoinsToCreate)
                {
                    txBankSetup
                        .AddOperation(new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(rivalCoinToCreate)).Build())
                        .AddOperation(new PaymentOperation.Builder(
                            bank.Account.Info.KeyPair,
                            Asset.Create(rivalCoinToCreate),
                            LiquidityVolume.ToString())
                        .SetSourceAccount(wrapperIssuer.Account.Signer!)
                        .Build());
                }

                var result = await bank.SubmitTransactionAsync(txBankSetup.Build(), true, "Fund bank");
                if (result?.IsSuccess() is not true)
                {
                    Console.Error.WriteLine("Failed to fund bank!");
                }
            }
        }

        return new RivalCoinAccounts(
            l1Wrapped, 
            l2Wrapped,
            (wrapperIssuer, wrapperDistributor));
    }

    private static async Task<(Wallet Issuer, Wallet Distributor)> InitializeWrappedAssetAccountsAsync(string wrappedAssetCode, (KeyPair Issuer, KeyPair Distributor) accounts, string horizonUrl)
    {
        // initialize wrapped asset issuer account
        var wrappedIssuer = new Wallet(horizonUrl, accounts.Issuer.SecretSeed, _rivalCoinsHomeDomain);
        await Wallet.CreateAccountAsync(accounts.Issuer, wrappedIssuer.NetworkUrl);
        await wrappedIssuer.InitializeAsync();
        var wrappedAsset = Asset.CreateNonNativeAsset(wrappedAssetCode, wrappedIssuer.Account.Info.AccountId);

        // initialize wrapped asset distributor account
        var wrappedDistributor = new Wallet(horizonUrl, accounts.Distributor.SecretSeed, _rivalCoinsHomeDomain);
        await Wallet.CreateAccountAsync(accounts.Distributor, wrappedDistributor.NetworkUrl);
        await wrappedDistributor.InitializeAsync();
        if(false)
        {
            if (horizonUrl != "https://horizon.stellar.org")
            {
                var txFundWrappedDistributor = new TransactionBuilder(wrappedDistributor.Account.Info);
                RivalCoins.Sdk.Util.SendPayment(wrappedAsset, (1 * 1000 * 1000 * 1000).ToString(), wrappedIssuer.Account.Signer!, wrappedDistributor.Account.Signer!, txFundWrappedDistributor);
                var result = await wrappedDistributor.SubmitTransactionAsync(txFundWrappedDistributor.Build(), true, "Fund wrapped asset distributor");
                if (result?.IsSuccess() is null or false)
                {
                    throw new Exception("Failed to fund wrapped asset distributor");
                }
            }
        }

        return (wrappedIssuer, wrappedDistributor);
    }

    public static async Task RivalCoinCirculatingSupplyServerAsync(
        KeyPair wapperDistributor,
        Wallet wallet,
        IProgress<List<CirculatingSupply>> progress)
    {
        var NonRivalCoinAssetCodes = new[] { "MONEY", "FakeMONEY", "USA", "FakeUSA" };
        var maxBalanceSignificantDigits = ChangeTrustOperation.MaxLimit.Length - 1;
        var MaxDecimalPrecision = 7;

        while (true)
        {
            var newCirculatingSupplies = new List<CirculatingSupply>();
            var rivalCoinAssets = await Wallet.GetRivalCoinsAsync(wallet.HomeDomain);
            var rivalCoins = rivalCoinAssets.Where(rivalCoin => !NonRivalCoinAssetCodes.Contains(rivalCoin.Asset.Code)).ToList();
            var nonRivalCoins = rivalCoinAssets.Where(rivalCoin => NonRivalCoinAssetCodes.Contains(rivalCoin.Asset.Code)).ToList();

            var rivalCoinDistributionAccount = await wallet.Server.Accounts.Account(wapperDistributor.AccountId);

            // get non-Rival Coins
            foreach (var nonRivalCoin in nonRivalCoins)
            {
                var nonRivalCoinAssetInfo = await wallet.Server.Assets.AssetCode(nonRivalCoin.Asset.Code).AssetIssuer(nonRivalCoin.Asset.Issuer).Execute();

                newCirculatingSupplies.Add(new CirculatingSupply(nonRivalCoin.Asset, double.Parse(nonRivalCoinAssetInfo.Records.First().Amount)));
            }

            // get Rival Coins
            foreach (var rivalCoin in rivalCoins)
            {
                var rivalCoinBalance = rivalCoinDistributionAccount.Balances.FirstOrDefault(balance => balance.AssetCode == rivalCoin.Asset.Code && balance.AssetIssuer == rivalCoin.Asset.Issuer);
                if (rivalCoinBalance != null)
                {
                    var rivalCoinAssetInfo = (await wallet.Server.Assets.AssetCode(rivalCoin.Asset.Code).AssetIssuer(rivalCoin.Asset.Issuer).Execute()).Records.First();
                    var amountOutOfCirculation = rivalCoinBalance.BalanceString.ToStroops();
                    var totalInExistence = rivalCoinAssetInfo.Amount.ToStroops();
                    var amountInCirculation = totalInExistence - amountOutOfCirculation;
                    var circulatingSupply = double.Parse(amountInCirculation.ToString($"D{maxBalanceSignificantDigits}").Insert(maxBalanceSignificantDigits - MaxDecimalPrecision, "."));

                    newCirculatingSupplies.Add(new CirculatingSupply(rivalCoin.Asset, circulatingSupply));
                }
            }

            progress.Report(newCirculatingSupplies);

            await Task.Delay(60 * 1 * 1000);
        }
    }

    private static IPropagatorBlock<TIn, IList<TIn>> CreateBuffer<TIn>(TimeSpan timeSpan, int count)
    {
        var inBlock = new BufferBlock<TIn>();
        var outBlock = new BufferBlock<IList<TIn>>();

        var outObserver = outBlock.AsObserver();
        inBlock.AsObservable()
                .Buffer(timeSpan, count)
                .ObserveOn(TaskPoolScheduler.Default)
                .Subscribe(outObserver);

        return DataflowBlock.Encapsulate(inBlock, outBlock);
    }

    private static async Task AirdropAsync(IList<(string Destination, AssetTypeCreditAlphaNum Asset)> airdropInfos)
    {
        const string MoneyAirdropAmount = "3.2876712";

        Console.WriteLine($"tx #{_transactionNum++}");

        if (airdropInfos.Any())
        {
            using var server = new stellar_dotnet_sdk.Server(_airdropFaucet.NetworkUrl);
            var airdropAccount = await server.Accounts.Account(_airdropFaucet.Account.Info.AccountId);
            var tx = new TransactionBuilder(airdropAccount);

            foreach (var airdropInfo in airdropInfos)
            {
                Console.WriteLine($"airdrop #{_airdropNum} - {airdropInfo.Destination} to receive {airdropInfo.Asset.CanonicalName()}");

                tx.AddOperation(
                    new PaymentOperation.Builder(
                        KeyPair.FromAccountId(airdropInfo.Destination),
                        airdropInfo.Asset,
                        MoneyAirdropAmount).Build());
            }

            var finalTx = tx.Build();
            finalTx.Sign(_airdropFaucet.Account.Signer!, _airdropFaucet.NetworkInfo!);

            var result = await server.SubmitTransaction(finalTx);
            Console.WriteLine($"************************************ airdrop #{_airdropNum++} success: {result.IsSuccess()}");
        }
    }
}