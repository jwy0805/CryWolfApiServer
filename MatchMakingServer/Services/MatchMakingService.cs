using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using MatchMakingServer.DB;
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable once CollectionNeverUpdated.Local

namespace AccountServer.Services;

public class MatchMakingService : BackgroundService
{
    private readonly ILogger<MatchMakingService> _logger;
    private readonly JobService _jobService;
    private readonly ApiService _apiService;
    private readonly Dictionary<int, PriorityQueue<MatchMakingPacketRequired, int>> _sheepUserQueues = new();
    private readonly Dictionary<int, PriorityQueue<MatchMakingPacketRequired, int>> _wolfUserQueues = new();
    private readonly Dictionary<int, int> _latestSessionByUser = new();
    private readonly Dictionary<int, int> _canceledSessionByUser = new();
    private readonly Dictionary<(int UserId, int SessionId), int> _retryCount = new();
    private readonly HashSet<(int UserId, int SessionId)> _inQueueKeys = new();
    
    private const int MaxRetryCount = 3;
    
    public MatchMakingService(ILogger<MatchMakingService> logger, JobService jobService, ApiService apiService)
    {
        _logger = logger;
        _jobService = jobService;
        _apiService = apiService;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _jobService.Push(() =>
            {
                var pairs = ConfirmMatch();
                foreach (var (sheep, wolf) in pairs)
                {
                    _ = ProcessMatchRequestAsync(sheep, wolf);
                }
            });
            
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private void EnsureQueues(int mapId)
    {
        if (!_sheepUserQueues.ContainsKey(mapId))
        {
            _sheepUserQueues[mapId] = new PriorityQueue<MatchMakingPacketRequired, int>();
        }

        if (!_wolfUserQueues.ContainsKey(mapId))
        {
            _wolfUserQueues[mapId] = new PriorityQueue<MatchMakingPacketRequired, int>();
        }
    }

    private bool IsStaleOrCanceled(MatchMakingPacketRequired required)
    {
        // 최신 세션 아니면 폐기
        if (_latestSessionByUser.TryGetValue(required.UserId, out var latest) && latest != required.SessionId) 
            return true;
        
        // 취소된 세션 폐기
        if (_canceledSessionByUser.TryGetValue(required.UserId, out var canceled) && canceled == required.SessionId)
            return true;

        return false;
    }

    private List<(MatchMakingPacketRequired Sheep, MatchMakingPacketRequired Wolf)> ConfirmMatch()
    {
        var results = new List<(MatchMakingPacketRequired, MatchMakingPacketRequired)>();

        foreach (var mapId in _sheepUserQueues.Keys.ToArray())
        {
            if (!_wolfUserQueues.ContainsKey(mapId)) continue;
            
            var sheepQ = _sheepUserQueues[mapId];
            var wolfQ = _wolfUserQueues[mapId];

            while (sheepQ.Count > 0 && wolfQ.Count > 0)
            {
                var match = FindMatch(mapId);
                if (match == null) break;
                results.Add(match.Value);
            }
        }

        return results;
    }

    private (MatchMakingPacketRequired Sheep, MatchMakingPacketRequired Wolf)? FindMatch(int mapId)
    {
        var sheepQ = _sheepUserQueues[mapId];
        var wolfQ = _wolfUserQueues[mapId];

        while (true)
        {
            if (sheepQ.Count == 0 || wolfQ.Count == 0) return null;
            
            var sheepPeek = sheepQ.Peek();
            if (IsStaleOrCanceled(sheepPeek))
            {
                sheepQ.Dequeue();
                _inQueueKeys.Remove((sheepPeek.UserId, sheepPeek.SessionId));
                
                if (_canceledSessionByUser.TryGetValue(sheepPeek.UserId, out var canceled) &&
                    canceled == sheepPeek.SessionId)
                {
                    _canceledSessionByUser.Remove(sheepPeek.UserId);
                }
                continue;
            }
            
            var wolfPeek = wolfQ.Peek();
            if (IsStaleOrCanceled(wolfPeek))
            {
                wolfQ.Dequeue();
                _inQueueKeys.Remove((wolfPeek.UserId, wolfPeek.SessionId));
                
                if (_canceledSessionByUser.TryGetValue(wolfPeek.UserId, out var canceled) &&
                    canceled == wolfPeek.SessionId)
                {
                    _canceledSessionByUser.Remove(wolfPeek.UserId);
                }
                continue;
            }
            
            var sheep = sheepQ.Dequeue();
            var wolf = wolfQ.Dequeue();
            _inQueueKeys.Remove((sheep.UserId, sheep.SessionId));
            _inQueueKeys.Remove((wolf.UserId, wolf.SessionId));

            Console.WriteLine($"Find Match: Sheep {sheep.UserId} vs Wolf {wolf.UserId}");
            
            return (sheep, wolf);
        }
    }

    private bool TryIncreaseRetry(MatchMakingPacketRequired required)
    {
        var key = (required.UserId, required.SessionId);
        _retryCount.TryGetValue(key, out var count);
        if (count >= MaxRetryCount) return false;
        _retryCount[key] = count + 1;
        return true;
    }

    private void RequeueIfStillValid(MatchMakingPacketRequired required)
    {
        // 최신 세션이 아니면 재큐잉x (이미 재진입 or 새 요청)
        if (_latestSessionByUser.TryGetValue(required.UserId, out var latest) && latest != required.SessionId) 
            return;
            
        // 취소된 세션 재큐잉x
        if (_canceledSessionByUser.TryGetValue(required.UserId, out var canceled) && canceled == required.SessionId)
            return;
            
        var key = (required.UserId, required.SessionId);
        if (_inQueueKeys.Contains(key)) return;
        
        EnsureQueues(required.MapId);

        if (required.Faction == Faction.Sheep)
        {
            _sheepUserQueues[required.MapId].Enqueue(required, required.RankPoint);
        }
        else
        {
            _wolfUserQueues[required.MapId].Enqueue(required, required.RankPoint);
        }
        
        _inQueueKeys.Add(key);
    }
    
    private async Task ProcessMatchRequestAsync(MatchMakingPacketRequired sheepRequired, MatchMakingPacketRequired wolfRequired)
    {
        try
        {
            var getRankPointPacket = new GetRankPointPacketRequired
            {
                SheepUserId = sheepRequired.UserId,
                WolfUserId = wolfRequired.UserId
            };
            
            var rankPointResponse = await _apiService
                .SendRequestToApiAsync<GetRankPointPacketResponse>(
                    "Match/GetRankPoint", getRankPointPacket, HttpMethod.Post);
           
            if (rankPointResponse == null)
            {
                _logger.LogError("GetRankPoint failed. Requeue attempt.");
                _jobService.Push(() =>
                {
                    if (TryIncreaseRetry(sheepRequired) && TryIncreaseRetry(wolfRequired))
                    {
                        RequeueIfStillValid(sheepRequired);
                        RequeueIfStillValid(wolfRequired);
                    }
                });
                return;
            }
                
            // Http Transfer of Socket Server
            var matchSuccessPacket = new MatchSuccessPacketRequired
            {
                IsAiSimulation = sheepRequired.IsAi && wolfRequired.IsAi,
                SheepUserId = sheepRequired.UserId,
                SheepSessionId = sheepRequired.SessionId,
                SheepUserName = sheepRequired.UserName,
                WolfUserId = wolfRequired.UserId,
                WolfSessionId = wolfRequired.SessionId,
                WolfUserName = wolfRequired.UserName,
                MapId = sheepRequired.MapId,
                SheepRankPoint = sheepRequired.RankPoint,
                WolfRankPoint = wolfRequired.RankPoint,
                WinPointSheep = rankPointResponse.WinPointSheep,
                WinPointWolf = rankPointResponse.WinPointWolf,
                LosePointSheep = rankPointResponse.LosePointSheep,
                LosePointWolf = rankPointResponse.LosePointWolf,
                SheepCharacterId = (CharacterId)sheepRequired.CharacterId,
                WolfCharacterId = (CharacterId)wolfRequired.CharacterId,
                SheepId = (SheepId)sheepRequired.AssetId,
                EnchantId = (EnchantId)wolfRequired.AssetId,
                SheepUnitIds = sheepRequired.UnitIds,
                WolfUnitIds = wolfRequired.UnitIds,
                SheepAchievements = sheepRequired.Achievements,
                WolfAchievements = wolfRequired.Achievements
            };

            await _apiService.SendRequestToSocketAsync("match", matchSuccessPacket, HttpMethod.Post);
            
            _jobService.Push(() =>
            {
                var mapId = sheepRequired.MapId;
                var sheepCount = _sheepUserQueues.TryGetValue(mapId, out var sq) ? sq.Count : 0;
                var wolfCount  = _wolfUserQueues.TryGetValue(mapId, out var wq) ? wq.Count : 0;
                // _logger.LogInformation($"Matched ... count in queue: {sheepCount} + {wolfCount}");
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "ProcessMatchRequest failed. Requeue attempt.");
        }
    }

    public void AddMatchRequest(MatchMakingPacketRequired packet, bool test = false)
    {
        if (test)
        {
            Console.WriteLine($"user {packet.UserId} {packet.Faction} : session {packet.SessionId} test match");
            _ = ProcessTestMatchRequest(packet);
            return;
        }
        
        _jobService.Push(() =>
        {
            _latestSessionByUser[packet.UserId] = packet.SessionId;

            // 다른 세션 취소 정보 정리
            if (_canceledSessionByUser.TryGetValue(packet.UserId, out var canceled) && canceled != packet.SessionId)
            {
                _canceledSessionByUser.Remove(packet.UserId);
            }
            
            // 취소된 세션 무시
            if (_canceledSessionByUser.TryGetValue(packet.UserId, out canceled) && canceled == packet.SessionId) return;
            
            // 중복 처리
            var key = (packet.UserId, packet.SessionId);
            if (_inQueueKeys.Contains(key)) return;
            
            EnsureQueues(packet.MapId);
            
            if (packet.Faction == Faction.Sheep)
            {
                _sheepUserQueues[packet.MapId].Enqueue(packet, packet.RankPoint);
            }
            else
            {
                _wolfUserQueues[packet.MapId].Enqueue(packet, packet.RankPoint);
            }

            _inQueueKeys.Add(key);
            _logger.LogInformation($"user {packet.UserId} {packet.Faction} : session {packet.SessionId} Enqueued for match, count in queue: {_sheepUserQueues[packet.MapId].Count} + ${_wolfUserQueues[packet.MapId].Count}");
        });
    }
    
    public int CancelMatchRequest(MatchCancelPacketRequired packet)
    {
        _jobService.Push(() =>
        {
            _canceledSessionByUser[packet.UserId] = packet.SessionId;
            _retryCount.Remove((packet.UserId, packet.SessionId));
            _inQueueKeys.Remove((packet.UserId, packet.SessionId));
            _logger.LogInformation($"user {packet.UserId} : session {packet.SessionId} Canceled match request, count in queue");
        });

        return packet.UserId;
    }
    
    private async Task ProcessTestMatchRequest(MatchMakingPacketRequired required)
    {
        try
        {
            _logger.LogInformation("Test Match Requested User {UserId} (Faction {Faction}, RankPoint {RankPoint})",
                required.UserId, required.Faction, required.RankPoint);

            var getRankPointPacket = new GetRankPointPacketRequired
            {
                SheepUserId = required.UserId,
                WolfUserId = required.UserId
            };

            var rankPointResponse = await _apiService
                .SendRequestToApiAsync<GetRankPointPacketResponse>("Match/GetRankPoint", getRankPointPacket, HttpMethod.Post);

            if (rankPointResponse == null)
            {
                _logger.LogError("Failed to get rank point (test match).");
                return;
            }
            
            MatchMakingPacketRequired userPacket;
            MatchSuccessPacketRequired matchSuccessPacket;
            if (required.Faction == Faction.Wolf)
            {
                userPacket = _sheepUserQueues[required.MapId].Dequeue();
                matchSuccessPacket = new MatchSuccessPacketRequired
                {
                    IsTestGame = true,
                    SheepUserId = userPacket.UserId,
                    SheepSessionId = userPacket.SessionId,
                    SheepUserName = userPacket.UserName,
                    WolfUserId = required.UserId,
                    WolfSessionId = required.SessionId,
                    WolfUserName = "Test",
                    MapId = required.MapId,
                    SheepRankPoint = userPacket.RankPoint,
                    WolfRankPoint = required.RankPoint,
                    WinPointSheep = rankPointResponse.WinPointSheep,
                    WinPointWolf = rankPointResponse.WinPointWolf,
                    LosePointSheep = rankPointResponse.LosePointSheep,
                    LosePointWolf = rankPointResponse.LosePointWolf,
                    SheepCharacterId = (CharacterId)userPacket.CharacterId,
                    WolfCharacterId = (CharacterId)required.CharacterId,
                    SheepId = (SheepId)userPacket.AssetId,
                    EnchantId = (EnchantId)required.AssetId,
                    SheepUnitIds = userPacket.UnitIds,
                    WolfUnitIds = required.UnitIds,
                    SheepAchievements = userPacket.Achievements,
                    WolfAchievements = required.Achievements
                };
            }
            else
            {
                userPacket = _wolfUserQueues[required.MapId].Dequeue();
                matchSuccessPacket = new MatchSuccessPacketRequired
                {
                    IsTestGame = true,
                    SheepUserId = required.UserId,
                    SheepSessionId = required.SessionId,
                    SheepUserName = "Test",
                    WolfUserId = userPacket.UserId,
                    WolfSessionId = userPacket.SessionId,
                    WolfUserName = userPacket.UserName,
                    MapId = required.MapId,
                    SheepRankPoint = required.RankPoint,
                    WolfRankPoint = userPacket.RankPoint,
                    WinPointSheep = rankPointResponse.WinPointSheep,
                    WinPointWolf = rankPointResponse.WinPointWolf,
                    LosePointSheep = rankPointResponse.LosePointSheep,
                    LosePointWolf = rankPointResponse.LosePointWolf,
                    SheepCharacterId = (CharacterId)required.CharacterId,
                    WolfCharacterId = (CharacterId)userPacket.CharacterId,
                    SheepId = (SheepId)required.AssetId,
                    EnchantId = (EnchantId)userPacket.AssetId,
                    SheepUnitIds = required.UnitIds,
                    WolfUnitIds = userPacket.UnitIds,
                    SheepAchievements = required.Achievements,
                    WolfAchievements = userPacket.Achievements
                };
            }

            await _apiService.SendRequestToSocketAsync("match", matchSuccessPacket, HttpMethod.Post);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Test match failed.");
        }
    }
}