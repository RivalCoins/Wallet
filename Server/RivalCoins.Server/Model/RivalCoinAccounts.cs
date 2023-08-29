using RivalCoins.Sdk;

namespace RivalCoins.Server.Model;

public record RivalCoinAccounts(
    (Wallet Issuer, Wallet Distributor) L1,
    (Wallet Issuer, Wallet Distributor) L2,
    (Wallet Issuer, Wallet Distributor) Wrapper);
