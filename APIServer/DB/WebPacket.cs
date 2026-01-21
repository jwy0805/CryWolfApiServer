using System.ComponentModel.DataAnnotations;

namespace ApiServer.DB;

#pragma warning disable CS8618 // 생성자를 종료할 때 null을 허용하지 않는 필드에 null이 아닌 값을 포함해야 합니다. null 허용으로 선언해 보세요.

#region DTO

public class UserInfo
{
    public string UserAccount { get; set; }
    public string UserName { get; set; }
    public string UserTag { get; set; }
    public UserRole UserRole { get; set; }
    public int Level { get; set; }
    public int Exp { get; set; }
    public int ExpToLevelUp { get; set; }
    public int RankPoint { get; set; }
    public int HighestRankPoint { get; set; }
    public int Victories { get; set; }
    public int WinRate { get; set; }
    public int Gold { get; set; }
    public int Spinel { get; set; }
    public bool NameInitialized { get; set; }
    public List<SubscriptionInfo> Subscriptions { get; set; }
}


public class UserTutorialInfo
{
    public bool WolfTutorialDone { get; set; }
    public bool SheepTutorialDone { get; set; }
    public bool ChangeFactionTutorialDone { get; set; }
    public bool CollectionTutorialDone { get; set; }
    public bool ReinforceTutorialDone { get; set; }
}

public class FriendUserInfo
{
    public string UserName { get; set; }
    public string UserTag { get; set; }
    public int Level { get; set; }
    public int RankPoint { get; set; }
    public UserAct Act { get; set; }
    public FriendStatus FriendStatus { get; set; }
}

