using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using RivalCoins.Airdrop.Common;

namespace RivalCoins.Airdrop.Api.Job;

public class DeletePendingAirdropsActivity
{
    //private readonly IRepository<Common.Repository.Queue.Model.Airdrop> _pendingAirdropRepo;

    //public DeletePendingAirdropsActivity(IRepository<Common.Repository.Queue.Model.Airdrop> pendingAirdropRepo)
    //{
    //    _pendingAirdropRepo = pendingAirdropRepo;
    //}

    [FunctionName(nameof(DeletePendingAirdropsActivity))]
    public async Task Run(
        [ActivityTrigger] List<BasicMessage<Common.Repository.Queue.Model.Airdrop>> airdrops,
        [Queue(Constants.AirdropQueue)] QueueClient queueClient,
        ILogger log)
    {
        //foreach (var airdrop in airdrops)
        //{
        //    await queueClient.DeleteMessageAsync(airdrop.MessageId, airdrop.PopReceipt);

        //    await _pendingAirdropRepo.DeleteAsync(airdrop.Value);
        //}
    }
}