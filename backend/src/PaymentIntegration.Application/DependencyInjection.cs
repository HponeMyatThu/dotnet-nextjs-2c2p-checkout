using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Application.Services;
using PaymentIntegration.Application.Validators;

namespace PaymentIntegration.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CheckoutRequestValidator>();

        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IPaymentNotificationService, PaymentNotificationService>();

        return services;
    }
}
