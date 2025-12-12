namespace ApiServer.DB;

#region Enum

public enum Env
{
    Local,
    Dev,
    Stage,
    Prod
}

public enum MatchType
{
    Friendly,
    Rank,
}

public enum UserRole
{
    User,
    Admin
}

public enum UserState
{
    Activate,
    Deactivate,
    Suspension,
    Banned
}

public enum AuthProvider
{
    Guest,
    Direct,
    Google,
    Apple
}

public enum TutorialType
{
    None,
    BattleWolf,
    BattleSheep,
    ChangeFaction,
    Collection,
    Reinforce,
}

public enum UserAct
{
    Pending,
    MatchMaking,
    InSingleGame,
    InMultiGame,
    InRankGame,
    InCustomGame,
    InTutorial,
    InLobby,
    Offline,
}

public enum FriendStatus
{
    None,
    Pending,
    Accepted,
    Blocked
}

public enum MailType
{
    None,
    Notice,
    Invite,
    Product,
}

public enum UnitClass
{
    None,
    Peasant,
    Squire,
    Knight,
    NobleKnight,
    Baron,
    Count,
    Duke,
}

public enum UnitRole
{
    None,
    Warrior,
    Ranger,
    Mage,
    Supporter,
    Tanker
}

public enum UnitRegion
{
    None = 0,
    Mistykile = 1,
    Zumarigloom = 2,
    Voltenar = 3,
    Iscreh = 4,
    Grellude = 5,
    Sandibreeze = 6,
    Bysscaligo = 7
}

public enum MaterialId
{
    None = 2000,
    Bristles = 2001,
    DownFeather = 2002,
    Feather = 2003,
    GuardHair = 2004,
    Hairball = 2005,
    CardPlatePeasant = 2006,
    CardPlateSquire = 2007,
    CardPlateKnight = 2008,
    CardPlateNobleKnight = 2009,
    CardPlateBaron = 2010,
    CardPlateEarl = 2011,
    CardPlateDuke = 2012,
    ClayDawn = 2013,
    ClayEarth = 2014,
    ClayFire = 2015,
    ClayForest = 2016,
    ClayRock = 2017,
    ClayWater = 2018,
    LeatherLowGrade = 2019,
    LeatherMidGrade = 2020,
    LeatherHighGrade = 2021,
    LeatherTopGrade = 2022,
    PigmentBlack = 2023,
    PigmentBlue = 2024,
    PigmentGreen = 2025,
    PigmentPurple = 2026,
    PigmentRed = 2027,
    PigmentYellow = 2028,
    SoulPowderBysscaligo = 2029,
    SoulPowderGrellude = 2030,
    SoulPowderIscreh = 2031,
    SoulPowderMistykile = 2032,
    SoulPowderSandibreeze = 2033,
    SoulPowderVoltenar = 2034,
    SoulPowderZumarigloom = 2035,
    RainbowEgg = 2036,
}

public enum CurrencyId
{
    None,
    Gold = 4001,
    Spinel = 4002
}

public enum Faction
{
    None,
    Sheep,
    Wolf
}

public enum UnitId
{
    UnknownUnit = 0,
    Bunny = 101,
    Rabbit = 102,
    Hare = 103,
    Mushroom = 104,
    Fungi = 105,
    Toadstool = 106,
    Seed = 107,
    Sprout = 108,
    FlowerPot = 109,
    Bud = 110,
    Bloom = 111,
    Blossom = 112,
    PracticeDummy = 113,
    TargetDummy = 114,
    TrainingDummy = 115,
    Shell = 116,
    Spike = 117,
    Hermit = 118,
    SunBlossom = 119, 
    SunflowerFairy = 120,
    SunfloraPixie = 121,
    MothLuna = 122,
    MothMoon = 123,
    MothCelestial = 124,
    Soul = 125,
    Haunt = 126,
    SoulMage = 127,
    DogPup = 501,
    DogBark = 502,
    DogBowwow = 503,
    Burrow = 504,
    MoleRat = 505,
    MoleRatKing = 506,
    MosquitoBug = 507,
    MosquitoPester = 508,
    MosquitoStinger = 509,
    WolfPup = 510,
    Wolf = 511,
    Werewolf = 512,
    Bomb = 513,
    SnowBomb = 514, 
    PoisonBomb = 515,
    Cacti = 516,
    Cactus = 517,
    CactusBoss = 518,
    Snakelet = 519,
    Snake = 520,
    SnakeNaga = 521,
    Lurker = 522,
    Creeper = 523,
    Horror = 524,
    Skeleton = 525,
    SkeletonGiant = 526,
    SkeletonMage = 527
}

