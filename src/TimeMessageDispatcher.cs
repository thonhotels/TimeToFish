using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;
using Serilog.Context;

namespace TimeToFish
{
    public interface IMessageDispatcher
    {
        Task Close();
        void RegisterMessageHandler(Func<ExceptionReceivedEventArgs, Task> exceptionReceivedHandler);
    }

    public class TimeMessageDispatcher : IMessageDispatcher
    {
        private IReceiverClient Client { get; }
        private IServiceScopeFactory ScopeFactory { get; }
        private Func<TimeEvent, Task<HandlerResult>> Handler { get; }

        internal TimeMessageDispatcher(IServiceScopeFactory scopeFactory, IReceiverClient client, Func<TimeEvent,Task<HandlerResult>> handler)
        {
            ScopeFactory = scopeFactory;
            Client = client;
            Handler = handler;
        }

        public async Task ProcessMessage(Microsoft.Azure.ServiceBus.Message message, CancellationToken token)
        {
            using (LogContext.PushProperty("CorrelationId", Guid.NewGuid()))
            {
                var body = Encoding.UTF8.GetString(message.Body);
                Log.Debug($"Received time message: SequenceNumber:{message.SystemProperties.SequenceNumber} Body:{body}");
                try
                {
                    if (!string.IsNullOrWhiteSpace(message.Label))
                    {
                        await ProcessMessage(message.Label, body,
                            () => Client.CompleteAsync(message.SystemProperties.LockToken),
                            m => AddToDeadLetter(message.SystemProperties.LockToken, m));
                    }
                    else
                    {
                        Log.Error("Message label is not set. \n Message: {@messageBody} \n Forwarding to DLX", body);
                        await AddToDeadLetter(message.SystemProperties.LockToken, "Message label is not set.");
                    }
                }
                catch (JsonException jsonException)
                {
                    Log.Error(jsonException, "Unable to deserialize message. \n Message: {@messageBody} \n Forwarding to DLX", body);
                    await AddToDeadLetter(message.SystemProperties.LockToken, jsonException.Message);
                }
            }
        }

        internal async Task ProcessMessage(string label, string body, Func<Task> markCompleted, Func<string, Task> abort)
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
        
            using (var scope = ScopeFactory.CreateScope())
            {
                var r = await Handler(message);
                if (HandlerResult.IsAbort(r))
                    await abort(r.Message);
                if (HandlerResult.IsSuccess(r))
                    await markCompleted();                    
            }
        }

        public async Task Close()
        {
            await Client.CloseAsync();
        }

        public void RegisterMessageHandler(Func<ExceptionReceivedEventArgs, Task> exceptionReceivedHandler)
        {
            Client.RegisterMessageHandler(ProcessMessage, new MessageHandlerOptions(exceptionReceivedHandler)
            {
                AutoComplete = false,
            });
        }

        private async Task AddToDeadLetter(string lockToken, string errorMessage)
        {
            await Client.DeadLetterAsync(lockToken, "Invalid message", errorMessage);
        }
    }
}