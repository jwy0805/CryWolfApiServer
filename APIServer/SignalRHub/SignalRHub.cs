using System.Collections.Concurrent;
using ApiServer.DB;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.SignalRHub;

public class SignalRHub: Hub
{
    private readonly ILogger<SignalRHub> _logger;
    private readonly AppDbContext _context;
    private readonly TokenValidator _tokenValidator;

    private static readonly ConcurrentDictionary<string, string> LobbyConnectionUsers = new();
    private static readonly ConcurrentDictionary<string, string> LobbyUserConnections = new();
    
    private static readonly ConcurrentDictionary<string, List<int>> GameWaitingRoom = new();
    private static readonly ConcurrentDictionary<string, Faction> HostFaction = new();
    
    public SignalRHub(ILogger<SignalRHub> logger, AppDbContext context, TokenValidator tokenValidator)
    {
        _logger = logger;
        _context = context;
        _tokenValidator = tokenValidator;
    }

    public async Task JoinLobby(string username)
    {
        LobbyConnectionUsers.TryAdd(Context.ConnectionId, username);
        LobbyUserConnections.TryAdd(username, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, "Lobby");
    }
    
    public async Task LeaveLobby()
    {
        LobbyConnectionUsers.TryRemove(Context.ConnectionId, out var username);
        if (username != null)
        {
            LobbyUserConnections.TryRemove(username, out _);
        }
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Lobby");
    }
    
