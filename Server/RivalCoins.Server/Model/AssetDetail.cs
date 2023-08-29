using System.Text.Json.Serialization;

namespace RivalCoins.Server.Model;

public class AssetDetail
{
    [JsonPropertyName("asset_code")]
    public string Code { get; set; } = null!;
    [JsonPropertyName("asset_issuer")]
    public string Issuer { get; set; } = null!;
    [JsonPropertyName("logo")]
    public string Logo { get; set; } = null!;
    [JsonPropertyName("domain")]
    public string HomeDomain { get; set; } = null!;
    [JsonPropertyName("is_verified")]
    public bool IsVerified { get; set; }
    [JsonPropertyName("trustline")]
    public int NumTrustlines { get; set; }
    [JsonPropertyName("description")]
    public string Description { get; set; } = null!;
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
}