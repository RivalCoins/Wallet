using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using RivalCoins.Airdrop.Common.Api.Model;
using RivalCoins.Airdrop.Common.Config;

namespace RivalCoins.Airdrop.Common;

public class PinwheelPayStubReader : IPayStubReader
{
    private readonly RestClient _client;
    private readonly IPinwheelConfig _config;

    public PinwheelPayStubReader(RestClient client, IPinwheelConfig config)
    {
        _client = client;
        _config = config;
    }

    public async Task<List<PayStub>> GetPayStubsAsync(string payrollApiAccountId, DateTimeOffset starting, DateTimeOffset ending)
    {
        var payStubs = new List<PayStub>();
        var request = new RestRequest($"accounts/{payrollApiAccountId}/paystubs", Method.Get);

        request.AddHeader("Accept", "application/json");
        request.AddHeader("Pinwheel-Version", "2022-06-22");
        request.AddHeader("x-api-secret", _config.ApiKey);

        var response = await _client.ExecuteAsync(request);

        var results = JsonNode.Parse(response.Content);
        var data = results["data"];
        if (data != null)
        {
            payStubs = data.AsArray().Select(p => p.Deserialize<PayStub>()).ToList();
        }

        return payStubs;
    }
}