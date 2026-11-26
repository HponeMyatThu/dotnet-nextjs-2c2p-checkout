using FluentValidation;
using PaymentIntegration.Application.DTOs;

namespace PaymentIntegration.Application.Validators;

public class CheckoutRequestValidator : AbstractValidator<CheckoutRequestDto>
{
    public CheckoutRequestValidator()
    {
        RuleFor(x => x.CustomerId)
            .GreaterThan(0)
            .WithMessage("CustomerId must be a valid positive integer.");

        RuleFor(x => x.ProductId)
            .GreaterThan(0)
            .WithMessage("ProductId must be a valid positive integer.");

        When(x => x.CustomerCardId.HasValue, () =>
        {
            RuleFor(x => x.CustomerCardId!.Value)
                .GreaterThan(0)
                .WithMessage("CustomerCardId must be a valid positive integer when provided.");
        });
    }
}
