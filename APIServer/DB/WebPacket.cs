namespace ApiServer.DB;

#pragma warning disable CS8618 // 생성자를 종료할 때 null을 허용하지 않는 필드에 null이 아닌 값을 포함해야 합니다. null 허용으로 선언해 보세요.

#region API Test

public class TestRequired
{
    public int UnitId { get; set; }
}

public class TestResponse
{
    public bool TestOk { get; set; }
    public string UnitName { get; set; }
}

public class ServerTestRequired
{
    public bool Test { get; set; }
}

public class ServerTestResponse
{
    public bool MatchTestOk { get; set; }
    public bool SocketTestOk { get; set; }
}

public class TestApiToMatchRequired
{
    public bool Test { get; set; }
}

public class TestApiToMatchResponse
{
    public bool TestOk { get; set; }
}

public class TestApiToSocketRequired
{
    public bool Test { get; set; }
}

public class TestApiToSocketResponse
{
    public bool TestOk { get; set; }
}

#endregion

#region For Client

public class UserInfo
{
    public string UserName { get; set; }
    public int Level { get; set; }
    public int Exp { get; set; }
    public int RankPoint { get; set; }
    public int Gold { get; set; }
    public int Spinel { get; set; }
}

public class ProductInfo
{
    public int Id { get; set; }
    public List<CompositionInfo> Compositions { get; set; }
    public int Price { get; set; }
    public CurrencyType CurrencyType { get; set; }
    public ProductCategory Category { get; set; }
}

public class CompositionInfo
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public ProductType Type { get; set; }
    public int Count { get; set; }
    public int MinCount { get; set; }
    public int MaxCount { get; set; }
    public bool Guaranteed { get; set; }
    public bool IsSelectable { get; set; }
}

public class DailyProductInfo
{
    public int Id { get; set; }
    public int Price { get; set; }
    public CurrencyType CurrencyType { get; set; }
    public ProductCategory Category { get; set; }
    public bool AlreadyBought { get; set; }
}

public class UnitInfo
{
    public int Id { get; set; }
    public UnitClass Class { get; set; }
    public int Level { get; set; }
    public int Species { get; set; }
    public UnitRole Role { get; set; }
    public Faction Faction { get; set; }
    public UnitRegion Region { get; set; }
}

public class SheepInfo
{
    public int Id { get; set; }
    public UnitClass Class { get; set; }
}

public class EnchantInfo
{
    public int Id { get; set; }
    public UnitClass Class { get; set; }
}

public class CharacterInfo
{
    public int Id { get; set; }
    public UnitClass Class { get; set; }
}

public class MaterialInfo
{
    public int Id { get; set; }
    public UnitClass Class { get; set; }
}

public class ReinforcePointInfo
{
    public UnitClass Class { get; set; }
    public int Level { get; set; }
    public int Point { get; set; }
}

public class OwnedUnitInfo
{
    public UnitInfo UnitInfo { get; set; }
    public int Count { get; set; }
}

public class OwnedSheepInfo
{
    public SheepInfo SheepInfo { get; set; }
    public int Count { get; set; }
}

public class OwnedEnchantInfo
{
    public EnchantInfo EnchantInfo { get; set; }
    public int Count { get; set; }
}

public class OwnedCharacterInfo
{
    public CharacterInfo CharacterInfo { get; set; }
    public int Count { get; set; }
}

public class OwnedMaterialInfo
{
    public MaterialInfo MaterialInfo { get; set; }
    public int Count { get; set; }
}

public class OwnedProductInfo
{
    public int ProductId { get; set; }
    public int Count { get; set; }
}

public class DeckInfo
{
    public int DeckId { get; set; }
    public UnitInfo[] UnitInfo { get; set; }
    public int DeckNumber { get; set; }
    public int Faction { get; set; }
    public bool LastPicked { get; set; }
}

public class BattleSettingInfo
{
    public SheepInfo SheepInfo { get; set; }
    public EnchantInfo EnchantInfo { get; set; }
    public CharacterInfo CharacterInfo { get; set; }
}

public class UnitMaterialInfo
{
    public int UnitId { get; set; }
    public List<OwnedMaterialInfo> Materials { get; set; }
}

public class RewardInfo
{
    public int ItemId { get; set; }
    public ProductType ProductType { get; set; }
    public int Count { get; set; }
}

public class ValidateNewAccountPacketRequired
{
    public string UserAccount { get; set; }
    public string Password { get; set; }
}

public class ValidateNewAccountPacketResponse
{
    public bool ValidateOk { get; set; }
    public int ErrorCode { get; set; }
}

public class CreateUserAccountPacketRequired
{
    public string UserAccount { get; set; }
    public string Password { get; set; }
}

public class CreateUserAccountPacketResponse
{
    public bool CreateOk { get; set; }
    public string Message { get; set; }
}

public class LoginUserAccountPacketRequired
{
    public string UserAccount { get; set; }
    public string Password { get; set; }
}

