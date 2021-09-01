using System;
using Azure.Core;
using Microsoft.Extensions.DependencyInjection;

namespace TimeToFish
{
    public static class ServicesExtensions
    {
        public static IServiceCollection ConfigureTimerJobs(
            this IServiceCollection services,
            string connectionString,
            string serviceName,
            params (Type jobType, string subscriptionName)[] jobs)
        {
            foreach(var (j,_) in jobs)
                services.AddTransient(j);
            services
                .AddSingleton(p => BuildConfiguration(connectionString, p.GetRequiredService<IServiceScopeFactory>(), serviceName, jobs))
                .AddHostedService<TimeMessagingService>();

            return services;
        }
        
        public static IServiceCollection ConfigureTimerJobs(
            this IServiceCollection services,
            TokenCredential tokenCredential,
            string fullyQualifiedNamespace,
            string entityName,
            string serviceName,
            params (Type jobType, string subscriptionName)[] jobs)
        {
            foreach(var (j,_) in jobs)
                services.AddTransient(j);
            services
                .AddSingleton(p => BuildConfiguration(tokenCredential, fullyQualifiedNamespace, entityName, p.GetRequiredService<IServiceScopeFactory>(), serviceName, jobs))
                .AddHostedService<TimeMessagingService>();

            return services;
        }

        private static ITimeMessagingConfiguration BuildConfiguration(
            string connectionString,
            IServiceScopeFactory scopeFactory,
            string serviceName,
            (Type jobType, string jobName)[] jobs)
        {
            string SubscriptionName(string jobName) => $"{serviceName}.{jobName}";

            var cfg = new TimeMessagingConfigurationForConnectionString(connectionString, scopeFactory);

            foreach ((Type jobType, string jobName) in jobs)
            {
                if (!typeof(ITimerJob).IsAssignableFrom(jobType))
                    throw new Exception("jobs must inherit from TimerJob");
                cfg.AddJob(SubscriptionName(jobName), jobType);
            }
            return cfg;
        }
        
        private static ITimeMessagingConfiguration BuildConfiguration(
            TokenCredential tokenCredential,
            string fullyQualifiedNamespace,
            string entityName,
            IServiceScopeFactory scopeFactory,
            string serviceName,
            (Type jobType, string jobName)[] jobs)
        {
            string SubscriptionName(string jobName) => $"{serviceName}.{jobName}";

            var cfg = new TimeMessagingConfigurationForTokenCredential(tokenCredential, fullyQualifiedNamespace, entityName, scopeFactory);

            foreach ((Type jobType, string jobName) in jobs)
            {
                if (!typeof(ITimerJob).IsAssignableFrom(jobType))
                    throw new Exception("jobs must inherit from TimerJob");
                cfg.AddJob(SubscriptionName(jobName), jobType);
            }
            return cfg;
        }
    }
}