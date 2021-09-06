using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;

namespace TimeToFish
{
    public class TimeMessagingConfigurationForConnectionString : ITimeMessagingConfiguration
    {
        private ICollection<IMessageDispatcher> Dispatchers { get; }
        private string ConnectionString { get; }
        private IServiceScopeFactory ScopeFactory { get; }

        public TimeMessagingConfigurationForConnectionString(string connectionString, IServiceScopeFactory scopeFactory)
        {
            Dispatchers = new List<IMessageDispatcher>();
            ConnectionString = connectionString;
            ScopeFactory = scopeFactory;
        }

        private string GetEntityName() => ServiceBusConnectionStringProperties.Parse(ConnectionString).EntityPath;
        
        public void AddJob(string subscriptionName, Type jobType) {
            var client = new ServiceBusClient(ConnectionString);
            var a = (client, client.CreateProcessor(GetEntityName(), subscriptionName,
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
}
