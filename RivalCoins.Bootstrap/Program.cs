﻿using RivalCoins.Sdk;
using stellar_dotnet_sdk;

const string HorizonUrl = "https://localhost:8001";
const string RivalCoinsHomeDomain = "https://localhost:32783";
const string CurrencyAssetCode = "FakeUSA";

const long UsaSupply = 100L * 1000L * 1000L * 1000L * 1000L;
const long FounderFee = UsaSupply / 1000L;

var networkFeeFunder = new Wallet(HorizonUrl, KeyPair.Random().SecretSeed, RivalCoinsHomeDomain);
await Wallet.CreateAccountAsync(KeyPair.FromSecretSeed(networkFeeFunder.AccountSecretSeed!), HorizonUrl);
await networkFeeFunder.InitializeAsync();

// create currency
var currencySystem = await RivalCoins.Sdk.Util.CreateCurrencySystemAsync(CurrencyAssetCode, UsaSupply, RivalCoins.Sdk.Util.MaxTrustlineLimit, networkFeeFunder);
var currency = Asset.CreateNonNativeAsset(CurrencyAssetCode, currencySystem.Issuing.AccountId);

// assess Rival Coins founder fee
var rivalCoinsFounder = new Wallet(HorizonUrl, KeyPair.Random().SecretSeed, RivalCoinsHomeDomain);
await Wallet.CreateAccountAsync(KeyPair.FromSecretSeed(rivalCoinsFounder.AccountSecretSeed!), HorizonUrl);
await rivalCoinsFounder.InitializeAsync();

var distributor = new Wallet(HorizonUrl, currencySystem.Distributions.First().SecretSeed, RivalCoinsHomeDomain);
await distributor.InitializeAsync();

var transaction = new TransactionBuilder(distributor.Account.Info)
    .AddOperation(new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(currency))
        .SetSourceAccount(rivalCoinsFounder.Account.Signer!)
        .Build())
    .AddOperation(new PaymentOperation.Builder(rivalCoinsFounder.Account.Info.KeyPair, currency, FounderFee.ToString()).Build())
    .Build();
var result = await distributor.SubmitTransactionAsync(transaction, true, "Assess founder fee");
if(result?.IsSuccess() is null or false)
{
    throw new Exception();
}

Console.WriteLine($"Founders Fee Account: {rivalCoinsFounder.Account.Signer!.AccountId}:{rivalCoinsFounder.Account.Signer.SecretSeed}");