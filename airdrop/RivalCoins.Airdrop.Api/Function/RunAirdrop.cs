using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RivalCoins.Airdrop.Api.Job;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Common.Repository.Cosmos.Model;
using stellar_dotnet_sdk;

namespace RivalCoins.Airdrop.Api.Function;

public class RunAirdrop
{
    private readonly Server _server;
    private readonly IRepository<AirdropParticipant> _airdropParticipantRepository;
    private readonly IPayStubReader _payStubReader;

    public RunAirdrop(
        Server server,
        IRepository<AirdropParticipant> airdropParticipantRepository,
        IPayStubReader payStubReader)
    {
        _server = server;
        _airdropParticipantRepository = airdropParticipantRepository;
        _payStubReader = payStubReader;
    }

    [FunctionName(nameof(RunAirdrop))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
        [Queue(Constants.AirdropQueue)] IAsyncCollector<Common.Repository.Queue.Model.Airdrop> queue,
        [DurableClient] IDurableOrchestrationClient starter,
        ILogger log)
    {
        await starter.StartNewAsync(nameof(RunAirdropOrchestrator), RunAirdropOrchestrator.SingletonId);

        var transactionSubmissionStatus = await starter.GetStatusAsync(SubmitStellarTransactionOrchestrator.SingletonId);
        if (transactionSubmissionStatus.RuntimeStatus != OrchestrationRuntimeStatus.Running)
        {
            await starter.StartNewAsync(nameof(SubmitStellarTransactionOrchestrator), SubmitStellarTransactionOrchestrator.SingletonId);
        }

        return await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(req, RunAirdropOrchestrator.SingletonId);
    }
}