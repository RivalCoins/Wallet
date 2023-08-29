using System.Text.Json.Serialization;

namespace RivalCoins.Server.Model;

public class AssetId
{
    [JsonPropertyName("asset_code")]
    public string Code { get; set; } = null!;
    [JsonPropertyName("asset_issuer")]
    public string Issuer { get; set; } = null!;
}