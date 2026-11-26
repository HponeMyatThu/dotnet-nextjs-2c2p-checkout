using Microsoft.Extensions.Logging;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Interfaces;

namespace PaymentIntegration.Application.Services;

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerCardRepository _customerCardRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CustomerService> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public CustomerService(
        ICustomerRepository customerRepository,
        ICustomerCardRepository customerCardRepository,
        ICacheService cacheService,
        ILogger<CustomerService> logger)
    {
        _customerRepository = customerRepository;
        _customerCardRepository = customerCardRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CustomerDto>> GetAllCustomersAsync(CancellationToken ct = default)
    {
        const string cacheKey = "customers:all";
        var cached = await _cacheService.GetAsync<IReadOnlyList<CustomerDto>>(cacheKey, ct);
        if (cached != null)
        {
            return cached;
        }

        var customers = await _customerRepository.GetAllAsync(ct);
        var dtos = customers.Select(c => new CustomerDto(
            c.Id,
            c.FirstName,
            c.LastName,
            c.Email,
            $"{c.FirstName} {c.LastName}".Trim()
        )).ToList();

        await _cacheService.SetAsync(cacheKey, (IReadOnlyList<CustomerDto>)dtos, CacheTtl, ct);
        return dtos;
    }

    public async Task<IReadOnlyList<CustomerCardDto>> GetCardsByCustomerIdAsync(int customerId, CancellationToken ct = default)
    {
        var cacheKey = $"customers:{customerId}:cards";
        var cached = await _cacheService.GetAsync<IReadOnlyList<CustomerCardDto>>(cacheKey, ct);
        if (cached != null)
        {
            return cached;
        }

        var cards = await _customerCardRepository.GetByCustomerIdAsync(customerId, ct);
        var dtos = cards.Select(c => new CustomerCardDto(
            c.Id,
            c.CustomerId,
            c.MaskedCardNo,
            c.CreatedAt
        )).ToList();

        await _cacheService.SetAsync(cacheKey, (IReadOnlyList<CustomerCardDto>)dtos, CacheTtl, ct);
        return dtos;
    }
}
