using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RivalCoins.Airdrop.Common;

namespace RivalCoins.Airdrop.Api.Job;

public static class GetPendingAirdropsActivity
{
    [FunctionName(nameof(GetPendingAirdropsActivity))]
    public static async Task<List<BasicMessage<Common.Repository.Queue.Model.Airdrop>>?> Run(
        [ActivityTrigger] int numAirdrops,
        [Queue(Constants.AirdropQueue)] QueueClient queueClient,
        ILogger log)
    {
        var messages = await queueClient.ReceiveMessagesAsync(Math.Min(numAirdrops, queueClient.MaxPeekableMessages));
        var airdrops = messages.Value.Select(message =>
            new BasicMessage<Common.Repository.Queue.Model.Airdrop>(
                JsonConvert.DeserializeObject<Common.Repository.Queue.Model.Airdrop>(message.MessageText),
                message.MessageId,
                message.PopReceipt)).ToList();

        return airdrops.Any() ? airdrops : null;
    }
}