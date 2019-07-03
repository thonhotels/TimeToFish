using System;
using Microsoft.Extensions.DependencyInjection;

namespace TimeToFish
{
    public static class ServicesExtensions
    {
        public static IServiceCollection ConfigureTimerJobs(this IServiceCollection services, string connectionString, string serviceName, params (Type jobType, string subscriptionName)[] jobs)
        {
            foreach(var (j,_) in jobs)
                services.AddTransient(j);
            services
                .AddSingleton(p => BuildConfiguration(connectionString, p.GetRequiredService<IServiceScopeFactory>(), p, serviceName, jobs))
                .AddHostedService<TimeMessagingService>();

            return services;
        }

        private static TimeMessagingConfiguration BuildConfiguration(string connectionString, IServiceScopeFactory scopeFactory, IServiceProvider provider, string serviceName, (Type jobType, string jobName)[] jobs)
        {
            string SubscriptionName(string jobName) => $"{serviceName}.{jobName}";

            var cfg = new TimeMessagingConfiguration(connectionString, scopeFactory);

            foreach ((Type jobType, string jobName) in jobs)
            {
                if (!jobType.IsSubclassOf(typeof(TimerJob)))
                    throw new Exception("jobs must inherit from TimerJob");
                var job = (TimerJob)provider.GetRequiredService(jobType);
                cfg.AddJob(SubscriptionName(jobName), job.Handler);
            }
            return cfg;
        }
    }
}