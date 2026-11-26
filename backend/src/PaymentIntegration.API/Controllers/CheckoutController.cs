using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Interfaces;

namespace PaymentIntegration.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CheckoutController : ControllerBase
{
    private readonly ICheckoutService _checkoutService;
    private readonly IValidator<CheckoutRequestDto> _validator;

    public CheckoutController(
        ICheckoutService checkoutService, 
        IValidator<CheckoutRequestDto> validator)
    {
        _checkoutService = checkoutService;
        _validator = validator;
    }

    /// <summary>
    /// Process checkout for OneTime, Recurring, or Saved-Card payment.
    /// </summary>
    /// <remarks>
    /// Sample request:
    /// 
    ///     POST /api/checkout
    ///     Header: Idempotency-Key: e44e881c-b473-41da-b45e-6c7c9e6d49fb
    ///     {
    ///        "customerId": 1,
    ///        "productId": 1,
    ///        "customerCardId": null
    ///     }
    /// 
    /// </remarks>
    /// <param name="idempotencyKey">UUID idempotency token provided in the header</param>
    /// <param name="request">Checkout details</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Checkout response with invoice number, transaction status, and optional redirect URL.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(CheckoutResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CheckoutResponseDto>> Checkout(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] CheckoutRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Missing Idempotency-Key",
                Detail = "The 'Idempotency-Key' HTTP header is required. Please provide a valid UUID.",
                Instance = HttpContext.Request.Path
            });
        }

        if (!Guid.TryParse(idempotencyKey, out _))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Idempotency-Key",
                Detail = "The 'Idempotency-Key' HTTP header must be a valid UUID format (e.g. 12345678-1234-1234-1234-123456789abc).",
                Instance = HttpContext.Request.Path
            });
        }

        var validationResult = await _validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Failed",
                Detail = "One or more validation errors occurred.",
                Instance = HttpContext.Request.Path
            };
            problemDetails.Extensions["errors"] = errors;
            return BadRequest(problemDetails);
        }

        var response = await _checkoutService.ProcessCheckoutAsync(idempotencyKey, request, ct);
        return Ok(response);
    }
}
