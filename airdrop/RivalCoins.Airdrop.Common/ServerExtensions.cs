using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using stellar_dotnet_sdk.requests;
using stellar_dotnet_sdk.responses;

namespace RivalCoins.Airdrop.Common;

public static class ServerExtensions
{
    public static async Task<AccountResponse?> AccountAsync(this AccountsRequestBuilder builder, string accountId)
    {
        AccountResponse? response = null;

        try
        {
            response = await builder.Account(accountId);
        }
        catch (Exception)
        {
        }

        return response;
    }
}