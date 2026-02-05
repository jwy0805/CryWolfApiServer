using System.Collections.Concurrent;

namespace ApiServer.Services;

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
    private readonly ConcurrentDictionary<ulong, MatchInfo> _matchInfos = new();
    private readonly ConcurrentDictionary<int, ulong> _userToMatchId = new();
    
    public bool AddMatchInfo(int sheepUserId, int sheepSessionId, int wolfUserId, int wolfSessionId)
    {
        var matchId = GenerateMatchId();
        var matchUsers = new MatchInfo
        {
            SheepUserId = sheepUserId,
            SheepSessionId = sheepSessionId,
            WolfUserId = wolfUserId,
            WolfSessionId = wolfSessionId
        };

        if (sheepUserId == wolfUserId)
        {
            // test match: 한 유저만 등록
            if (!_userToMatchId.TryAdd(sheepUserId, matchId))
                return false;

            if (!_matchInfos.TryAdd(matchId, matchUsers))
            {
                _userToMatchId.TryRemove(sheepUserId, out _);
                return false;
            }

            return true;
        }

        // sheep 선점
        if (!_userToMatchId.TryAdd(sheepUserId, matchId))
            return false;

        // wolf 실패 -> sheep 롤백
        if (!_userToMatchId.TryAdd(wolfUserId, matchId))
        {
            _userToMatchId.TryRemove(sheepUserId, out _);
            return false;
        }

        // 등록 실패 -> 두 유저 롤백
        if (!_matchInfos.TryAdd(matchId, matchUsers))
        {
            _userToMatchId.TryRemove(sheepUserId, out _);
            _userToMatchId.TryRemove(wolfUserId, out _);
            return false;
        }

        return true;
    }
    
    public bool RemoveMatchInfo(int userId)
    {
        if (!_userToMatchId.TryRemove(userId, out var matchId))
            return false;

        // matchInfo를 꺼내서 양쪽 유저 제거 (userId가 sheep/wolf 중 어느쪽이든 가능)
        if (_matchInfos.TryGetValue(matchId, out var matchInfo))
        {
            _userToMatchId.TryRemove(matchInfo.SheepUserId, out _);
            _userToMatchId.TryRemove(matchInfo.WolfUserId, out _);
        }

        // matchInfos 제거
        _matchInfos.TryRemove(matchId, out _);
        return true;
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

