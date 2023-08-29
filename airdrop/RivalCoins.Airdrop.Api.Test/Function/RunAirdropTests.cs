using Azure.Storage.Queues;
using Castle.DynamicProxy.Generators.Emitters.SimpleAST;
using FakeItEasy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NUnit.Framework;
using RivalCoins.Airdrop.Api.Function;
using RivalCoins.Airdrop.Api.Job;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Common.Api.Model;
using RivalCoins.Airdrop.Common.Repository.Cosmos.Model;
using RivalCoins.Airdrop.Test.Common;
using RivalCoins.Sdk;
using stellar_dotnet_sdk;
using Constants = RivalCoins.Airdrop.Common.Constants;
using DateTimeOffset = System.DateTimeOffset;

namespace RivalCoins.Airdrop.Api.Test.Function;

[TestFixture]
public class RunAirdropTests
{
    [Test]
    public async Task Run()
    {
        // Arrange
        var airdropParticipantWallet = new Sdk.Wallet("https://localhost:8001", KeyPair.Random().SecretSeed, "https://localhost:7777");
        using var server = new Server(airdropParticipantWallet.NetworkUrl);
        var airdropParticipantRepo = A.Fake<IRepository<AirdropParticipant>>(x => x.Strict());
        var payStubReader = A.Fake<IPayStubReader>();
        var sut = new RunAirdrop(server, airdropParticipantRepo, payStubReader);
        var queue = A.Fake<IAsyncCollector<Airdrop.Common.Repository.Queue.Model.Airdrop>>();
        var durableOrchestrationClient = A.Fake<IDurableOrchestrationClient>();
        var airdropParticipant = new AirdropParticipant() { StellarAccountId = KeyPair.FromSecretSeed(airdropParticipantWallet.AccountSecretSeed).AccountId, Asset = Constants.USA.CanonicalName() };
        var airdropParticipants = new List<AirdropParticipant>() { airdropParticipant };
        var queuedAirdrops = new Capture<Airdrop.Common.Repository.Queue.Model.Airdrop>();
        var payStubs = new List<PayStub>();
        var payStubStart = new DateTimeOffset(DateTimeOffset.Now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var payStubEnd = new Capture<DateTimeOffset>();
        var request = A.Dummy<HttpRequest>();

        payStubs.Add(new PayStub() { Currency = Constants.AirdropCurrency, TaxTotal = 10.0 });
        payStubs.Add(new PayStub() { Currency = Constants.AirdropCurrency, TaxTotal = 20.0 });
        payStubs.Add(new PayStub() { Currency = $"Not {Constants.AirdropCurrency}", TaxTotal = 50.0 });

        A.CallTo(() => airdropParticipantRepo.GetByQueryAsync(A<string>.Ignored, default))
            .Returns(ValueTask.FromResult((IEnumerable<AirdropParticipant>)airdropParticipants));

        A.CallTo(() => queue.AddAsync(queuedAirdrops, default))
            .Returns(A.Dummy<Task>());

        A.CallTo(() => payStubReader.GetPayStubsAsync(airdropParticipant.PayrollApiAccountId, payStubStart, payStubEnd))
            .Returns(payStubs);

        Console.SetOut(TestContext.Progress);

        await Wallet.CreateAccountAsync(KeyPair.FromSecretSeed(airdropParticipantWallet.AccountSecretSeed), airdropParticipantWallet.NetworkUrl);
        await airdropParticipantWallet.InitializeAsync();

        var createTrustlineTx = new TransactionBuilder(airdropParticipantWallet.Account.Info);
        createTrustlineTx.AddOperation(new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(Constants.USA)).Build());

        var signedTx = createTrustlineTx.Build();

        var response = await airdropParticipantWallet.SubmitTransactionAsync(signedTx, true, "Trust USA");
        if (response?.IsSuccess() is null or false)
        {
            throw new Exception("failed to create trustline");
        }

        // Act
        await sut.Run(request, queue, durableOrchestrationClient, A.Dummy<ILogger>());

        // Assert

        queuedAirdrops.MultiCaptures
            .Should().HaveCount(payStubs.Count(payStub => payStub.Currency == Constants.AirdropCurrency));

        (DateTimeOffset.Now - payStubEnd.Captured)
            .Should().BeLessThan(TimeSpan.FromSeconds(1));

        A.CallTo(() => durableOrchestrationClient.StartNewAsync(nameof(RunAirdropOrchestrator), RunAirdropOrchestrator.SingletonId))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => durableOrchestrationClient.CreateCheckStatusResponse(request, RunAirdropOrchestrator.SingletonId, false))
            .MustHaveHappenedOnceExactly();
    }
}