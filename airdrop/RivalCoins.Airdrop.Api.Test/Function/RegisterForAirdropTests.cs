using FakeItEasy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NUnit.Framework;
using RivalCoins.Airdrop.Api.Function;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Common.Repository.Cosmos.Model;
using RivalCoins.Airdrop.Test.Common;
using RivalCoins.Airdrop.Test.Common.Generic;
using RivalCoins.Sdk;
using stellar_dotnet_sdk;

namespace RivalCoins.Airdrop.Api.Test.Function;

[TestFixture]
public class RegisterForAirdropTests : TestClassBase<RegisterForAirdrop>
{
    private IRepository<AirdropParticipant> _airdropParticipantRepo;

    protected override void OnSetup()
    {
        base.OnSetup();

        _airdropParticipantRepo = A.Fake<IRepository<AirdropParticipant>>(x => x.Strict());

        this.SUT = new RegisterForAirdrop(_airdropParticipantRepo);
    }

    #region Tests

    // Pinwheel is the authority for accounts account (end user ID == Stellar account ID)
    // airdrop
    //      load accounts from pinwheel
    //      validate account
    //      

    [Test]
    public async Task UserNot()
    {
        // Arrange
        var request = A.Fake<HttpRequest>();
        var savedAirdropParticipant = new Capture<AirdropParticipant>();

        A.CallTo(() => _airdropParticipantRepo.CreateAsync(savedAirdropParticipant, default))
            .Returns(A.Dummy<ValueTask<AirdropParticipant>>());

        A.CallTo(() => request.Query)
            .Returns(new QueryCollection(new Dictionary<string, StringValues>()
            {
                { "payroll-api", new StringValues("Expected Payroll API")},
                { "payroll-id", new StringValues("Expected Payroll API Account Id")},
                { "stellar-id", new StringValues("Expected Stellar Id")},
                { "asset", new StringValues("Expected Asset")},
            }));

        // Act
        await this.SUT.Run(request, A.Dummy<ILogger>());

        // Assert
        savedAirdropParticipant.Captured.Asset
            .Should().Be(request.Query["asset"]);

        savedAirdropParticipant.Captured.PayrollApi
            .Should().Be(request.Query["payroll-api"]);

        savedAirdropParticipant.Captured.PayrollApiAccountId
            .Should().Be(request.Query["payroll-id"]);

        savedAirdropParticipant.Captured.StellarAccountId
            .Should().Be(request.Query["stellar-id"]);

        A.CallTo(() => _airdropParticipantRepo.CreateAsync(savedAirdropParticipant.Captured, default))
            .MustHaveHappenedOnceExactly();
    }

    #endregion Tests
}