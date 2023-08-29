using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Grpc.Core;
using Microsoft.AspNetCore.Server.HttpSys;
using RivalCoins.Sdk;
using RivalCoins.Sdk.Grpc;
using RivalCoins.Server.Model;
using stellar_dotnet_sdk;
using Transaction = RivalCoins.Sdk.Grpc.Transaction;

namespace RivalCoins.Server.Services;

public record CirculatingSupply(AssetTypeCreditAlphaNum Asset, double Value);

public class RivalCoinsService : Sdk.Grpc.RivalCoinsService.RivalCoinsServiceBase
{
    private const long HumanPopulation = 8L * 1000L * 1000L * 1000L;
    private const long MoneySupply = 9600L * 1000L * 1000L * 1000L;

    private readonly string _azureFormRecognizerApiKey;
    private readonly RivalCoinAccounts _accounts;

    private readonly AssetTypeCreditAlphaNum _fakeUsa;

    public RivalCoinsService(RivalCoinAccounts account, IConfiguration configuration)
    {
        _azureFormRecognizerApiKey = configuration.GetValue<string>("AzureFormRecognizerApiKey");
        _accounts = account;
        _fakeUsa = Asset.CreateNonNativeAsset("FakeUSA", _accounts.L1.Issuer.Account.Info.AccountId);
    }

    private static double UsaPricedInMoney { get; } = GetMoneyPrice(330L * 1000L * 1000L, 30L * 1000L * 1000L * 1000L * 1000L);

    private static double GetMoneyPrice(long currencyTargetMarketPopulation, long currencySupply)
    {
        var currencyTargetMarketPercentageOfWorld = currencyTargetMarketPopulation / (double)HumanPopulation;
        var moneyAllocationToCurrencyTargetMarket = currencyTargetMarketPercentageOfWorld * MoneySupply;

        return moneyAllocationToCurrencyTargetMarket / currencySupply;
    }

    public override async Task<Transaction> Swap(SwapRequest request, ServerCallContext context)
    {
        var bank = default(Wallet);
        var user = await bank.Server.Accounts.Account(request.User);
        var txSwap = new TransactionBuilder(user)
            // decrement swap out
            .AddOperation(new PaymentOperation.Builder(
                bank.Account.Info.KeyPair,
                Asset.Create(request.SwapOut),
                request.Quantity).Build())

            // increment swap in
            .AddOperation(new PaymentOperation.Builder(
                    user.KeyPair,
                    Asset.Create(request.SwapIn),
                    request.Quantity)
                .SetSourceAccount(bank.Account.Info.KeyPair)
                .Build())
            .Build();

        // sign transaction
        txSwap.Sign(bank.Account.Signer!, bank.NetworkInfo!);

        return new Transaction() { Success = true, SignedXdr = txSwap.ToEnvelopeXdrBase64(), Message = $"Out: {request.SwapOut}, In: {request.SwapIn}, Quantity: {request.Quantity}"};
    }

