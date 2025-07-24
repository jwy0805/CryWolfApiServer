using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#pragma warning disable CS8618 // 생성자를 종료할 때 null을 허용하지 않는 필드에 null이 아닌 값을 포함해야 합니다. null 허용으로 선언해 보세요.

namespace ApiServer.DB;

// User before email authentication
[Table("TempUser")]
public class TempUser
{
    [MaxLength(60)]
    public string TempUserAccount { get; set; }
    [MaxLength(120)]
    public string TempPassword { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public UserAct Act { get; set; }
}

[Table("User")]
public class User
{
    public int UserId { get; set; }
    [MaxLength(30)]
    public string UserName { get; set; }
    public bool NameInitialized { get; set; }
    public UserRole Role { get; set; }
    public UserAct Act { get; set; }
    public UserState State { get; set; }
    public DateTime? LastPingTime { get; set; }
    
    // Navigation properties
    public ICollection<BattleSetting> BattleSettings { get; set; }
    public ICollection<Deck> Decks { get; set; }
    public ICollection<Mail> Mails { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; }
    public ICollection<Transaction> Transactions { get; set; }
    public ICollection<UserAuth> UserAuths { get; set; }
    public ICollection<UserCharacter> UserCharacters { get; set; }
    public ICollection<UserEnchant> UserEnchants { get; set; }
    public ICollection<UserMaterial> UserMaterials { get; set; }
    public ICollection<UserMatch> UserMatches { get; set; }
    public ICollection<UserProduct> UserProducts { get; set; }
    public ICollection<UserSheep> UserSheep { get; set; }
    public ICollection<UserStage> UserStages { get; set; }
    public ICollection<UserSubscription> UserSubscriptions { get; set; }
    public UserStats UserStats { get; set; }
    public ICollection<UserTutorial> UserTutorials { get; set; }
    public ICollection<UserUnit> UserUnits { get; set; }
}

[Table("UserAuth")]
public class UserAuth
{
    public int UserAuthId { get; set; }
    public int UserId { get; set; }
    public AuthProvider Provider { get; set; }
    
    // ID, sub, deviceId
    [MaxLength(60)]
    public string       UserAccount  { get; set; } = null!;
    [MaxLength(120)]
    public string?      PasswordHash    { get; set; }
    public bool PolicyAgreed { get; set; }
    public DateTime     LinkedAt        { get; set; }      
    
    // Navigation properties
    public User User { get; set; }
}

[Table("UserStats")]
public class UserStats
{
    public int UserId { get; set; }
    public int UserLevel { get; set; }
    public int RankPoint { get; set; }
    public int HighestRankPoint { get; set; }
    public int Exp { get; set; }
    public int Gold { get; set; }
    public int Spinel { get; set; }
    
    // Navigation properties
    public User User { get; set; }
}

[Table("UserMatch")]
public class UserMatch
{
    [Key]
    public int UserId { get; set; }
    public int WinRankMatch { get; set; }
    public int LoseRankMatch { get; set; }
    public int WinFriendlyMatch { get; set; }
    public int LoseFriendlyMatch { get; set; }
    
    // Navigation properties
    public User User { get; set; }
}

[Table("UserSubscription")]
public class UserSubscription
{
    public int UserId { get; set; }
    public SubscriptionType SubscriptionType { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? CanceledAtUtc { get; set; }
    public bool IsCanceled { get; set; }
    public bool IsTrial { get; set; }
    
    // Navigation properties
    public User User { get; set; }
}

[Table("UserSubscriptionHistory")]
public class UserSubscriptionHistory
{
    public long HistoryId { get; set; }
    public int UserId { get; set; }
    public SubscriptionType SubscriptionType { get; set; }
    public DateTime         FromUtc          { get; set; }
    public DateTime         ToUtc            { get; set; }
    public SubscriptionEvent EventType       { get; set; }   // Started / Renewed / Canceled / Expired …
}

[Table("UserTutorial")]
public class UserTutorial
{
    public int UserTutorialId { get; set; }
    public int UserId { get; set; }
    public TutorialType TutorialType { get; set; }
    public int TutorialStep { get; set; }
    public bool Done { get; set; }
    
