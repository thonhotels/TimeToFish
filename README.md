# TimeToFish
Library for simple handling of time events to implement crontab like functionality using azure service bus events.
Inspired by [Passage of time event pattern](http://verraes.net/2019/05/patterns-for-decoupling-distsys-passage-of-time-event/)

The library consist of two extension methods on the IServiceCollection interface. </br>
`IServiceCollection ConfigureTimerJobs(this IServiceCollection services, string connectionString, string serviceName, params (Type jobType, string subscriptionName)[] jobs)`

`IServiceCollection ConfigureTimerJobs(this IServiceCollection services, TokenCredential tokenCredential, string fullyQualifiedNamespace, string entityName, string serviceName, params (Type jobType, string subscriptionName)[] jobs)`
- `connectionString`: The connection string to the Azure Service Bus topic where the TimeHasPassed events are published
- `serviceName`: Unique name of the service 
- `jobs`: Array of tuples containing `jobType` and `subscriptionName`. `jobType` is the type of the class implementing the event handler/the job. `subscriptionName` is the name of a subscription on the Topic. The topic should be configured to filter on the right Time events. [Bluefin](https://www.nuget.org/packages/Bluefin/) has a function `Jobs.createJobSubscriptions` which can be used to create subscriptions.
- `tokenCredential`: Azure Active Directory token authentication support.
- `fullyQualifiedNamespace`: The fully qualified Service Bus namespace that the connection is associated with. This is likely to be similar to `{yournamespace}.servicebus.windows.net`.
- `entityName`: The name of the specific Service Bus entity instance under the associated Service Bus namespace.

Source code for Bluefin `Jobs.createJobSubscriptions` can be found [here](https://github.com/thonhotels/bluefin/blob/master/src/Jobs.fs)

A JobHandler must implement the interface `TimeToFish.ITimerJob`</br> 
This interface has a single method: `Task<HandlerResult> Handler(TimeEvent message)`
The handler will be called when there is a message on the subscription and should return one of
- `HandlerResult.Success`
- `HandlerResult.Failed`
- `HandlerResult.Abort` // used when you don´t want any retries

Example:</br>
Handlers:</br>
<pre><code>
    public class MyFirstJob : ITimerJob
    {
        public Task<HandlerResult> Handler(TimeEvent message)
        {
            Console.WriteLine($"My first job was started. {DateTime.Now}");
            return Task.FromResult(HandlerResult.Success());
        }

        public static (Type, string) JobDefinition { get => (typeof(MyFirstJob), "my-first-job"); }           
    }

    public class MySecondJob : ITimerJob
    {
        public Task<HandlerResult> Handler(TimeEvent message)
        {
            Console.WriteLine($"My second job was started. {DateTime.Now}");
            return Task.FromResult(HandlerResult.Success());
        }

        public static (Type, string) JobDefinition { get => (typeof(MySecondJob), "my-second-job"); }           
    }
</code></pre>

Initialization, typically in Program.cs
<pre><code>
await new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.ConfigureTimerJobs(Configuration.GetConnectionString("timeTopic"),
                                    "myservice", 
                                    MyFirstJob.JobDefinition,
                                    MySecondJob.JobDefinition);
                })                    
                .RunConsoleAsync();
</code></pre>

This will register the two types MyFirstJob and MySecondJob in the IoC container, wire up the two jobs with the 
azure servicebus and register a HostedService that will contain the messaghandlers.
The code will assume that there are two subscriptions named "myservice.my-first-job" and "myservice.my-second-job" 
registered on the topic.
