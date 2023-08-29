using Microsoft.Azure.CosmosRepository;
using Newtonsoft.Json;

namespace RivalCoins.Airdrop.Common.Repository.Cosmos.Model;

public class AirdropParticipant : Item
{
    [JsonProperty("payroll-api-account-id")]
    public string PayrollApiAccountId { get; set; } = string.Empty;
  
    [JsonProperty("stellar-account-id")]
    public string StellarAccountId { get; set; } = string.Empty;

    [JsonProperty("payroll-api")]
    public string PayrollApi { get; set; } = string.Empty;

    [JsonProperty("asset")]
    public string Asset { get; set; }

    [JsonProperty("last-airdrop")]
    public DateTimeOffset? LastAirdrop { get; set; }

    [JsonProperty("airdrop-campaigns")]
    public List<string> AirdropCampaigns { get; set; } = new();
}