public enum SheepId
{
    PrimeSheepWhite = 901,
    PrimeSheepPink = 903,
    PrimeSheepBack = 905,
}

public enum EnchantId
{
    Wind = 1001,
    Fire = 1002,
    Earth = 1003,
}

public enum CharacterId
{
    PlayerCharacterBasic = 1101,
    PlayerCharacter2 = 1102,
    PlayerCharacter3 = 1103,
    PlayerCharacter4 = 1104,
    PlayerCharacter5 = 1105,
}

public enum ProductType
{
    Container = 0, // Other Product e.g. packages
    Unit = 1,
    Material = 2,
    Enchant = 3,
    Sheep = 4,
    Character = 5,
    Gold = 6,
    Spinel = 7,
    Exp = 8,
    Subscription = 9,
}

public enum ProductCategory
{
    // In shop category
    None = 0,
    SpecialPackage = 1,
    BeginnerPackage = 2,
    GoldStore = 3,
    SpinelStore = 4,
    GoldPackage = 5,
    SpinelPackage = 6,
    ReservedSale = 7,
    DailyDeal = 8,
    Pass = 9,
    Other = 100,
}

public enum ProductOpenType
{
    None = 0,
    Single = 1,
    Random = 2,
    Select = 3,
    Subscription = 4,
}

public enum AcquisitionPath
{
    None = 0,
    Shop = 1,
    Reward = 2,
    Rank = 3,
    Single = 4,
    Mission = 5,
    Tutorial = 6,
    Event = 7,
    // 현재 open중인 패키지
    // ex) Beginner's Ambition(7)과 Top Grade Material Box(21) 를 동시 개봉하는 경우
    // 7-21은 한번 이미 순회했으니 none, 21은 지금 열어야 하니 open
    Open = 8, 
}

public enum SubscriptionType
{
    None = 0, 
    SeasonPass = 1,
    AdsRemover = 2,
}

public enum SubscriptionEvent
{
    None = 0,
    Started = 1,
    Renewed = 2,
    Canceled = 3,
    Expired = 4,
}

public enum RewardPopupType
{
    None = 0, // from result popup, to main lobby
    Item = 1, // all item popup
    Select = 2, // select popup
    Open = 3, // random open popup 
    OpenResult = 4,
    Subscription = 5, // subscription reward popup
}

public enum CurrencyType
{
    None,
    Cash,
    Gold,
    Spinel
}

public enum CashCurrencyType
{
    None,
    KRW,
    USD,
    JPY,
    CNY,
    EUR,
    GBP, // Great Britain Pound
    CAD, // Canadian Dollar
    AUD, // Australian Dollar
    NZD, // New Zealand Dollar
    CHF, // Swiss Franc
    SEK, // Swedish Krona
    DKK, // Danish Krone
    NOK, // Norwegian Krone
    ZAR, // South African Rand
    RUB, // Russian Ruble
    BRZ, // Brazilian Real
    MXN, // Mexican Peso
    INR, // Indian Rupee
    IDR, // Indonesian Rupiah
    VNĐ, // Vietnamese Dong
    THB, // Thai Baht
}

public enum TransactionStatus
{
    None,
    Completed,
    Refunded,
    Cancelled,
    Failed,
    Pending,
    Expired,
    PartiallyRefunded,
}

public enum StoreType
{
    None,
    GooglePlay,
    AppStore,
}

public enum CashPaymentErrorCode
{
    None = 0,
    InvalidReceipt = 1,     // 위조/만료/포맷 오류 - 재시도x
    Unauthorized = 2,       // 토큰 만료/권한 오류 - 재로그인
    AlreadyProcessed = 3,   // 이미 처리된 영수증 - 멱등성 처리용
    InternalError = 4,      // 서버 내부 오류 - 재시도
}

public enum VirtualPaymentCode
{
    None,
    Product,
    Subscription
}

public enum NoticeType
{
    None,
    Notice,
    Event,
    Emergency
}

#endregion