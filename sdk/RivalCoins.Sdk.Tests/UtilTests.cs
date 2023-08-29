using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Reflection.Metadata;
using Common.Logging.Configuration;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.DataCollection;
using NUnit.Framework;
using Serilog;
using Serilog.Events;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using stellar_dotnet_sdk.xdr;
using Asset = stellar_dotnet_sdk.Asset;
using ChangeTrustAsset = stellar_dotnet_sdk.ChangeTrustAsset;
using Signer = stellar_dotnet_sdk.Signer;
using Transaction = stellar_dotnet_sdk.Transaction;

namespace RivalCoins.Sdk.Tests;

[TestFixture]
public class UtilTests
{
    private const string StellarContainerName = "stellar";
    private const string DefaultCurrencyAssetCode = "MyAsset";

    private Process Proc { get; set; }
    private Wallet Wallet { get; set; }

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        Console.SetOut(TestContext.Progress);

        this.Proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = @$"run --rm -it -p ""8000:8000"" -p ""8001:8001"" --name {StellarContainerName} rivalcoins/stellar-quickstart --standalone",
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        this.Proc.Start();

        Task.Run(() =>
        {
            string standardOutput;
            while ((standardOutput = this.Proc.StandardOutput.ReadLine()) != null)
            {
                Console.WriteLine(standardOutput);
            }
        });

