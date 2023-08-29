using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using FakeItEasy;
using FsCheck;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.FSharp.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RestSharp;
using RivalCoins.Airdrop.Api.Function;
using RivalCoins.Airdrop.Api.Test.Generators;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Common.Config;
using RivalCoins.Airdrop.Test.Common.Generators;
using RivalCoins.Airdrop.Test.Common.Generic;
using RivalCoins.Sdk;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.xdr;
using Constants = RivalCoins.Airdrop.Common.Constants;

namespace RivalCoins.Airdrop.Api.Test.Function;

public class GetPinwheelLinkTokenTests : TestClassBase<GetPinwheelLinkToken>
{
    private IPinwheelConfig _pinwheelConfig;
    private Server _server;

    #region Setup

    protected override void OnSetup()
    {
        base.OnSetup();

        _pinwheelConfig = A.Fake<IPinwheelConfig>();
    }

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _server = new Server("https://localhost:8001");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _server.Dispose();
    }

    #endregion Setup

    public static class InvalidPinwheelLinkTokenRequest
    {
        public static Arbitrary<HttpRequest> Generate()
        {
            var malformedStellarAccount = CommonGenerator.MalformedStellarAccountId;

            var invalidAccounts = Gen.OneOf(
                // account malformed
                malformedStellarAccount,

                // accepts USA but is not subscribed to Gov Fund Rewards
                CommonGenerator.InitializedWallet
                    .WithTrusline(Constants.USA)
                    .NoTrustline(Constants.GovFundRewards)
                    .GetAccountId().Nullable(),

                // subscribed to Gov Fund Rewards but does not accept USA
                CommonGenerator.InitializedWallet
                    .WithTrusline(Constants.GovFundRewards)
                    .NoTrustline(Constants.USA)
                    .GetAccountId().Nullable(),

                // neither accepts USA nor is subscribed to Gov Fund Rewards
                CommonGenerator.InitializedWallet
                    .GetAccountId().Nullable())
                
                .StringValuesFromNullable();

            var validAccountForTokenRequest =
                CommonGenerator.InitializedWallet
                    .WithTrusline(Constants.USA)
                    .WithTrusline(Constants.GovFundRewards)
                    .GetAccountId();

            var malformedStringValues =
                Gen.OneOf(
                    // no value set at all
                    Gen.Constant(new StringValues()),

                    // extraneous value
                    validAccountForTokenRequest
                        .Select(validAccount => new StringValues(new[] { validAccount, "Some other value that shouldn't be here" })));

            var allInvalidStringValues = Gen.OneOf(invalidAccounts, malformedStringValues);

            var invalidQueryParametersValues = allInvalidStringValues
                .Select(invalidStringValues => new Dictionary<string, StringValues>() { {"stellar_account", invalidStringValues} });

            var allInvalidQueryParameterCollections =
                Gen.OneOf(
                    // invalid query parameter name
                    validAccountForTokenRequest.Select(validAccount =>
                        new Dictionary<string, StringValues>()
                        {
                            { "A value other than 'stellar_account'", validAccount }
                        }),
                    
                    // invalid query parameter values
                    invalidQueryParametersValues);

            return allInvalidQueryParameterCollections
                .Select(invalidQueryParameterCollection =>
                {
                    var request = A.Fake<HttpRequest>();

                    A.CallTo(() => request.Query)
                        .Returns(new QueryCollection(invalidQueryParameterCollection));

                    return request;
                }).ToArbitrary();
        }
    }

    #region Tests

    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        private string msg;

        public FakeHttpMessageHandler(string msg)
        {
            this.msg = msg;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            response.Content = new StringContent(this.msg);
            response.RequestMessage = new HttpRequestMessage();

            return Task.FromResult(response);
        }
    }

    [FsCheck.NUnit.Property(Replay = "93920454,297080206", MaxTest = 10, Arbitrary = new []{ typeof(InvalidPinwheelLinkTokenRequest) })]
    public Property InvalidRequest(HttpRequest request)
    {
        // Arrange
        var msg = @$"
        {{ 
            'data' : {{ 'expires' : '{DateTime.Now}', 'token' : 'my_token' }}
        }}";

        this.SUT = new GetPinwheelLinkToken(
            new RestClient(new HttpClient(new FakeHttpMessageHandler(msg)) { BaseAddress = new Uri("http://localhost:7777") }),
            _pinwheelConfig,
            _server);

        // Act
        var response = this.SUT.Run(request, A.Dummy<ILogger>()).Result;

        return
            (response is BadRequestErrorMessageResult)
                .Label("400 bad request response")

            ;
    }

    #endregion Test
}