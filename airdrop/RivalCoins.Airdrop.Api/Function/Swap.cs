using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Common.Repository.Cosmos.Model;
using RivalCoins.Sdk;
using RivalCoins.Sdk.Grpc;
using stellar_dotnet_sdk;

namespace RivalCoins.Airdrop.Api.Function;

public class Swap
{
    private readonly IRepository<RivalCoinUser> _rivalCoinUserRepo;
    private readonly Server _server;

    public Swap(IRepository<RivalCoinUser> rivalCoinUserRepo, Server server)
    {
        _rivalCoinUserRepo = rivalCoinUserRepo;
        _server = server;
    }

    [FunctionName(nameof(Swap))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "Swap")] HttpRequest req,
        ILogger log)
    {
        var body = await req.ReadAsStringAsync();
        var request = JsonConvert.DeserializeObject<SwapRequest>(body);
        var tx = stellar_dotnet_sdk.Transaction.FromEnvelopeXdr(null as string);

        // validate user signature
        var user = KeyPair.FromAccountId(tx.SourceAccount.AccountId);
        var userSignedSwapRequest = tx.Signatures.All(signature => Enumerable.SequenceEqual(signature.Hint.InnerValue, user.SignatureHint.InnerValue));
        if(userSignedSwapRequest)
        {
            var swapOperation = tx.Operations.OfType<PathPaymentStrictReceiveOperation>().FirstOrDefault();
            if (swapOperation != null)
            {
                // get user Rival Coins
                var rivalCoinUser =
                    (await _rivalCoinUserRepo.GetByQueryAsync($"select * from c where c['stellar-account-id'] = '{user.AccountId}'")).FirstOrDefault()
                    ??
                    new RivalCoinUser() { StellarAccountId = user.AccountId };

                // update USA-2024 Rival Coins
                rivalCoinUser.USA2024RivalCoins ??= await this.CreateRivalCoinsAsync(Common.Constants.USA2024, user);

                // buy USA-2024 Rival Coin
                if (swapOperation.SendAsset.CanonicalName() == Common.Constants.USA2024.CanonicalName())
                {
                    // decrement USA-2024
                    rivalCoinUser.USA2024RivalCoins.Wrapped = new(
                        rivalCoinUser.USA2024RivalCoins.Wrapped!.Asset,
                        rivalCoinUser.USA2024RivalCoins.Wrapped.Quantity - double.Parse(swapOperation.DestAmount));
                    
                    // increment Rival Coin
                    this.IncrementRivalCoin(rivalCoinUser.USA2024RivalCoins, swapOperation.DestAsset, swapOperation.DestAmount);
                }

                // sell USA-2024 Rival Coin
                if (swapOperation.DestAsset.CanonicalName() == Common.Constants.USA2024.CanonicalName())
                {
                    // increment USA-2024
                    rivalCoinUser.USA2024RivalCoins.Wrapped = new(
                        rivalCoinUser.USA2024RivalCoins.Wrapped!.Asset,
                        rivalCoinUser.USA2024RivalCoins.Wrapped.Quantity + double.Parse(swapOperation.DestAmount));

                    // decrement Rival Coin
                    this.DecrementRivalCoin(rivalCoinUser.USA2024RivalCoins, swapOperation.SendAsset, swapOperation.DestAmount);
                }

                // swap one Rival Coin for another
                if (swapOperation.SendAsset.Issuer() == swapOperation.DestAsset.Issuer())
                {
                    // decrement swapped out Rival Coin
                    this.DecrementRivalCoin(rivalCoinUser.USA2024RivalCoins, swapOperation.SendAsset, swapOperation.DestAmount);

                    // increment swapped in Rival Coin
                    this.IncrementRivalCoin(rivalCoinUser.USA2024RivalCoins, swapOperation.DestAsset, swapOperation.DestAmount);
                }
            }
        }

        return new OkResult();
    }

    private void IncrementRivalCoin(Common.Repository.Cosmos.Model.RivalCoins rivalCoins, Asset asset, string amount)
    {
        // increment Rival Coin quantity
        Balance incrementedRivalCoin;

        var existingRivalCoin = rivalCoins.Wrappers.FirstOrDefault(wrapper => wrapper.Asset == asset.CanonicalName());
        if (existingRivalCoin != null)
        {
            // increment Rival Coin
            incrementedRivalCoin = existingRivalCoin with
            {
                Quantity = existingRivalCoin.Quantity + double.Parse(amount)
            };

            // update Rival Coin collection
            rivalCoins.Wrappers.Remove(existingRivalCoin);
            rivalCoins.Wrappers.Add(incrementedRivalCoin);
        }
        else
        {
            // increment Rival Coin
            incrementedRivalCoin = new(asset.CanonicalName(), double.Parse(amount));

            // update Rival Coin collection
            rivalCoins.Wrappers.Add(incrementedRivalCoin);
        }
    }

    private void DecrementRivalCoin(Common.Repository.Cosmos.Model.RivalCoins rivalCoins, Asset asset, string amount)
    {
        // decrement Rival Coin quantity
        Balance decrementedRivalCoin;

        var existingRivalCoin = rivalCoins.Wrappers.FirstOrDefault(wrapper => wrapper.Asset == asset.CanonicalName());
        if (existingRivalCoin != null)
        {
            // decrement Rival Coin
            decrementedRivalCoin = existingRivalCoin with
            {
                Quantity = existingRivalCoin.Quantity - double.Parse(amount)
            };

            // update Rival Coin collection
            rivalCoins.Wrappers.Remove(existingRivalCoin);
            rivalCoins.Wrappers.Add(decrementedRivalCoin);
        }
        else
        {
            throw new System.Exception($"Attempted to sell non-existent Rival Coin: {asset.CanonicalName()}");
        }
    }

    private async Task<Common.Repository.Cosmos.Model.RivalCoins> CreateRivalCoinsAsync(Asset asset, KeyPair account)
    {
        return new Common.Repository.Cosmos.Model.RivalCoins() { Wrapped = await this.GetBalanceAsync(asset, account) };
    }

    private async Task<Balance> GetBalanceAsync(Asset asset, KeyPair account)
    {
        var accountInfo = await _server.Accounts.AccountAsync(account.AccountId);
        var foundBalance = accountInfo.Balances.First(b => b.Asset.CanonicalName() == asset.CanonicalName());

        return new Balance(asset.CanonicalName(), double.Parse(foundBalance.BalanceString));
    }
}