public class LoginUserAccountPacketResponse
{
    public bool LoginOk { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}

public class ChangeActPacketRequired
{
    public string AccessToken { get; set; }
    public int SessionId { get; set; }
    public Faction Faction { get; set; }
    public int MapId { get; set; }
}

public class ChangeActPacketResponse
{
    public bool ChangeOk { get; set; }
}

public class ChangeActTestPacketRequired
{
    public string AccessToken { get; set; }
    public int SessionId { get; set; }
    public Faction Faction { get; set; }
    public int MapId { get; set; }
}

public class ChangeActTestPacketResponse
{
    public bool ChangeOk { get; set; }
}

public class CancelMatchPacketRequired
{
    public string AccessToken { get; set; }
}

public class CancelMatchPacketResponse
{
    public bool CancelOk { get; set; }
}

public class SurrenderPacketRequired
{
    public string AccessToken { get; set; }
}

public class SurrenderPacketResponse
{
    public bool SurrenderOk { get; set; }
}

public class LoadInfoPacketRequired
{
    public bool LoadInfo { get; set; }
}

public class LoadInfoPacketResponse
{
    public bool LoadInfoOk { get; set; }
    public List<UnitInfo> UnitInfos { get; set; }
    public List<SheepInfo> SheepInfos { get; set; }
    public List<EnchantInfo> EnchantInfos { get; set; }
    public List<CharacterInfo> CharacterInfos { get; set; }
    public List<MaterialInfo> MaterialInfos { get; set; }
    public List<ReinforcePointInfo> ReinforcePoints { get; set; }
    public List<UnitMaterialInfo> CraftingMaterials { get; set; }
}

public class LoadUserInfoPacketRequired
{
    public string AccessToken { get; set; }
}

public class LoadUserInfoPacketResponse
{
    public bool LoadUserInfoOk { get; set; }
    public UserInfo UserInfo { get; set; }
}

public class LoadTestUserPacketRequired
{
    public int UserId { get; set; }
}

public class LoadTestUserPacketResponse
{
    public bool LoadTestUserOk { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}

public class UpdateUserInfoPacketRequired
{
    public string AccessToken { get; set; }
    public UserInfo UserInfo { get; set; }
}

public class UpdateUserInfoPacketResponse
{
    public bool UpdateUserInfoOk { get; set; }
}

public class RefreshTokenRequired
{
    public string RefreshToken { get; set; }
}

public class RefreshTokenResponse
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}

public class InitCardsPacketRequired
{
    public string AccessToken { get; set; }
    public Env Environment { get; set; }
}

public class InitCardsPacketResponse
{
    public bool GetCardsOk { get; set; }
    public List<OwnedUnitInfo> OwnedCardList { get; set; }
    public List<UnitInfo> NotOwnedCardList { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}

public class InitSheepPacketRequired
{
    public string AccessToken { get; set; }
    public Env Environment { get; set; }
}

public class InitSheepPacketResponse
{
    public bool GetSheepOk { get; set; }
    public List<OwnedSheepInfo> OwnedSheepList { get; set; }
    public List<SheepInfo> NotOwnedSheepList { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}

public class InitEnchantPacketRequired
{
    public string AccessToken { get; set; }
    public Env Environment { get; set; }
}

public class InitEnchantPacketResponse
{
    public bool GetEnchantOk { get; set; }
    public List<OwnedEnchantInfo> OwnedEnchantList { get; set; }
    public List<EnchantInfo> NotOwnedEnchantList { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}

public class InitCharacterPacketRequired
{
    public string AccessToken { get; set; }
    public Env Environment { get; set; }
}

public class InitCharacterPacketResponse
{
    public bool GetCharacterOk { get; set; }
    public List<OwnedCharacterInfo> OwnedCharacterList { get; set; }
    public List<CharacterInfo> NotOwnedCharacterList { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}


public class InitMaterialPacketRequired
{
    public string AccessToken { get; set; }
    public Env Environment { get; set; }
}

public class InitMaterialPacketResponse
{
    public bool GetMaterialOk { get; set; }
    public List<OwnedMaterialInfo> OwnedMaterialList { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}

public class GetInitDeckPacketRequired
{
    public string AccessToken { get; set; }
    public Env Environment { get; set; }
}

public class GetInitDeckPacketResponse
{
    public bool GetDeckOk { get; set; }
    public List<DeckInfo> DeckList { get; set; }
    public BattleSettingInfo BattleSetting { get; set; }
}

public class GetSelectedDeckRequired
{
    public string AccessToken { get; set; }
    public int Faction { get; set; }
    public int DeckNumber { get; set; }
}

public class GetSelectedDeckResponse
{
    public bool GetSelectedDeckOk { get; set; }
    public DeckInfo Deck { get; set; }
    public BattleSettingInfo BattleSetting{ get; set; }
}

public class UpdateDeckPacketRequired
{
    public string AccessToken { get; set; }
    public int DeckId { get; set; }
    public UnitId UnitIdToBeDeleted { get; set; }
    public UnitId UnitIdToBeUpdated { get; set; }
}

public class UpdateDeckPacketResponse
{
    public int UpdateDeckOk { get; set; }
}

public class UpdateLastDeckPacketRequired
{
    public string AccessToken { get; set; }
    public Dictionary<int, bool> LastPickedInfo { get; set; }
}

public class UpdateLastDeckPacketResponse
{
    public bool UpdateLastDeckOk { get; set; }
}

public class UpdateBattleSettingPacketRequired
{
    public string AccessToken { get; set; }
    public BattleSettingInfo BattleSettingInfo { get; set; }
}

public class UpdateBattleSettingPacketResponse
{
    public bool UpdateBattleSettingOk { get; set; }   
}

public class LoadMaterialsPacketRequired
{
    public string AccessToken { get; set; }
    public UnitId UnitId { get; set; }
}

public class LoadMaterialsPacketResponse
{
    public List<OwnedMaterialInfo> CraftingMaterialList { get; set; }
    public bool LoadMaterialsOk { get; set; }
}

public class CraftCardPacketRequired
{
    public string AccessToken { get; set; }
    public List<OwnedMaterialInfo> Materials { get; set; }
    public UnitId UnitId { get; set; }
    public int Count { get; set; }
}

public class CraftCardPacketResponse
{
    // Error: 0 - Success, 1 - Not enough materials
    public bool CraftCardOk { get; set; }
    public int Error { get; set; }
}

public class ReinforceResultPacketRequired
{
    public string AccessToken { get; set; }
    public UnitInfo UnitInfo { get; set; }
    public List<UnitInfo> UnitList { get; set; }
}

public class ReinforceResultPacketResponse
{
    public bool ReinforceResultOk { get; set; }
    public bool IsSuccess { get; set; }
    public List<OwnedUnitInfo> UnitList { get; set; }
    public int Error { get; set; }
}

public class InitProductPacketRequired
{
    public string AccessToken { get; set; }
}

public class InitProductPacketResponse
{
    public bool GetProductOk { get; set; }
    public List<ProductInfo> SpecialPackages { get; set; }
    public List<ProductInfo> BeginnerPackages { get; set; }
    public List<ProductInfo> GoldPackages { get; set; }
    public List<ProductInfo> SpinelPackages { get; set; }
    public List<ProductInfo> GoldItems { get; set; }
    public List<ProductInfo> SpinelItems { get; set; }
    public List<ProductInfo> ReservedSales { get; set; }
    public List<DailyProductInfo> DailyDeals { get; set; }
}

#endregion

#region For Match Making Server

public class MatchMakingPacketRequired
{
    public bool Test { get; set; } = false;
    public int UserId { get; set; }
    public int SessionId { get; set; }
    public string UserName { get; set; }
    public Faction Faction { get; set; }
    public int RankPoint { get; set; }
    public DateTime RequestTime { get; set; }
    public int MapId { get; set; }
    public int CharacterId { get; set; }
    public int AssetId { get; set; }
    public UnitId[] UnitIds { get; set; }
    public List<int> Achievements { get; set; }
}

public class MatchMakingPacketResponse
{
    
}

public class MatchCancelPacketRequired
{
    public int UserId { get; set; }
}
    
public class MatchCancelPacketResponse
{
    public int UserId { get; set; }
}

public class GetRankPointPacketRequired
{
    public int SheepUserId { get; set; }
    public int WolfUserId { get; set; }
}

public class GetRankPointPacketResponse
{
    public int WinPointSheep { get; set; }
    public int WinPointWolf { get; set; }
    public int LosePointSheep { get; set; }
    public int LosePointWolf { get; set; }
}

#endregion

#region For Socket Server

public class SendMatchInfoPacketRequired
{
    public int SheepUserId { get; set; }
    public int SheepSessionId { get; set; }
    public int WolfUserId { get; set; }
    public int WolfSessionId { get; set; }
}

public class SendMatchInfoPacketResponse
{
    public bool SendMatchInfoOk { get; set; }
}

public class GameResultPacketRequired
{
    public int UserId { get; set; }
    public bool IsWin { get; set; }
}

public class GameResultPacketResponse
{
    public bool GetGameResultOk { get; set; }
}

public class GameRewardPacketRequired
{
    public int WinUserId { get; set; }
    public int WinRankPoint { get; set; }
    public int LoseUserId { get; set; }
    public int LoseRankPoint { get; set; }
}

public class GameRewardPacketResponse
{
    public bool GetGameRewardOk { get; set; }
    public List<RewardInfo> WinnerRewards { get; set; }
    public List<RewardInfo> LoserRewards { get; set; }
}

public class SessionDisconnectPacketRequired
{
    public int UserId { get; set; }
    public int SessionId { get; set; }
}

public class SessionDisconnectPacketResponse
{
    public bool SessionDisconnectOk { get; set; }
}

#endregion