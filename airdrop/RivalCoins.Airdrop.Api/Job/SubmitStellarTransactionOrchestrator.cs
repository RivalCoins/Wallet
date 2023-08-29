using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using RivalCoins.Airdrop.Common.Repository.Queue.Model;

namespace RivalCoins.Airdrop.Api.Job;

public static class SubmitStellarTransactionOrchestrator
{
    public const string SingletonId = "3CDE48BD-6DD1-41CD-B371-B1ADCB7F296A";
    public const string QueueTransactionEvent = "6FF2E1E2-C243-4566-8AFD-3BBF14CDDAB1";

    [FunctionName(nameof(SubmitStellarTransactionOrchestrator))]
    public static async Task RunOrchestrator(
        [OrchestrationTrigger] IDurableOrchestrationContext context,
        ILogger log)
    {
        if (context.InstanceId != SingletonId)
        {
            throw new Exception($"You must use the singleton instance! (ie {nameof(SubmitStellarTransactionOrchestrator)}.{nameof(SingletonId)})");
        }

        var tx = await context.WaitForExternalEvent<StellarTransaction>(QueueTransactionEvent);

        await context.CallActivityAsync<bool>(nameof(SubmitStellarTransactionActivity), tx);

        context.ContinueAsNew(null);
    }
}