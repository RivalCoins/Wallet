using System.Text.Json.Serialization;

namespace RivalCoins.Airdrop.Common.Api.Model;

public class PayStub
{
    [JsonPropertyName("total_taxes")]
    public double TaxTotal { get; set; }

    [JsonPropertyName("pay_date")]
    public DateTime PayDate { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; }
}