using Microsoft.AspNetCore.Mvc;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Interfaces;

namespace PaymentIntegration.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    /// <summary>
    /// List all seeded customers.
    /// </summary>
    /// <returns>A list of customers with ID, name, and email.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> GetAll(CancellationToken ct)
    {
        var customers = await _customerService.GetAllCustomersAsync(ct);
        return Ok(customers);
    }

    /// <summary>
    /// List saved payment cards for a specific customer.
    /// </summary>
    /// <param name="id">The customer ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A list of tokenized saved cards with masked card numbers.</returns>
    [HttpGet("{id:int}/cards")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerCardDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerCardDto>>> GetCards(int id, CancellationToken ct)
    {
        var cards = await _customerService.GetCardsByCustomerIdAsync(id, ct);
        return Ok(cards);
    }
}
