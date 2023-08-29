using System;
using System.Collections;
using System.Threading.Tasks;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Common.Repository.Cosmos.Model;
using stellar_dotnet_sdk;

namespace RivalCoins.Airdrop.Api.Job;

public class RunAirdropOrchestrator
{
    public const string SingletonId = "B3D47CF1-59CC-4412-B446-A8460979D243";

    private readonly IRepository<AirdropParticipant> _airdropParticipantRepository;
    //private readonly IRepository<Common.Repository.Queue.Model.Airdrop> _airdropRepo;
    private readonly Server _server;
    private readonly IPayStubReader _payStubReader;
    private readonly string _serviceBusConnection;

    public RunAirdropOrchestrator(
        IRepository<AirdropParticipant> airdropParticipantRepository,
        Server server,
        IPayStubReader payStubReader,
        //IRepository<Common.Repository.Queue.Model.Airdrop> airdropRepo,
        IConfiguration config
        )
    {
        _airdropParticipantRepository = airdropParticipantRepository;
        _server = server;
        _payStubReader = payStubReader;
        //_airdropRepo = airdropRepo;
        _serviceBusConnection = config.GetConnectionString("ServiceBus");
    }

    private async Task<bool> ValidAirdropParticipantAsync(AirdropParticipant airdropParticipant)
    {
        var acceptsUsa = Helpers.TrustlineExistsAsync(
            Constants.USA,
            KeyPair.FromAccountId(airdropParticipant.StellarAccountId),
            _server);
        var subscribedToGovFundRewards = Helpers.TrustlineExistsAsync(
            Constants.GovFundRewards,
            KeyPair.FromAccountId(airdropParticipant.StellarAccountId),
            _server);

        _ = await Task.WhenAll(acceptsUsa, subscribedToGovFundRewards);

        var validAirdropParticipant = subscribedToGovFundRewards.Result && acceptsUsa.Result;

        return validAirdropParticipant;
    }

    [FunctionName(nameof(RunAirdropOrchestrator))]
    public async Task<bool> RunOrchestrator(
        [OrchestrationTrigger] IDurableOrchestrationContext context,
        [Queue(Constants.AirdropQueue)] IAsyncCollector<Common.Repository.Queue.Model.Airdrop> queue,
        ILogger log)
    {
        if (context.InstanceId != SingletonId)
        {
            throw new Exception($"You must use the singleton instance! (ie {nameof(RunAirdropOrchestrator)}.{nameof(SingletonId)})");
        }

        var airdropId = context.NewGuid();

        await context.CallActivityAsync(nameof(GenerateAirdropsActivity), airdropId.ToString());
        
        return true;
    }
}