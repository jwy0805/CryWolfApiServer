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
    
    private async void ProcessMatchRequest(MatchMakingPacketRequired sheepRequest, MatchMakingPacketRequired wolfRequest)
    {   
        _logger.LogInformation(
            $"Matched User {sheepRequest.UserId} (Faction {sheepRequest.Faction}, RankPoint {sheepRequest.RankPoint}) " +
            $"with User {wolfRequest.UserId} (Faction {wolfRequest.Faction}, RankPoint {wolfRequest.RankPoint})");
        
        // Http Transfer of Socket Server
        var matchSuccessPacket = new MatchSuccessPacketRequired
        {
            SheepUserId = sheepRequest.UserId,
            WolfUserId = wolfRequest.UserId,
            MapId = sheepRequest.MapId
        };

        await _apiService.SendRequestToSocketAsync("match", matchSuccessPacket, HttpMethod.Post);
    }
    
    public async void AddMatchRequest(MatchMakingPacketRequired packet, bool test = false)
    {
        if (test)
        {
            var sheepUserId = packet.Faction == Faction.Sheep ? packet.UserId : 0;
            var wolfUserId = packet.Faction == Faction.Wolf ? packet.UserId : 0;
            var matchSuccessPacket = new MatchSuccessPacketRequired
            {
                SheepUserId = sheepUserId,
                WolfUserId = wolfUserId
            };
            
            await _apiService.SendRequestToSocketAsync("match", matchSuccessPacket, HttpMethod.Post);
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