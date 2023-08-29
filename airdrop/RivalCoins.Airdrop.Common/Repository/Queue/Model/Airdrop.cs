using Newtonsoft.Json;

namespace RivalCoins.Airdrop.Common.Repository.Queue.Model;

public class Airdrop
{
    [JsonProperty("stellar-account-id")]
    public string StellarAccoutId { get; set; }

    [JsonProperty("quantity")]
    public double Quantity { get; set; }

    [JsonProperty("asset")]
    public string Asset { get; set; }

    [JsonProperty("pay-date")]
    public DateTime PayDate { get; set; }
}