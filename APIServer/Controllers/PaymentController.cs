using ApiServer.DB;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenService _tokenService;
    private readonly TokenValidator _tokenValidator;
    
    public PaymentController(AppDbContext context, TokenService tokenService, TokenValidator tokenValidator)
    {
        _context = context;
        _tokenService = tokenService;
        _tokenValidator = tokenValidator;
    }

    [HttpPost]
    [Route("InitProducts")]
    public IActionResult InitProducts([FromBody] InitProductPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var productGroups = _context.Product
            .Where(product => product.IsFixed)
            .GroupBy(product => product.Category)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.ToList());
        var compositions = _context.ProductComposition.ToList();
        var probabilities = _context.CompositionProbability.ToList();
        var res = new InitProductPacketResponse
        {
            GetProductOk = true,
            SpecialPackages = GetProductInfoList(ProductCategory.SpecialPackage, productGroups, compositions, probabilities),
            BeginnerPackages = GetProductInfoList(ProductCategory.BeginnerPackage, productGroups, compositions, probabilities),
            GoldPackages = GetProductInfoList(ProductCategory.GoldPackage, productGroups, compositions, probabilities),
            SpinelPackages = GetProductInfoList(ProductCategory.SpinelPackage, productGroups, compositions, probabilities),
            GoldItems = GetProductInfoList(ProductCategory.GoldItem, productGroups, compositions, probabilities),
            SpinelItems = GetProductInfoList(ProductCategory.SpinelItem, productGroups, compositions, probabilities),
            ReservedSales = GetProductInfoList(ProductCategory.ReservedSale, productGroups, compositions, probabilities),
        };
        
        return Ok(res);
    }
    
    private List<ProductInfo> GetProductInfoList(
        ProductCategory category,
        Dictionary<ProductCategory, List<Product>> productGroups,
        List<ProductComposition> compositions,
        List<CompositionProbability> probabilities)
    {
        return productGroups.TryGetValue(category, out var products)
            ? products.Select(product => new ProductInfo
            {
                Id = product.ProductId,
                Compositions = compositions.Where(pc => pc.ProductId == product.ProductId)
                    .Select(pc => new CompositionInfo
                    {
                        Id = pc.ProductId,
                        CompositionId = pc.CompositionId,
                        Type = pc.Type,
                        Count = pc.Count,
                        MinCount = pc is { Count: 0, Guaranteed: false } ? probabilities
                                .Where(cp => cp.ProductId == pc.ProductId && cp.CompositionId == pc.CompositionId)
                                .Min(cp => cp.Count) : 0,
                        MaxCount = pc is { Count: 0, Guaranteed: false } ? probabilities
                                .Where(cp => cp.ProductId == pc.ProductId && cp.CompositionId == pc.CompositionId)
                                .Max(cp => cp.Count) : 0,
                        Guaranteed = pc.Guaranteed,
                        IsSelectable = pc.IsSelectable,
                    }).ToList(),
                Price = product.Price,
                CurrencyType = product.Currency,
                Category = product.Category,
            }).ToList()
            : new List<ProductInfo>();
    }
}