using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using ApiServer.DB;
using ApiServer.Providers;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
// ReSharper disable InconsistentNaming

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenService _tokenService;
    private readonly UserService _userService;
    private readonly TokenValidator _tokenValidator;
    private readonly ProductClaimService _claimService;
    private readonly IDailyProductService _dailyProductService;
    private readonly CachedDataProvider _cachedDataProvider;
    private readonly ILogger<PaymentController> _logger;
    
    public PaymentController(
        AppDbContext context,
        TokenService tokenService,
        UserService userService,
        TokenValidator tokenValidator,
        ProductClaimService productClaimService,
        IDailyProductService dailyProductService,
        CachedDataProvider cachedDataProvider,
        ILogger<PaymentController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _userService = userService;
        _tokenValidator = tokenValidator;
        _claimService = productClaimService;
        _dailyProductService = dailyProductService;
        _cachedDataProvider = cachedDataProvider;
        _logger = logger;
    }

    [HttpPost]
    [Route("InitProducts")]
    public async Task<IActionResult> InitProducts([FromBody] InitProductPacketRequired required)
    {
        var userId = _tokenValidator.Authorize(required.AccessToken);
        if (userId == -1) return Unauthorized();

        var products = _cachedDataProvider.GetProducts();
        var productGroups = products
            .Where(product => product.IsFixed)
            .GroupBy(product => product.Category)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.ToList());
        var compositions = _cachedDataProvider.GetProductCompositions();
        var probabilities = _cachedDataProvider.GetProbabilities();
        
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dailyProductExists = await _context.UserDailyProduct.AsNoTracking()
            .AnyAsync(udp => udp.UserId == userId && udp.SeedDate == today);
        var userDailyProducts = await _context.UserDailyProduct.AsNoTracking()
            .Where(udp => udp.UserId == userId && udp.SeedDate == today)
            .OrderBy(udp => udp.Slot)
            .ToListAsync();
        
        if (dailyProductExists == false || userDailyProducts.Count == 0)
        {
            await _dailyProductService.CreateUserDailyProductSnapshotAsync(userId, today, 0);
            userDailyProducts = await _context.UserDailyProduct.AsNoTracking()
                .Where(udp => udp.UserId == userId && udp.SeedDate == today)
                .OrderBy(udp => udp.Slot)
                .ToListAsync();
        }
        
        // Get
        var dailyProductInfos = await _dailyProductService
            .GetDailyProductInfos(userId, products, compositions, probabilities);
            
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
            DailyProducts = dailyProductInfos,
            AdsRemover = GetProductInfoList(ProductCategory.Pass, productGroups, compositions, probabilities)
                .First(pi => pi.ProductCode == "com.hamon.crywolf.non-consumable.ads_remover"),
            RefreshTime = userDailyProducts.First().RefreshAt + TimeSpan.FromHours(6),
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

    [HttpPut]
    [Route("Purchase")]
    public async Task<IActionResult> Purchase([FromBody] VirtualPaymentPacketRequired required)
    {
        var userId = _tokenValidator.Authorize(required.AccessToken);
        if (userId == -1) return Unauthorized();
        
        var userStat = _context.UserStats.FirstOrDefault(u => u.UserId == userId);
        var product = _context.Product.AsNoTracking().FirstOrDefault(p => p.ProductCode == required.ProductCode);
        if (product == null || userStat == null) return BadRequest("Invalid product code");

        var response = new VirtualPaymentPacketResponse();
        var balance = product.Currency switch
        {
            CurrencyType.Gold => userStat.Gold,
            CurrencyType.Spinel => userStat.Spinel,
            _ => 0
        };
        
        if (balance < product.Price)
        {
            response.PaymentOk = false;
            return Ok(response);
        }

        switch (product.Currency)
        {
            case CurrencyType.Gold:
                userStat.Gold -= product.Price;
                break;
            case CurrencyType.Spinel:
                userStat.Spinel -= product.Price;
                break;
            default:
                return BadRequest("지원하지 않는 화폐입니다.");
        }

        await PurchaseComplete(userId, required.ProductCode);
        
        response.PaymentOk = true;
        return Ok(response);
    }

    [HttpPut]
    [Route("PurchaseDaily")]
    public async Task<IActionResult> PurchaseDaily([FromBody] DailyPaymentPacketRequired required)
    {
        var userId = _tokenValidator.Authorize(required.AccessToken);
        if (userId == -1) return Unauthorized();
        
        var userStat = _context.UserStats.FirstOrDefault(u => u.UserId == userId);
        var product = _context.Product.FirstOrDefault(p => p.ProductCode == required.ProductCode);
        var dailyProducts = _cachedDataProvider.GetDailyProductSnapshots();
        if (product == null || userStat == null) return BadRequest("Invalid product code");
        
        var response = new DailyPaymentPacketResponse();
        var balanceGold = userStat.Gold;
        var price = dailyProducts.First(dp => dp.ProductId == product.ProductId).Price;
        if (balanceGold < price) return Ok(response);
        
        userStat.Gold -= price;
        await PurchaseComplete(userId, required.ProductCode);
        
        response.PaymentOk = true;
        return Ok(response);
    }
    
    [HttpPut]
    [Route("PurchaseSpinel")]
    public async Task<IActionResult> PurchaseSpinel([FromBody] CashPaymentPacketRequired required)
    {
        var userId = _tokenValidator.Authorize(required.AccessToken);
        if (userId == -1) return Unauthorized();
        
        var isGoogleReceipt = required.Receipt.Contains("purchaseToken");
        var isAppleReceipt = required.Receipt.Contains("transaction_id");
        var res = new CashPaymentPacketResponse { PaymentOk = false };

        if (isGoogleReceipt)
        {
            var validationResult = await ValidateGoogleReceiptAsync(required.Receipt);
            if (validationResult.IsValid == false) return BadRequest(validationResult.Message);
        }
        else if (isAppleReceipt)
        {
            var validationResult = await ValidateAppleReceiptAsync(required.Receipt);
            if (validationResult.IsValid == false) return BadRequest(validationResult.Message);
        }

        // test
        res.PaymentOk = true;

        if (res.PaymentOk)
        {
            await PurchaseComplete(userId, required.ProductCode);
        }
        
        return Ok(res);
    }

    public async Task PurchaseComplete(int userId, string productCode)
    {
        var product = await _context.Product.FirstOrDefaultAsync(p => p.ProductCode == productCode);
        if (product == null) return;
        
        var mail = new Mail
        {
            UserId = userId,
            Type = MailType.Product,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            ProductId = product.ProductId,
            ProductCode = product.ProductCode,
            Claimed = false,
            Message = "Product Purchased",
            Sender = "Cry Wolf"
        };
        
        _context.Mail.Add(mail);
        await _context.SaveChangesExtendedAsync();
    }

    [HttpPut]
    [Route("SelectProduct")]
    public async Task<IActionResult> SelectProduct([FromBody] SelectProductPacketRequired required)
    {
        var userId = _tokenValidator.Authorize(required.AccessToken);
        if (userId == -1) return Unauthorized();
        var productCompositionStored = _cachedDataProvider.GetProductCompositions()
            .First(pc => 
                pc.CompositionId == required.SelectedCompositionInfo.CompositionId &&
                pc.ProductId == required.SelectedCompositionInfo.ProductId &&
                pc.ProductType == required.SelectedCompositionInfo.ProductType);
        
        _claimService.StoreProduct(userId, productCompositionStored);
        _claimService.AddDisplayingComposition(userId, required.SelectedCompositionInfo);
        _claimService.RemoveUserProduct(userId, required.SelectedCompositionInfo.ProductId);
        
        var data = await _claimService.ClassifyAndClaim(userId);
        await _context.SaveChangesExtendedAsync();
        
        var res = new ClaimProductPacketResponse
        {
            ProductInfos = data.ProductInfos,
            RandomProductInfos = data.RandomProductInfos,
            CompositionInfos = data.CompositionInfos,
            RewardPopupType = data.RewardPopupType,
        };

        if (data.CompositionInfos.Count == 0 && data.RandomProductInfos.Count == 0 && data.ProductInfos.Count == 0)
        {
            res.ClaimOk = false;
        }

        res.ClaimOk = true;

        return Ok(res);
    }

    [HttpPut]
    [Route("ClaimProduct")]
    public async Task<IActionResult> ClaimProduct([FromBody] ClaimProductPacketRequired required)
    {
        var userId = _tokenValidator.Authorize(required.AccessToken);
        if (userId == -1) return Unauthorized();

        List<Mail> mails;
        if (required.ClaimAll)
        {
            mails = await _context.Mail
                .Where(m => m.UserId == userId && m.Type == MailType.Product && m.Claimed == false)
                .ToListAsync();
        }
        // Claim individual product in mail
        else if (required.MailId != 0)
        {
            mails = await _context.Mail
                .Where(m => m.MailId == required.MailId && m.UserId == userId && m.Claimed == false)
                .ToListAsync();
        }
        else
        {
            return BadRequest();
        }
        
        await _claimService.UnpackPackages(userId, mails);
        var data = await _claimService.ClassifyAndClaim(userId);
        await _context.SaveChangesExtendedAsync();
        
        var res = new ClaimProductPacketResponse
        {
            ProductInfos = data.ProductInfos,
            RandomProductInfos = data.RandomProductInfos,
            CompositionInfos = data.CompositionInfos,
            RewardPopupType = data.RewardPopupType,
            ClaimOk = true
        };
        
        return Ok(res);
    }

    [HttpPut]
    [Route("DisplayClaimedProduct")]
    public async Task<IActionResult> DisplayClaimedProduct([FromBody] DisplayClaimedProductPacketRequired required)
    {
        var userId = _tokenValidator.Authorize(required.AccessToken);
        if (userId == -1) return Unauthorized();
        var data = await _claimService.ClassifyAndClaim(userId);
        await _context.SaveChangesExtendedAsync();
        
        if (_cachedDataProvider.DisplayingCompositions.TryRemove(userId, out var dispCompositions))
        {
            data.CompositionInfos = dispCompositions.Items.ToList();
            data.RewardPopupType = RewardPopupType.Item;
        }
        
        var res = new ClaimProductPacketResponse
        {
            ProductInfos = data.ProductInfos,
            RandomProductInfos = data.RandomProductInfos,
            CompositionInfos = data.CompositionInfos,
            RewardPopupType = data.RewardPopupType,
            ClaimOk = true
        };
        
        return Ok(res);
    }
    
    private async Task<(bool IsValid, string Message)> ValidateGoogleReceiptAsync(string receipt)
    {
        var receiptData = JsonConvert.DeserializeObject<GoogleReceiptData>(receipt);
        var packageName = receiptData.PackageName;
        var productId = receiptData.ProductId;
        var purchaseToken = receiptData.PurchaseToken;
        
        // Get Google API Access Token (OAuth2)
        var accessToken = await GetGoogleAccessTokenAsync();
        
        using var httpClient = new HttpClient();
        var url = $"https://www.googleapis.com/androidpublisher/v3/applications/{packageName}/purchases/products/{productId}/tokens/{purchaseToken}";
        
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await httpClient.GetAsync(url);

        if (response.IsSuccessStatusCode == false)
        {
            return (false, "Failed to validate receipt");
        }
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<GooglePurchaseValidationResult>(content);

        return result.PurchaseState == 0 
            ? (true, "Valid receipt") 
            : (false, "Invalid receipt");
    }

    private async Task<string> GetGoogleAccessTokenAsync()
    {
        var clientId = "";
        var clientSecret = "";

        using var httpClient = new HttpClient();
        var requestContent = new FormUrlEncodedContent(new []
        {
            new KeyValuePair<string?, string?>("client_id", clientId),
            new KeyValuePair<string?, string?>("client_secret", clientSecret),
            new KeyValuePair<string?, string?>("grant_type", "client_credentials"),
        });
        
        var response = await httpClient.PostAsync("https://accounts.google.com/o/oauth2/token", requestContent);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<GoogleTokenResponse>(content);

        return result.AccessToken;
    }
    
    private async Task<(bool IsValid, string Message)> ValidateAppleReceiptAsync(string receipt)
    {
        var requestData = new
        {
            receipt_data = receipt,
            password = "password"
        };
        
        using var httpClient = new HttpClient();
        var response = await httpClient.PostAsync("https://sandbox.itunes.apple.com/verifyReceipt",
            new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json"));
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<AppleReceiptValidationResult>(content);
        
        return result.Status == 0 
            ? (true, "Valid receipt") 
            : (false, "Invalid receipt");
    }
}

public struct GoogleTokenResponse
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; }

    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonProperty("token_type")]
    public string TokenType { get; set; }
}

public struct GoogleReceiptData
{
    public string PackageName { get; set; }
    public string ProductId { get; set; }
    public string PurchaseToken { get; set; }
}

public struct GooglePurchaseValidationResult
{
    public int PurchaseState { get; set; }
}

public struct AppleReceiptValidationResult
{
    public int Status { get; set; }
    public AppleReceipt Receipt { get; set; }
}

public struct AppleReceipt
{
    public string bundle_id { get; set; }
    public string product_id { get; set; }
}