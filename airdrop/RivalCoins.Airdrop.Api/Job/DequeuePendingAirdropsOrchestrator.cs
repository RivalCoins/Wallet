using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace RivalCoins.Airdrop.Api.Job;

public record BasicMessage<TValue>(TValue Value, string MessageId, string PopReceipt);

public class DequeuePendingAirdropsOrchestrator
{
    public const string SingletonId = "1F4448B9-0C63-45C2-B951-B0747149BCFE";

    [FunctionName(nameof(DequeuePendingAirdropsOrchestrator))]
    public async Task<List<Common.Repository.Queue.Model.Airdrop>> RunOrchestrator(
        [OrchestrationTrigger] IDurableOrchestrationContext context,
        ILogger log)
    {
        const int MaxStellarOperationsPerTransaction = 100;

        if (context.InstanceId != SingletonId)
        {
            throw new Exception($"You must use the singleton instance! (ie {nameof(DequeuePendingAirdropsOrchestrator)}.{nameof(SingletonId)})");
        }

        var totalPendingAirdrops = 0;
        var pendingAirdrops = new List<BasicMessage<Common.Repository.Queue.Model.Airdrop>>();

        while (totalPendingAirdrops < MaxStellarOperationsPerTransaction)
        {
            var pendingAirdropsBatch = await context.CallActivityAsync<List<BasicMessage<Common.Repository.Queue.Model.Airdrop>>>(
                nameof(GetPendingAirdropsActivity),
                MaxStellarOperationsPerTransaction - totalPendingAirdrops);
            
            if (pendingAirdropsBatch == null)
            {
                break;
            }

            pendingAirdrops.AddRange(pendingAirdropsBatch);

            totalPendingAirdrops += pendingAirdropsBatch.Count;
        }

        // delete airdrops
        await context.CallActivityAsync(nameof(DeletePendingAirdropsActivity), pendingAirdrops);

        return pendingAirdrops.Select(message => message.Value).ToList();
    }
}