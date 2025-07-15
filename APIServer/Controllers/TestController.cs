using System.Text;
using ApiServer.DB;
using ApiServer.Providers;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TestController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ApiService _apiService;
    private readonly ProductClaimService _claimService;
    private readonly CachedDataProvider _cachedDataProvider;
    private readonly ILogger<TestController> _logger;
    
    public TestController(
        AppDbContext context,
        ApiService apiService, 
        ProductClaimService claimService, 
        CachedDataProvider cachedDataProvider,
        ILogger<TestController> logger)
    {
        _context = context;
        _apiService = apiService;
        _claimService = claimService;
        _cachedDataProvider = cachedDataProvider;
        _logger = logger;
    }
    
    [HttpPost]
    [Route("Test")]
    public IActionResult Test([FromBody] TestRequired required)
    {
        var unit = _context.Unit.FirstOrDefault(unit => unit.UnitId == (UnitId)required.UnitId);
        var res = new TestResponse();

        if (unit != null)
        {
            res.TestOk = true;
            res.UnitName = unit.UnitId.ToString();
        }
        else
        {
            res.TestOk = false;
        }
        
        return Ok(res);
    }
    
    [HttpPost]
    [Route("TestServers")]
    public async Task<IActionResult> TestServers([FromBody] ServerTestRequired required)
    {
        var apiToMatch = new TestApiToMatchRequired { Test = required.Test };
        var apiToSocket = new TestApiToSocketRequired { Test = required.Test };
        var taskMatch = _apiService
            .SendRequestAsync<TestApiToMatchResponse>("MatchMaking/Test", apiToMatch, HttpMethod.Post);
        var taskSocket = _apiService
            .SendRequestToSocketAsync<TestApiToSocketResponse>("test", apiToSocket, HttpMethod.Post);
        
        await Task.WhenAll(taskMatch, taskSocket);
        
        if (taskMatch.Result == null || taskSocket.Result == null)
        {
            return BadRequest();
        }

        var res = new ServerTestResponse
        {
            MatchTestOk = taskMatch.Result.TestOk,
            SocketTestOk = taskSocket.Result.TestOk
        };
        
        return Ok(res);
    }
    
    [HttpPost]
    [Route("Test_InitProducts")]
    public IActionResult InitProducts([FromBody] TestInitProductPacketRequired required)
    {
        var products = _cachedDataProvider.GetProducts();
        var productGroups = products
            .Where(product => product.IsFixed)
            .GroupBy(product => product.Category)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.ToList());
        var compositions = _cachedDataProvider.GetProductCompositions();
        var probabilities = _cachedDataProvider.GetProbabilities();
            
        var res = new InitProductPacketResponse
        {
            GetProductOk = true,
            SpecialPackages = GetProductInfoList(ProductCategory.SpecialPackage, productGroups, compositions, probabilities),
            BeginnerPackages = GetProductInfoList(ProductCategory.BeginnerPackage, productGroups, compositions, probabilities),
            GoldPackages = GetProductInfoList(ProductCategory.GoldStore, productGroups, compositions, probabilities),
            SpinelPackages = GetProductInfoList(ProductCategory.SpinelStore, productGroups, compositions, probabilities),
            GoldItems = GetProductInfoList(ProductCategory.GoldPackage, productGroups, compositions, probabilities),
            SpinelItems = GetProductInfoList(ProductCategory.SpinelPackage, productGroups, compositions, probabilities),
            ReservedSales = GetProductInfoList(ProductCategory.ReservedSale, productGroups, compositions, probabilities),
            AdsRemover = GetProductInfoList(ProductCategory.Pass, productGroups, compositions, probabilities)
                .First(pi => pi.ProductCode == "com.hamon.crywolf.non-consumable.ads_remover"),
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
                ProductId = product.ProductId,
                Compositions = compositions.Where(pc => pc.ProductId == product.ProductId)
                    .Select(pc => new CompositionInfo
                    {
                        ProductId = pc.ProductId,
                        CompositionId = pc.CompositionId,
                        ProductType = pc.ProductType,
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
                ProductType = product.ProductType,
                Category = product.Category,
                ProductCode = product.ProductCode
            }).ToList()
            : new List<ProductInfo>();
    }
    
    [HttpPost]
    [Route("Test_MapProductInfo")]
    public IActionResult Test_MapProductInfo([FromBody] TestMapProductInfoPacketRequired required)
    {
        var res = new TestMapProductInfoPacketResponse();
        var product = _context.Product.FirstOrDefault(p => p.ProductId == required.ProductId);

        if (product == null)
        {
            return NotFound();
        }

        res.ProductInfo = _claimService.MapProductInfo(new UserProduct
        {
            UserId = 1,
            ProductId = required.ProductId,
            ProductType = required.ProductType,
            AcquisitionPath = AcquisitionPath.None,
            Count = 1,
        });
        
        return Ok(res);
    }

    [HttpPost]
    [Route("Test_GetFinalProducts")]
    public IActionResult Test_GetFinalProducts([FromBody] TestClassifyProductPacketRequired required)
    {
        var res = new TestClassifyProductPacketResponse();
        var sb  = new StringBuilder();
        
        var ids = string.Join("," , required.ProductIds);
        sb.AppendLine($"┌─ ProductId──┐");
        var list = new List<ProductComposition>();
        foreach (var productId in required.ProductIds)
        {
            list.AddRange(_claimService.GetFinalProducts(productId));
        }
        
        foreach (var composition in list)
        {
            sb.AppendLine($"│   {composition.ProductId,-6} : {composition.CompositionId}");
        }
        
        sb.AppendLine("└──────────────────────────");
        
        _logger.LogInformation(sb.ToString());
        
        return Ok(res);
    }
    
    [HttpPost]
    [Route("Test_ClassifyProduct")]
    public IActionResult Test_ClassifyProduct([FromBody] TestClassifyProductPacketRequired required)
    {
        var res = new TestClassifyProductPacketResponse
        {
            Compositions = new List<ProductComposition>()
        };
        
        var dict = _claimService.ClassifyProducts(1, required.ProductIds.ToArray());
        foreach (var value in dict.Values)
        {
            res.Compositions.AddRange(value);
        }

        return Ok(res);
    }

    [HttpPost]
    [Route("Test_DrawRandomProduct")]
    public IActionResult Test_DrawRandomProduct([FromBody] TestDrawRandomProductPacketRequired required)
    {
        var res = new TestDrawRandomProductPacketResponse
        {
            CompositionInfos = new List<CompositionInfo>()
        };

        var allProductList = _cachedDataProvider.GetProducts();
        // var allCompositionList = _cachedDataProvider.GetProductCompositions();
        foreach (var productId in required.ProductIds)
        {
            var product = allProductList.FirstOrDefault(p => p.ProductId == productId);
            if (product != null)
            {
                var compositionInfoList = _claimService.DrawRandomProduct(product)
                    .Select(_claimService.MapCompositionInfo)
                    .ToList();
                res.CompositionInfos.AddRange(compositionInfoList);
            }
        }
        return Ok(res);
    }

    [HttpPost]
    [Route("Test_ClaimAllProduct")]
    public IActionResult Test_ClaimProduct([FromBody] TestClaimAllPacketRequired required)
    {
        return Ok(new ClaimProductPacketResponse());
    }
}