    // Navigation properties
    public User User { get; set; }
}

[Table("Friends")]
public class Friend
{
    public int UserId { get; set; }
    public int FriendId { get; set; }
    public int RequestReceiverId { get; set; }
    public FriendStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Table("Mail")]
public class Mail
{
    public int MailId { get; set; }
    public int UserId { get; set; }
    public MailType Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int? ProductId { get; set; }
    [MaxLength(120)]
    public string? ProductCode { get; set; }
    public bool Claimed { get; set; }
    public bool Expired { get; set; }
    [MaxLength(120)]
    public string? Message { get; set; }
    [MaxLength(30)]
    public string? Sender { get; set; }
    
    // Navigation properties
    public User User { get; set; }
}

[Table("RefreshToken")]
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [MaxLength(120)]
    public string Token { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdateAt { get; set; }
    
    // Navigation properties
    public User User { get; set; }
}

[Table("ExpTable")]
public class ExpTable
{
    public int Level { get; set; }
    public int Exp { get; set; }
}

[Table("ExpReward")]
public class ExpReward
{
    public int Level { get; set; }
    public int ProductId { get; set; }
    public ProductType ProductType { get; set; }
    public int Count { get; set; }
}

[Table("Unit")]
public class Unit
{
    public UnitId UnitId { get; set; }
    [MaxLength(30)]
    public string UnitName { get; set; }
    public UnitClass Class { get; set; }
    public int Level { get; set; }
    public UnitId Species { get; set; }
    public UnitRole Role { get; set; }
    public Faction Faction { get; set; }
    public UnitRegion Region { get; set; }
}

[Table("Deck")]
public class Deck
{
    public int DeckId { get; set; }
    public int UserId { get; set; }
    public Faction Faction { get; set; }
    public int DeckNumber { get; set; }
    public bool LastPicked { get; set; }
    
    // Navigation properties
    public User User { get; set; }
    public ICollection<DeckUnit> DeckUnits { get; set; }
}

[Table("Deck_Unit")]
public class DeckUnit
{
    public int DeckId { get; set; }
    public UnitId UnitId { get; set; }
    
    // Navigation properties
    public Deck Deck { get; set; }
}

[Table("User_Unit")]
public class UserUnit
{
    public int UserId { get; set; }
    public UnitId UnitId { get; set; }
    public int Count { get; set; }
    
    // Navigation properties
    public User User { get; set; }
}

[Table("Sheep")]
public class Sheep
{
    public SheepId SheepId { get; set; }
    public UnitClass Class { get; set; }
}

[Table("Enchant")]
public class Enchant
{
    public EnchantId EnchantId { get; set; }
    public UnitClass Class { get; set; }
}

[Table("Character")]
public class Character
{
    public CharacterId CharacterId { get; set; }
    public UnitClass Class { get; set; }
}

[Table("Material")]
public class Material
{
    public MaterialId MaterialId { get; set; }
    [MaxLength(50)]
    public string MaterialName { get; set; }
    public UnitClass Class { get; set; }
}

[Table("Product")]
public class Product
{
    public int ProductId { get; set; }
    [MaxLength(60)]
    public string ProductName { get; set; }
    [MaxLength(120)]
    public string ProductCode { get; set; }
    public int Price { get; set; }
    public CurrencyType Currency { get; set; }
    public ProductCategory Category { get; set; }
    public ProductType ProductType { get; set; }
    public bool IsFixed { get; set; }
    
    // Navigation properties
    public DailyProduct DailyProduct { get; set; }
    public DailyFreeProduct DailyFreeProduct { get; set; }
}

[Table("DailyProduct")]
public class DailyProduct
{
    public int ProductId { get; set; }
    public int Weight { get; set; }
    public int Price { get; set; }
    public UnitClass Class { get; set; }
    
    // Navigation properties
    public Product Product { get; set; }
}

[Table("DailyFreeProduct")]
public class DailyFreeProduct
{
    public int ProductId { get; set; }
    public int Weight { get; set; }
    public UnitClass Class { get; set; }
    
    // Navigation properties
    public Product Product { get; set; }
}

[Table("UserDailyProduct")]
public class UserDailyProduct
{
    public int UserId { get; set; }
    public byte Slot { get; set; }
    public int ProductId { get; set; }
    public DateOnly SeedDate { get; set; }
    public byte RefreshIndex { get; set; }
    public DateTime RefreshAt { get; set; }
    public bool Bought { get; set; }
    public bool NeedAds { get; set; }
    public bool AdsWatched { get; set; }
    
