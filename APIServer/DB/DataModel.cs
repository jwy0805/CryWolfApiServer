using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#pragma warning disable CS8618 // 생성자를 종료할 때 null을 허용하지 않는 필드에 null이 아닌 값을 포함해야 합니다. null 허용으로 선언해 보세요.

namespace ApiServer.DB;

[Table("TempUser")]
public class TempUser
{
    public string TempUserAccount { get; set; }
    public string TempPassword { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Table("User")]
public class User
{
    public int UserId { get; set; }
    public string UserAccount { get; set; }
    public string Password { get; set; }
    public string UserName { get; set; }
    public UserRole Role { get; set; }
    public UserAct Act { get; set; }
    public UserState State { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Table("UserStats")]
public class UserStats
{
    public int UserId { get; set; }
    public int UserLevel { get; set; }
    public int RankPoint { get; set; }
    public int Exp { get; set; }
    public int Gold { get; set; }
    public int Spinel { get; set; }
}

[Table("UserMatch")]
public class UserMatch
{
    [Key]
    public int UserId { get; set; }
    public int WinRankMatch { get; set; }
    public int LoseRankMatch { get; set; }
    public int DrawRankMatch { get; set; }
    public int WinFriendlyMatch { get; set; }
    public int LoseFriendlyMatch { get; set; }
    public int DrawFriendlyMatch { get; set; }
}

[Table("Friends")]
public class Friends
{
    public int UserId { get; set; }
    public int FriendId { get; set; }
    public FriendStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Table("RefreshToken")]
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Token { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdateAt { get; set; }
}

[Table("ExpTable")]
public class ExpTable
{
    public int Level { get; set; }
    public int Exp { get; set; }
}

[Table("Unit")]
public class Unit
{
    public UnitId UnitId { get; set; }
    [MaxLength(50)]
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
}

[Table("Deck_Unit")]
public class DeckUnit
{
    public int DeckId { get; set; }
    public UnitId UnitId { get; set; }
}

[Table("User_Unit")]
public class UserUnit
{
    public int UserId { get; set; }
    public UnitId UnitId { get; set; }
    public int Count { get; set; }
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
    [MaxLength(50)]
    public string ProductName { get; set; }
    public int Price { get; set; }
    public CurrencyType Currency { get; set; }
    public ProductCategory Category { get; set; }
    public bool IsFixed { get; set; }
}

[Table("ProductComposition")]
public class ProductComposition
{
    public int ProductId { get; set; }
    public int CompositionId { get; set; }
    public ProductType Type { get; set; }
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

[Table("User_Product")]
public class UserProduct
{
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public int Count { get; set; }
}

[Table("User_Sheep")]
public class UserSheep
{
    public int UserId { get; set; }
    public SheepId SheepId { get; set; }
    public int Count { get; set; }
}

[Table("User_Enchant")]
public class UserEnchant
{
    public int UserId { get; set; }
    public EnchantId EnchantId { get; set; }
    public int Count { get; set; }
}

[Table("User_Character")]
public class UserCharacter
{
    public int UserId { get; set; }
    public CharacterId CharacterId { get; set; }
    public int Count { get; set; }
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
}

[Table("BattleSetting")]
public class BattleSetting
{
    public int UserId { get; set; }
    public int SheepId { get; set; }
    public int EnchantId { get; set; }
    public int CharacterId { get; set; }
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