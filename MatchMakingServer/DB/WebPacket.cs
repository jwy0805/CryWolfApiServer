namespace MatchMakingServer.DB;

#region For API Server

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

public class MatchSuccessPacketRequired
{
    public int SheepUserId { get; set; }
    public int WolfUserId { get; set; }
    public int MapId { get; set; }
}

public class MatchSuccessPacketResponse
{
    public bool IsSuccess { get; set; }
}

#endregion