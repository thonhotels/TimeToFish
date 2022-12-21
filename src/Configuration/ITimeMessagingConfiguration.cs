using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace TimeToFish;

public interface ITimeMessagingConfiguration
{
    void AddJob(string subscriptionName, Type jobType);
    Task RegisterMessageHandlers(Func<ProcessErrorEventArgs, Task> exceptionReceivedHandler);
    Task Close();
}