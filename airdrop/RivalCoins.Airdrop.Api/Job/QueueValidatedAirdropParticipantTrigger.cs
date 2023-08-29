using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Queues;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Common.Repository.Cosmos.Model;
using stellar_dotnet_sdk;

namespace RivalCoins.Airdrop.Api.Job;

public class QueueValidatedAirdropParticipantTrigger
{
    private readonly Server _server;

    public QueueValidatedAirdropParticipantTrigger(Server server)
    {
        _server = server;
    }

    [FunctionName(nameof(QueueValidatedAirdropParticipantTrigger))]
    public async Task Run(
        [ServiceBusTrigger(Constants.AirdropParticipantsPendingValidationQueue, Connection = Constants.ServiceBusConnection)] AirdropParticipant airdropParticipantPendingValidation,
        [ServiceBus(Constants.ValidatedAirdropParticipantQueue, Connection = Constants.ServiceBusConnection)] IAsyncCollector<AirdropParticipant> validatedAirdropQueue,
        ILogger log)
    {
        const string ValidPayrollApi = "PINWHEEL";

        KeyPair? stellarAccount = null;
        AirdropParticipant? validatedAirdropParticipant = null;

        try
        {
            stellarAccount = KeyPair.FromAccountId(airdropParticipantPendingValidation.StellarAccountId);
        }
        catch (Exception)
        {
        }

        if (stellarAccount != null 
            && airdropParticipantPendingValidation.PayrollApi == ValidPayrollApi
            && airdropParticipantPendingValidation.PayrollApiAccountId != null)
        {
            var acceptsUsa = Helpers.TrustlineExistsAsync(
                Constants.USA,
                KeyPair.FromAccountId(airdropParticipantPendingValidation.StellarAccountId),
                _server);
            var subscribedToGovFundRewards = Helpers.TrustlineExistsAsync(
                Constants.GovFundRewards,
                KeyPair.FromAccountId(airdropParticipantPendingValidation.StellarAccountId),
                _server);

            _ = await Task.WhenAll(acceptsUsa, subscribedToGovFundRewards);

            if (acceptsUsa.Result && subscribedToGovFundRewards.Result)
            {
                await validatedAirdropQueue.AddAsync(airdropParticipantPendingValidation);
            }
        }
    }
}