    public override async Task<Success> Airdrop(AirDropRequest request, ServerCallContext context)
    {
        const int AirdropQuantity = 100;

        var tx = new Success() { Success_ = false };

        try
        {
            var l1UserAccount = await _accounts.L1.Distributor.Server.Accounts.Account(request.RecipientAddress);
            var l2UserAccount = await _accounts.L2.Distributor.Server.Accounts.Account(request.RecipientAddress);
            var wrapperAsset = Asset.Create(request.Asset);

            if (l2UserAccount.Balances.FirstOrDefault(b => b.Asset.CanonicalName() == _fakeUsa.CanonicalName()) == null)
            {
                tx = new() { Success_ = false, Message = $"L1 User does not trust {_fakeUsa.CanonicalName()}" };
            }
            else if (l2UserAccount.Balances.FirstOrDefault(b => b.Asset.CanonicalName() == _fakeUsa.CanonicalName()) == null)
            {
                tx = new() { Success_ = false, Message = $"L2 User does not trust {_fakeUsa.CanonicalName()}" };
            }
            else if (l2UserAccount.Balances.FirstOrDefault(b => b.Asset.CanonicalName() == request.Asset) == null)
            {
                tx = new() { Success_ = false, Message = $"L2 User does not trust {request.Asset}" };
            }
            else
            {
                // L1 airdrop of wrapped asset
                var txL1Airdrop = new TransactionBuilder(_accounts.L1.Distributor.Account.Info);
                txL1Airdrop.AddOperation(new PaymentOperation.Builder(l1UserAccount.KeyPair, _fakeUsa, AirdropQuantity.ToString()).Build());
                var result = await _accounts.L1.Distributor.SubmitTransactionAsync(txL1Airdrop.Build(), true, $"L1 airdrop of {_fakeUsa.CanonicalName()} to {request.RecipientAddress}");
                if (result?.IsSuccess() is null or false)
                {
                    tx = new()
                    {
                        Success_ = false,
                        Message = $"L1 failed to airdrop {_fakeUsa.CanonicalName()} to {request.RecipientAddress}"
                    };
                }

                // L2 airdrop of wrapper asset
                var bank = default(Wallet);
                var txL2Airdrop = new TransactionBuilder(bank.Account.Info);
                txL2Airdrop.AddOperation(new PaymentOperation.Builder(l2UserAccount.KeyPair, wrapperAsset, AirdropQuantity.ToString()).Build());
                result = await bank.SubmitTransactionAsync(txL2Airdrop.Build(), true, $"L2 airdrop of {wrapperAsset.CanonicalName()} to {request.RecipientAddress}");
                if (result?.IsSuccess() is null or false)
                {
                    tx = new()
                    {
                        Success_ = false,
                        Message = $"L2 failed to airdrop {wrapperAsset.CanonicalName()} to {request.RecipientAddress}"
                    };
                }

                tx = new() { Success_ = true };
            }
        }
        catch (Exception e)
        {
            tx = new() { Success_ = false, Message = e.Message };
        }

        return tx;
    }

