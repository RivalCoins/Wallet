using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Queues;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Common.Repository.Cosmos.Model;
using stellar_dotnet_sdk;

namespace RivalCoins.Airdrop.Api.Job;

public class QueueAirdropsTrigger
{
    private readonly ServiceBusClient _serviceBus;
    private readonly IPayStubReader _payStubReader;

    public QueueAirdropsTrigger(ServiceBusClient serviceBus, IPayStubReader payStubReader)
    {
        _serviceBus = serviceBus;
        _payStubReader = payStubReader;
    }

    [FunctionName(nameof(QueueAirdropsTrigger))]
    public async Task Run(
        [ServiceBusTrigger(Constants.ValidatedAirdropParticipantQueue, Connection = Constants.ServiceBusConnection)] AirdropParticipant validatedAirdropParticipant,
        ILogger log)
    {
        try
        {
            var payStubs = await _payStubReader.GetPayStubsAsync(
                validatedAirdropParticipant.PayrollApiAccountId,
                new DateTimeOffset(DateTimeOffset.Now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero),
                DateTimeOffset.Now);

            await using var airdropQueuer = _serviceBus.CreateSender(Constants.AirdropQueue);
            using var airdropBatch = await airdropQueuer.CreateMessageBatchAsync();

            if (airdropBatch != null)
            {
                var airdrops = new List<ServiceBusMessage>();

                foreach (var payStub in payStubs.Where(p => p.Currency == Constants.AirdropCurrency))
                {
                    var airdrop = new Common.Repository.Queue.Model.Airdrop()
                    {
                        Asset = validatedAirdropParticipant.Asset,
                        PayDate = payStub.PayDate,
                        Quantity = payStub.TaxTotal,
                        StellarAccoutId = validatedAirdropParticipant.StellarAccountId
                    };

                    var airdropMessage = new ServiceBusMessage(JsonConvert.SerializeObject(airdrop))
                    {
                        SessionId = airdrop.PayDate.ToString("yyyyMMdd")
                    };

                    if (!airdropBatch.TryAddMessage(airdropMessage))
                    {
                        throw new Exception($"Failed to queue airdrop for participant {validatedAirdropParticipant.Id}");
                    }

                    airdrops.Add(airdropMessage);
                }

                using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    foreach (var airdrop in airdrops)
                    {
                        await airdropQueuer.SendMessageAsync(airdrop);
                    }

                    scope.Complete();
                }
            }
        }
        catch (Exception e)
        {
            log.LogError(e, nameof(QueueAirdropsTrigger));
            throw;
        }
    }
}