using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Common.Repository.Cosmos.Model;
using stellar_dotnet_sdk;

namespace RivalCoins.Airdrop.Api.Function;

public class ClaimAirdrop
{
    private readonly IRepository<AirdropParticipant> _airdropParticipantRepo;
    private readonly Server _server;

    public ClaimAirdrop(IRepository<AirdropParticipant> airdropParticipantRepo, Server server)
    {
        _airdropParticipantRepo = airdropParticipantRepo;
        _server = server;
    }

    [FunctionName("GetDailyAirdrop")]
    public async Task<IActionResult> Run2(
    [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetDailyAirdrop")] HttpRequest req,
    ILogger log)
    {
        string payrollId = req.Query["stellar-id"];
        string asset = req.Query["asset"];

        return new OkResult();
    }

    [FunctionName(nameof(ClaimAirdrop))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "ClaimAirdrop")] HttpRequest req,
        ILogger log)
    {
        string payrollId = req.Query["stellar-id"];

        return new OkResult();
    }
}