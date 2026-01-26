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
using MySqlConnector;
using Newtonsoft.Json;
// ReSharper disable InconsistentNaming

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenValidator _tokenValidator;
    private readonly ProductClaimService _claimService;
    private readonly IapService _iapService;
    private readonly IDailyProductService _dailyProductService;
    private readonly CachedDataProvider _cachedDataProvider;
    private readonly ILogger<PaymentController> _logger;
    
    public PaymentController(
        AppDbContext context,
        TokenValidator tokenValidator,
        ProductClaimService productClaimService,
        IapService iapService,
        IDailyProductService dailyProductService,
        CachedDataProvider cachedDataProvider,
        ILogger<PaymentController> logger)
    {
        _context = context;
        _tokenValidator = tokenValidator;
        _claimService = productClaimService;
        _iapService = iapService; 
        _dailyProductService = dailyProductService;
        _cachedDataProvider = cachedDataProvider;
        _logger = logger;
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
            var product = _cachedDataProvider.GetProducts().FirstOrDefault(p => p.ProductCode == required.ProductCode);
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
                response.CurrencyType = product.Currency;
                return;
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
            if (balanceGold < price)
            {
                response.PaymentOk = false;
                return;
            }
    
            var userDaily = _context.UserDailyProduct.FirstOrDefault(udp =>
                udp.UserId == userId && udp.ProductId == product.ProductId);
            if (userDaily == null || userDaily.Bought)
            {
                response.PaymentOk = false;
                return;
            }
            
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
        var receiptRaw = required.Receipt ?? string.Empty;

        try
        {
            var userId = _tokenValidator.Authorize(required.AccessToken);
            if (userId == -1) return Ok(_iapService.MakeResponse(false, CashPaymentErrorCode.Unauthorized));
            
            if (!_iapService.TryGetStoreInfo(receiptRaw, out var storeType, out var storeTxId))
                return Ok(_iapService.MakeResponse(false, CashPaymentErrorCode.InvalidReceipt));

            if (!_iapService.TryGetProductId(required.ProductCode, out var productId))
                return Ok(_iapService.MakeResponse(false, CashPaymentErrorCode.InvalidReceipt));
            
            // 1) 멱등 Seed 확보 + 현재 상태 조회 (예외 없이)
            var seed = await _iapService.EnsureSeedAndGetStatusAsync(userId, productId, storeType, storeTxId);
            
            // 2) 이미 처리된 상태면 즉시 응답
            var early = _iapService.MapStateToResponse(seed.Status, seed.FailureCode);
            if (early != null) return Ok(early);

            // 검증 후 지급
            var validation = await _iapService.ValidateReceiptAsync(storeType, required.ProductCode, receiptRaw);
            var final = await FinalizeAsync(
                seed.TransactionId, userId, required.ProductCode, receiptRaw, validation);

            return Ok(final);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "PurchaseSpinel error");
            return Ok(_iapService.MakeResponse(false, CashPaymentErrorCode.InternalError));
        }
    }
    
    private async Task<CashPaymentPacketResponse> FinalizeAsync(
        long transactionId, 
        int requestUserId,
        string productCode, 
        string receiptRaw, 
        IapService.ReceiptValidation validation)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var dbTx = await _context.Database.BeginTransactionAsync();

            try
            {
                // row lock: 같은 TransactionId로 동시 지급 방지
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT `TransactionId` FROM `Transaction` WHERE `TransactionId` = {transactionId} FOR UPDATE");

                var txRow = await _context.Transaction
                    .Include(t => t.Failure)
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

                if (txRow == null)
                {
                    await dbTx.RollbackAsync();
                    return _iapService.MakeResponse(false, CashPaymentErrorCode.InternalError);
                }

                // 정합성/보안: 트랜잭션 소유자 체크
                if (txRow.UserId != requestUserId)
                {
                    txRow.Status = TransactionStatus.Failed;
                    _iapService.UpsertFailureIfNull(txRow, validation, receiptRaw);
                    await _context.SaveChangesAsync();
                    await dbTx.CommitAsync();
                    return _iapService.MakeResponse(false, CashPaymentErrorCode.InvalidReceipt);
                }

                // 멱등 응답(이미 다른 요청이 처리했을 가능성)
                var early = _iapService.MapStateToResponse(txRow.Status, txRow.Failure?.HttpStatusCode);
                if (early != null)
                {
                    await dbTx.CommitAsync();
                    return early;
                }

                // 검증 실패
                if (!validation.IsValid)
                {
                    _iapService.UpsertFailureIfNull(txRow, validation, receiptRaw);

                    if (validation.Retryable)
                    {
                        await _context.SaveChangesAsync();
                        await dbTx.CommitAsync();
                        return _iapService.MakeResponse(false, CashPaymentErrorCode.InternalError);
                    }

                    // Invalid -> Failed
                    txRow.Status = TransactionStatus.Failed;
                    await _context.SaveChangesAsync();
                    await dbTx.CommitAsync();
                    return _iapService.MakeResponse(false, CashPaymentErrorCode.InvalidReceipt);
                }

                // Pending -> Processing 선점
                if (txRow.Status != TransactionStatus.Pending)
                {
                    await dbTx.CommitAsync();
                    return _iapService.MakeResponse(false, CashPaymentErrorCode.Processing);
                }
                txRow.Status = TransactionStatus.Processing;
                await _context.SaveChangesAsync();

                // 지급
                await PurchaseComplete(txRow.UserId, productCode);

                // 완료
                txRow.Status = TransactionStatus.Completed;
                await _context.SaveChangesAsync();

                await dbTx.CommitAsync();
                return _iapService.MakeResponse(true, CashPaymentErrorCode.None);
            }
            catch (Exception e)
            {
                await dbTx.RollbackAsync();
                _logger.LogError(e, "FinalizeAsync failed. TxId={TxId}", transactionId);
                throw;
            }
        });
    }

    public async Task PurchaseComplete(int userId, string productCode)
    {
        var product = _cachedDataProvider.GetProducts().FirstOrDefault(p => p.ProductCode == productCode);
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
            RandomProductInfos = new List<TotalProductInfo>
            {
                new() { ProductInfo = new ProductInfo { ProductId = required.ProductId, }, Count = resCount, }
            }
        };
        
        return Ok(res);
    }
}
