namespace MatchMakingServer.DB;

public class TestApiToMatchRequired
{
    public bool Test { get; set; }
}

public class TestApiToMatchResponse
{
    public bool TestOk { get; set; }
}

#region For API Server

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

public class ReportQueueCountsRequired
{
    public int QueueCountsSheep { get; set; }
    public int QueueCountsWolf { get; set; }
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

public class MatchSuccessPacketRequired
{
    public bool IsTestGame { get; set; }
    public int SheepUserId { get; set; }
    public int SheepSessionId { get; set; }
    public string SheepUserName { get; set; }
    public int WolfUserId { get; set; }
    public int WolfSessionId { get; set; }
    public string WolfUserName { get; set; }
    public int MapId { get; set; }
    public int SheepRankPoint { get; set; }
    public int WolfRankPoint { get; set; }
    public int WinPointSheep { get; set; }
    public int WinPointWolf { get; set; }
    public int LosePointSheep { get; set; }
    public int LosePointWolf { get; set; }
    public CharacterId SheepCharacterId { get; set; }
    public CharacterId WolfCharacterId { get; set; }
    public SheepId SheepId { get; set; }
    public EnchantId EnchantId { get; set; }
    public UnitId[] SheepUnitIds { get; set; }
    public UnitId[] WolfUnitIds { get; set; }
    public List<int> SheepAchievements { get; set; }
    public List<int> WolfAchievements { get; set; }
}

public class MatchSuccessPacketResponse
{
    public bool IsSuccess { get; set; }
}

#endregion