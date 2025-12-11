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

        if (product.ProductType == ProductType.Subscription)
        {
            await SubscriptionComplete(userId, required.ProductCode);
            response.PaymentCode = VirtualPaymentCode.Subscription;
        }
        else
        {
            await PurchaseComplete(userId, required.ProductCode);
            response.PaymentCode = VirtualPaymentCode.Product;
        }
        
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
        var res = new CashPaymentPacketResponse
        {
            PaymentOk = false,
            ErrorCode = CashPaymentErrorCode.InternalError,
        };

        try
        {
            var userId = _tokenValidator.Authorize(required.AccessToken);
            if (userId == -1)
            {
                res.ErrorCode = CashPaymentErrorCode.Unauthorized;
                return Ok(res);
            }
            
            // Store/TransactionId 추출
            var (storeType, storeTxId) = ExtractStoreInfo(required.Receipt);
            if (storeType == StoreType.None || string.IsNullOrEmpty(storeTxId))
            {
                res.ErrorCode = CashPaymentErrorCode.InvalidReceipt;
                return Ok(res);
            }
            
            // 이미 처리된 영수증인지 확인
            var alreadyProcess = await _context.Transaction.AnyAsync(t =>
                t.StoreType == storeType && t.StoreTransactionId == storeTxId &&
                t.Status == TransactionStatus.Completed);
            if (alreadyProcess)
            {
                res.PaymentOk = true;
                res.ErrorCode = CashPaymentErrorCode.AlreadyProcessed;
                return Ok(res);
            }
            
            // 실제 영수증 검증
            if (storeType == StoreType.GooglePlay)
            {
                var validationResult =
                    await ValidateGoogleReceiptAsync(required.ProductCode, required.Receipt);
                if (!validationResult.IsValid)
                {
                    res.ErrorCode = CashPaymentErrorCode.InvalidReceipt;
                    return Ok(res);
                }
            }
            else if (storeType == StoreType.AppStore)
            {
                var validationResult = 
                    await ValidateAppleReceiptAsync(required.ProductCode, required.Receipt);
                if (!validationResult.IsValid)
                {
                    res.ErrorCode = CashPaymentErrorCode.InvalidReceipt;
                    return Ok(res);
                }
            }
            
            await SubscriptionComplete(userId, required.ProductCode);
            
            var productId = _context.Product
                .AsNoTracking()
                .Where(p => p.ProductCode == required.ProductCode)
                .Select(p => p.ProductId)
                .FirstOrDefault();
            var count = 1;
            var transaction = new Transaction(userId, productId, count)
            {
                StoreType = storeType,
                StoreTransactionId = storeTxId,
                ReceiptRaw = required.Receipt,
                Currency = CurrencyType.Cash,
                // TODO: region에 맞게
                CashCurrency = CashCurrencyType.KRW,
                Status = TransactionStatus.Completed,
            };
            
            _context.Transaction.Add(transaction);
            await _context.SaveChangesExtendedAsync();
            
            res.PaymentOk = true;
            res.ErrorCode = CashPaymentErrorCode.None;
            return Ok(res);
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

    // 구독 상품, 스피넬 상품에 적용
    public async Task SubscriptionComplete(int userId, string productCode)
    {
        var product = await _context.Product.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProductCode == productCode);
        if (product == null) return;
        
        var composition = _context.ProductComposition.AsNoTracking()
            .FirstOrDefault(pc => pc.ProductId == product.ProductId);
        if (composition == null) return;
        
        await _claimService.StoreProductAsync(userId, composition);
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
        
        await _claimService.StoreProductAsync(userId, productCompositionStored);
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
    
    private async Task<(bool IsValid, string Message)> ValidateGoogleReceiptAsync(string productCode, string receipt)
    {
        UnityIapReceiptWrapper? wrapper;

        try
        {
            wrapper = JsonConvert.DeserializeObject<UnityIapReceiptWrapper>(receipt);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to deserialize Unity IAP wrapper for Google receipt");
            return (false, "Invalid receipt format");
        }

        if (wrapper == null || wrapper.Store != "GooglePlay")
            return (false, "Not a GooglePlay receipt");

        if (string.IsNullOrWhiteSpace(wrapper.Payload))
            return (false, "Google receipt payload is empty");

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
            return (false, "Invalid Google payload");
        }

        if (purchaseData == null) return (false, "Google receipt payload is empty");
        
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
            return (false, "Google receipt package name mismatch");
        }
        
        // Validate Product ID
        if (!string.Equals(purchaseData.ProductId, productCode, StringComparison.Ordinal))
        {
            _logger.LogWarning("Google receipt product ID mismatch. Expected: {Expected}, Actual: {Actual}",
                productCode, purchaseData.ProductId);
            return (false, "Google receipt product ID mismatch");
        }
        
        // Get Google API Access Token (OAuth2)
        var accessToken = await GetGoogleAccessTokenAsync();
        var url = $"https://www.googleapis.com/androidpublisher/v3/applications/{purchaseData.PackageName}/purchases/products/{purchaseData.ProductId}/tokens/{purchaseData.PurchaseToken}";
        
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "Google purchase validation failed. StatusCode: {Status}, Body: {Body}",
                response.StatusCode, error);

            return (false, "Failed to validate receipt");
        }

        var content = await response.Content.ReadAsStringAsync();
        GoogleProductPurchase? productPurchase;

        try
        {
            productPurchase = JsonConvert.DeserializeObject<GoogleProductPurchase>(content);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to deserialize Google ProductPurchase");
            return (false, "Failed to parse Google validation response");
        }

        if (productPurchase == null)
            return (false, "Empty Google ProductPurchase response");

        // purchaseState: 0 = PURCHASED, 1 = CANCELED, 2 = PENDING
        if (productPurchase.PurchaseState != 0)
        {
            _logger.LogWarning("Google purchase state invalid: {State}", productPurchase.PurchaseState);
            return (false, "Invalid Google purchase state");
        }

        // 여기서 consumptionState / acknowledgementState 로 중복 처리/미소비 여부 검증도 추가 가능
        return (true, "Valid Google receipt");
    }

    private async Task<string> GetGoogleAccessTokenAsync()
    {
        var serviceAccountJson = _config["Google:ServiceAccountJson"];
        if (string.IsNullOrEmpty(serviceAccountJson))
        {
            throw new Exception("Google Service Account not found");
        }
        
        var credential = GoogleCredential
            .FromJson(serviceAccountJson)
            .CreateScoped("https://www.googleapis.com/auth/androidpublisher");
        
        return await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
    }
    
    private async Task<(bool IsValid, string Message)> ValidateAppleReceiptAsync(string productCode, string receipt)
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
            return (false, $"Failed to deserialize Apple receipt JSON");
        }

        if (wrapper == null || string.IsNullOrEmpty(wrapper.Payload))
        {
            _logger.LogWarning("Invalid Apple receipt: missing payload");
            return (false, "Invalid Apple receipt: missing payload");
        }

        if (string.IsNullOrWhiteSpace(wrapper.TransactionID))
        {
            return (false, "Apple transactionId is empty");
        }

        var jwt = CreateAppStoreJwt();
        
        // 1st call to production
        var prodResult = 
            await CallAppStoreTransactionEndpointAsync(wrapper.TransactionID, jwt, false);
        AppleTransactionResponse? tx = null;
        var env = "Production";

        if (prodResult.IsSuccess)
        {
            tx = prodResult.Payload;
        }
        else if (prodResult.ShouldRetrySandbox)
        {
            // retry sandbox
            var sandboxResult = 
                await CallAppStoreTransactionEndpointAsync(wrapper.TransactionID, jwt, true);
            env = "Sandbox";

            if (!sandboxResult.IsSuccess || sandboxResult.Payload == null)
            {
                _logger.LogWarning("Apple sandbox validation failed. Raw");
                return (false, "Failed to validate Apple receipt (sandbox)");
            }
            
            tx = sandboxResult.Payload;
        }
        else
        {
            _logger.LogWarning("Apple production validation failed.");
            return (false, "Failed to validate Apple receipt (production)");
        }
        
        if (tx == null)
        {
            return (false, "Apple transaction data is null");
        }
        
        var expectedBundleId = _config["BundleId"];
        if (!string.Equals(tx.BundleId, expectedBundleId, StringComparison.Ordinal))
        {
            _logger.LogWarning("Apple receipt bundle ID mismatch. Expected: {Expected}, Actual: {Actual}",
                expectedBundleId, tx.BundleId);
            return (false, "Apple receipt bundle ID mismatch");
        }
        
        if (!string.Equals(tx.ProductId, productCode, StringComparison.Ordinal))
        {
            _logger.LogWarning("Apple receipt product ID mismatch. Expected: {Expected}, Actual: {Actual}",
                productCode, tx.ProductId);
            return (false, "Apple receipt product ID mismatch");
        }
        
        if (tx.RevocationReason.HasValue)
        {
            _logger.LogWarning("Apple transaction revoked. Reason={Reason}, Env={Env}, TxId={TxId}",
                tx.RevocationReason.Value, env, tx.TransactionId);
            return (false, "Revoked transaction");
        }

        return (true, $"Valid Apple receipt ({env})");
    }

    private async Task<(bool IsSuccess, bool ShouldRetrySandbox, AppleTransactionResponse? Payload, string Raw)>
        CallAppStoreTransactionEndpointAsync(string transactionId, string jwt, bool sandbox)
    {
        var baseUrl = sandbox
            ? "https://api.storekit-sandbox.itunes.apple.com"
            : "https://api.storekit.itunes.apple.com";

        var url = $"{baseUrl}/inApps/v1/transactions/{transactionId}";

        using var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            try
            {
                var tx = JsonConvert.DeserializeObject<AppleTransactionResponse>(body);
                return (tx != null, false, tx, body);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to deserialize AppleTransactionResponse. Body: {Body}", body);
                return (false, false, null, body);
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
                    return (false, true, null, body);
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

        return (false, false, null, body);
    }
    
    private string CreateAppStoreJwt()
    {
        var issuerId = _config["Apple:IssuerId"];
        var keyId = _config["Apple:KeyId"];
        var bundleId = _config["BundleId"];
        var privateKeyBase64 = _config["Apple:PrivateKey"];

        if (string.IsNullOrWhiteSpace(issuerId) ||
            string.IsNullOrWhiteSpace(keyId) ||
            string.IsNullOrWhiteSpace(bundleId) ||
            string.IsNullOrWhiteSpace(privateKeyBase64))
        {
            throw new InvalidOperationException("Apple App Store Server API config is not complete.");
        }

        var keyBytes = ParseApplePrivateKey(privateKeyBase64);
        var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(keyBytes, out _);

        var securityKey = new ECDsaSecurityKey(ecdsa)
        {
            KeyId = keyId
        };

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
            SigningCredentials = new SigningCredentials(
                securityKey,
                SecurityAlgorithms.EcdsaSha256)
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(descriptor);
    }

    private byte[] ParseApplePrivateKey(string raw)
    {
        raw = raw.Trim();
        
        if (raw.Contains("BEGIN PRIVATE KEY"))
        {
            // PEM -> base64 추출
            var lines = raw
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !l.StartsWith("-----"))
                .ToArray();
            var base64 = string.Join("", lines);
            return Convert.FromBase64String(base64);
        }

        // 이미 base64만 들어온 경우
        return Convert.FromBase64String(raw);
    }
}