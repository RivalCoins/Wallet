using Microsoft.Azure.CosmosRepository;
using Newtonsoft.Json;

namespace RivalCoins.Airdrop.Common.Repository.Cosmos.Model;

public record Balance(string Asset, double Quantity);

public class RivalCoins
{
    public Balance? Wrapped { get; set; }
    public List<Balance> Wrappers { get; } = new List<Balance>();
}

public class RivalCoinUser : Item
{
    [JsonProperty("stellar-account-id")]
    public string StellarAccountId { get; set; } = default!;

    [JsonProperty("usa2024-rivalcoins")]
    public RivalCoins? USA2024RivalCoins { get; set; } = default!;

    [JsonProperty("wealth2024-rivalcoins")]
    public RivalCoins? Wealth2024RivalCoins { get; set; } = default!;
}