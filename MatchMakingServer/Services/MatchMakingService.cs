using System.Collections.Concurrent;
using System.Net;
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
    private readonly HashSet<int> _cancelUserList = new();
    private readonly object _lock = new();
    
    public MatchMakingService(ILogger<MatchMakingService> logger, JobService jobService, ApiService apiService)
    {
        _logger = logger;
        _jobService = jobService;
        _apiService = apiService;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Matchmaking Service is starting.");

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            await ReportQueueCountsAsync(stoppingToken);
        }, stoppingToken);

        while (stoppingToken.IsCancellationRequested == false)
        {
            ProcessMatchMaking();
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
        
        _logger.LogInformation("Matchmaking Service has stopped.");
    }
    
    private void ProcessMatchMaking()
    {
        lock (_lock)
        {
            foreach (var mapId in _sheepUserQueues.Keys)
            {
                var sheepUserQueue = _sheepUserQueues[mapId];
                var wolfUserQueue = _wolfUserQueues[mapId];
                
                while (sheepUserQueue.Count > 0 && wolfUserQueue.Count > 0)
                {
                    Console.WriteLine($"Processing Matchmaking... {sheepUserQueue.Count}, {wolfUserQueue.Count}");
                    var matchResult = FindMatch(mapId);
                    if (matchResult == null) break;
                    _ = ProcessMatchRequest(matchResult.Value.Item1, matchResult.Value.Item2);
                }
            }
        }
    }

    private async Task ReportQueueCountsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                int sheepCount = 0;
                int wolfCount = 0;
                
                if (_sheepUserQueues.TryGetValue(1, out var queue))
                {
                    sheepCount = queue.Count;
                }
                
                if (_wolfUserQueues.TryGetValue(1, out var wolfQueue))
                {
                    wolfCount = wolfQueue.Count;
                }
                
                var packet = new ReportQueueCountsRequired
                {
                    QueueCountsSheep = sheepCount,
                    QueueCountsWolf = wolfCount
                };
                
                var res = await _apiService.SendRequestToApiAsync<ReportQueueCountsResponse>(
                    "Match/ReportQueueCounts", packet, HttpMethod.Post);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to report queue counts.");
            }
            
            await Task.Delay(TimeSpan.FromSeconds(10), token);
        }
    }

    private (MatchMakingPacketRequired, MatchMakingPacketRequired)? FindMatch(int mapId)
    {
        var sheepUserQueue = _sheepUserQueues[mapId];
        var wolfUserQueue = _wolfUserQueues[mapId];
        
        while (true)
        {
            if (sheepUserQueue.Count == 0 || wolfUserQueue.Count == 0) return null; 

            var sheepRequired = sheepUserQueue.Peek();
            var wolfRequired = wolfUserQueue.Peek();

            if (_cancelUserList.Contains(sheepRequired.UserId))
            {
                sheepUserQueue.Dequeue();
                _cancelUserList.Remove(sheepRequired.UserId);
            }
            else if (_cancelUserList.Contains(wolfRequired.UserId))
            {
                wolfUserQueue.Dequeue();
                _cancelUserList.Remove(wolfRequired.UserId);
            }
            else
            {
                sheepRequired = sheepUserQueue.Dequeue();
                wolfRequired = wolfUserQueue.Dequeue();
                return (sheepRequired, wolfRequired);
            }
        }
    }
    
    private async Task ProcessMatchRequest(MatchMakingPacketRequired sheepRequired, MatchMakingPacketRequired wolfRequired)
    {   
        _logger.LogInformation(
            $"Matched User {sheepRequired.UserId} (Faction {sheepRequired.Faction}, RankPoint {sheepRequired.RankPoint}) " +
            $"with User {wolfRequired.UserId} (Faction {wolfRequired.Faction}, RankPoint {wolfRequired.RankPoint})");
        
        var getRankPointPacket = new GetRankPointPacketRequired
        {
            SheepUserId = sheepRequired.UserId,
            WolfUserId = wolfRequired.UserId
        };
        
        var rankPointResponse = await _apiService
            .SendRequestToApiAsync<GetRankPointPacketResponse>("Match/GetRankPoint", getRankPointPacket, HttpMethod.Post);
       
        if (rankPointResponse == null)
        {
            _logger.LogError("Failed to get rank point.");
            return;
        }
            
        // Http Transfer of Socket Server
        var matchSuccessPacket = new MatchSuccessPacketRequired
        {
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
    }

    private async Task ProcessTestMatchRequest(MatchMakingPacketRequired required)
    {
        _logger.LogInformation($"Test Match Requested User {required.UserId} (Faction {required.Faction}, RankPoint {required.RankPoint})");
        
        var getRankPointPacket = new GetRankPointPacketRequired
        {
            SheepUserId = required.UserId,
            WolfUserId = required.UserId
        };
        
        var rankPointResponse = await _apiService
            .SendRequestToApiAsync<GetRankPointPacketResponse>("Match/GetRankPoint", getRankPointPacket, HttpMethod.Post);
               
        if (rankPointResponse == null)
        {
            _logger.LogError("Failed to get rank point.");
            return;
        }

        MatchMakingPacketRequired userPacket;
        MatchSuccessPacketRequired matchSuccessPacket;
        // required = temp test packet
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
    
    public void AddMatchRequest(MatchMakingPacketRequired packet, bool test = false)
    {
        if (test)
        {
            Console.WriteLine($"user {packet.UserId} : session {packet.SessionId} test match");
            _ = ProcessTestMatchRequest(packet);
        }
        else
        {
            _jobService.Push(() => 
            {
                if (_cancelUserList.Contains(packet.UserId)) _cancelUserList.Remove(packet.UserId);
                
                if (_sheepUserQueues.ContainsKey(packet.MapId) == false)
                {
                    _sheepUserQueues[packet.MapId] = new PriorityQueue<MatchMakingPacketRequired, int>();
                }
                
                if (_wolfUserQueues.ContainsKey(packet.MapId) == false)
                {
                    _wolfUserQueues[packet.MapId] = new PriorityQueue<MatchMakingPacketRequired, int>();
                }
                
                if (packet.Faction == Faction.Sheep)
                {
                    _sheepUserQueues[packet.MapId].Enqueue(packet, packet.RankPoint);
                }
                else if (packet.Faction == Faction.Wolf)
                {
                    _wolfUserQueues[packet.MapId].Enqueue(packet, packet.RankPoint);
                }
                
                Console.WriteLine($"user {packet.UserId} : session {packet.SessionId}, {_sheepUserQueues[packet.MapId].Count}, {_wolfUserQueues[packet.MapId].Count}");
            });
        }
    }
    
    public int CancelMatchRequest(MatchCancelPacketRequired packet)
    {
        _jobService.Push(() => _cancelUserList.Add(packet.UserId));
        return packet.UserId;
    }
}