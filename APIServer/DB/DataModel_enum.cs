namespace AccountServer.DB;

#region Enum

public enum Env
{
    Local,
    Dev,
    Stage,
    Prod
}

public enum UserRole
{
    Admin,
    User
}

public enum UserState
{
    Activate,
    Deactivate,
    Suspension,
    Ban
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
    InLobby
}

public enum UnitClass
{
    None,
    Peasant,
    Squire,
    Knight,
    HighRankingKnight,
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
    None = 0,
    Bristles = 1,
    DownFeather = 2,
    Feather = 3,
    GuardHair = 4,
    Hairball = 5,
    CardFramePeasant = 6,
    CardFameSquire = 7,
    CardFrameKnight = 8,
    CardFrameNobleKnight = 9,
    CardFrameBaron = 10,
    CardFrameEarl = 11,
    CardFrameDuke = 12,
    ClayDawn = 13,
    ClayEarth = 14,
    ClayFire = 15,
    ClayForest = 16,
    ClayRock = 17,
    ClayWater = 18,
    LeatherLowGrade = 19,
    LeatherMidGrade = 20,
    LeatherHighGrade = 21,
    LeatherTopGrade = 22,
    PigmentBlack = 23,
    PigmentBlue = 24,
    PigmentGreen = 25,
    PigmentPurple = 26,
    PigmentRed = 27,
    PigmentYellow = 28,
    SoulPowderBysscaligo = 29,
    SoulPowderGrellude = 30,
    SoulPowderIscreh = 31,
    SoulPowderMistykile = 32,
    SoulPowderSandibreeze = 33,
    SoulPowderVoltenar = 34,
    SoulPowderZumarigloom = 35,
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
    PlayerCharacter = 2001,
}

#endregion