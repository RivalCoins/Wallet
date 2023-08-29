using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Common.Repository.Cosmos.Model;

namespace RivalCoins.Airdrop.Api.Function;

public class RegisterForAirdrop
{
    private readonly IRepository<AirdropParticipant> _airdropParticipantRepo;

    public RegisterForAirdrop(IRepository<AirdropParticipant> airdropParticipantRepo)
    {
        _airdropParticipantRepo = airdropParticipantRepo;
    }

    [FunctionName(nameof(RegisterForAirdrop))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "RegisterForAirdrop")] HttpRequest req,
        ILogger log)
    {
        string payrollId = req.Query["payroll-id"];
        string stellarId = req.Query["stellar-id"];
        string payrollApi = req.Query["payroll-api"];
        string queryAsset = req.Query["asset"];

        var usr = new AirdropParticipant();
        usr.PayrollApi = payrollApi;
        usr.PayrollApiAccountId = payrollId;
        usr.StellarAccountId = stellarId;
        usr.Asset = queryAsset;

        await _airdropParticipantRepo.CreateAsync(usr);

        return new OkResult();
    }
}