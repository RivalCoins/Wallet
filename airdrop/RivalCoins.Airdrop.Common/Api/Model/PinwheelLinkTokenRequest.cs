using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RivalCoins.Airdrop.Common.Api.Model;

public class PinwheelLinkTokenRequest
{
    [JsonPropertyName("org_name")]
    public string OrganizationName { get; set; } = "Rival Coins";

    [JsonPropertyName("account_type")]
    public string AccountType { get; set; }

    [JsonPropertyName("required_jobs")]
    public List<string> RequiredJobs { get; set; } = new List<string>();

    [JsonPropertyName("end_user_id")]
    public string EndUserId { get; set; }
}