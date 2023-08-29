using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using RivalCoins.Airdrop.Common.Repository.Queue.Model;
using RivalCoins.Sdk;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.xdr;
using Operation = stellar_dotnet_sdk.Operation;
using StellarTransaction = RivalCoins.Airdrop.Common.Repository.Queue.Model.StellarTransaction;

namespace RivalCoins.Airdrop.Api.Job;

public class SubmitStellarTransactionActivity
{
    private readonly Server _server;
    private readonly Wallet _airdropWallet;

    public SubmitStellarTransactionActivity(Server server, Wallet wallet)
    {
        _server = server;
        _airdropWallet = wallet;
    }

    [FunctionName(nameof(SubmitStellarTransactionActivity))]
    public async Task<bool> Run(
        [ActivityTrigger] StellarTransaction transactionInfo,
        ILogger log)
    {
        var success = false;

        try
        {
            var airdropAccount = await _airdropWallet.Server.Accounts.Account(_airdropWallet.Account.Signer.AccountId);
            var transactionBuilder = new TransactionBuilder(airdropAccount);

            // add memo
            if (!string.IsNullOrWhiteSpace(transactionInfo.Memo))
            {
                transactionBuilder.AddMemo(new MemoText(transactionInfo.Memo));
            }

            // add operations
            foreach (var operation in transactionInfo.Operations)
            {
                transactionBuilder.AddOperation(Operation.FromXdr(stellar_dotnet_sdk.xdr.Operation.Decode(new XdrDataInputStream(Convert.FromBase64String(operation)))));
            }

            // sign transaction
            var transaction = transactionBuilder.Build();
            Wallet.AutoSign(transaction, _airdropWallet);

            // submit transaction
            var response = await _server.SubmitTransaction(transaction);

            success = response.IsSuccess();
        }
        catch (Exception e)
        {
            log.LogError(e, $"{nameof(SubmitStellarTransactionActivity)} - Transaction submission exception");
        }

        return success;
    }
}