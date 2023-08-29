using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using RestSharp;
using RivalCoins.Airdrop.Api.Function;
using RivalCoins.Airdrop.Common.Api.Model;
using RivalCoins.Airdrop.Common.Config;

namespace RivalCoins.Airdrop.Api.test;

public class GetEnvironmentConfigs
{
    private readonly RestClient _client;
    private readonly IPinwheelConfig _config;

    public GetEnvironmentConfigs(RestClient client, IPinwheelConfig config)
    {
        _client = client;
        _config = config;
    }

    [FunctionName(nameof(GetEnvironmentConfigs))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
        ILogger log)
    {
        try
        {
            var stellarAccount = req.Query["stellar_account"];
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

            //var jsonResponse = JObject.Parse(response.Content);
            //var expires = DateTimeOffset.Parse(jsonResponse["data"]["expires"].Value<string>());

            //token = jsonResponse["data"]["token"].Value<string>();

            return new ContentResult() { Content = _config.EnvironmentUrl + Environment.NewLine + response.Content, ContentType = "text/plain", StatusCode = 200 };
        }
        catch (Exception e)
        {
            log.LogError(e, nameof(GetPinwheelLinkToken));
            throw;
        }

        return new OkResult();
    }
}