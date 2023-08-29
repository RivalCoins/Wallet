using Newtonsoft.Json;

namespace RivalCoins.Airdrop.Common.Repository.Queue.Model;

public class StellarTransaction
{
    [JsonProperty("memo")]
    public string Memo { get; set; }

    [JsonProperty("operations")]
    public List<string> Operations { get; set; } = new();
}