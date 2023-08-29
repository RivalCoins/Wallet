using System;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using RivalCoins.Airdrop.Api.Job;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Common.Api.Model;
using RivalCoins.Airdrop.Common.Config;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;

namespace RivalCoins.Airdrop.Api.Function;

public class GetPinwheelLinkToken
{
    private readonly RestClient _client;
    private readonly IPinwheelConfig _config;
    private readonly Server _server;

    public GetPinwheelLinkToken(RestClient client, IPinwheelConfig config, Server server)
    {
        _client = client;
        _config = config;
        _server = server;
    }

    [FunctionName(nameof(GetPinwheelLinkToken))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
        ILogger log)
    {
        try
        {
            var stellarAccount = req.Query["stellar_account"];
            
            var stellarAccountIsValid = await this.ValidateTokenRequestAsync(stellarAccount);
            if (!stellarAccountIsValid.Valid)
            {
                return new BadRequestErrorMessageResult(stellarAccountIsValid.Error);
            }

            string token;

            var request = new RestRequest($"link_tokens", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Pinwheel-Version", "2022-06-22");
            request.AddHeader("x-api-secret", _config.ApiKey);

            var body = new PinwheelLinkTokenRequest();
            body.AccountType = "checking";
            body.EndUserId = stellarAccount;
            body.RequiredJobs.Add("paystubs");
            request.AddJsonBody(body);

            var response = await _client.ExecuteAsync(request);

            var jsonResponse = JObject.Parse(response.Content);

            token = jsonResponse["data"]["token"].Value<string>();

            return new ContentResult() { Content = token, ContentType = "text/plain", StatusCode = 200 };
        }
        catch (Exception e)
        {
            log.LogError(e, nameof(GetPinwheelLinkToken));
            throw;
        }
    }

    private async Task<(bool Valid, string? Error)> ValidateTokenRequestAsync(StringValues stellarAccoutId)
    {
        var result = (true, null as string);

        if (!stellarAccoutId.Any())
        {
            result = (false, "No Stellar account supplied.");
        }
        else if(string.IsNullOrWhiteSpace(stellarAccoutId.First()))
        {
            result = (false, "Empty Stellar account supplied.");
        }
        else if (stellarAccoutId.Count > 1)
        {
            result = (false, "Multiple Stellar accounts supplied.");
        }
        else
        {
            var preformattedStellarAccountId = stellarAccoutId.First();
            KeyPair? formattedStellarAccountId = null;

            try
            {
                formattedStellarAccountId = KeyPair.FromAccountId(preformattedStellarAccountId);
            }
            catch (Exception)
            {
            }

            if (formattedStellarAccountId == null)
            {
                result = (false, "Malformed Stellar account.");
            }
            else
            {
                AccountResponse? accountInfo = null;

                try
                {
                    accountInfo = await _server.Accounts.Account(formattedStellarAccountId.AccountId);
                }
                catch (Exception)
                {
                }

                if (accountInfo != null)
                {
                    // does not accept USA
                    if (accountInfo.Balances.All(b => b.Asset.CanonicalName() != Constants.USA.CanonicalName()))
                    {
                        result = (false, $"Account does not accept {Constants.USA.CanonicalName()}.");
                    }

                    // not subscribed to Gov Fund Rewards
                    if (accountInfo.Balances.All(b => b.Asset.CanonicalName() != Constants.GovFundRewards.CanonicalName()))
                    {
                        result = (false, "Account not subscribed to Gov Fund Rewards.");
                    }
                }
            }
        }

        return result;
    }
}