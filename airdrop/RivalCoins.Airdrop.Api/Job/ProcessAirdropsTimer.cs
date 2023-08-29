using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.CosmosRepository.Specification;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RivalCoins.Airdrop.Common;
using RivalCoins.Airdrop.Common.Repository.Queue.Model;
using stellar_dotnet_sdk;

namespace RivalCoins.Airdrop.Api.Job;

public class ProcessAirdropsTimer
{
    private readonly ServiceBusClient _serviceBus;

    public ProcessAirdropsTimer(ServiceBusClient serviceBus)
    {
        _serviceBus = serviceBus;
    }

    private async Task<List<string>> GetQueueSessionsExAsync(string queueName)
    {
        var sessions = new List<string>();
        var receivedAllSessions = false;
        var options = new ServiceBusSessionReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock };

        while (!receivedAllSessions)
        {
            try
            {
                var session = await _serviceBus.AcceptNextSessionAsync(queueName, options);
                if (sessions.Any(s => s == session.SessionId))
                {
                    receivedAllSessions = true;
                }
                else
                {
                    sessions.Add(session.SessionId);
                }
            }
            catch (ServiceBusException e)
            {
                receivedAllSessions = true;
            }
            catch (Exception e)
            {
                ;
            }
        }

        return sessions;
    }

    private async Task<AsyncDisposableList<ServiceBusSessionReceiver>> GetQueueSessionsAsync(string queueName)
    {
        var sessions = new List<ServiceBusSessionReceiver>();
        var receivedAllSessions = false;
        var options = new ServiceBusSessionReceiverOptions()
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            PrefetchCount = Constants.MaxStellarOperationsPerTransaction
        };

        while (!receivedAllSessions)
        {
            try
            {
                var session = await _serviceBus.AcceptNextSessionAsync(queueName, options);
                if (sessions.Any(s => s.SessionId == session.SessionId))
                {
                    receivedAllSessions = true;
                }
                else
                {
                    sessions.Add(session);
                }
            }
            catch (ServiceBusException e)
            {
                receivedAllSessions = true;
            }
            catch (Exception e)
            {
                ;
            }
        }

        return new AsyncDisposableList<ServiceBusSessionReceiver>(sessions);
    }

    private class AsyncDisposableList<T> 
        : IEnumerable<T>, IAsyncDisposable
        where T : class, IAsyncDisposable
    {
        private List<T> _list;

        public AsyncDisposableList(List<T> list)
        {
            _list = list;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var asyncDisposable in _list.OfType<IAsyncDisposable>())
            {
                await asyncDisposable.DisposeAsync();
            }
        }
    }

    private async Task<ServiceBusSessionReceiver> GetSessionAsync(string sessionId)
    {
        return await _serviceBus.AcceptNextSessionAsync(
            Constants.AirdropQueue,
            new ServiceBusSessionReceiverOptions()
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                PrefetchCount = Constants.MaxStellarOperationsPerTransaction
            });
    }

    private async Task<List<ServiceBusSessionReceiver>> GetAirdropsByPayDateAscendingAsync()
    {
        var sessions = new List<ServiceBusSessionReceiver>();
        var receivedAllSessions = false;

        while (!receivedAllSessions)
        {
            try
            {
                var session = await _serviceBus.AcceptNextSessionAsync(
                    Constants.AirdropQueue,
                    new ServiceBusSessionReceiverOptions()
                    {
                        ReceiveMode = ServiceBusReceiveMode.PeekLock,
                        PrefetchCount = Constants.MaxStellarOperationsPerTransaction
                    });

                if (sessions.Any(s => s.SessionId == session.SessionId))
                {
                    receivedAllSessions = true;
                }
                else
                {
                    sessions.Add(session);
                }
            }
            catch (ServiceBusException e)
            {
                receivedAllSessions = true;
            }
        }

        return sessions.OrderBy(session => session.SessionId).ToList();
    }

    private async Task<ServiceBusSessionReceiver> GetRefreshedReceiverAsync(ServiceBusSessionReceiver airdropsForPayDate)
    {
        var refreshed = airdropsForPayDate;

        try
        {
            if (refreshed.IsClosed)
            {
                await airdropsForPayDate.CloseAsync();
                await airdropsForPayDate.DisposeAsync();

                //refreshed = await _serviceBus.AcceptSessionAsync(
                //    Constants.AirdropQueue,
                //    airdropsForPayDate.SessionId,
                //    new ServiceBusSessionReceiverOptions()
                //    {
                //        ReceiveMode = ServiceBusReceiveMode.PeekLock,
                //        PrefetchCount = Constants.MaxStellarOperationsPerTransaction
                //    });

                refreshed = await this.GetSessionAsync(refreshed.SessionId);
            }
            else
            {
                await refreshed.RenewSessionLockAsync();
            }
        }
        catch (ServiceBusException e)
        {
            ;
        }

        return refreshed;
    }

    [FunctionName(nameof(ProcessAirdropsTimer))]
    public async Task Run(
        [TimerTrigger("* * * * *")] TimerInfo myTimer,
        //[HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
        ILogger log)
    {
        try
        {
            await using var transactionQueuer = _serviceBus.CreateSender(Constants.StellarTransactionQueue);
            var airdropsByPayDay = await this.GetAirdropsByPayDateAscendingAsync();
            
            foreach (var staleAirdropByPayDate in airdropsByPayDay)
            {
                try
                {
                    await using var airdropsForPayDate = await this.GetRefreshedReceiverAsync(staleAirdropByPayDate);
                    var airdropsForPayDateBatch =
                        await airdropsForPayDate.ReceiveMessagesAsync(Constants.MaxStellarOperationsPerTransaction);

                    if (airdropsForPayDateBatch != null && airdropsForPayDateBatch.Any())
                    {
                        var airdrops = airdropsForPayDateBatch.Select(a =>
                                JsonConvert.DeserializeObject<Common.Repository.Queue.Model.Airdrop>(a.Body.ToString()))
                            .ToList();
                        var tx = new StellarTransaction();

                        tx.Memo = $"{airdrops.First().PayDate:yyyy-MM-dd} Gov Fund Rewards";

                        foreach (var a in airdrops)
                        {
                            tx.Operations.Add(new PaymentOperation.Builder(
                                    KeyPair.FromAccountId(a.StellarAccoutId),
                                    Asset.Create(a.Asset),
                                    a.Quantity.ToString())
                                .Build().ToXdrBase64());
                        }

                        using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                        {
                            await transactionQueuer.SendMessageAsync(new ServiceBusMessage(JsonConvert.SerializeObject(tx)));

                            foreach (var airdropForPayDate in airdropsForPayDateBatch)
                            {
                                await airdropsForPayDate.CompleteMessageAsync(airdropForPayDate);
                            }

                            scope.Complete();
                        }
                    }
                }
                catch (ServiceBusException e)
                {
                    ;
                }
                catch (Exception e)
                {
                    log.LogError(e, nameof(ProcessAirdropsTimer));
                    throw;
                }
            }
        }
        catch (ServiceBusException e)
        {
            ;
        }
        catch (Exception e)
        {
            log.LogError(e, nameof(ProcessAirdropsTimer));
            throw;
        }

        //return new OkResult();
    }
}