    public async Task HeartBeat(string username)
    {
        var user = _context.User.FirstOrDefault(u => u.UserName == username);
        if (user == null) return;
        user.LastPingTime = DateTime.UtcNow;
        await _context.SaveChangesExtendedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (LobbyConnectionUsers.TryRemove(Context.ConnectionId, out var username))
        {
            var user = _context.User.FirstOrDefault(u => u.UserName == username);
            if (user != null)
            {
                user.Act = UserAct.Offline;
                user.LastPingTime = DateTime.UtcNow;
                LobbyUserConnections.TryRemove(username, out _);
                
                await _context.SaveChangesExtendedAsync();
            }
            
            _logger.LogInformation($"User {username} disconnected. Conn={Context.ConnectionId}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task HandleInviteFriendlyMatch(InviteFriendlyMatchPacketRequired required)
    {
        if (LobbyConnectionUsers.TryGetValue(Context.ConnectionId, out var myName) == false) return;
        if (LobbyUserConnections.TryGetValue(required.InviteeName, out var friendConnectId))
        {
            HostFaction.TryAdd(Context.ConnectionId, required.InviterFaction);
            // Invitee 에게 메일 보내고, 메일 알림
            var userId = _context.User.FirstOrDefault(u => u.UserName == required.InviteeName)?.UserId;
            if (userId == null)
            {
                _logger.LogError($"User {required.InviteeName} not found.");
                return;
            }

            var mail = new Mail
            {
                UserId = userId.Value,
                Type = MailType.Invite,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddMinutes(5),
                Claimed = false,
                Message = $"{myName} sent you a game invite.",
                Sender = myName
            };
            
            _context.Mail.Add(mail);
            await _context.SaveChangesExtendedAsync();
            
            var mailTask = Clients.Client(friendConnectId).SendAsync("RefreshMailAlert");
            // Inviter 에게 toast 알림
            var notifyTask = Clients.Client(Context.ConnectionId).SendAsync("ToastNotification");
            await Task.WhenAll(mailTask, notifyTask);
        }
    }

    public async Task HandleAcceptInvitation(AcceptInvitationPacketRequired required)
    {
        if (LobbyConnectionUsers.TryGetValue(Context.ConnectionId, out var myName) == false) return;
        var gameRoomId = Guid.NewGuid().ToString();
        GameWaitingRoom.TryAdd(
            gameRoomId,
            new List<int> {int.Parse(Context.ConnectionId), int.Parse(LobbyUserConnections[required.InviterName])});
        
        await Groups.AddToGroupAsync(Context.ConnectionId, gameRoomId);
        
        if (LobbyUserConnections.TryGetValue(required.InviterName, out var inviterConnectId))
        {
            var inviteeConnectId = Context.ConnectionId;
            await Groups.AddToGroupAsync(inviterConnectId, gameRoomId);
            
            // 상대의 덱 정보를 보내야 하므로 상대의 이름을 인자로 넘겨준다.
            if (HostFaction.TryRemove(inviterConnectId, out var hostFaction))
            {
                var inviteeFaction = hostFaction == Faction.Sheep ? Faction.Wolf : Faction.Sheep;
                var responseInviter = CreateAcceptInvitationPacket(myName, hostFaction);
                var responseInvitee = CreateAcceptInvitationPacket(required.InviterName, inviteeFaction);
                var inviterTask = Clients.Client(inviterConnectId).SendAsync("GameRoomJoined", responseInviter);
                var inviteeTask = Clients.Client(inviteeConnectId).SendAsync("GameRoomJoined", responseInvitee);
            
                await Task.WhenAll(inviterTask, inviteeTask);
            }
        }
    }

    public async Task HandleRejectInvitation(AcceptInvitationPacketRequired required)
    {
        if (LobbyUserConnections.TryGetValue(required.InviterName, out var inviterConnectId))
        {
            var res = new AcceptInvitationPacketResponse { AcceptInvitationOk = false };
            await Clients.Client(inviterConnectId).SendAsync("RejectInvitation", res);
        }
    }

    private AcceptInvitationPacketResponse CreateAcceptInvitationPacket(string username, Faction myFaction)
    {
        var user = _context.User.AsNoTracking().FirstOrDefault(u => u.UserName == username);
        if (user == null)
        {
            _logger.LogError($"User {username} not found.");
            return new AcceptInvitationPacketResponse { AcceptInvitationOk = false };
        }
        
        var userStats = _context.UserStats.AsNoTracking().FirstOrDefault(s => s.UserId == user.UserId);
        if (userStats == null)
        {
            _logger.LogError($"UserStats {username} not found.");
            return new AcceptInvitationPacketResponse { AcceptInvitationOk = false };
        }

        var userInfo = new UserInfo
        {
            UserName = username,
            RankPoint = userStats.RankPoint,
        };
        
        var deckInfoSheep = GetDeckInfo(user.UserId, Faction.Sheep);
        var deckInfoWolf = GetDeckInfo(user.UserId, Faction.Wolf);
        if (deckInfoSheep != null && deckInfoWolf != null)
        {
            return new AcceptInvitationPacketResponse
            {
                AcceptInvitationOk = true,
                MyFaction = myFaction,
                EnemyInfo = userInfo,
                EnemyDeckSheep = deckInfoSheep,
                EnemyDeckWolf = deckInfoWolf
            };
        }
        
        _logger.LogError($"DeckInfo {username} not found.");
        return new AcceptInvitationPacketResponse { AcceptInvitationOk = false };
    }
    
    private DeckInfo? GetDeckInfo(int userId, Faction faction)
    {
        return _context.Deck.AsNoTracking()
            .Where(d => d.UserId == userId && d.Faction == faction && d.LastPicked)
            .Select(d => new DeckInfo
            {
                DeckId = d.DeckId,
                UnitInfo = _context.DeckUnit.AsNoTracking()
                    .Where(du => du.DeckId == d.DeckId)
                    .Select(du => _context.Unit.AsNoTracking().FirstOrDefault(u => u.UnitId == du.UnitId))
                    .Where(u => u != null)
                    .Select(unit => new UnitInfo
                    {
                        Id = (int)unit!.UnitId,
                        Class = unit.Class,
                        Level = unit.Level,
                        Species = (int)unit.Species,
                        Role = unit.Role,
                        Faction = unit.Faction,
                        Region = unit.Region
                    }).ToArray(),
                DeckNumber = d.DeckNumber,
                Faction = (int)d.Faction,
                LastPicked = d.LastPicked
            }).FirstOrDefault();
    }
    
    public async Task StartFriendlyMatch(string gameRoomId)
    {
        if (GameWaitingRoom.TryGetValue(gameRoomId, out var players))
        {
            await Clients.Group(gameRoomId).SendAsync("StartGame");
        }
    }
    
    public async Task<FriendRequestPacketResponse> HandleFriendRequest(FriendRequestPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null)
        {
            return new FriendRequestPacketResponse
            {
                FriendRequestOk = false,
                FriendStatus = FriendStatus.None,
            };
        }

        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null)
        {
            return new FriendRequestPacketResponse
            {
                FriendRequestOk = false,
                FriendStatus = FriendStatus.None,
            };
        }
        
        var friendId = _context.User.FirstOrDefault(u => u.UserName == required.FriendUsername)?.UserId;
        if (friendId == null)
        {
            return new FriendRequestPacketResponse
            {
                FriendRequestOk = false,
                FriendStatus = FriendStatus.None,
            };
        }

        var res = new FriendRequestPacketResponse();
        var strategy = _context.Database.CreateExecutionStrategy();
        
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var friendRelations = _context.Friends
                    .Where(f => (f.UserId == userId && f.FriendId == friendId) 
                                || (f.UserId == friendId && f.FriendId == userId))
                    .ToList();
                var friendRelation1 = friendRelations
                    .FirstOrDefault(f => f.UserId == userId && f.FriendId == friendId);
                var friendRelation2 = friendRelations
                    .FirstOrDefault(f => f.UserId == friendId && f.FriendId == userId);

                if (friendRelation1 == null && required.CurrentFriendStatus == FriendStatus.None)
                {
                    var newFriend = new Friends
                    {
                        UserId = userId.Value,
                        FriendId = friendId.Value,
                        CreatedAt = DateTime.Now,
                        Status = FriendStatus.Pending
                    };
                    _context.Friends.Add(newFriend);
                    res.FriendStatus = FriendStatus.Pending;
                }
                else
                {
                    if (friendRelation1 != null) _context.Friends.Remove(friendRelation1);
                    if (friendRelation2 != null) _context.Friends.Remove(friendRelation2);

                    res.FriendStatus = FriendStatus.None;
                }

                await _context.SaveChangesExtendedAsync();
                await transaction.CommitAsync();
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Transaction Error: {e.Message}");
                throw;
            }
        });
        
        if (LobbyConnectionUsers.TryGetValue(required.FriendUsername, out var connectId))
        {
            await Clients.Client(connectId).SendAsync("FriendRequestNotification", res);
        }
        else
        {
            Console.WriteLine("Friend is not in lobby.");
        }

        res.FriendRequestOk = true;
        return res;
    }
}