using System.Net.Http.Headers;
using System.Security.Cryptography;
using ApiServer.DB;
using ApiServer.Models;
using ApiServer.Providers;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace ApiServer.Services;

public class IapService
{
    public sealed record ReceiptValidation(bool IsValid, bool Retryable, int? HttpStatusCode, string? RawResponse, string Message);
    public sealed record TxSeed(long TransactionId, TransactionStatus Status, int? FailureCode);
    
    private readonly CachedDataProvider _cachedDataProvider;
    private readonly IConfiguration _config;
    private readonly AppDbContext _context;
    private readonly ILogger<IapService> _logger;
    
    public IapService(CachedDataProvider cachedDataProvider, IConfiguration config, AppDbContext context, ILogger<IapService> logger)
    {
        _cachedDataProvider = cachedDataProvider;
        _config = config;
        _context = context;
        _logger = logger;
    }

    private bool IsRetryableHttp(int? httpCode) => httpCode is null or 401 or >= 500 and <= 599;
    
    public CashPaymentPacketResponse MakeResponse(bool ok, CashPaymentErrorCode code) =>
        new() { PaymentOk = ok, ErrorCode = code };
    
    public bool TryGetStoreInfo(string receiptRaw, out StoreType storeType, out string storeTxId)
    {
        (storeType, storeTxId) = ExtractStoreInfo(receiptRaw);
        return storeType != StoreType.None && !string.IsNullOrEmpty(storeTxId);
    }

    public bool TryGetProductId(string productCode, out int productId)
    {
        productId = _cachedDataProvider.GetProducts()
            .Where(p => p.ProductCode == productCode)
            .Select(p => p.ProductId)
            .FirstOrDefault();
        
        return productId != 0;
    }

    public async Task<ReceiptValidation> ValidateReceiptAsync(StoreType storeType, string productCode,
        string receiptRaw)
    {
        (bool IsValid, string Message, int? HttpStatusCode, string? RawResponse) record = storeType switch
        {
            StoreType.GooglePlay => await ValidateGoogleReceiptAsync(productCode, receiptRaw),
            StoreType.AppStore => await ValidateAppleReceiptAsync(productCode, receiptRaw),
            _ => (false, "Unsupported store type", null, null)
        };

        if (record.IsValid)
            return new ReceiptValidation(true, false, 200, null, record.Message);

        var retryable = IsRetryableHttp(record.HttpStatusCode);
        return new ReceiptValidation(false, retryable, record.HttpStatusCode, record.RawResponse, record.Message);
    }
    
    public async Task<TxSeed> EnsureSeedAndGetStatusAsync(int userId, int productId, StoreType storeType,
        string storeTxId)
    {
        _context.ChangeTracker.Clear();
        
        await _context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO `Transaction`
(`UserId`,`ProductId`,`Count`,`Currency`,`CashCurrency`,`StoreType`,`StoreTransactionId`,`Status`,`PurchaseAt`)
VALUES
({userId},{productId},1,{CurrencyType.Cash},{CashCurrencyType.KRW},{storeType},{storeTxId},{TransactionStatus.Pending},{DateTime.UtcNow})
ON DUPLICATE KEY UPDATE
`TransactionId` = `TransactionId`;
");    
        
        // 상태 조회
        var row = await _context.Transaction.AsNoTracking()
            .Where(t => t.StoreType == storeType && t.StoreTransactionId == storeTxId)
            .Select(t => new
            {
                t.TransactionId,
                t.Status,
                FailureCode = t.Failure != null ? t.Failure.HttpStatusCode : null
            })
            .FirstAsync();
        
        return new TxSeed(row.TransactionId, row.Status, row.FailureCode);
    }
    