        Task.Delay(40 * 1000).Wait();

    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Process.Start("docker", $"kill {StellarContainerName}").WaitForExit();
    }


    [Test]
    public async Task CreatePlayMoneyCurrencySystem()
    {
        const Network Network = Network.Local;

        var root = KeyPair.FromSecretSeed("<CHANGE ME>");
        using var wallet = Wallet.Default[Network] with { AccountSecretSeed = KeyPair.Random().SecretSeed };
        await Wallet.CreateAccountAsync(KeyPair.FromSecretSeed(wallet.AccountSecretSeed), Network);
        await wallet.InitializeAsync();

        var system = await Util.CreateCurrencySystemAsync(
            "PlayMONEY",
            9600L * 1000L * 1000L * 1000L,
            Util.MaxTrustlineLimit,
            wallet);

        var assetWrapping = (Issuing: KeyPair.Random(), Liquidity: KeyPair.Random());
        await Wallet.CreateAccountAsync(assetWrapping.Issuing, wallet.Network);
        await Wallet.CreateAccountAsync(assetWrapping.Liquidity, wallet.Network);
        //var txAssetWrappingAccountCreation = new TransactionBuilder(wallet.Account.Info)
        //    .AddOperation(new CreateAccountOperation(assetWrapping.Issuing, "1000"))
        //    .AddOperation(new CreateAccountOperation(assetWrapping.Liquidity, "1000"))
        //    .AddOperation(new SetOptionsOperation.Builder()
        //        .SetSetFlags((int)AccountFlag.AuthImmutableFlag)
        //        .SetSourceAccount(assetWrapping.Issuing)
        //        .Build())
        //    .Build();
        //await wallet.SubmitTransactionAsync(txAssetWrappingAccountCreation, true, "Create asset wrapping");

        Console.WriteLine($"Issuing - {assetWrapping.Issuing.AccountId}:{assetWrapping.Issuing.SecretSeed}");
        Console.WriteLine($"Liquidity - {assetWrapping.Liquidity.AccountId}:{assetWrapping.Liquidity.SecretSeed}");

        var issuing = assetWrapping.Issuing;
        var liquidity = assetWrapping.Liquidity;

        var playMoneyIssuer = system.Issuing;
        var playMoneyFunder = system.Distributions.First();
        await Util.CreateRivalCoinAsync(
            (AssetTypeCreditAlphaNum)Asset.CreateNonNativeAsset("PlayMONEY", playMoneyIssuer.AccountId),
            playMoneyFunder,
            1000.0,
            "JeromeBellSr",
            (issuing, liquidity),
            wallet);
    }

    [Test]
    public async Task Performance()
    {
        const Network Network = Network.Local;

        var numTransactions = 0;
        var numAccounts = 100; // Environment.ProcessorCount;
        using var cancel = new CancellationTokenSource();
        using var httpClient = new HttpClient();

        //System.Net.ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

        using var wallet = Wallet.Default[Network] with { AccountSecretSeed = KeyPair.Random().SecretSeed };
        await Wallet.CreateAccountAsync(KeyPair.FromSecretSeed(wallet.AccountSecretSeed), Network);
        await wallet.InitializeAsync();

        async Task ExecuteTransactionsAsync(AccountResponse account, KeyPair signer)
        {
            while (!cancel.IsCancellationRequested)
            {
                var tx = new TransactionBuilder(account)
                    .AddOperation(new BumpSequenceOperation(0))
                    .Build();

                tx.Sign(signer, wallet.NetworkInfo);

                await wallet.SubmitTransactionAsync(tx, false, String.Empty);

                Interlocked.Increment(ref numTransactions);
            }
        }

        // for (var i = 0; i < accounts.Length; i++)
        //{
        //     accounts[i] = (Wallet.Default[Network] with { AccountSecretSeed = KeyPair.Random().SecretSeed }, null);
        //    await Wallet.CreateAccountAsync(KeyPair.FromSecretSeed(accounts[i].Wallet.AccountSecretSeed), Network);
        //    await accounts[i].Wallet.InitializeAsync();
        //}

        var accounts = new List<(AccountResponse Account, KeyPair Signer)>();
        for(var i = 0; i < 1; i++)
        {
            accounts.AddRange(await Task.WhenAll(Enumerable.Range(1, 10).Select(async _ => {
                var account = KeyPair.Random();
                await Wallet.CreateAccountAsync(account, Network, httpClient);

                return (await wallet.Server.Accounts.Account(account.AccountId), account);
            })));
        }

        foreach (var w in accounts)
        {
            _ = ExecuteTransactionsAsync(w.Account, w.Signer);
        }

        await Task.Delay(10 * 1000);

        Console.WriteLine($"Num tx: {numTransactions}");

        cancel.Cancel();
    }

    [Test]
    public async Task Testnet()
    {
        const Network Network = Network.Testnet;

        var senderWallet = Wallet.Default[Network] with { AccountSecretSeed = KeyPair.Random().SecretSeed };
        await Wallet.CreateAccountAsync(KeyPair.FromSecretSeed(senderWallet.AccountSecretSeed), Network);
        await senderWallet.InitializeAsync();

        var accountToCreate = KeyPair.Random();

        var latestLedger = senderWallet.Server.Root().CoreLatestLedger;
        var recipientAccountCreationLedgerBounds = new stellar_dotnet_sdk.LedgerBounds((uint)latestLedger, (uint)latestLedger + 100u);

        var txRecipientAccountCreation = new TransactionBuilder(senderWallet.Account.Info)
            // create recipient account
            .AddOperation(new CreateAccountOperation(accountToCreate, "20"))

            // set recipient account sequence number
            .AddOperation(new BumpSequenceOperation(recipientAccountCreationLedgerBounds.MaxLedger + 10) { SourceAccount = accountToCreate })

            //// recipient accepts USA
            //.AddOperation(new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(usa))
            //    .SetSourceAccount(accounts.Recipient)
            //    .Build())

            // make deterministic recipient account sequence number
            .AddPreconditions(new TransactionPreconditions() { LedgerBounds = recipientAccountCreationLedgerBounds })

            .Build();

        var response = await senderWallet.SubmitTransactionAsync(txRecipientAccountCreation, true, "Create account");
        if(response?.IsSuccess() is null or false)
        {
            throw new Exception("failure!");
        }

        Console.WriteLine($"Tx submitter - {senderWallet.Account.Info.AccountId}");
    }

    [Test]
    public async Task Test1()
    {
        const Network Network = Network.Local;

        var usa = (AssetTypeCreditAlphaNum)Asset.CreateNonNativeAsset("USA", "GBKIY2TXQFCVTR5UFP3WOOPK3BPXAATZPIFZYSXDQVC2WWL7CURYY7YK");
        var govFundRewards = (AssetTypeCreditAlphaNum)Asset.CreateNonNativeAsset("GFRewards", "GCBWYXOGB35C7HTJPCUSADBKESJAV7IEE3VIARHFOX3JHT6VB2AZSNOL");
        var airdrop = Wallet.Default[Network] with { AccountSecretSeed = "<CHANGE ME>" };
        var rabetUser = KeyPair.FromAccountId("GDIWVNF65PPP3OXLZQBHJD6MDYYCHDVIZSFUTEUK6GJDVAZQYRIIDVXL");

        //await Util.CreateGovFundRewardsEnvironmentAsync(Network);
        //return;

        var existingRecipient = Wallet.Default[Network] with { AccountSecretSeed = KeyPair.Random().SecretSeed };
        var nonExistentRecipient = KeyPair.Random();

        await airdrop.InitializeAsync();

        {
            var random = KeyPair.Random();
            await Wallet.CreateAccountAsync(random, Network);
            var randomWallet = Wallet.Default[Network] with { AccountSecretSeed = random.SecretSeed };
            await randomWallet.InitializeAsync();
            var txFund = new TransactionBuilder(randomWallet.Account.Info)
                .AddOperation(new PaymentOperation.Builder(airdrop.Account.Signer, new AssetTypeNative(), "900").Build())
                .Build();
            txFund.Sign(random, randomWallet.NetworkInfo);
            await randomWallet.SubmitTransactionAsync(txFund, false, "Fund airdrop");
        }

        await Wallet.CreateAccountAsync(KeyPair.FromSecretSeed(existingRecipient.AccountSecretSeed), Network);
        await existingRecipient.InitializeAsync();

        var txAcceptUsa = new TransactionBuilder(existingRecipient.Account.Info)
            .AddOperation(new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(usa)).Build())
            .Build();

        await existingRecipient.SubmitTransactionAsync(txAcceptUsa, true, "Recipient accepts USA");

        // create escrow account
        var escrowCreation = await GetEscrowCreatorAsync(
            existingRecipient.Account.Info.KeyPair,
            Network);

        var response = await existingRecipient.SubmitTransactionAsync(escrowCreation.EscrowCreation, true, "Create escrow account");
        if (response?.IsSuccess() is null or false)
        {
            throw new Exception("Failed to create escrow account");
        }

        // configure escrow account
        var escrowBuilder = await GetEscrowBuilderAsync(
            (airdrop.Account.Signer, existingRecipient.Account.Info.KeyPair, escrowCreation.Escrow),
            usa,
            Network);

        var escrowWallet = Wallet.Default[Network] with { AccountSecretSeed = escrowCreation.Escrow.SecretSeed };
        await escrowWallet.InitializeAsync();

        response = await escrowWallet.SubmitTransactionAsync(escrowBuilder.Configuration, false, "Configure escrow account");
        if (response?.IsSuccess() is null or false)
        {
            throw new Exception("Failed to configure escrow account");
        }
        
        //{
        //    var channelClose = await GetAirdropForExistingUserAsync(
        //        (airdrop.Account.Signer, existingRecipient.Account.Info.KeyPair, escrowWallet.Account.Signer),
        //        usa,
        //        10.0,
        //        Network);

        //    escrowBuilder.Declaration.Sign(existingRecipient.Account.Signer, existingRecipient.NetworkInfo);

        //    var closing = Transaction.FromEnvelopeXdr(channelClose);
        //    closing.Sign(existingRecipient.Account.Signer, existingRecipient.NetworkInfo);

        //    await CloseChannelAsync(closing, escrowBuilder.Declaration, existingRecipient);

        //    Console.WriteLine($"Check created account: {existingRecipient.Account.Info.AccountId}");
        //}

        {
            var channel = await GetAirdropForNonExistentRecipientAsync(
                (airdrop.Account.Signer, nonExistentRecipient),
                usa,
                govFundRewards,
                10.0,
                Network);

            var currentLedger = airdrop.Server.Root().CoreLatestLedger;

            channel.RecipientAccountCreationXdr.Sign(nonExistentRecipient, airdrop.NetworkInfo);

            response = await airdrop.SubmitTransactionAsync(channel.RecipientAccountCreationXdr, false, "Create recipient account");
            if (response?.IsSuccess() is null or false)
            {
                throw new Exception("Failed to create recipient account");
            }

            var recipientAccount = await airdrop.Server.Accounts.Account(nonExistentRecipient.AccountId);
            Console.WriteLine($"Tx XDR - {channel.RecipientAccountCreationXdr}");
            Console.WriteLine($"Ledger before tx: {currentLedger}");
            Console.WriteLine($"Recipient account sequence number: {recipientAccount.SequenceNumber}");
            Console.WriteLine($"Recipient account: {nonExistentRecipient.AccountId}");

            var arbitrary = Wallet.Default[Network] with { AccountSecretSeed = KeyPair.Random().SecretSeed };
            await Wallet.CreateAccountAsync(KeyPair.FromSecretSeed(arbitrary.AccountSecretSeed), Network);
            await arbitrary.InitializeAsync();
            var txSendXlm = new TransactionBuilder(arbitrary.Account.Info)
                .AddOperation(new PaymentOperation.Builder(nonExistentRecipient, new AssetTypeNative(), "100.0").Build())
                .Build();
            await arbitrary.SubmitTransactionAsync(txSendXlm, true, "Send XLM");

            channel.LoanPayback.Sign(nonExistentRecipient, airdrop.NetworkInfo);
            response = await airdrop.SubmitTransactionAsync(channel.LoanPayback, false, "Loan payback");
            if (response?.IsSuccess() is null or false)
            {
                throw new Exception("Failed to payback loan");
            }

            return;

            channel.Channel.Escrow.Configuration.Sign(nonExistentRecipient, airdrop.NetworkInfo);
            response = await airdrop.SubmitTransactionAsync(channel.Channel.Escrow.Configuration, false, "Configure escrow account");
            if (response?.IsSuccess() is null or false)
            {
                throw new Exception("Failed to configure escrow account");
            }

            channel.Channel.Channel.Declaration.Sign(nonExistentRecipient, airdrop.NetworkInfo);

            channel.Channel.Channel.Close.Sign(airdrop.Account.Signer, airdrop.NetworkInfo);
            channel.Channel.Channel.Close.Sign(nonExistentRecipient, airdrop.NetworkInfo);

            await CloseChannelAsync(
                channel.Channel.Channel.Close,
                channel.Channel.Channel.Declaration,
                airdrop.Network);

            Console.WriteLine($"Check created account: {nonExistentRecipient.AccountId}");
        }
    }

    private static async Task CloseChannelAsync(Transaction close, Transaction declaration, Wallet closer)
    {
        // submit declaration
        await closer.SubmitTransactionAsync(declaration, true, "Channel close declaration");

        // close channel
        await closer.SubmitTransactionAsync(close, true, "Channel close");
    }

    private static async Task CloseChannelAsync(Transaction close, Transaction declaration, Network network)
    {
        // submit declaration
        await Wallet.SubmitTransactionAsync(declaration, true, network, "Channel close declaration");

        // close channel
        await Wallet.SubmitTransactionAsync(close, true, network, "Channel close");
    }

    private static async Task<(KeyPair Escrow, Transaction EscrowCreation)> GetEscrowCreatorAsync(
        KeyPair recipient,
        Network network)
    {
        const double NetworkFeesBuffer = 10.0;

        using var server = new Server(Wallet.GetHorizonUri(network));

        var minimumEscrowXlmBalance = Wallet.GetMinimumBalance(
            0.5,
            // XLM
            1

            // USA
            + 1,
            0,
            0);

        var wallet = KeyPair.Random();
        var recipientAccount = await server.Accounts.Account(recipient.AccountId);
        var txCreateWallet = new TransactionBuilder(recipientAccount)
            // create wallet account
            .AddOperation(new CreateAccountOperation(wallet, (NetworkFeesBuffer + minimumEscrowXlmBalance).ToString()))
            .Build();

        return (wallet, txCreateWallet);
    }

    private static async Task<string> GetAirdropForExistingUserAsync(
        (KeyPair Sender, KeyPair Recipient, KeyPair Escrow) accounts,
        AssetTypeCreditAlphaNum usa,
        double amount,
        Network network)
    {
        var escrowWallet = Wallet.Default[network] with { AccountSecretSeed = accounts.Escrow.SecretSeed };
        await escrowWallet.InitializeAsync();

        //var declarationKey = new SignerKey() { Ed25519SignedPayload = new SignerKey.SignerKeyEd25519SignedPayload()
        //    {
        //        Ed25519 = new Uint256(escrowWallet.Account.Info.KeyPair.PublicKey),
        //        Payload = txDeclaration.Hash()
        //    }
        //};
        
        var txClose = new TransactionBuilder(new AccountResponse(escrowWallet.Account.Info.AccountId, escrowWallet.Account.Info.SequenceNumber + 1))
            // fund escrow account
            .AddOperation(new PaymentOperation.Builder(
                escrowWallet.Account.Info.KeyPair,
                usa,
                amount.ToString())
                .SetSourceAccount(accounts.Sender)
                .Build())

            // pay recipient from escrow account
            .AddOperation(new PaymentOperation.Builder(
                accounts.Recipient,
                usa,
                amount.ToString())
                .SetSourceAccount(escrowWallet.Account.Info.KeyPair)
                .Build())

            // grant sender full control over escrow account
            .AddOperation(new SetOptionsOperation.Builder()
                .SetSigner(accounts.Sender.XdrSignerKey, 2)
                .Build())
            .Build();

        txClose.Sign(accounts.Sender, escrowWallet.NetworkInfo);

        return txClose.ToEnvelopeXdrBase64();
    }

    private static async Task<(Transaction Configuration, Transaction Declaration, Transaction FaultRecovery)> GetEscrowBuilderAsync(
        (KeyPair Sender, KeyPair Recipient, KeyPair Escrow) accounts,
        AssetTypeCreditAlphaNum usa,
        Network network)
    {
        var escrowWallet = Wallet.Default[network] with { AccountSecretSeed = accounts.Escrow.SecretSeed };
        await escrowWallet.InitializeAsync();

        var txDeclaration = new TransactionBuilder(new AccountResponse(escrowWallet.Account.Info.AccountId, escrowWallet.Account.Info.SequenceNumber + 1))
            // add a "no op" operation
            .AddOperation(new BumpSequenceOperation(0))
            .Build();

        var txFaultRecovery = new TransactionBuilder(new AccountResponse(escrowWallet.Account.Info.AccountId, escrowWallet.Account.Info.SequenceNumber + 2))
            // grant sender full control over escrow account
            .AddOperation(new SetOptionsOperation.Builder()
                .SetSigner(accounts.Sender.XdrSignerKey, 2)
                .Build())

            // add synchrony period
            .AddPreconditions(new TransactionPreconditions() { MinSeqAge = (ulong)TimeSpan.FromDays(1).Milliseconds })
            .Build();

        // configure escrow account
        var txEscrowConfiguration = new TransactionBuilder(new AccountResponse(escrowWallet.Account.Info.AccountId, escrowWallet.Account.Info.SequenceNumber))
            // accept USA
            .AddOperation(new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(usa)).Build())

            // require 2 signers
            .AddOperation(new SetOptionsOperation.Builder()
                .SetLowThreshold(2)
                .SetMediumThreshold(2)
                .SetHighThreshold(2)
                .SetMasterKeyWeight(0)
                .Build())

            // add sender as signer
            .AddOperation(new SetOptionsOperation.Builder()
                .SetSigner(accounts.Sender.XdrSignerKey, 1).Build())

            // add recipient as signer
            .AddOperation(new SetOptionsOperation.Builder()
                .SetSigner(accounts.Recipient.XdrSignerKey, 1).Build())

            // add declaration transaction as signer
            .AddOperation(new SetOptionsOperation.Builder()
                .SetSigner(Signer.PreAuthTx(txDeclaration, escrowWallet.NetworkInfo), 1).Build())

            // add fault recover transaction as signer
            .AddOperation(new SetOptionsOperation.Builder()
                .SetSigner(Signer.PreAuthTx(txFaultRecovery, escrowWallet.NetworkInfo), 1).Build())

            .Build();

        txEscrowConfiguration.Sign(escrowWallet.Account.Signer, escrowWallet.NetworkInfo);

        return (txEscrowConfiguration, txDeclaration, txFaultRecovery);
    }

    private static async Task<Account> CreateEscrowAccountAsync(double startingBalance, Wallet funder)
    {
        const uint TransactionLedgerBuffer = 100u;
        const int AccountSequenceBuffer = 10;

        var latestLedger = funder.Server.Root().CoreLatestLedger;
        var escrowAccountCreationLedgerBounds = new stellar_dotnet_sdk.LedgerBounds((uint)latestLedger, (uint)latestLedger + TransactionLedgerBuffer);
        var escrowAccountSequenceNumber = (1L * escrowAccountCreationLedgerBounds.MaxLedger + AccountSequenceBuffer) << 32;
        var escrow = KeyPair.Random();

        var txEscrowCreation = new TransactionBuilder(funder.Account.Info)
            // create recipient account
            .AddOperation(new CreateAccountOperation(escrow, startingBalance.ToString()))

            // set recipient account sequence number
            .AddOperation(new BumpSequenceOperation(escrowAccountSequenceNumber) { SourceAccount = escrow })

            // make deterministic recipient account sequence number
            .AddPreconditions(new TransactionPreconditions() { LedgerBounds = escrowAccountCreationLedgerBounds })

            .Build();

        var response = await funder.SubmitTransactionAsync(txEscrowCreation, false, "Create escrow account");
        if(response?.IsSuccess() is null or false)
        {
            throw new Exception("Failed to create escrow account");
        }

        return new Account(escrow.AccountId, escrowAccountSequenceNumber);
    }

    private record PaymentChannel(
        (Transaction Creation, Transaction Configuration) Escrow, 
        (Transaction Declaration, Transaction Close, Transaction FaultRecovery) Channel);

    private static async Task<(Transaction RecipientAccountCreationXdr, Transaction LoanPayback, PaymentChannel Channel)> GetAirdropForNonExistentRecipientAsync(
            (KeyPair Sender, KeyPair Recipient) accounts,
            AssetTypeCreditAlphaNum usa,
            AssetTypeCreditAlphaNum govFundRewards,
            double amount,
            Network network)
    {
        const double NetworkFeesBuffer = 10.0;

        var senderWallet = Wallet.Default[network] with { AccountSecretSeed = accounts.Sender.SecretSeed };
        await senderWallet.InitializeAsync();

        //var declarationKey = new SignerKey() { Ed25519SignedPayload = new SignerKey.SignerKeyEd25519SignedPayload()
        //    {
        //        Ed25519 = new Uint256(escrowWallet.Account.Info.KeyPair.PublicKey),
        //        Payload = txDeclaration.Hash()
        //    }
        //};

        var minimumRecipientXlmBalance = Wallet.GetMinimumBalance(
            0.5,
            // XLM
            1

            // USA
            + 1

            // Gov Fund Rewards subscription
            +1,
            0,
            0);

        const uint TransactionLedgerBuffer = 100u;
        const int AccountSequenceBuffer = 10;

        var latestLedger = senderWallet.Server.Root().CoreLatestLedger;
        var recipientAccountCreationLedgerBounds = new stellar_dotnet_sdk.LedgerBounds((uint)latestLedger, (uint)latestLedger + TransactionLedgerBuffer);
        var recipientAccountSequenceNumber = (1L * recipientAccountCreationLedgerBounds.MaxLedger + AccountSequenceBuffer) << 32;
        var recipientStartingBalance = NetworkFeesBuffer + minimumRecipientXlmBalance;

        Console.WriteLine($"Min ledger - {recipientAccountCreationLedgerBounds.MinLedger}");
        Console.WriteLine($"Max ledger - {recipientAccountCreationLedgerBounds.MaxLedger}");
        Console.WriteLine($"Account start - {recipientAccountSequenceNumber}");

        var txRecipientAccountCreation = new TransactionBuilder(new Account(senderWallet.Account.Info.AccountId, senderWallet.Account.Info.SequenceNumber))
            // create recipient account
            .AddOperation(new CreateAccountOperation(accounts.Recipient, recipientStartingBalance.ToString()))
            
            // set recipient account sequence number
            .AddOperation(new BumpSequenceOperation(recipientAccountSequenceNumber) { SourceAccount = accounts.Recipient })
    
            .AddOperation(new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(usa))
                .SetSourceAccount(accounts.Recipient)
                .Build())
                           
            .AddOperation(new PaymentOperation.Builder(
                    accounts.Recipient,
                    usa,
                    amount.ToString())
                .SetSourceAccount(accounts.Sender)
                .Build())

            // recipient subscribes to Gov Fund Rewards
            .AddOperation(new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(govFundRewards))
                .SetSourceAccount(accounts.Recipient)
                .Build())

            // make deterministic recipient account sequence number
            .AddPreconditions(new TransactionPreconditions() { LedgerBounds = recipientAccountCreationLedgerBounds })

            .Build();

        txRecipientAccountCreation.Sign(senderWallet.Account.Signer, senderWallet.NetworkInfo);

        var txPaybackLoan = new TransactionBuilder(new Account(accounts.Recipient, recipientAccountSequenceNumber))
            .AddOperation(new PaymentOperation.Builder(
                accounts.Sender,
                new AssetTypeNative(),
                recipientStartingBalance.ToString())
                .Build())

            .Build();

        var txDeclaration = new TransactionBuilder(new AccountResponse(accounts.Recipient.AccountId, recipientAccountSequenceNumber + 1))
            // add a "no op" operation
            .AddOperation(new BumpSequenceOperation(0))
            .Build();

        var txFaultRecovery = new TransactionBuilder(new AccountResponse(accounts.Recipient.AccountId, recipientAccountSequenceNumber + 2))
            // grant sender full control over escrow account
            .AddOperation(new SetOptionsOperation.Builder()
                .SetSigner(accounts.Sender.XdrSignerKey, 2)
                .Build())

            // add synchrony period
            .AddPreconditions(new TransactionPreconditions() { MinSeqAge = (ulong)TimeSpan.FromDays(1).Milliseconds })
            .Build();

        var txClose = new TransactionBuilder(new AccountResponse(accounts.Recipient.AccountId, recipientAccountSequenceNumber + 2))
            // recipient accepts USA
            .AddOperation(new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(usa))
                .SetSourceAccount(accounts.Recipient)
                .Build())

            // recipient subscribes to Gov Fund Rewards
            .AddOperation(new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(govFundRewards))
                .SetSourceAccount(accounts.Recipient)
                .Build())

            // fund escrow account
            .AddOperation(new PaymentOperation.Builder(
                    accounts.Recipient,
                    usa,
                    amount.ToString())
                .SetSourceAccount(accounts.Sender)
                .Build())

            // pay recipient from escrow account
            .AddOperation(new PaymentOperation.Builder(
                accounts.Recipient,
                usa,
                amount.ToString())
                .SetSourceAccount(accounts.Recipient)
                .Build())

            // grant sender full control over escrow account
            .AddOperation(new SetOptionsOperation.Builder()
                .SetSigner(accounts.Sender.XdrSignerKey, 2)
                .Build())
            .Build();

        // configure escrow account
        var txEscrowConfiguration = new TransactionBuilder(new AccountResponse(accounts.Recipient.AccountId, recipientAccountSequenceNumber))
            // accept USA
            .AddOperation(new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(usa)).Build())

            // require 2 signers
            .AddOperation(new SetOptionsOperation.Builder()
                .SetLowThreshold(2)
                .SetMediumThreshold(2)
                .SetHighThreshold(2)
                .SetMasterKeyWeight(0)
                .Build())

            // add sender as signer
            .AddOperation(new SetOptionsOperation.Builder()
                .SetSigner(accounts.Sender.XdrSignerKey, 1).Build())

            // add recipient as signer
            .AddOperation(new SetOptionsOperation.Builder()
                .SetSigner(accounts.Recipient.XdrSignerKey, 1).Build())

            // add declaration transaction as signer
            .AddOperation(new SetOptionsOperation.Builder()
                .SetSigner(Signer.PreAuthTx(txDeclaration, senderWallet.NetworkInfo), 1).Build())

            // add fault recover transaction as signer
            .AddOperation(new SetOptionsOperation.Builder()
                .SetSigner(Signer.PreAuthTx(txFaultRecovery, senderWallet.NetworkInfo), 1).Build())
            .Build();

        //await senderWallet.SubmitTransactionAsync(txEscrowConfiguration, true, "Configure escrow account");

        return (
            txRecipientAccountCreation,
            txPaybackLoan,
            new PaymentChannel((txEscrowConfiguration, txEscrowConfiguration), (txDeclaration, txClose, txFaultRecovery))
            );
    }
}