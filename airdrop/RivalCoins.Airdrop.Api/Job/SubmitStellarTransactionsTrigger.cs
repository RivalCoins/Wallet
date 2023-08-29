using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Common.Repository.Cosmos.Model;
using RivalCoins.Airdrop.Common.Repository.Queue.Model;
using RivalCoins.Sdk;
using RivalCoins.Sdk.Grpc;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.xdr;
using Constants = RivalCoins.Airdrop.Common.Constants;
using Operation = stellar_dotnet_sdk.Operation;
using StellarTransaction = RivalCoins.Airdrop.Common.Repository.Queue.Model.StellarTransaction;

namespace RivalCoins.Airdrop.Api.Job;

public class SubmitStellarTransactionsTrigger
{
    private readonly Wallet _wallet;

    public SubmitStellarTransactionsTrigger(Wallet wallet)
    {
        _wallet = wallet;
    }

    [Singleton(Mode = SingletonMode.Listener)]
    [FunctionName(nameof(SubmitStellarTransactionsTrigger))]
    public async Task Run(
        [ServiceBusTrigger(Constants.StellarTransactionQueue, Connection = Constants.ServiceBusConnection)] StellarTransaction tx,
        [DurableClient] IDurableOrchestrationClient starter,
        ILogger log)
    {
        var transactionSubmissionStatus = await starter.GetStatusAsync(SubmitStellarTransactionOrchestrator.SingletonId);
        if (transactionSubmissionStatus.RuntimeStatus != OrchestrationRuntimeStatus.Running)
        {
            await starter.StartNewAsync(nameof(SubmitStellarTransactionOrchestrator), SubmitStellarTransactionOrchestrator.SingletonId);
        }

        await starter.RaiseEventAsync(
            SubmitStellarTransactionOrchestrator.SingletonId,
            SubmitStellarTransactionOrchestrator.QueueTransactionEvent,
            tx);

        return;
        var airdropAccount = await _wallet.Server.Accounts.Account(_wallet.Account.Signer.AccountId);
        var transactionBuilder = new TransactionBuilder(airdropAccount);

        // add memo
        if (!string.IsNullOrWhiteSpace(tx.Memo))
        {
            transactionBuilder.AddMemo(new MemoText(tx.Memo));
        }

        // add operations
        foreach (var operation in tx.Operations)
        {
            transactionBuilder.AddOperation(Operation.FromXdr(stellar_dotnet_sdk.xdr.Operation.Decode(new XdrDataInputStream(Convert.FromBase64String(operation)))));
        }

        // sign transaction
        var transaction = transactionBuilder.Build();

        // submit transaction
        var response = await _wallet.SubmitTransactionAsync(transaction, true, tx.Memo);
        if (response?.IsSuccess() is null or false)
        {
            throw new Exception($"{nameof(SubmitStellarTransactionsTrigger)} - {tx.Memo}");
        }
    }
}