    public CashPaymentPacketResponse? MapStateToResponse(TransactionStatus status, int? failureCode)
    {
        return status switch
        {
            TransactionStatus.Completed => MakeResponse(true, CashPaymentErrorCode.AlreadyProcessed),
            TransactionStatus.Processing => MakeResponse(false, CashPaymentErrorCode.Processing),
            TransactionStatus.Failed => IsRetryableHttp(failureCode)
                ? MakeResponse(false, CashPaymentErrorCode.InternalError)
                : MakeResponse(false, CashPaymentErrorCode.InvalidReceipt),
            _ => null
        };
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
    
    public void UpsertFailureIfNull(Transaction txRow, ReceiptValidation v, string receiptRaw)
    {
        if (txRow.Failure != null) return;

        txRow.Failure = new TransactionReceiptFailure
        {
            TransactionId = txRow.TransactionId,
            CreatedAt = DateTime.UtcNow,
            HttpStatusCode = v.HttpStatusCode,
            ErrorMessage = Util.Util.Truncate(v.Message, 1024),
            ReceiptRawGzip = string.IsNullOrEmpty(receiptRaw) ? null : Util.Util.GzipUtf8(receiptRaw),
            ReceiptHash = string.IsNullOrEmpty(receiptRaw) ? null : Util.Util.Sha256Utf8(receiptRaw),
            ResponseRawGzip = string.IsNullOrEmpty(v.RawResponse) ? null : Util.Util.GzipUtf8(v.RawResponse),
        };
    }
    
    #region Google Receipt Validation
    
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

        var json = await File.ReadAllTextAsync(path);

        var credential = GoogleCredential
            .FromJson(json)
            .CreateScoped("https://www.googleapis.com/auth/androidpublisher");

        return await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
    }
    
    #endregion

    #region Apple Receipt Validation
    
    private async Task<(bool IsValid, string Message, int? HttpStatusCode, string? RawResponse)> ValidateAppleReceiptAsync(
        string productCode, string receipt)
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
        
        if (string.IsNullOrWhiteSpace(tx.BundleId) || string.IsNullOrWhiteSpace(tx.ProductId))
        {
            return (false, "Apple transaction parse failed", prod.HttpStatusCode, prod.Raw);
        }       
        
        if (!string.Equals(tx.BundleId, expectedBundleId, StringComparison.Ordinal))
        {
            _logger.LogWarning("Apple receipt bundle ID mismatch. Expected: {Expected}, Actual: {Actual}",
                expectedBundleId, tx.BundleId);

            return (false, "Apple receipt bundle ID mismatch", 200, prod.Raw);
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
    
    private static AppleTransactionResponse DecodeSignedTransactionInfoToTx(string signedTransactionInfo)
    {
        // JWS == header.payload.signature (JWT와 동일 포맷)
        var handler = new JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(signedTransactionInfo);

        string GetStr(string name)
            => jwt.TryGetPayloadValue(name, out object? v) && v != null ? v.ToString()! : string.Empty;

        int? GetIntNullable(string name)
        {
            if (!jwt.TryGetPayloadValue(name, out object? v) || v == null) return null;
            if (v is int i) return i;
            if (v is long l) return (int)l;
            if (int.TryParse(v.ToString(), out var parsed)) return parsed;
            return null;
        }

        long? GetLongNullable(string name)
        {
            if (!jwt.TryGetPayloadValue(name, out object? v) || v == null) return null;
            if (v is long l) return l;
            if (v is int i) return i;
            if (long.TryParse(v.ToString(), out var parsed)) return parsed;
            return null;
        }

        return new AppleTransactionResponse
        {
            BundleId = GetStr("bundleId"),
            ProductId = GetStr("productId"),
            TransactionId = GetStr("transactionId"),
            OriginalTransactionId = GetStr("originalTransactionId"),
            Environment = GetStr("environment"),
            RevocationReason = GetIntNullable("revocationReason"),
            RevocationDate = GetLongNullable("revocationDate"),
        };
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
                // Transaction Lookup 응답 -> tx 필드가 signedTransactionInfo(JWS)로 오는 경우가 일반적
                var lookup = JsonConvert.DeserializeObject<AppleTransactionLookupResponse>(body);

                if (lookup?.SignedTransactionInfo is null || lookup.SignedTransactionInfo.Length < 20)
                {
                    _logger.LogWarning("Apple lookup response missing signedTransactionInfo. Body={Body}", body);
                    return (false, false, null, status, body);
                }

                var tx = DecodeSignedTransactionInfoToTx(lookup.SignedTransactionInfo);

                // 파싱 결과가 비어있으면 모델/응답 구조 불일치 가능성 -> Raw를 남기고 실패 처리
                if (string.IsNullOrWhiteSpace(tx.BundleId) || string.IsNullOrWhiteSpace(tx.ProductId))
                {
                    _logger.LogWarning("Apple signedTransactionInfo decoded but bundleId/productId empty. Body={Body}", body);
                    return (false, false, null, status, body);
                }

                return (true, false, tx, status, body);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to parse Apple transaction lookup response. Body={Body}", body);
                return (false, false, null, status, body);
            }
        }

