using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Interfaces;

namespace PaymentIntegration.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly ICacheService _cacheService;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public ProductService(
        IProductRepository productRepository,
        ICacheService cacheService)
    {
        _productRepository = productRepository;
        _cacheService = cacheService;
    }

    public async Task<IReadOnlyList<ProductDto>> GetAllProductsAsync(CancellationToken ct = default)
    {
        const string cacheKey = "products:all";
        var cached = await _cacheService.GetAsync<IReadOnlyList<ProductDto>>(cacheKey, ct);
        if (cached != null)
        {
            return cached;
        }

        var products = await _productRepository.GetAllAsync(ct);
        var dtos = products.Select(p => new ProductDto(
            p.Id,
            p.Name,
            p.Description,
            p.Price,
            p.Currency,
            p.PaymentType.ToString()
        )).ToList();

        await _cacheService.SetAsync(cacheKey, (IReadOnlyList<ProductDto>)dtos, CacheTtl, ct);
        return dtos;
    }
}
