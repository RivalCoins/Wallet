using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Common.Repository.Cosmos.Model;

namespace RivalCoins.Airdrop.Api.Job;

public class GenerateAirdropsActivity
{
    private readonly IRepository<AirdropParticipant> _airdropParticipantRepository;
    private readonly ServiceBusClient _serviceBus;

    public GenerateAirdropsActivity(
        IRepository<AirdropParticipant> airdropParticipantRepository,
        ServiceBusClient serviceBus)
    {
        _airdropParticipantRepository = airdropParticipantRepository;
        _serviceBus = serviceBus;
    }

    [FunctionName(nameof(GenerateAirdropsActivity))]
    public async Task Run(
        [ActivityTrigger] string airdropId,
        ILogger log)
    {
        await using var airdropParticipantsPendingValidation = _serviceBus.CreateSender(Constants.AirdropParticipantsPendingValidationQueue);

        await foreach (var airdropParticipantPendingAirdrop in this.GetAirdropParticipantsPendingAirdrop(airdropId).Take(2))
        {
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                airdropParticipantPendingAirdrop.AirdropCampaigns.Add(airdropId);

                // queue
                await airdropParticipantsPendingValidation.SendMessageAsync(new ServiceBusMessage(JsonConvert.SerializeObject(airdropParticipantPendingAirdrop)));

                // save airdrop campaign
                await _airdropParticipantRepository.UpdateAsync(airdropParticipantPendingAirdrop);

                scope.Complete();
            }
        }
    }

    private async IAsyncEnumerable<AirdropParticipant> GetAirdropParticipantsPendingAirdrop(string airdropId)
    {
        string? continuationToken = null;

        do
        {
            var page = await _airdropParticipantRepository.PageAsync(
                airdropParticipant =>  true, //!airdropParticipant.AirdropCampaigns.Contains(airdropId), // airdropParticipant.Asset == Constants.USA.CanonicalName(),
                25,
                continuationToken);

            foreach (var airdropParticipant in page.Items)
            {
                yield return airdropParticipant;
            }

            continuationToken = page.Continuation;

        } while (continuationToken != null);
    }
}