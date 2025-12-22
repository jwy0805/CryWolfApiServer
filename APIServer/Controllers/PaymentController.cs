using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using ApiServer.DB;
using ApiServer.Models;
using ApiServer.Providers;
using ApiServer.Services;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
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
    private readonly IConfiguration _config;
    
    public PaymentController(
        AppDbContext context,
        TokenService tokenService,
        UserService userService,
        TokenValidator tokenValidator,
        ProductClaimService productClaimService,
        IDailyProductService dailyProductService,
        CachedDataProvider cachedDataProvider,
        ILogger<PaymentController> logger,
        IConfiguration configuration)
    {
        _context = context;
        _tokenService = tokenService;
        _userService = userService;
        _tokenValidator = tokenValidator;
        _claimService = productClaimService;
        _dailyProductService = dailyProductService;
        _cachedDataProvider = cachedDataProvider;
        _logger = logger;
        _config = configuration;
    }

    public record ValidationResult(
        bool IsValid,
        string? Message = null,
        int? HttpStatusCode = null,
        string? ErrorCode = null,
        string? RawResponse = null)
    {
        public static ValidationResult Ok() => new(true);
        public static ValidationResult Fail(string message, int? http = null, string? code = null, string? raw = null) 
            => new(false, message, http, code, raw);
    }

    public bool IsUniqueViolationOnStoreKey(DbUpdateException exception)
    {
        if (exception.InnerException is MySqlConnector.MySqlException mysql)
        {
            return mysql.Number == 1062;
        }

        return false;
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
        var dailyProductInfos = await _dailyProductService.GetDailyProductInfos(userId);
            
        var res = new InitProductPacketResponse
        {
            GetProductOk = true,
            SpecialPackages = GetProductInfoList(ProductCategory.SpecialPackage, productGroups),
            BeginnerPackages = GetProductInfoList(ProductCategory.BeginnerPackage, productGroups),
            GoldPackages = GetProductInfoList(ProductCategory.GoldStore, productGroups),
            SpinelPackages = GetProductInfoList(ProductCategory.SpinelStore, productGroups),
            GoldItems = GetProductInfoList(ProductCategory.GoldPackage, productGroups),
            SpinelItems = GetProductInfoList(ProductCategory.SpinelPackage, productGroups),
            ReservedSales = GetProductInfoList(ProductCategory.ReservedSale, productGroups),
            DailyProducts = dailyProductInfos,
            AdsRemover = GetProductInfoList(ProductCategory.Pass, productGroups)
                .First(pi => pi.ProductCode == "com.hamon.crywolf.non-consumable.ads_remover"),
            RefreshTime = userDailyProducts.First().RefreshAt + TimeSpan.FromHours(6),
        };
        
        return Ok(res);
    }
    
    private List<ProductInfo> GetProductInfoList(
        ProductCategory category,
        Dictionary<ProductCategory, List<Product>> productGroups)
    {
        return productGroups.TryGetValue(category, out var products)
            ? products.Select(product => new ProductInfo
            {
                ProductId = product.ProductId,
                Compositions = _cachedDataProvider.GetProductCompositions()
                    .Where(pc => pc.ProductId == product.ProductId)
                    .Select(pc => new CompositionInfo
                    {
                        ProductId = pc.ProductId,
                        CompositionId = pc.CompositionId,
                        ProductType = pc.ProductType,
                        Count = pc.Count,
                        MinCount = pc is { Count: 0, Guaranteed: false } 
                            ? _cachedDataProvider.GetProbabilities()
                                .Where(cp => cp.ProductId == pc.ProductId && cp.CompositionId == pc.CompositionId)
                                .Min(cp => cp.Count) 
                            : 0,
                        MaxCount = pc is { Count: 0, Guaranteed: false } 
                            ? _cachedDataProvider.GetProbabilities()
                                .Where(cp => cp.ProductId == pc.ProductId && cp.CompositionId == pc.CompositionId)
                                .Max(cp => cp.Count) 
                            : 0,
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

        var response = new VirtualPaymentPacketResponse();
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var userStat = _context.UserStats.FirstOrDefault(u => u.UserId == userId);
            var product = _context.Product.AsNoTracking().FirstOrDefault(p => p.ProductCode == required.ProductCode);
            if (product == null || userStat == null) return;
        
            var balance = product.Currency switch
            {
                CurrencyType.Gold => userStat.Gold,
                CurrencyType.Spinel => userStat.Spinel,
                _ => 0
            };
    
            if (balance < product.Price)
            {
                response.PaymentOk = false;
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
                    return;
            }

            await PurchaseComplete(userId, required.ProductCode);
            await _context.SaveChangesExtendedAsync();
            await transaction.CommitAsync();
    
            response.PaymentOk = true;
            response.PaymentCode = product.ProductType == ProductType.Subscription 
                ? VirtualPaymentCode.Subscription 
                : VirtualPaymentCode.Product;
        });

        return Ok(response);
    }

    [HttpPut]
    [Route("PurchaseDaily")]
    public async Task<IActionResult> PurchaseDaily([FromBody] DailyPaymentPacketRequired required)
    {
        var userId = _tokenValidator.Authorize(required.AccessToken);
        if (userId == -1) return Unauthorized();
        
        var response = new DailyPaymentPacketResponse();
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var userStat = _context.UserStats.FirstOrDefault(u => u.UserId == userId);
            var product = _context.Product.FirstOrDefault(p => p.ProductCode == required.ProductCode);
            var dailyProducts = _cachedDataProvider.GetDailyProductSnapshots();
            if (product == null || userStat == null) return;
    
            var balanceGold = userStat.Gold;
            var price = dailyProducts.First(dp => dp.ProductId == product.ProductId).Price;
            if (balanceGold < price) response.PaymentOk = false;
    
            var userDaily = _context.UserDailyProduct.FirstOrDefault(udp =>
                udp.UserId == userId && udp.ProductId == product.ProductId);
            if (userDaily == null) return;
            userDaily.Bought = true;
            userStat.Gold -= price;

            await PurchaseComplete(userId, required.ProductCode);
            await _context.SaveChangesExtendedAsync();
            await transaction.CommitAsync();
    
            response.PaymentOk = true;
            response.Slot = userDaily.Slot;
        });
        
        return Ok(response);
    }
    
    [HttpPut]
    [Route("PurchaseSpinel")]
    public async Task<IActionResult> PurchaseSpinel([FromBody] CashPaymentPacketRequired required)
    {
        var res = new CashPaymentPacketResponse
        {
            PaymentOk = false,
            ErrorCode = CashPaymentErrorCode.InternalError,
        };

        var receiptRaw = required.Receipt ?? string.Empty;

        try
        {
            var userId = _tokenValidator.Authorize(required.AccessToken);
            if (userId == -1)
            {
                res.ErrorCode = CashPaymentErrorCode.Unauthorized;
                return Ok(res);
            }
            
            // Store/TransactionId 추출
            var (storeType, storeTxId) = ExtractStoreInfo(receiptRaw);
            if (storeType == StoreType.None || string.IsNullOrEmpty(storeTxId))
            {
                res.ErrorCode = CashPaymentErrorCode.InvalidReceipt;
                return Ok(res);
            }

            var product = _cachedDataProvider.GetProducts()
                .Where(p => p.ProductCode == required.ProductCode)
                .Select(p => new { p.ProductId })
                .FirstOrDefault();

            if (product == null)
            {
                res.ErrorCode = CashPaymentErrorCode.InvalidReceipt;
                return Ok(res);
            }
            
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var dbTx = await _context.Database.BeginTransactionAsync();

                // Transaction 만들어서 TransactionId 확보
                var transaction = new Transaction(
                    userId: userId,
                    productId: product.ProductId,
                    count: 1,
                    currency: CurrencyType.Cash,
                    cashCurrency: CashCurrencyType.KRW,
                    storeType: storeType,
                    storeTransactionId: storeTxId
                );

                _context.Transaction.Add(transaction);

                try
                {
                    await _context.SaveChangesExtendedAsync();
                }
                catch (DbUpdateException dbEx)
                {
                    await dbTx.RollbackAsync();

                    // 이미 같은 StoreTxId가 존재 => 중복 결제 요청/재시도
                    res.PaymentOk = true;
                    res.ErrorCode = CashPaymentErrorCode.AlreadyProcessed;
                    return Ok(res);
                }

                (bool IsValid, string Message, int? HttpStatusCode, string? RawResponse) validationResult =
                    storeType switch
                    {
                        StoreType.GooglePlay => await ValidateGoogleReceiptAsync(required.ProductCode, receiptRaw),
                        StoreType.AppStore => await ValidateAppleReceiptAsync(required.ProductCode, receiptRaw),
                        _ => (false, "Unsupported store type", null, null)
                    };

                if (!validationResult.IsValid)
                {
                    // 실패: Transaction=Failed + Failure 테이블에만 원문 저장
                    transaction.Status = TransactionStatus.Failed;

                    _context.TransactionReceiptFailure.Add(new TransactionReceiptFailure
                    {
                        TransactionId = transaction.TransactionId,
                        CreatedAt = DateTime.UtcNow,
                        HttpStatusCode = validationResult.HttpStatusCode,
                        ErrorMessage = Util.Util.Truncate(validationResult.Message, 1024),
                        ReceiptHash = Util.Util.Sha256Utf8(receiptRaw),
                        ReceiptRawGzip = Util.Util.GzipUtf8(receiptRaw),
                        ResponseRawGzip = string.IsNullOrEmpty(validationResult.RawResponse)
                            ? null
                            : Util.Util.GzipUtf8(validationResult.RawResponse),
                    });

                    await _context.SaveChangesExtendedAsync();
                    await dbTx.CommitAsync();

                    res.PaymentOk = false;
                    res.ErrorCode = CashPaymentErrorCode.InvalidReceipt;
                    return Ok(res);
                }

                // 3) 지급 처리
                // 중요: PurchaseComplete가 내부에서 SaveChanges를 호출하더라도,
                // 지금은 같은 DbContext + 같은 DB transaction 범위라 원자적으로 묶임.
                await PurchaseComplete(userId, required.ProductCode);

                // 4) 완료 마킹
                transaction.Status = TransactionStatus.Completed;
                await _context.SaveChangesExtendedAsync();

                await dbTx.CommitAsync();

                res.PaymentOk = true;
                res.ErrorCode = CashPaymentErrorCode.None;
                return Ok(res);
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "PurchaseSpinel error");
            res.PaymentOk = false;
            res.ErrorCode = CashPaymentErrorCode.InternalError;
            return Ok(res);
        }
    }

    private (StoreType storeType, string storeTransactionId) ExtractStoreInfo(string receiptJson)
    {
        if (string.IsNullOrWhiteSpace(receiptJson)) return (StoreType.None, string.Empty);
        
        UnityIapReceiptWrapper? wrapper;
        try
        {
            wrapper = JsonConvert.DeserializeObject<UnityIapReceiptWrapper>(receiptJson);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to deserialize receipt JSON");;
            return (StoreType.None, string.Empty);
        }
        
        if (wrapper == null) return (StoreType.None, string.Empty);
        return wrapper.Store switch
        {
            "GooglePlay" => (StoreType.GooglePlay, wrapper.TransactionID),
            "AppleAppStore" => (StoreType.AppStore, wrapper.TransactionID),
            _ => (StoreType.None, string.Empty)
        };
    }

    public async Task PurchaseComplete(int userId, string productCode)
    {
        var product = await _context.Product.AsNoTracking().
            FirstOrDefaultAsync(p => p.ProductCode == productCode);
        if (product == null) return;

        if (product.ProductType is
            ProductType.Gold or ProductType.Spinel or ProductType.Exp or ProductType.Subscription)
        {
            var composition = _cachedDataProvider.GetProductCompositions()
                .FirstOrDefault(pc => pc.ProductId == product.ProductId);
            if (composition == null) return;
        
            await _claimService.StoreProductAsync(userId, composition);   
        }
        else
        {
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
        }
    }

    [HttpPut]
    [Route("StartClaim")]
    public async Task<IActionResult> StartClaim([FromBody] ContinueClaimPacketRequired required)
    {
        var userId = _tokenValidator.Authorize(required.AccessToken);
        if (userId == -1) return Unauthorized();

        // 0) 메일에서 아직 클레임 안 한 Product 메일들 읽기
        List<Mail> mails;
        if (required.MailId != 0)
        {
            var mail = await _context.Mail.FirstOrDefaultAsync(m =>
                m.MailId == required.MailId && m.UserId == userId && !m.Claimed);
            
            if (mail == null)
            {
                return BadRequest("Invalid mail ID");
            }
            
            mails = new List<Mail>
            {
                mail
            };
        }
        else
        {
            mails = await _context.Mail
                .Where(m => m.UserId == userId &&
                            m.Claimed == false &&
                            m.Type == MailType.Product &&
                            m.ProductId != null)
                .ToListAsync();
        }

        // 1) 있으면 언팩해서 User_Product(Open/None)에 스테이징
        if (mails.Count > 0)
            await _claimService.UnpackPackages(userId, mails);

        // 2) 다음 팝업 리턴
        var data = await _claimService.GetNextPopupAsync(userId);

        return Ok(new ClaimProductPacketResponse
        {
            ClaimOk = data.RewardPopupType != RewardPopupType.None,
            RewardPopupType = data.RewardPopupType,
            ProductInfos = data.ProductInfos,
            RandomProductInfos = data.RandomProductInfos,
            CompositionInfos = data.CompositionInfos
        });
    }
    
    [HttpPut]
    [Route("ContinueClaim")]
    public async Task<IActionResult> ContinueClaim([FromBody] ContinueClaimPacketRequired required)
    {
        var userId = _tokenValidator.Authorize(required.AccessToken);
        if (userId == -1) return Unauthorized();

        var data = await _claimService.GetNextPopupAsync(userId);

        return Ok(new ClaimProductPacketResponse
        {
            ClaimOk = data.RewardPopupType != RewardPopupType.None,
            RewardPopupType = data.RewardPopupType,
            ProductInfos = data.ProductInfos,
            RandomProductInfos = data.RandomProductInfos,
            CompositionInfos = data.CompositionInfos
        });
    }
    
    [HttpPut]
    [Route("SelectProduct")]
    public async Task<IActionResult> SelectProduct([FromBody] SelectProductPacketRequired required)
    {
        var userId = _tokenValidator.Authorize(required.AccessToken);
        if (userId == -1) return Unauthorized();

        var strategy = _context.Database.CreateExecutionStrategy();

        string? bad = null;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            var userProduct = await _context.UserProduct.FirstOrDefaultAsync(up =>
                up.UserId == userId &&
                up.ProductId == required.SelectedCompositionInfo.ProductId &&
                up.AcquisitionPath == AcquisitionPath.Open);

            if (userProduct is not { Count: > 0 })
            {
                bad = "Selected product not found";
                await tx.RollbackAsync();
                return;
            }

            var selected = _cachedDataProvider.GetProductCompositions().FirstOrDefault(pc =>
                pc.ProductId == required.SelectedCompositionInfo.ProductId &&
                pc.CompositionId == required.SelectedCompositionInfo.CompositionId &&
                pc.ProductType == required.SelectedCompositionInfo.ProductType &&
                pc.IsSelectable);

            if (selected == null)
            {
                bad = "Selected composition is not selectable";
                await tx.RollbackAsync();
                return;
            }

            await _claimService.StoreProductAsync(userId, selected);

            _claimService.AddDisplayingComposition(userId, required.SelectedCompositionInfo);

            userProduct.Count -= 1;
            if (userProduct.Count <= 0)
                _context.UserProduct.Remove(userProduct);

            await _context.SaveChangesExtendedAsync();
            await tx.CommitAsync();
        });

        if (bad != null) return BadRequest(bad);

        // 커밋 이후 “다음 팝업”은 재시도 단위 밖에서 1회만
        var data = await _claimService.GetNextPopupAsync(userId);

        var res = new ClaimProductPacketResponse
        {
            ClaimOk = data.RewardPopupType != RewardPopupType.None,
            ProductInfos = data.ProductInfos,
            RandomProductInfos = data.RandomProductInfos,
            CompositionInfos = data.CompositionInfos,
            RewardPopupType = data.RewardPopupType
        };

        return Ok(res);
    }

    [HttpPut]
    [Route("OpenProduct")]
    public async Task<IActionResult> OpenProduct([FromBody] OpenProductPacketRequired required)
    {
        var userId = _tokenValidator.Authorize(required.AccessToken);
        if (userId == -1) return Unauthorized();

        var strategy = _context.Database.CreateExecutionStrategy();

        int resCount = 0;
        string? bad = null;
        List<CompositionInfo> openResultInfos = new(); // 커밋 성공한 최종 결과만 담김
        List<CompositionInfo> closedResultInfos = new();

        await _context.Database.OpenConnectionAsync();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                bad = null;
                openResultInfos.Clear();
                closedResultInfos.Clear();

                await using var tx = await _context.Database.BeginTransactionAsync();
                var dbTx = tx.GetDbTransaction();

                // 0) 상품 유효성(캐시)
                var product = _cachedDataProvider.GetProducts().FirstOrDefault(p => p.ProductId == required.ProductId);
                if (product == null)
                {
                    bad = "Invalid product";
                    return;
                }

                // 1) 먼저 소비(원자) — 동시 클릭/중복 요청 방지
                var consume = await _claimService.ConsumeUserProductAsync(
                    userId,
                    required.ProductId,
                    AcquisitionPath.Open,
                    consumeAll: required.OpenAll,
                    dbTx);

                if (consume.OpenCount <= 0)
                {
                    bad = "Product not found or already opened";
                    return;
                }
                
                // 2) 1레벨 확정(재귀 금지)
                var resolved = _claimService.ResolveRandomOpenOneLevel(required.ProductId, consume.OpenCount);

                foreach (var ((compId, type), cnt) in resolved.Resolved)
                {
                    var pc = new ProductComposition
                    {
                        ProductId = required.ProductId,
                        CompositionId = compId,
                        ProductType = type,
                        Count = cnt
                    };

                    var info = _claimService.MapCompositionInfo(pc);
                    openResultInfos.Add(info);

                    if (type == ProductType.Container)
                    {
                        for (int i = 0; i < cnt; i++)
                        {
                            _context.Mail.Add(new Mail
                            {
                                UserId = userId,
                                Type = MailType.Product,
                                ProductId = compId,
                                Claimed = false
                            });
                        }
                    }
                    else
                    {
                        await _claimService.StoreProductAsync(userId, pc);
                    }
                }

                await _context.SaveChangesExtendedAsync();
                await tx.CommitAsync();
                resCount = consume.OpenCount;
            });
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        if (bad != null) return BadRequest(bad);
        if (openResultInfos.Count == 0) return StatusCode(500, "OpenProduct failed");

        // 커밋 이후 1회만 누적 로그 반영 (재시도/롤백 중복 방지)
        var allInfos = openResultInfos.Concat(closedResultInfos).ToList();
        foreach (var info in allInfos)
            _claimService.AddDisplayingComposition(userId, info);

        var res = new ClaimProductPacketResponse
        {
            ClaimOk = true,
            RewardPopupType = RewardPopupType.OpenResult,
            CompositionInfos = openResultInfos,
            ProductInfos = new List<ProductInfo>(),
            RandomProductInfos = new List<RandomProductInfo>
            {
                new() { ProductInfo = new ProductInfo { ProductId = required.ProductId, }, Count = resCount, }
            }
        };
        
        return Ok(res);
    }
        
    private async Task<(bool IsValid, string Message, int? HttpStatusCode, string? RawResponse)>
        ValidateGoogleReceiptAsync(string productCode, string receipt)
    {
        UnityIapReceiptWrapper? wrapper;

        try
        {
            wrapper = JsonConvert.DeserializeObject<UnityIapReceiptWrapper>(receipt);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to deserialize Unity IAP wrapper for Google receipt");
            return (false, "Invalid receipt format", null, null);
        }

        if (wrapper == null || wrapper.Store != "GooglePlay")
            return (false, "Not a GooglePlay receipt", null, null);

        if (string.IsNullOrWhiteSpace(wrapper.Payload))
            return (false, "Google receipt payload is empty", null, null);

        GooglePlayPayload? payload;
        GooglePlayPurchaseData? purchaseData;

        try
        {
            payload = JsonConvert.DeserializeObject<GooglePlayPayload>(wrapper.Payload);
            purchaseData = payload != null ? JsonConvert.DeserializeObject<GooglePlayPurchaseData>(payload.Json) : null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to deserialize Google payload");
            return (false, "Invalid Google payload", null, null);
        }

        if (purchaseData == null) return (false, "Google receipt payload is empty", null, null);
        
        var expectedPackageName = _config["BundleId"];
        if (string.IsNullOrWhiteSpace(expectedPackageName))
        {
            throw new InvalidOperationException("Google:PackageName is not configured.");    
        }
        
        // Validate Package Name
        if (!string.Equals(purchaseData.PackageName, expectedPackageName, StringComparison.Ordinal))
        {
            _logger.LogWarning("Google receipt package name mismatch. Expected: {Expected}, Actual: {Actual}",
                expectedPackageName, purchaseData.PackageName);
            return (false, "Google receipt package name mismatch", null, null);
        }
        
        // Validate Product ID
        if (!string.Equals(purchaseData.ProductId, productCode, StringComparison.Ordinal))
        {
            _logger.LogWarning("Google receipt product ID mismatch. Expected: {Expected}, Actual: {Actual}",
                productCode, purchaseData.ProductId);
            return (false, "Google receipt product ID mismatch", null, null);
        }
        
        // Get Google API Access Token (OAuth2)
        var accessToken = await GetGoogleAccessTokenAsync();
        var url = $"https://www.googleapis.com/androidpublisher/v3/applications/{purchaseData.PackageName}/purchases/products/{purchaseData.ProductId}/tokens/{purchaseData.PurchaseToken}";
        
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        
        var response = await httpClient.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google purchase validation failed. StatusCode: {Status}, Body: {Body}",
                response.StatusCode, body);

            return (false, "Failed to validate receipt", (int)response.StatusCode, body);
        }        
        
        GoogleProductPurchase? productPurchase;
        try
        {
            productPurchase = JsonConvert.DeserializeObject<GoogleProductPurchase>(body);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to deserialize Google ProductPurchase");
            return (false, "Failed to parse Google validation response", 200, body);
        }

        if (productPurchase == null)
            return (false, "Empty Google ProductPurchase response", 200, body);

        if (productPurchase.PurchaseState != 0)
            return (false, "Invalid Google purchase state", 200, body);

        return (true, "Valid Google receipt", 200, null);
    }

    private async Task<string> GetGoogleAccessTokenAsync()
    {
        var path = _config["Google:ServiceAccountJsonPath"];
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            throw new Exception("Google Service Account not found");

        var json = await System.IO.File.ReadAllTextAsync(path);

        var credential = GoogleCredential
            .FromJson(json)
            .CreateScoped("https://www.googleapis.com/auth/androidpublisher");

        return await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
    }
    
    private async Task<(bool IsValid, string Message, int? HttpStatusCode, string? RawResponse)>
        ValidateAppleReceiptAsync(string productCode, string receipt)
    {
        // parse Unity IAP Receipt Wrapper
        UnityIapReceiptWrapper? wrapper;
        try
        {   
            wrapper = JsonConvert.DeserializeObject<UnityIapReceiptWrapper>(receipt);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to deserialize Apple receipt JSON");
            return (false, $"Failed to deserialize Apple receipt JSON", null, null);
        }

        if (wrapper == null || string.IsNullOrEmpty(wrapper.Payload))
        {
            _logger.LogWarning("Invalid Apple receipt: missing payload");
            return (false, "Invalid Apple receipt: missing payload", null, null);
        }

        if (string.IsNullOrWhiteSpace(wrapper.TransactionID))
        {
            return (false, "Apple transactionId is empty", null, null);
        }

        var jwt = CreateAppStoreJwt();
        
        // 1st call to production
        var prod = 
            await CallAppStoreTransactionEndpointAsync(wrapper.TransactionID, jwt, false);
        AppleTransactionResponse? tx = null;
        var env = "Production";

        if (prod.IsSuccess && prod.Payload != null)
        {
            tx = prod.Payload;
        }
        else if (prod.ShouldRetrySandbox)
        {
            // retry sandbox
            var sandbox = await CallAppStoreTransactionEndpointAsync(wrapper.TransactionID, jwt, sandbox: true);
            env = "Sandbox";

            if (!sandbox.IsSuccess || sandbox.Payload == null)
            {
                _logger.LogWarning("Apple sandbox validation failed. Status={Status} Raw={Raw}",
                    sandbox.HttpStatusCode, sandbox.Raw);

                return (false,
                    "Failed to validate Apple receipt (sandbox)",
                    sandbox.HttpStatusCode,
                    sandbox.Raw);
            }

            tx = sandbox.Payload;
        }
        else
        {
            _logger.LogWarning("Apple production validation failed. Status={Status} Raw={Raw}",
                prod.HttpStatusCode, prod.Raw);

            return (false,
                "Failed to validate Apple receipt (production)",
                prod.HttpStatusCode,
                prod.Raw);
        }
        
        if (tx == null)
        {
            return (false, "Apple transaction data is null", prod.HttpStatusCode, prod.Raw);
        }
        
        var expectedBundleId = _config["BundleId"];
        if (!string.Equals(tx.BundleId, expectedBundleId, StringComparison.Ordinal))
        {
            _logger.LogWarning("Apple receipt bundle ID mismatch. Expected: {Expected}, Actual: {Actual}",
                expectedBundleId, tx.BundleId);

            return (false, "Apple receipt bundle ID mismatch", 200, null);
        }
        
        if (!string.Equals(tx.ProductId, productCode, StringComparison.Ordinal))
        {
            _logger.LogWarning("Apple receipt product ID mismatch. Expected: {Expected}, Actual: {Actual}",
                productCode, tx.ProductId);

            return (false, "Apple receipt product ID mismatch", 200, null);
        }

        if (tx.RevocationReason.HasValue)
        {
            _logger.LogWarning("Apple transaction revoked. Reason={Reason}, Env={Env}, TxId={TxId}",
                tx.RevocationReason.Value, env, tx.TransactionId);

            return (false, "Revoked transaction", 200, null);
        }

        return (true, $"Valid Apple receipt ({env})", 200, null);
    }

    private async Task<(bool IsSuccess, bool ShouldRetrySandbox, AppleTransactionResponse? Payload, int? HttpStatusCode, string Raw)>
        CallAppStoreTransactionEndpointAsync(string transactionId, string jwt, bool sandbox)
    {
        var baseUrl = sandbox
            ? "https://api.storekit-sandbox.itunes.apple.com"
            : "https://api.storekit.itunes.apple.com";

        var url = $"{baseUrl}/inApps/v1/transactions/{transactionId}";

        using var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        HttpResponseMessage response;
        string body;
        
        try
        {
            response = await httpClient.SendAsync(request);
            body = await response.Content.ReadAsStringAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "App Store Server API call exception. Sandbox={Sandbox}", sandbox);
            return (false, false, null, null, string.Empty);
        }

        var status = (int)response.StatusCode;
        if (response.IsSuccessStatusCode)
        {
            try
            {
                var tx = JsonConvert.DeserializeObject<AppleTransactionResponse>(body);
                return tx == null ? (false, false, null, status, body) : (true, false, tx, status, body);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to deserialize AppleTransactionResponse. Body: {Body}", body);
                return (false, false, null, status, body);
            }
        }

        if (!sandbox && response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            try
            {
                var error = JsonConvert.DeserializeObject<AppleErrorResponse>(body);
                if (error != null && error.ErrorCode == 4040010) // TransactionIdNotFoundError
                {
                    // sandbox로 재시도
                    return (false, true, null, status, body);
                }
            }
            catch (Exception e)
            {
                // ignored
            }
        }
        
        _logger.LogWarning(
            "App Store Server API call failed. Sandbox={Sandbox}, StatusCode={Status}, Body={Body}",
            sandbox, response.StatusCode, body);

        return (false, false, null, status, body);
    }
    
    private string CreateAppStoreJwt()
    {
        var issuerId = _config["Apple:IssuerId"];
        var keyId = _config["Apple:KeyId"];
        var bundleId = _config["BundleId"];
        var privateKeyRaw = _config["Apple:PrivateKey"];

        LogApplePrivateKeyDiagnostics(privateKeyRaw ?? string.Empty);
        
        if (string.IsNullOrWhiteSpace(issuerId) ||
            string.IsNullOrWhiteSpace(keyId) ||
            string.IsNullOrWhiteSpace(bundleId) ||
            string.IsNullOrWhiteSpace(privateKeyRaw))
        {
            throw new InvalidOperationException("Apple App Store Server API config is not complete.");
        }

        using var ecdsa = LoadAppleEcdsaFromConfig(privateKeyRaw);

        var securityKey = new ECDsaSecurityKey(ecdsa) { KeyId = keyId };

        var now = DateTimeOffset.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuerId,
            IssuedAt = now.UtcDateTime,
            Expires = now.AddMinutes(5).UtcDateTime,
            Audience = "appstoreconnect-v1",
            Claims = new Dictionary<string, object>
            {
                ["bid"] = bundleId
            },
            SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256)
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(descriptor);
    }

    private ECDsa LoadAppleEcdsaFromConfig(string raw)
    {
        // 1) 흔한 케이스: 환경변수/JSON에서 \n 이스케이프
        raw = raw.Trim().Trim('"').Replace("\\n", "\n").Replace("\\r", "\r");

        // 2) PEM이 그대로 들어온 경우
        if (raw.Contains("BEGIN PRIVATE KEY", StringComparison.Ordinal) ||
            raw.Contains("BEGIN EC PRIVATE KEY", StringComparison.Ordinal))
        {
            var ecdsaPem = ECDsa.Create();
            ecdsaPem.ImportFromPem(raw);
            return ecdsaPem;
        }

        // 3) base64로 들어온 경우: (a) DER일 수도 있고 (b) PEM 텍스트를 base64로 감싼 것일 수도 있음
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(raw);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Apple:PrivateKey is neither PEM nor valid base64.", ex);
        }

        // 3-b) base64를 풀었더니 PEM 텍스트인 케이스
        // (p8 파일 내용을 그대로 base64 인코딩해서 넣은 경우가 여기에 해당)
        if (LooksLikePemText(bytes, out var pemText))
        {
            var ecdsaPem2 = ECDsa.Create();
            ecdsaPem2.ImportFromPem(pemText);
            return ecdsaPem2;
        }

        // 3-a) 정상 DER(PKCS#8)로 판단되면 그대로 Import
        var ecdsaDer = ECDsa.Create();
        ecdsaDer.ImportPkcs8PrivateKey(bytes, out _);
        return ecdsaDer;
    }

    private bool LooksLikePemText(byte[] bytes, out string pem)
    {
        try
        {
            pem = System.Text.Encoding.UTF8.GetString(bytes);
            return pem.Contains("BEGIN PRIVATE KEY", StringComparison.Ordinal) ||
                   pem.Contains("BEGIN EC PRIVATE KEY", StringComparison.Ordinal);
        }
        catch
        {
            pem = string.Empty;
            return false;
        }
    }
    
    private void LogApplePrivateKeyDiagnostics(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            _logger.LogWarning("[AppleIAP] Apple:PrivateKey is NULL/empty");
            return;
        }

        var originalLen = raw.Length;

        // 흔한 케이스: JSON/env에서 \n 이스케이프
        var normalized = raw.Trim().Trim('"').Replace("\\n", "\n").Replace("\\r", "\r");

        var containsPemHeader =
            normalized.Contains("BEGIN PRIVATE KEY", StringComparison.Ordinal) ||
            normalized.Contains("BEGIN EC PRIVATE KEY", StringComparison.Ordinal);

        // base64 여부 체크 (완전 엄격할 필요 없음)
        bool looksBase64 = LooksLikeBase64(normalized, out var decodedBytes, out var decodeErr);

        string decodedType = "N/A";
        int decodedLen = 0;

        if (looksBase64 && decodedBytes != null)
        {
            decodedLen = decodedBytes.Length;

            // base64 decode 결과가 PEM 텍스트인지(= PEM을 base64로 감싼 실수) 확인
            if (LooksLikePemText(decodedBytes, out var pemText))
                decodedType = "Base64OfPemText";
            else
                decodedType = "Base64Binary(DER?)";
        }

        _logger.LogInformation(
            "[AppleIAP] Apple:PrivateKey diagnostics: originalLen={OriginalLen}, normalizedLen={NormalizedLen}, containsPemHeader={ContainsPemHeader}, looksBase64={LooksBase64}, base64DecodedType={DecodedType}, decodedBytesLen={DecodedLen}, base64DecodeErr={DecodeErr}",
            originalLen,
            normalized.Length,
            containsPemHeader,
            looksBase64,
            decodedType,
            decodedLen,
            decodeErr);
    }

    private static bool LooksLikeBase64(string s, out byte[]? decoded, out string? err)
    {
        decoded = null;
        err = null;

        // PEM이면 base64 취급할 필요 없음
        if (s.Contains("BEGIN ", StringComparison.Ordinal)) return false;

        try
        {
            decoded = Convert.FromBase64String(s);
            return true;
        }
        catch (Exception e)
        {
            err = e.GetType().Name;
            return false;
        }
    }
}