        // prod에서 401이면 sandbox로 1회 폴백 허용
        if (!sandbox && response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning(
                "App Store Server API prod returned 401; will retry sandbox once. StatusCode={Status}, Body={Body}",
                response.StatusCode, body);

            return (false, true, null, status, body);
        }
        
        // prod에서 NotFound(404) + 4040010(TransactionIdNotFoundError)면 sandbox 폴백
        if (!sandbox && response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            try
            {
                var error = JsonConvert.DeserializeObject<AppleErrorResponse>(body);
                if (error != null && error.ErrorCode == 4040010)
                {
                    return (false, true, null, status, body);
                }
            }
            catch
            {
                // ignore
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

        var securityKey = new ECDsaSecurityKey(ecdsa)
        {
            KeyId = keyId,
            CryptoProviderFactory = new CryptoProviderFactory
            {
                CacheSignatureProviders = false
            }
        };

        // SigningCredentials에도 캐시 비활성화를 한 번 더 걸어두면(라이브러리 버전 차이) 더 안전
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256)
        {
            CryptoProviderFactory = new CryptoProviderFactory
            {
                CacheSignatureProviders = false
            }
        };
        
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuerId,
            IssuedAt = DateTimeOffset.UtcNow.UtcDateTime,
            Expires = DateTimeOffset.UtcNow.AddMinutes(5).UtcDateTime,
            Audience = "appstoreconnect-v1",
            Claims = new Dictionary<string, object> { ["bid"] = bundleId },
            SigningCredentials = signingCredentials,
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private ECDsa LoadAppleEcdsaFromConfig(string raw)
    {
        raw = raw.Trim().Trim('"')
            .Replace("\\r", "\r")
            .Replace("\\n", "\n");

        // PEM이 들어온 경우: ImportFromPem 대신 한 줄 PEM도 되는 방식으로 직접 파싱
        if (raw.Contains("BEGIN PRIVATE KEY", StringComparison.Ordinal) ||
            raw.Contains("BEGIN EC PRIVATE KEY", StringComparison.Ordinal))
        {
            var ecdsa = ECDsa.Create();

            // PKCS#8 (Apple .p8)
            if (raw.Contains("BEGIN PRIVATE KEY", StringComparison.Ordinal))
            {
                var der = ExtractDerFromPem(raw, "PRIVATE KEY");
                ecdsa.ImportPkcs8PrivateKey(der, out _);
                return ecdsa;
            }

            // SEC1 EC PRIVATE KEY 형식 - 가능성 낮음
            var ecDer = ExtractDerFromPem(raw, "EC PRIVATE KEY");
            ecdsa.ImportECPrivateKey(ecDer, out _);
            return ecdsa;
        }

        // base64로 들어옴 - 기존 로직
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(raw);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Apple:PrivateKey is neither PEM nor valid base64.", ex);
        }

        if (LooksLikePemText(bytes, out var pemText))
        {
            // base64로 감싼 PEM 텍스트인 경우
            return LoadAppleEcdsaFromConfig(pemText);
        }

        var ecdsaDer = ECDsa.Create();
        ecdsaDer.ImportPkcs8PrivateKey(bytes, out _);
        return ecdsaDer;
    }

    private static byte[] ExtractDerFromPem(string pem, string label)
    {
        var begin = $"-----BEGIN {label}-----";
        var end   = $"-----END {label}-----";

        var start = pem.IndexOf(begin, StringComparison.Ordinal);
        var stop  = pem.IndexOf(end, StringComparison.Ordinal);
        if (start < 0 || stop < 0 || stop <= start)
            throw new InvalidOperationException($"PEM format invalid. Missing {begin}/{end}");

        start += begin.Length;

        // BEGIN/END 사이의 base64 본문만 추출 + 공백/개행 제거
        var base64Body = pem.Substring(start, stop - start);
        base64Body = new string(base64Body.Where(c => !char.IsWhiteSpace(c)).ToArray());

        return Convert.FromBase64String(base64Body);
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

            // base64 decode 결과가 PEM 텍스트인지(PEM을 base64로 감싼 실수) 확인
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
    
    #endregion
}