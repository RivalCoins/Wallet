using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using RivalCoins.Airdrop.Test.Common;

namespace RivalCoins.Airdrop.Api.Test.Integration;

public class RunAirdopIntegrationTests : TestClassBase
{
    private const string ApiEndpoint = "http://localhost:7071";

    private string RunAirdropEndpoint => $"{ApiEndpoint}/api/{nameof(Api.Function.RunAirdrop)}";

    [Test]
    public void RunAirdrop()
    {
        // create environment 
        using var http = new HttpClient();

        // register user for airdrop

        // run airdrop
        var response = http.PostAsync(RunAirdropEndpoint, null).Result;

        response.StatusCode
            .Should().Be(HttpStatusCode.OK);
    }
}