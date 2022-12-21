using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;
using Serilog.Context;

namespace TimeToFish;

public interface IMessageDispatcher
{
    Task Close();
    Task RegisterMessageHandler(Func<ProcessErrorEventArgs, Task> exceptionReceivedHandler);
}

public class TimeMessageDispatcher : IMessageDispatcher
{
    private IServiceScopeFactory ScopeFactory { get; }
    private Type JobType { get; }
    private ServiceBusProcessor Processor { get; }
    private ServiceBusClient Client { get; }

    internal TimeMessageDispatcher(IServiceScopeFactory scopeFactory, (ServiceBusClient, ServiceBusProcessor) c_p, Type jobType)
    {
        ScopeFactory = scopeFactory;
        Client = c_p.Item1;
        Processor = c_p.Item2;
        JobType = jobType;
    }

    private async Task ProcessMessage(ProcessMessageEventArgs args)
    {
        using (LogContext.PushProperty("CorrelationId", Guid.NewGuid()))
        {
            var body = args.Message.Body.ToString();
            var message = args.Message;
            Log.Debug($"Received time message: SequenceNumber:{message.SequenceNumber} Body:{body}");
            try
            {
                if (!string.IsNullOrWhiteSpace(message.Subject))
                {
                    await ProcessMessage(message.Subject, body,
                        () => args.CompleteMessageAsync(args.Message),
                        m => AddToDeadLetter(args, m));
                }
                else
                {
                    Log.Error("Message label is not set. \n Message: {@messageBody} \n Forwarding to DLX", body);
                    await AddToDeadLetter(args, "Message label is not set.");
                }
            }
            catch (JsonException jsonException)
            {
                Log.Error(jsonException, "Unable to deserialize message. \n Message: {@messageBody} \n Forwarding to DLX", body);
                await AddToDeadLetter(args, jsonException.Message);
            }
        }
    }

    internal async Task ProcessMessage(string subject, string body, Func<Task> markCompleted, Func<string, Task> abort)
    {
        bool MessageExpired(TimeEvent m)
        {
            var age = DateTime.Now.Subtract(DateTime.Parse(m.Time));
            return age > TimeSpan.FromMinutes(3.0);
        }

        var message = (TimeEvent)JsonConvert.DeserializeObject(body, typeof(TimeEvent));
        if (MessageExpired(message))
        {
            Log.Warning($"Ignoring expired message. Time: {message.Time}");
            await markCompleted();
            return;
        }

        using var scope = ScopeFactory.CreateScope();
        var job = (ITimerJob)scope.ServiceProvider.GetRequiredService(JobType);
        var r = await job.Handler(message);
        if (HandlerResult.IsAbort(r))
            await abort(r.Message);
        if (HandlerResult.IsSuccess(r))
            await markCompleted();
    }

    public async Task Close()
    {
        await Processor.StopProcessingAsync();
        await Processor.CloseAsync();
        await Client.DisposeAsync();
    }

    public Task RegisterMessageHandler(Func<ProcessErrorEventArgs, Task> exceptionReceivedHandler)
    {
        Processor.ProcessMessageAsync += ProcessMessage;
        Processor.ProcessErrorAsync += exceptionReceivedHandler;
        return Processor.StartProcessingAsync();
    }

    private Task AddToDeadLetter(ProcessMessageEventArgs args, string errorMessage) =>
        args.DeadLetterMessageAsync(args.Message, "Invalid message", errorMessage);
}