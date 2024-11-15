namespace AccountServer.Services;

public struct MatchInfo
{
    public int SheepUserId { get; set; }
    public int SheepSessionId { get; set; }
    public int WolfUserId { get; set; }
    public int WolfSessionId { get; set; }
}

public class MatchService
{
    private ulong _dailyMatchId;
    private DateTime _lastDate = DateTime.UtcNow.Date;
    private Dictionary<ulong, MatchInfo> _matchInfos = new();
    private Dictionary<int, ulong> _userToMatchId = new();
    
    public void AddMatchInfo(int sheepUserId, int sheepSessionId, int wolfUserId, int wolfSessionId)
    {
        var matchId = GenerateMatchId();
        var matchUsers = new MatchInfo
        {
            SheepUserId = sheepUserId,
            SheepSessionId = sheepSessionId,
            WolfUserId = wolfUserId,
            WolfSessionId = wolfSessionId
        };
        
        if (_userToMatchId.ContainsKey(sheepUserId) || _userToMatchId.ContainsKey(wolfUserId))
        {
            Console.WriteLine("User already in match");
            return;
        }
        
        if (sheepUserId == wolfUserId)
        {
            // test match
            _userToMatchId.Add(sheepUserId, matchId);
            _matchInfos.Add(matchId, matchUsers);
            return;
        }
        
        _userToMatchId.Add(sheepUserId, matchId);
        _userToMatchId.Add(wolfUserId, matchId);
        _matchInfos.Add(matchId, matchUsers);
    }
    
    public void RemoveMatchInfo(int userId)
    {
        _userToMatchId.TryGetValue(userId, out var matchId);
        _matchInfos.TryGetValue(matchId, out var matchInfo);
        _userToMatchId.Remove(matchInfo.SheepUserId);
        _userToMatchId.Remove(matchInfo.WolfUserId);
        _matchInfos.Remove(matchId);
    }
    
    public MatchInfo? GetMatchInfo(ulong matchId)
    {
        if (!_matchInfos.Remove(matchId, out var users)) return null;
        return users;
    }
    
    public MatchInfo? FindMatchInfo(int userId)
    {
        _userToMatchId.TryGetValue(userId, out var matchId);
        _matchInfos.TryGetValue(matchId, out var matchInfo);
        return matchInfo;
    }
    
    private ulong GenerateMatchId()
    {
        if (DateTime.UtcNow.Date > _lastDate)
        {
            _dailyMatchId = 0;
            _lastDate = DateTime.UtcNow.Date;
        }

        ulong matchType = 0;
        string datePart = DateTime.UtcNow.ToString("yyMMdd");
        ulong matchCount = _dailyMatchId++;
        string matchIdString = $"{matchType}{datePart}{matchCount:D11}";
        return ulong.Parse(matchIdString);
    }
}

