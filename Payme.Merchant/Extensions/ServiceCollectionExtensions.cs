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
            services.AddScoped<PaymeDispatcher>(sp => new PaymeDispatcher(
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PaymeOptions>>(),
                sp.GetRequiredService<Abstractions.IPaymeOrderService>(),
                sp.GetRequiredService<Abstractions.IPaymeTransactionStore>(),
                (TimeProvider?)sp.GetService(typeof(TimeProvider)),
                (Microsoft.Extensions.Logging.ILogger<PaymeDispatcher>?)sp.GetService(typeof(Microsoft.Extensions.Logging.ILogger<PaymeDispatcher>))));
            return services;
        }
    }
}
