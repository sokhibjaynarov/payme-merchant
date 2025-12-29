using System;
using Microsoft.Extensions.DependencyInjection;
using Payme.Merchant.Configuration;

namespace Payme.Merchant.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddPaymeMerchant(this IServiceCollection services, Action<PaymeOptions> configureOptions)
        {
            services.Configure(configureOptions);
            services.AddScoped<PaymeDispatcher>();
            return services;
        }
    }
}
