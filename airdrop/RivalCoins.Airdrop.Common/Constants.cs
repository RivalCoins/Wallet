using stellar_dotnet_sdk;

namespace RivalCoins.Airdrop.Common;

public static class Constants
{
    public static readonly AssetTypeCreditAlphaNum USA = (AssetTypeCreditAlphaNum)Asset.Create("USA:GDMVZDKKIC3WSSGL2AWSM7V5E3YZNCAGXQMIXUWCVYKOLHRZMHJLVZ5C");
    public static readonly AssetTypeCreditAlphaNum USA2024 = (AssetTypeCreditAlphaNum)Asset.Create("PlayMONEY:GDWN72Y2FAXARUASIEN7WQNROJKKWIDWD7LIMG3IOZOTH3KBZ2PXLUKE");
    public static readonly AssetTypeCreditAlphaNum GovFundRewards = (AssetTypeCreditAlphaNum)Asset.Create("GFRewards:GCXTP2PBYHJRNA42FKYSBA2L2BI2TGULJIU3MRXCCMRTGKEQIXOXP3FO");

    public const int MaxStellarOperationsPerTransaction = 100;

    public const string AirdropQueue = "airdrop";
    public const string AirdropParticipantsPendingValidationQueue = "airdrop-participants-pending-validation";
    public const string ValidatedAirdropParticipantQueue = "validated-airdrop-participant";
    public const string StellarTransactionQueue = "stellar-transaction";

    public const string AirdropParticipantContainer = "airdrop-participant";
    public const string AirdropRunContainer = "airdrop-run";
    public const string RivalCoinUserContainer = "rivalcoin-user";

    public const string AirdropCurrency = "USD";

    public const string ServiceBusConnection = "ServiceBus";
}