public class MailInfo
{
    public int MailId { get; set; }
    public MailType Type { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int ProductId { get; set; }
    public ProductCategory ProductCategory { get; set; }
    public bool Claimed { get; set; }
    public string Message { get; set; }
    public string Sender { get; set; }
}

public class SubscriptionInfo
{
    public SubscriptionType SubscriptionType { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime StartAt { get; set; }
}

public class ProductInfo
{
    public int ProductId { get; set; }
    public List<CompositionInfo> Compositions { get; set; }
    public int Price { get; set; }
    public CurrencyType CurrencyType { get; set; }
    public ProductType ProductType { get; set; }
    public ProductCategory Category { get; set; }
    public string ProductCode { get; set; }
}

public class CompositionInfo
{
    public int ProductId { get; set; }
    public int CompositionId { get; set; }
    public ProductType ProductType { get; set; }
    public int Count { get; set; }
    public int MinCount { get; set; }
    public int MaxCount { get; set; }
    public bool Guaranteed { get; set; }
    public bool IsSelectable { get; set; }
}

public class DailyProductInfo
{
    public ProductInfo ProductInfo { get; set; }
    public UnitClass Class { get; set; }
    public int Slot { get; set; }
    public bool Bought { get; set; }
    public bool AdsWatched { get; set; }
    public bool NeedAds { get; set; }
}

public class TotalProductInfo
{
    public ProductInfo ProductInfo { get; set; }
    public int Count { get; set; }
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

public class StageInfo
{
    public int StageId { get; set; }
    public int StageLevel { get; set; }
    public Faction UserFaction { get; set; }
    public int AssetId { get; set; }
    public int CharacterId { get; set; }
    public int MapId { get; set; }
    public List<StageEnemy> StageEnemy { get; set; }
    public List<StageReward> StageReward { get; set; }
}

public class UserStageInfo
{
    public int UserId { get; set; }
    public int StageId { get; set; }
    public int StageLevel { get; set; }
    public int StageStar { get; set; }
    public bool IsCleared { get; set; }
    public bool IsAvailable { get; set; }
}

public class StageEnemyInfo
{
    public int StageId { get; set; }
    public List<UnitId> UnitIds { get; set; }
}

public class StageRewardInfo
{
    public int StageId { get; set; }
    public List<SingleRewardInfo> RewardProducts { get; set; }
}

public class SingleRewardInfo
{
    public int ItemId { get; set; }
    public ProductType ProductType { get; set; }
    public int Count { get; set; }
    public int Star { get; set; }
}

public class RewardInfo
{
    public int ItemId { get; set; }
    public ProductType ProductType { get; set; }
    public int Count { get; set; }
}

public class NoticeInfo
{
    public int NoticeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EventInfo
{
    public int EventId { get; set; }
    public string EventKey { get; set; } = "";
    public DateTime? StartAtUtc { get; set; }
    public DateTime? EndAtUtc { get; set; }

    public bool IsPinned { get; set; }
    public int Priority { get; set; }

    public string Title { get; set; } = "";
    public string Content { get; set; } = ""; 
}

public class LocalizationInfo
{
    public string LanguageCode { get; set; } = "en";
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class TierInfo
{
    public int Tier { get; set; }
    public string ConditionJson { get; set; } = "{}";
    public string RewardJson { get; set; } = "{}"; 
    public int MinEventVersion { get; set; } = 1;
    public int? MaxEventVersion { get; set; }
    public bool IsClaimed { get; set; }
    public bool IsClaimable { get; set; }   
}

#endregion

#region External

public class DeleteUserAccountHardPacketRequired
{
    public int UserId { get; set; }
    public string AdminPassword { get; set; }
}

public class DeleteUserAccountHardPacketResponse
{
    public bool DeleteOk { get; set; }
}

public class SendMailByAdminPacketRequired
{
    public int[] UserIds { get; set; }
    public MailType Type { get; set; }
    public int? ProductId { get; set; }
    public string Message { get; set; }
    [MaxLength(20)]
    public string Sender { get; set; }
}

public class SendMailByAdminPacketResponse
{
    public bool SendMailOk { get; set; }
}

public class DeleteNoticePacketRequired
{
    public int NoticeId { get; set; }
    public string AdminPassword { get; set; }
}

public class DeleteEventPacketRequired 
{
    public int EventId { get; set; }
    public string AdminPassword { get; set; }
}

public class ClearNoticesPacketRequired
{
    public string AdminPassword { get; set; }
}

public class ClearEventsPacketRequired
{
    public string AdminPassword { get; set; }
}

#endregion

#region For Client

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

public class LoginApplePacketRequired
{
    public string IdToken { get; set; }
}

public class LoginApplePacketResponse
{
    public bool LoginOk { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}

public class LoginGooglePacketRequired
{
    public string IdToken { get; set; }
}

public class LoginGooglePacketResponse
{
    public bool LoginOk { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}

public class LoginGuestPacketRequired
{
    public string GuestId { get; set; }
}

public class LoginGuestPacketResponse
{
    public bool LoginOk { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}

public class LoginTokenPacketRequired
{
    public string RefreshToken { get; set; }
}

public class LoginTokenPacketResponse
{
    public bool LoginOk { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public int ErrorCode { get; set; } // 0 - Success, 1 - Invalid Token, 2 - Token Expired, 3 - Network
}

public class LogoutPacketRequired
{
    public string AccessToken { get; set; }
}

public class LogoutPacketResponse
{
    public bool LogoutOk { get; set; }
}

public class DeleteUserAccountPacketRequired
{
    public string AccessToken { get; set; }
}

public class DeleteUserAccountPacketResponse
{
    public bool DeleteOk { get; set; }
}

public class PolicyAgreedPacketRequired
{
    public string AccessToken { get; set; }
    public bool PolicyAgreed { get; set; }
}

public class PolicyAgreedPacketResponse
{
    public bool PolicyAgreedOk { get; set; }
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

public class ChangeActPacketSingleRequired
{
    public string AccessToken { get; set; }
    public int SessionId { get; set; }
    public int StageId { get; set; }
    public Faction Faction { get; set; }
}

public class ChangeActPacketSingleResponse
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

public class GetQueueCountsPacketRequired
{
    public string AccessToken { get; set; }
}

public class GetQueueCountsPacketResponse
{
    public bool GetQueueCountsOk { get; set; }
    public int QueueCountsSheep { get; set; }
    public int QueueCountsWolf { get; set; }
}

public class UpdateNamePacketRequired
{
    public string AccessToken { get; set; }
    public string NewName { get; set; }
}

public class UpdateNamePacketResponse
{
    public bool ChangeNameOk { get; set; }
    public int ErrorCode { get; set; } 
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
    public UserTutorialInfo UserTutorialInfo { get; set; }
    public Dictionary<int, int> ExpTable { get; set; }
}

public class LoadTestUserPacketRequired
{
    public int UserId { get; set; }
}

public class LoadTestUserPacketResponse
{
    public bool LoadTestUserOk { get; set; }
    public UserInfo UserInfo { get; set; }
    public UserTutorialInfo UserTutorialInfo { get; set; }
    public Dictionary<int, int> ExpTable { get; set; }
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

public class UpdateTutorialRequired
{
    public string AccessToken { get; set; }
    public TutorialType[] TutorialTypes { get; set; }
    public bool Done { get; set; }
    public int TutorialStep { get; set; }
}

public class UpdateTutorialResponse
{
    public bool UpdateTutorialOk { get; set; }
}

public class GetNoticeRequired
{
    public string AccessToken { get; set; }
    public string LanguageCode { get; set; }
}

public class GetNoticeResponse
{
    public bool GetNoticeOk { get; set; }
    public List<NoticeInfo> NoticeInfos { get; set; }
}

public class GetEventRequired
{
    public string AccessToken { get; set; }
    public string LanguageCode { get; set; }
}

public class GetEventResponse
{
    public bool GetEventOk { get; set; }
    public DateTime ServerNowUtc { get; set; }
    public List<EventInfo> EventInfos { get; set; } = new();
}

public class GetEventProgressRequired
{
    public string AccessToken { get; set; }
    public string LanguageCode { get; set; }
    public int EventId { get; set; }
}

public class GetEventProgressResponse
{
    public bool GetEventProgressOk { get; set; }
    public int EventId { get; set; }
    public string EventKey { get; set; } = "";
    public string CycleKey { get; set; } = "default";
    public int ProgressValue { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public List<TierInfo> TierInfos { get; set; } = new();
}

public class ClaimEventRewardRequired
{
    public string AccessToken { get; set; }
    public int EventId { get; set; }
    public int Tier { get; set; }
}

public class ClaimEventRewardResponse
{
    public bool ClaimOk { get; set; }
    public bool AlreadyClaimed { get; set; }
    public int EventId { get; set; }
    public int Tier { get; set; }
    public string CycleKey { get; set; } = "default";
    public string? Error { get; set; }
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
    public List<DailyProductInfo> DailyProducts { get; set; }
    public ProductInfo AdsRemover { get; set; }
    public DateTime RefreshTime { get; set; }
}

public class FriendListPacketRequired
{
    public string AccessToken { get; set; }
}

public class FriendListPacketResponse
{
    public bool FriendListOk { get; set; }
    public List<FriendUserInfo> FriendList { get; set; }
}

public class SearchUsernamePacketRequired
{
    public string AccessToken { get; set; }
    public string Username { get; set; }
}

public class SearchUsernamePacketResponse
{
    public bool SearchUsernameOk { get; set; }
    public List<FriendUserInfo> FriendUserInfos { get; set; }
} 

public class FriendRequestPacketRequired
{
    public string AccessToken { get; set; }
    public string FriendUserTag { get; set; }
    public FriendStatus CurrentFriendStatus { get; set; }
}

public class FriendRequestPacketResponse
{
    public bool FriendRequestOk { get; set; }
    public FriendStatus FriendStatus { get; set; }
}

public class LoadPendingFriendPacketRequired
{
    public string AccessToken { get; set; }
}

public class LoadPendingFriendPacketResponse
{
    public bool LoadPendingFriendOk { get; set; }
    public List<FriendUserInfo> PendingFriendList { get; set; }
    public List<FriendUserInfo> SendingFriendList { get; set; }
}

public class AcceptFriendPacketRequired
{
    public string AccessToken { get; set; }
    public string FriendUserTag { get; set; }
    public bool Accept { get; set; }
}

public class AcceptFriendPacketResponse
{
    public bool AcceptFriendOk { get; set; }
    public bool Accept { get; set; }
}

public class LoadPendingMailPacketRequired
{
    public string AccessToken { get; set; }
}

public class LoadPendingMailPacketResponse
{
    public bool LoadPendingMailOk { get; set; }
    public List<MailInfo> PendingMailList { get; set; }
}

public class ClaimMailPacketRequired
{
    public string AccessToken { get; set; }
    public int MailId { get; set; }
}

public class ClaimMailPacketResponse
{
    public bool ClaimMailOk { get; set; }
    public bool IsProductMail { get; set; }
    public int ProductId { get; set; }
}

public class LoadInvitableFriendPacketRequired
{
    public string AccessToken { get; set; }
}

public class LoadInvitableFriendPacketResponse
{
    public bool LoadInvitableFriendOk { get; set; }
    public List<FriendUserInfo> InvitableFriends { get; set; }
    public List<FriendUserInfo> Others { get; set; }
}

public class InviteFriendlyMatchPacketRequired
{
    public string AccessToken { get; set; }
    public string InviteeTag { get; set; }
}

public class InviteFriendlyMatchPacketResponse
{
    public bool InviteOk { get; set; }
}

public class AcceptInvitationPacketRequired
{
    public string AccessToken { get; set; }
    public bool Accept { get; set; }
    public int MailId { get; set; }
}

public class AcceptInvitationPacketResponse
{
    public bool AcceptInvitationOk { get; set; }
    public Faction MyFaction { get; set; }
    public UserInfo EnemyInfo { get; set; }
    public DeckInfo EnemyDeckSheep { get; set; }
    public DeckInfo EnemyDeckWolf { get; set; }
}

public class DeleteReadMailPacketRequired
{
    public string AccessToken { get; set; }
}

public class DeleteReadMailPacketResponse
{
    public bool DeleteReadMailOk { get; set; }
}

public class VirtualPaymentPacketRequired
{
    public string AccessToken { get; set; }
    public string ProductCode { get; set; }
}

public class VirtualPaymentPacketResponse
{
    public bool PaymentOk { get; set; }
    public VirtualPaymentCode PaymentCode { get; set; }
    public CurrencyType CurrencyType { get; set; }
}

public class DailyPaymentPacketRequired
{
    public string AccessToken { get; set; }
    public string ProductCode { get; set; }
}

public class DailyPaymentPacketResponse
{
    public bool PaymentOk { get; set; }
    public int Slot { get; set; }
}

public class CashPaymentPacketRequired
{
    public string AccessToken { get; set; }
    public string Receipt { get; set; }
    public string ProductCode { get; set; }
}

public class CashPaymentPacketResponse
{
    public bool PaymentOk { get; set; }
    public CashPaymentErrorCode ErrorCode { get; set; }
}

public class SelectProductPacketRequired
{
    public string AccessToken { get; set; }
    public CompositionInfo SelectedCompositionInfo { get; set; }
}

public class OpenProductPacketRequired
{
    public string AccessToken { get; set; }
    public int ProductId { get; set; }
    public bool OpenAll { get; set; }
}

public class ContinueClaimPacketRequired
{
    public string AccessToken { get; set; }
    public int MailId { get; set; } = 0;
}

public class ClaimProductPacketResponse
{
    public bool ClaimOk { get; set; }
    public List<ProductInfo> ProductInfos { get; set; }
    public List<TotalProductInfo> RandomProductInfos { get; set; }
    public List<CompositionInfo> CompositionInfos { get; set; }
    public RewardPopupType RewardPopupType { get; set; }
}

public class LoadStageInfoPacketRequired
{
    public string AccessToken { get; set; }
}

public class LoadStageInfoPacketResponse
{
    public bool LoadStageInfoOk { get; set; }
    public List<UserStageInfo> UserStageInfos { get; set; }
    public List<StageEnemyInfo> StageEnemyInfos { get; set; }
    public List<StageRewardInfo> StageRewardInfos { get; set; }
}

public class GetDailyProductRefreshTimePacketRequired
{
    public string AccessToken { get; set; }
}

public class GetDailyProductRefreshTimePacketResponse
{
    public bool GetRefreshTimeOk { get; set; }
    public DateTime RefreshAt { get; set; }
}

public class RevealDailyProductPacketRequired
{
    public string AccessToken { get; set; }
    public int Slot { get; set; }
}

public class RevealDailyProductPacketResponse
{
    public bool RevealDailyProductOk { get; set; }
    public List<DailyProductInfo> DailyProductInfos { get; set; }
}

public class RefreshDailyProductPacketRequired
{
    public string AccessToken { get; set; }
}

public class RefreshDailyProductPacketResponse
{
    public bool RefreshDailyProductOk { get; set; }
    public List<DailyProductInfo> DailyProducts { get; set; }
    public DateTime RefreshTime { get; set; }
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
    public int SessionId { get; set; }
}
    
public class MatchCancelPacketResponse
{
    public int UserId { get; set; }
}

public class ReportQueueCountsRequired
{
    public int SheepQueueCount { get; set; }
    public int WolfQueueCount { get; set; }
}

public class ReportQueueCountsResponse
{
    public bool ReportQueueCountsOk { get; set; }
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

public class FriendlyMatchPacketRequired
{
    public int SheepUserId { get; set; }
    public int SheepSessionId { get; set; }
    public string SheepUserName { get; set; }
    public int WolfUserId { get; set; }
    public int WolfSessionId { get; set; }
    public string WolfUserName { get; set; }
    public int MapId { get; set; }
    public CharacterId SheepCharacterId { get; set; }
    public CharacterId WolfCharacterId { get; set; }
    public SheepId SheepId { get; set; }
    public EnchantId EnchantId { get; set; }
    public UnitId[] SheepUnitIds { get; set; }
    public UnitId[] WolfUnitIds { get; set; }
}

public class FriendlyMatchPacketResponse
{
    public bool IsSuccess { get; set; }
}

public class SinglePlayStartPacketRequired
{
    public int UserId { get; set; }
    public Faction UserFaction { get; set; }
    public UnitId[] UnitIds { get; set; }
    public int CharacterId { get; set; }
    public int AssetId { get; set; }
    public UnitId[] EnemyUnitIds { get; set; }
    public int EnemyCharacterId { get; set; }
    public int EnemyAssetId { get; set; }
    public int MapId { get; set; }
    public int SessionId { get; set; }
    public int StageId { get; set; }
}

public class SinglePlayStartPacketResponse
{
    public bool SinglePlayStartOk { get; set; }
}

public class TutorialStartPacketRequired
{
    public int UserId { get; set; }
    public Faction UserFaction { get; set; }
    public UnitId[] UnitIds { get; set; }
    public int CharacterId { get; set; }
    public int AssetId { get; set; }
    public int EnemyCharacterId { get; set; }
    public int EnemyAssetId { get; set; }
    public int MapId { get; set; }
    public int SessionId { get; set; }
}

public class TutorialStartPacketResponse
{
    public bool TutorialStartOk { get; set; }
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

public class RankGameRewardPacketRequired
{
    public int WinUserId { get; set; }
    public int WinRankPoint { get; set; }
    public int LoseUserId { get; set; }
    public int LoseRankPoint { get; set; }
}

public class RankGameRewardPacketResponse
{
    public bool GetGameRewardOk { get; set; }
    public List<RewardInfo> WinnerRewards { get; set; }
    public List<RewardInfo> LoserRewards { get; set; }
}

public class SingleGameRewardPacketRequired
{
    public int UserId { get; set; }
    public int StageId { get; set; }
    public int Star { get; set; }
}

public class SingleGameRewardPacketResponse
{
    public bool GetGameRewardOk { get; set; }
    public List<SingleRewardInfo> Rewards { get; set; }
}

public class TutorialRewardPacketRequired
{
    public int UserId { get; set; }
    public Faction Faction { get; set; }
}

public class TutorialRewardPacketResponse
{
    public bool GetGameRewardOk { get; set; }
    public List<RewardInfo> Rewards { get; set; }
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

public class SendEventProgressPacketRequired
{
    public List<int> UserIds { get; set; }
    public int RoomId { get; set; }
    public string EventKey { get; set; }
    public EventCounterKey CounterKey { get; set; }
}

public class SendEventProgressPacketResponse
{
    public bool SendEventProgressOk { get; set; }
}

#endregion

#region For Web

public class PublishNoticeRequired
{
    public bool IsPinned { get; set; } = false;
    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public List<LocalizationInfo> Localizations { get; set; } = new();
}

public class PublishEventRequired
{
    public string EventKey { get; set; } = "";
    public int Version { get; set; } = 1;
    public bool IsPinned { get; set; } = false;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public EventRepeatType RepeatType { get; set; } = EventRepeatType.None;
    public string? RepeatTimeZone { get; set; } = "UTC";
    public int Priority { get; set; } = 0;
    
    public List<LocalizationInfo> Localizations { get; set; } = new();
    public List<TierInfo> Tiers { get; set; } = new();
}

#endregion
