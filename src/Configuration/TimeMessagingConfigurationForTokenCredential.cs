using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;

namespace TimeToFish;

public class TimeMessagingConfigurationForTokenCredential : ITimeMessagingConfiguration
{
    private string EntityName { get; }
    private ICollection<IMessageDispatcher> Dispatchers { get; }
    private TokenCredential TokenCredential { get; }
    public string FullyQualifiedNamespace { get; }
    private IServiceScopeFactory ScopeFactory { get; }

    public TimeMessagingConfigurationForTokenCredential(TokenCredential tokenCredential, string fullyQualifiedNamespace, string entityName, IServiceScopeFactory scopeFactory)
    {
        EntityName = entityName;
        Dispatchers = new List<IMessageDispatcher>();
        TokenCredential = tokenCredential;
        FullyQualifiedNamespace = fullyQualifiedNamespace;
        ScopeFactory = scopeFactory;
    }
        
    public void AddJob(string subscriptionName, Type jobType) {
        var client = new ServiceBusClient(FullyQualifiedNamespace, TokenCredential);
        var a = (client, client.CreateProcessor(EntityName, subscriptionName,
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1
            }));
        Dispatchers.Add(new TimeMessageDispatcher(ScopeFactory, a, jobType));
    }
        
    public async Task RegisterMessageHandlers(Func<ProcessErrorEventArgs, Task> exceptionReceivedHandler)
    {
        foreach (var d in Dispatchers)
            await d.RegisterMessageHandler(exceptionReceivedHandler);
    }

    public async Task Close()
    {
        await Task.WhenAll(
            Dispatchers
                .Select(async d => await d.Close())
                .ToArray()
        );
    }
}