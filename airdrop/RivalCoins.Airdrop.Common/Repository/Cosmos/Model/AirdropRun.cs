using Microsoft.Azure.CosmosRepository;
using Newtonsoft.Json;

namespace RivalCoins.Airdrop.Common.Repository.Cosmos.Model;

public class AirdropRun : Item
{
    [JsonProperty("airdrop-participant-query-continuation-token")]
    public string? AirdropParticipantQueryContinuationToken { get; set; }
}