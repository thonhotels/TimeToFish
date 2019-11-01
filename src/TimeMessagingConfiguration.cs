using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Extensions.DependencyInjection;

namespace TimeToFish
{
    public class TimeMessagingConfiguration
    {
        public ICollection<IMessageDispatcher> Dispatchers { get; private set; }
        private string ConnectionString { get; }
        private IServiceScopeFactory ScopeFactory { get; }

        public TimeMessagingConfiguration(string connectionString, IServiceScopeFactory scopeFactory)
        {
            Dispatchers = new List<IMessageDispatcher>();
            ConnectionString = connectionString;
            ScopeFactory = scopeFactory;
        }

        private IReceiverClient CreateClient(string subscriptionName) =>
            new SubscriptionClient(new ServiceBusConnectionStringBuilder(ConnectionString), subscriptionName); 
        
        public void AddJob(string subscriptionName, Type jobType) =>
            Dispatchers.Add(new TimeMessageDispatcher(ScopeFactory, CreateClient(subscriptionName), jobType));

        public void RegisterMessageHandlers(Func<ExceptionReceivedEventArgs, Task> exceptionReceivedHandler)
        {
            Dispatchers
                .ToList()
                .ForEach(d => d.RegisterMessageHandler(exceptionReceivedHandler));
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
