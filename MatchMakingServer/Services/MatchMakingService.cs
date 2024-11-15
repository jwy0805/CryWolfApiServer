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
                    var matchResult = FindMatch(mapId);
                    if (matchResult == null) break;
                    ProcessMatchRequest(matchResult.Value.Item1, matchResult.Value.Item2);
                }
            }
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
    
    private async void ProcessMatchRequest(MatchMakingPacketRequired sheepRequired, MatchMakingPacketRequired wolfRequired)
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
            SheepCharacterId = sheepRequired.CharacterId,
            WolfCharacterId = wolfRequired.CharacterId,
            SheepId = (SheepId)sheepRequired.AssetId,
            EnchantId = (EnchantId)wolfRequired.AssetId,
            SheepUnitIds = sheepRequired.UnitIds,
            WolfUnitIds = wolfRequired.UnitIds,
            SheepAchievements = sheepRequired.Achievements,
            WolfAchievements = wolfRequired.Achievements
        };

        await _apiService.SendRequestToSocketAsync("match", matchSuccessPacket, HttpMethod.Post);
    }

    private async void ProcessTestMatchRequest(MatchMakingPacketRequired required)
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
        
        var matchSuccessPacket = new MatchSuccessPacketRequired
        {
            SheepUserId = required.UserId,
            SheepSessionId = required.SessionId,
            SheepUserName = required.Faction == Faction.Sheep ? required.UserName : "Test",
            WolfUserId = required.UserId,
            WolfSessionId = required.SessionId,
            WolfUserName = required.Faction == Faction.Wolf ? required.UserName : "Test",
            MapId = required.MapId,
            SheepRankPoint = required.RankPoint,
            WolfRankPoint = required.RankPoint,
            WinPointSheep = rankPointResponse.WinPointSheep,
            WinPointWolf = rankPointResponse.WinPointWolf,
            LosePointSheep = rankPointResponse.LosePointSheep,
            LosePointWolf = rankPointResponse.LosePointWolf,
            SheepCharacterId = required.CharacterId,
            WolfCharacterId = required.CharacterId,
            SheepId = (SheepId)required.AssetId,
            EnchantId = (EnchantId)required.AssetId,
            SheepUnitIds = required.UnitIds,
            WolfUnitIds = required.UnitIds,
            SheepAchievements = required.Achievements,
            WolfAchievements = required.Achievements
        };

        await _apiService.SendRequestToSocketAsync("match", matchSuccessPacket, HttpMethod.Post);
    }
    
    public void AddMatchRequest(MatchMakingPacketRequired packet, bool test = false)
    {
        if (test)
        {
            Console.WriteLine($"user {packet.UserId} : session {packet.SessionId} test match");
            ProcessTestMatchRequest(packet);
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
            });
        }
    }
    
    public int CancelMatchRequest(MatchCancelPacketRequired packet)
    {
        _jobService.Push(() => _cancelUserList.Add(packet.UserId));
        return packet.UserId;
    }
}