    public User User { get; set; }
    public Product Product { get; set; }
}

[Table("ProductComposition")]
public class ProductComposition
{
    public int ProductId { get; set; }
    public int CompositionId { get; set; }
    public ProductType ProductType { get; set; }
    public int Count { get; set; }
    public bool Guaranteed { get; set; }
    public bool IsSelectable { get; set; }
}

[Table("CompositionProbability")]
public class CompositionProbability
{
    public int ProductId { get; set; }
    public int CompositionId { get; set; }
    public int GroupId { get; set; }
    public int Count { get; set; }
    public double Probability { get; set; }
}

[Table("Stage")]
public class Stage
{
    public int StageId { get; set; }
    public int StageLevel { get; set; }
    public Faction UserFaction { get; set; }
    public int AssetId { get; set; }
    public int CharacterId { get; set; }
    public int MapId { get; set; }
}

[Table("Stage_Enemy")]
public class StageEnemy
{
    public int StageId { get; set; }
    public UnitId UnitId { get; set; }
}

[Table("Stage_Reward")]
public class StageReward
{
    public int StageId { get; set; }
    public int ProductId { get; set; }
    public ProductType ProductType { get; set; }
    public int Count { get; set; }
    public int Star { get; set; }
}

[Table("User_Stage")]
public class UserStage
{
    public int UserId { get; set; }
    public int StageId { get; set; }
    public int StageLevel { get; set; }
    public int StageStar { get; set; }
    public bool IsCleared { get; set; }
    public bool IsAvailable { get; set; }
    
    // Navigation properties
    public User User { get; set; }
}

[Table("User_Product")]
public class UserProduct
{
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public ProductType ProductType { get; set; }
    public AcquisitionPath AcquisitionPath { get; set; }
    public int Count { get; set; }
    
    // Navigation properties
    public User User { get; set; }
}

[Table("User_Sheep")]
public class UserSheep
{
    public int UserId { get; set; }
    public SheepId SheepId { get; set; }
    public int Count { get; set; }
    
    // Navigation properties
    public User User { get; set; }
}

[Table("User_Enchant")]
public class UserEnchant
{
    public int UserId { get; set; }
    public EnchantId EnchantId { get; set; }
    public int Count { get; set; }
    
    // Navigation properties
    public User User { get; set; }
}

[Table("User_Character")]
public class UserCharacter
{
    public int UserId { get; set; }
    public CharacterId CharacterId { get; set; }
    public int Count { get; set; }
    
    // Navigation properties
    public User User { get; set; }
}

[Table("Unit_Material")]
public class UnitMaterial
{
    public UnitId UnitId { get; set; }
    public MaterialId MaterialId { get; set; }
    public int Count { get; set; }
}

[Table("User_Material")]
public class UserMaterial
{
    public int UserId { get; set; }
    public MaterialId MaterialId { get; set; }
    public int Count { get; set; }
    
    // Navigation properties
    public User User { get; set; }
}

[Table("Transaction")]
public class Transaction
{
    [Key]
    public long TransactionTimestamp { get; set; }
    [Key]
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public int Count { get; set; }
    public DateTime PurchaseAt { get; set; }
    public CurrencyType Currency { get; set; }
    public TransactionStatus Status { get; set; }
    public CashCurrencyType CashCurrency { get; set; }

    public Transaction(int userId, int productId, int count)
    {
        UserId = userId;
        ProductId = productId;
        Count = count;
        PurchaseAt = DateTime.UtcNow;
        SetTimestamp();
    }

    private void SetTimestamp()
    {
        var timestampString = $"{PurchaseAt:yyyyMMddHHmmssfff}";
        TransactionTimestamp = long.Parse(timestampString);
    }
    
    // Navigation properties
    public User User { get; set; }
}

[Table("BattleSetting")]
public class BattleSetting
{
    public int UserId { get; set; }
    public int SheepId { get; set; }
    public int EnchantId { get; set; }
    public int CharacterId { get; set; }
    
    // Navigation properties
    public User User { get; set; }
}

[Table("ReinforcePoint")]
public class ReinforcePoint
{
    [Key, Column(Order = 1)]
    public UnitClass Class { get; set; }
    [Key, Column(Order = 2)]
    public int Level { get; set; }
    public int Constant { get; set; }
}