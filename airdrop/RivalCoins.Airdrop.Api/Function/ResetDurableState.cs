using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using DurableTask.SqlServer;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using RivalCoins.Airdrop.Api.Job;

namespace RivalCoins.Airdrop.Api.Function;

class ResetDurableStateHelper
{
    readonly INameResolver nameResolver;

    // INameResolver is a service of the Functions host that can
    // be used to look up app settings.
    public ResetDurableStateHelper(INameResolver nameResolver)
    {
        this.nameResolver = nameResolver;
    }

    [FunctionName("StopAirdrop")]
    public async Task<HttpResponseMessage> StopAirdrop(
        [HttpTrigger(AuthorizationLevel.Function, "post")]
        HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient starter,
        ILogger log)
    {
        await starter.TerminateAsync(RunAirdropOrchestrator.SingletonId, "API intervention");

        //await starter.RestartAsync(RunAirdropOrchestrator.SingletonId, false);

        return new HttpResponseMessage(HttpStatusCode.OK);
    }

    [FunctionName("ResetDurableState")]
    public async Task<HttpResponseMessage> ResetDurableState(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestMessage req,
        [DurableClient] IDurableClient client,
        ILogger log)
    {
        // Get the connection string from app settings
        string connString = this.nameResolver.Resolve("AzureWebJobsStorage");
        var settings = new AzureStorageOrchestrationServiceSettings
        {
            StorageConnectionString = connString,
            TaskHubName = client.TaskHubName,
        };

        // AzureStorageOrchestrationService is defined in
        // Microsoft.Azure.DurableTask.AzureStorage, which is an
        // implicit dependency of the Durable Functions extension.
        var storageService = new AzureStorageOrchestrationService(settings);

        // Delete all Azure Storage tables, blobs, and queues in the task hub
        log.LogInformation(
            "Deleting all storage resources for task hub {taskHub}...",
            settings.TaskHubName);
        await storageService.DeleteAsync();

        // Wait for a minute since Azure Storage won't let us immediately
        // recreate resources with the same names as before.
        log.LogInformation(
            "The delete operation completed. Waiting one minute before recreating...");
        await Task.Delay(TimeSpan.FromMinutes(1));

        // Optional: Recreate all the Azure Storage resources for this task hub.
        // This happens automatically whenever the function app restarts, so it's
        // not a required step.
        log.LogInformation(
            "Recreating storage resources for task hub {taskHub}...",
            settings.TaskHubName);
        await storageService.CreateIfNotExistsAsync();

        return req.CreateResponse(System.Net.HttpStatusCode.OK);
    }
}