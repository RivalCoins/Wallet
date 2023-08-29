using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RivalCoins.Airdrop.Common;

namespace RivalCoins.Airdrop.Api.Job;

public static class GetPendingAirdropsForNextDayActivity
{
    private static bool IsSameDay(DateTime day1, DateTime day2) =>
        day1.Year == day2.Year 
        && day1.Month == day2.Month 
        && day1.Day == day2.Day;

    [FunctionName(nameof(GetPendingAirdropsForNextDayActivity))]
    public static async Task<List<BasicMessage<Common.Repository.Queue.Model.Airdrop>>?> Run(
        [ActivityTrigger] int numAirdrops,
        [Queue(Constants.AirdropQueue)] QueueClient queueClient,
        ILogger log)
    {
        var airdrops = (await queueClient.ReceiveMessagesAsync(Math.Min(numAirdrops, queueClient.MaxPeekableMessages))).Value
            .Select(message => (Message: message, Airdrop: JsonConvert.DeserializeObject<Common.Repository.Queue.Model.Airdrop>(message.MessageText))).ToList();
        
        var nextAirdrop = airdrops.FirstOrDefault(airdrop => airdrop.Airdrop != null);
        if (nextAirdrop.Airdrop != null)
        {
            var payDateForAirdropBatch = nextAirdrop.Airdrop.PayDate;
            var airdropBatch = airdrops
                .Where(airdrop => airdrop.Airdrop != null && IsSameDay(payDateForAirdropBatch, airdrop.Airdrop.PayDate))
                .Select(airdrop => (airdrop.Message, Airdrop: airdrop.Airdrop ?? new Common.Repository.Queue.Model.Airdrop()))
                .ToList();

            return airdropBatch.Select(airdrop =>
                new BasicMessage<Common.Repository.Queue.Model.Airdrop>(
                    airdrop.Airdrop,
                    airdrop.Message.MessageId,
                    airdrop.Message.PopReceipt)).ToList();
        }

        return null;
    }
}