using RivalCoins.Airdrop.Common.Api.Model;

namespace RivalCoins.Airdrop.Common;

public interface IPayStubReader
{
    Task<List<PayStub>> GetPayStubsAsync(string payrollApiAccountId, DateTimeOffset starting, DateTimeOffset ending);
}