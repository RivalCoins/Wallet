using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace RivalCoins.Airdrop.Api.Job;

public class DequeuePendingAirdropsForNextDayOrchestrator
{
    public const string SingletonId = "C7EC4E5F-4D22-416C-AEAB-946773C3E898";

    [FunctionName(nameof(DequeuePendingAirdropsForNextDayOrchestrator))]
    public async Task<List<Common.Repository.Queue.Model.Airdrop>> RunOrchestrator(
        [OrchestrationTrigger] IDurableOrchestrationContext context,
        ILogger log)
    {
        const int MaxStellarOperationsPerTransaction = 100;

        if (context.InstanceId != SingletonId)
        {
            throw new Exception($"You must use the singleton instance! (ie {nameof(DequeuePendingAirdropsForNextDayOrchestrator)}.{nameof(SingletonId)})");
        }

        var pendingAirdropsForNextDay = await context.CallActivityAsync<List<BasicMessage<Common.Repository.Queue.Model.Airdrop>>>(
            nameof(GetPendingAirdropsForNextDayActivity),
            MaxStellarOperationsPerTransaction);

        // delete airdrops
        await context.CallActivityAsync(nameof(DeletePendingAirdropsActivity), pendingAirdropsForNextDay);

        return pendingAirdropsForNextDay.Select(message => message.Value).ToList();
    }
}