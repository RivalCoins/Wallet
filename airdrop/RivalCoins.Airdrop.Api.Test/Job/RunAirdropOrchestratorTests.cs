using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FakeItEasy;
using FluentAssertions;
using FsCheck;
using FsCheck.NUnit;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using RivalCoins.Airdrop.Api.Job;
using RivalCoins.Airdrop.Api.Test.Generators;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Common.Repository.Cosmos.Model;
using RivalCoins.Sdk;
using stellar_dotnet_sdk;
using Constants = RivalCoins.Airdrop.Common.Constants;

namespace RivalCoins.Airdrop.Api.Test.Job;

[TestFixture]
public class RunAirdropOrchestratorTests
{
    [FsCheck.NUnit.Property(Replay  = "1094334707,297068188", MaxTest = 1, Arbitrary = new []{ typeof(InvalidAirdrop) })]
    public Property Run_Test1(Airdrop.Common.Repository.Queue.Model.Airdrop invalidAirdrop)
    {
        // 140281786, 297068187
        //StartSize = 140281786, EndSize = 297068187,
        // Arrange
        var sut = new RunAirdropOrchestrator(A.Dummy<IRepository<AirdropParticipant>>(), A.Dummy<Server>(), A.Dummy<IPayStubReader>(), A.Dummy<IConfiguration>());
        var orchestrationContext = A.Fake<IDurableOrchestrationContext>();
        var validAirdrop1 = new Airdrop.Common.Repository.Queue.Model.Airdrop();
        var validAirdrop2 = new Airdrop.Common.Repository.Queue.Model.Airdrop();
        var pendingAirdrops = new List<Airdrop.Common.Repository.Queue.Model.Airdrop>() { validAirdrop1, validAirdrop2, invalidAirdrop };
        var airdropParticipantWallet = new Sdk.Wallet("https://localhost:8001", KeyPair.Random().SecretSeed, "https://localhost:7777");

        validAirdrop1.Asset = validAirdrop2.Asset = Constants.USA.CanonicalName();
        validAirdrop2.Quantity = validAirdrop2.Quantity = 10.0;
        validAirdrop1.PayDate = validAirdrop2.PayDate = DateTime.Now;
        validAirdrop1.StellarAccoutId = validAirdrop2.StellarAccoutId = KeyPair.FromSecretSeed(airdropParticipantWallet.AccountSecretSeed).AccountId;

        Wallet.CreateAccountAsync(KeyPair.FromSecretSeed(airdropParticipantWallet.AccountSecretSeed), airdropParticipantWallet.NetworkUrl).Wait();

        airdropParticipantWallet.InitializeAsync().Wait();

        var createTrustlineTx = new TransactionBuilder(airdropParticipantWallet.Account.Info);
        createTrustlineTx.AddOperation(new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(Constants.USA)).Build());

        var signedTx = createTrustlineTx.Build();

        var response = airdropParticipantWallet.SubmitTransactionAsync(signedTx, true, "Trust USA").Result;
        if (response?.IsSuccess() is null or false)
        {
            throw new Exception("failed to create trustline");
        }

        A.CallTo(() => orchestrationContext.InstanceId)
            .Returns(RunAirdropOrchestrator.SingletonId);

        A.CallTo(() => orchestrationContext.CallSubOrchestratorAsync<List<Airdrop.Common.Repository.Queue.Model.Airdrop>>(nameof(DequeuePendingAirdropsOrchestrator), DequeuePendingAirdropsOrchestrator.SingletonId, null ))
            .Returns(pendingAirdrops);

        A.CallTo(() => orchestrationContext.CallSubOrchestratorAsync<bool>(nameof(SubmitStellarTransactionOrchestrator), SubmitStellarTransactionOrchestrator.SingletonId, null))
            .Returns(true);

        // Act
        var actual = sut.RunOrchestrator(orchestrationContext, A.Dummy<IAsyncCollector<Airdrop.Common.Repository.Queue.Model.Airdrop>>(), A.Dummy<ILogger>()).Result;

        // Assert
        // validate failed transactions and requeue

        return
            (actual == false)
                .Label("Successful airdrop");
    }

    [Test]
    public async Task Run_Test2()
    {
        // Arrange
        var sut = new RunAirdropOrchestrator(
            A.Dummy<IRepository<AirdropParticipant>>(),
            A.Dummy<Server>(), 
            A.Dummy<IPayStubReader>(),
            A.Dummy<IConfiguration>());
        var orchestrationContext = A.Fake<IDurableOrchestrationContext>();

        A.CallTo(() => orchestrationContext.InstanceId)
            .Returns($"Not {RunAirdropOrchestrator.SingletonId}");

        // Act
        Action invocation = () => sut.RunOrchestrator(orchestrationContext, A.Dummy<IAsyncCollector<Airdrop.Common.Repository.Queue.Model.Airdrop>>(), A.Dummy<ILogger>()).Wait();

        // Assert
        invocation
            .Should().Throw<Exception>("singleton access is required");
    }
}