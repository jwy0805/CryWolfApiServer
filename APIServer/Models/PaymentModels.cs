using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace ApiServer.Models;

public class GooglePlayPayload
{
    [JsonProperty("json")]
    public string Json { get; set; } = string.Empty;
    
    [JsonProperty("signature")]
    public string Signature { get; set; } = string.Empty;
}

public class GooglePlayPurchaseData
{
    [JsonProperty("packageName")]
    public string PackageName { get; set; } = string.Empty;
    
    [JsonProperty("productId")]
    public string ProductId { get; set; } = string.Empty;
    
    [JsonProperty("purchaseToken")]
    public string PurchaseToken { get; set; } = string.Empty;
}

public class GoogleProductPurchase
{
    [JsonProperty("purchaseState")]
    public int PurchaseState { get; set; }
    
    [JsonProperty("consumptionState")]
    public int ConsumptionState { get; set; }
    
    [JsonProperty("acknowledgementState")]
    public int AcknowledgementState { get; set; }
    
    [JsonProperty("purchaseTimeMillis")]
    public long PurchaseTimeMillis { get; set; }
}

public class AppleTransactionResponse
{
    [JsonProperty("bundleId")]
    public string BundleId { get; set; } = string.Empty;

    [JsonProperty("productId")]
    public string ProductId { get; set; } = string.Empty;

    [JsonProperty("transactionId")]
    public string TransactionId { get; set; } = string.Empty;

    [JsonProperty("originalTransactionId")]
    public string OriginalTransactionId { get; set; } = string.Empty;

    [JsonProperty("environment")]
    public string Environment { get; set; } = string.Empty;

    [JsonProperty("revocationReason")]
    public int? RevocationReason { get; set; }

    [JsonProperty("revocationDate")]
    public long? RevocationDate { get; set; }
}

public sealed class AppleTransactionLookupResponse
{
    [JsonProperty("signedTransactionInfo")]
    public string? SignedTransactionInfo { get; set; }
}

public class AppleErrorResponse
{
    [JsonProperty("errorCode")]
    public int ErrorCode { get; set; }

    [JsonProperty("errorMessage")]
    public string ErrorMessage { get; set; } = string.Empty;
}

public class AppleReceiptValidationResult
{
    [JsonProperty("status")] public int Status { get; set; }
}
    
public class UnityIapReceiptWrapper
{
    [JsonPropertyName("Store")] public string Store { get; set; } = string.Empty;
    [JsonPropertyName("TransactionID")] public string TransactionID { get; set; } = string.Empty;
    [JsonPropertyName("Payload")] public string Payload { get; set; } = string.Empty;
}