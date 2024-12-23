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
    
    private static ConcurrentDictionary<string, List<int>> _gameWaitingRoom = new();
    
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

    public async Task SendGameInvite(string friendName)
    {
        if (LobbyConnectionUsers.TryGetValue(Context.ConnectionId, out var myName) == false) return;
        if (LobbyUserConnections.TryGetValue(friendName, out var friendConnectId))
        {
            // Invitee 에게 메일 보내고, 메일 알림
            var userId = _context.User.FirstOrDefault(u => u.UserName == friendName)?.UserId;
            if (userId == null)
            {
                _logger.LogError($"User {friendName} not found.");
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
            
            var mailTask = Clients.Client(friendConnectId).SendAsync("RefreshMailAlert", myName);
            // Inviter 에게 toast 알림
            var notifyTask = Clients.Client(Context.ConnectionId).SendAsync("ToastNotification");
            await Task.WhenAll(mailTask, notifyTask);
        }
    }

    public async Task HandleAcceptInvitation(string inviterName)
    {
        if (LobbyConnectionUsers.TryGetValue(Context.ConnectionId, out var myName) == false) return;
        var gameRoomId = Guid.NewGuid().ToString();
        _gameWaitingRoom.TryAdd(
            gameRoomId,
            new List<int> {int.Parse(Context.ConnectionId), int.Parse(LobbyUserConnections[inviterName])});
        
        await Groups.AddToGroupAsync(Context.ConnectionId, gameRoomId);
        
        if (LobbyUserConnections.TryGetValue(inviterName, out var inviterConnectId))
        {
            await Groups.AddToGroupAsync(inviterConnectId, gameRoomId);
        }

        var res = new AcceptInvitationPacketResponse
        {
            AcceptInvitationOk = true,
            
        };
        
        await Clients.Group(gameRoomId).SendAsync("GameRoomJoined", res);
    }

    public async Task StartFriendlyMatch(string gameRoomId)
    {
        if (_gameWaitingRoom.TryGetValue(gameRoomId, out var players))
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