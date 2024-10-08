namespace AccountServer.DB;

#pragma warning disable CS8618 // 생성자를 종료할 때 null을 허용하지 않는 필드에 null이 아닌 값을 포함해야 합니다. null 허용으로 선언해 보세요.

#region For Client

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
    public UserAct Act { get; set; }
    public Faction Faction { get; set; }
    public int MapId { get; set; }
}

public class ChangeActPackerResponse
{
    public bool ChangeOk { get; set; }
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

#endregion


#region For Match Making Server

public class MatchMakingPacketRequired
{
    public int UserId { get; set; }
    public Faction Faction { get; set; }
    public int RankPoint { get; set; }
    public DateTime RequestTime { get; set; }
    public int MapId { get; set; }
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

#endregion

#region For Socket Server

public class GetUserIdPacketRequired
{
    public string UserAccount { get; set; }
}

public class GetUserIdPacketResponse
{
    public int UserId { get; set; }
}

#endregion