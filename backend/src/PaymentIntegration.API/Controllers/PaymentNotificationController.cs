using Microsoft.AspNetCore.Mvc;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Interfaces;

namespace PaymentIntegration.API.Controllers;

[ApiController]
[Route("api/payments/2c2p")]
[Produces("application/json")]
public class PaymentNotificationController : ControllerBase
{
    private readonly IPaymentNotificationService _notificationService;
    private readonly ILogger<PaymentNotificationController> _logger;

    public PaymentNotificationController(
        IPaymentNotificationService notificationService,
        ILogger<PaymentNotificationController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Webhook endpoint called by 2C2P with the payment result notification.
    /// </summary>
    /// <remarks>
    /// 2C2P POSTs a JSON payload containing the HMAC SHA-256 signed JWT:
    /// 
    ///     POST /api/payments/2c2p/notification
    ///     {
    ///        "payload": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
    ///     }
    /// 
    /// </remarks>
    /// <param name="request">Notification wrapper containing the JWT payload</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>HTTP 200 OK acknowledging receipt.</returns>
    [HttpPost("notification")]
    [ProducesResponseType(typeof(PaymentNotificationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentNotificationResultDto>> HandleNotification(
        [FromBody] PaymentNotificationRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Payload))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Notification Payload",
                Detail = "The notification request body must contain a non-empty 'payload' string.",
                Instance = HttpContext.Request.Path
            });
        }

        var result = await _notificationService.HandleNotificationAsync(request.Payload, ct);
        if (!result.Success)
        {
            _logger.LogWarning("Notification processing failed: {Message} for invoice {InvoiceNo}", 
                result.Message, result.InvoiceNo);
            // Return 400 if verification failed so 2C2P can see signature error, or 200 if desired
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Notification Verification Failed",
                Detail = result.Message,
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(result);
    }
}
