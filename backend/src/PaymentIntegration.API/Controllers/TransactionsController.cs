using Microsoft.AspNetCore.Mvc;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Interfaces;

namespace PaymentIntegration.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;

    public TransactionsController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    /// <summary>
    /// Get the latest status and details of a transaction by invoice number.
    /// </summary>
    /// <param name="invoiceNo">The unique merchant invoice number</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Transaction details and current status (Pending, Succeeded, or Failed).</returns>
    [HttpGet("{invoiceNo}")]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionDto>> GetByInvoiceNo(string invoiceNo, CancellationToken ct)
    {
        var transaction = await _transactionService.GetByInvoiceNoAsync(invoiceNo, ct);
        if (transaction == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Transaction Not Found",
                Detail = $"Transaction with invoice number '{invoiceNo}' was not found.",
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(transaction);
    }
}