    public override async Task<TaxContributionResponse> ReceiveTaxContributionHonor(TaxContributionInfo request, ServerCallContext context)
    {
        string endpoint = "https://receipt-tax-reader.cognitiveservices.azure.com";
        AzureKeyCredential credential = new AzureKeyCredential(_azureFormRecognizerApiKey);
        DocumentAnalysisClient client = new DocumentAnalysisClient(new Uri(endpoint), credential);

        var receiptBytes = new byte[request.Receipt.Length];
        request.Receipt.CopyTo(receiptBytes, 0);
       
        using var receiptStream = new MemoryStream(receiptBytes);
        AnalyzeDocumentOperation operation = await client.AnalyzeDocumentAsync(WaitUntil.Started, "prebuilt-receipt", receiptStream);

        await operation.WaitForCompletionAsync();

        AnalyzeResult result = operation.Value;

        for (int i = 0; i < result.Documents.Count; i++)
        {
            Console.WriteLine($"Document {i}:");

            AnalyzedDocument document = result.Documents[i];

            if (document.Fields.TryGetValue("TotalTax", out DocumentField? totalTaxField))
            {
                if (totalTaxField.FieldType == DocumentFieldType.Double)
                {
                    double totalTax = totalTaxField.Value.AsDouble();
                    Console.WriteLine($"Total Tax: '{totalTax}', with confidence {totalTaxField.Confidence}");

                    var recipient = await _accounts.L1.Distributor.Server.Accounts.Account(request.AccountId);
                    var airDropTx = new TransactionBuilder(recipient);

                    // create trustline
                    //      Remember, the TRANSACTION source account is the user's account, thus any
                    //      OPERATION without a source account explicitly set will default to the
                    //      transaction's source account, which is the user's account.  Thus, a 
                    //      trustline is being made on the user's account.
                    airDropTx.AddOperation(
                        new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(_fakeUsa))
                            .Build());

                    airDropTx.AddOperation(new PaymentOperation.Builder(
                        KeyPair.FromAccountId(request.AccountId),
                        _fakeUsa,
                        totalTax.ToString("N7"))
                        .SetSourceAccount(_accounts.L1.Distributor.Account.Info.KeyPair)
                        .Build());

                    // create trustline
                    //      Remember, the TRANSACTION source account is the user's account, thus any
                    //      OPERATION without a source account explicitly set will default to the
                    //      transaction's source account, which is the user's account.  Thus, a 
                    //      trustline is being made on the user's account.
                    airDropTx.AddOperation(
                        new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(_fakeUsa)).Build());

                    airDropTx.AddOperation(new PaymentOperation.Builder(
                        KeyPair.FromAccountId(request.AccountId),
                        _fakeUsa,
                        (totalTax * UsaPricedInMoney).ToString("N7"))
                        .SetSourceAccount(_accounts.L1.Distributor.Account.Info.KeyPair)
                        .Build());

                    var signedAirDropTxXdr = airDropTx.Build();
                    signedAirDropTxXdr.Sign(_accounts.L1.Distributor.Account.Signer!, _accounts.L1.Distributor.NetworkInfo!);

                    return new TaxContributionResponse()
                    {
                        Success = new Success() { Success_ = true, Message = "Hello World!" },
                        SignedXdr = signedAirDropTxXdr.ToEnvelopeXdrBase64()
                    };
                }
            }
        }

        return new TaxContributionResponse() {  Success = new Success() {  Success_ = false } };
    }

    public override async Task<Sdk.Grpc.Transaction> CreateAirDropTransaction(AirDropRequest request, ServerCallContext context)
    {
        // user will pay the air drop transaction fee

        //TODO validate transaction ability to execute transaction

        var recipient = await _accounts.L1.Distributor.Server.Accounts.Account(request.RecipientAddress);
        var airDropTransaction = new TransactionBuilder(recipient);

        // create trustline
        //      Remember, the TRANSACTION source account is the user's account, thus any
        //      OPERATION without a source account explicitly set will default to the
        //      transaction's source account, which is the user's account.  Thus, a 
        //      trustline is being made on the user's account.
        //      
        //      The second parameter really should be null, but the API does not allow it.
        //      The value is only used when signing the transaction.  I'm intentionally
        //      setting it to the Air Drop account, since that's redundant with
        //      the payment operation and will thus have no real effect.
        airDropTransaction.AddOperation(
            new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(_fakeUsa)).Build());

        // send air drop
        airDropTransaction.AddOperation(
            new PaymentOperation.Builder(
                KeyPair.FromAccountId(request.RecipientAddress),
                _fakeUsa,
                (MoneySupply / (double)HumanPopulation / 365).ToString("N7"))
                .SetSourceAccount(_accounts.L1.Distributor.Account.Info.KeyPair)
                .Build());

        // sign
        var signedTx = airDropTransaction.Build();
        signedTx.Sign(_accounts.L1.Distributor.Account.Signer!, _accounts.L1.Distributor.NetworkInfo!);

        return new Sdk.Grpc.Transaction() { UnsignedXdr = signedTx.ToEnvelopeXdrBase64() };
    }

    public override async Task<Success> SubmitAirDropTransaction(SignedTransaction request, ServerCallContext context)
    {
        var tx = stellar_dotnet_sdk.Transaction.FromEnvelopeXdr(request.Xdr);

        tx.Sign(_accounts.L1.Distributor.Account.Signer!, _accounts.L1.Distributor.NetworkInfo!);

        var xdr = tx.ToEnvelopeXdrBase64();
        Console.WriteLine(xdr);
        Console.WriteLine();
        Console.WriteLine();

        var response = await _accounts.L1.Distributor.Server.SubmitTransaction(tx);

        if (!response.IsSuccess())
        {
            Console.WriteLine($"Failure Message: {response.ResultXdr}");
        }

        return new Success() { Success_ = response.IsSuccess() };
    }
}
