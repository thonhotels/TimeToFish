using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace TimeToFish;

public class TimeMessagingService : IHostedService
{
    private ITimeMessagingConfiguration Configuration { get; }

    public TimeMessagingService(ITimeMessagingConfiguration configuration)
    {
        Configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Configuration.RegisterMessageHandlers(ExceptionReceivedHandler);
        }
        catch (Exception exception)
        {
            Log.Error($"Error registering message handler", exception);
        }
    }

    Task ExceptionReceivedHandler(ProcessErrorEventArgs args)
    {
        Log.Error(args.Exception,
            $@"Message handler encountered an exception.
                        ErrorSource: {Enum.GetName(typeof(ServiceBusErrorSource), args.ErrorSource)}
                        Entity Path: {args.EntityPath}
                        Namespace: {args.FullyQualifiedNamespace}");

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Log.Information("Signal received. Gracefully shutting down.");
        await Configuration.Close();
        Thread.Sleep(1000);
    }
}