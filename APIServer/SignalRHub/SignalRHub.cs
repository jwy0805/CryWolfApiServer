using System.Collections.Concurrent;
using ApiServer.DB;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.SignalRHub;

public class SignalRHub: Hub
{
    private readonly AppDbContext _context;
    private readonly TokenValidator _tokenValidator;
    private readonly WebSocketService _webSocketService;
    private readonly ApiService _apiService;
    private readonly MailService _mailService;
    private readonly ILogger<SignalRHub> _logger;

    private static readonly ConcurrentDictionary<string, string> LobbyConnectionUsers = new();
    private static readonly ConcurrentDictionary<string, string> LobbyUserConnections = new();
    
    private static readonly ConcurrentDictionary<string, string> GameUserRooms = new();
    private static readonly ConcurrentDictionary<string, string> GameConnectionRooms = new();
    private static readonly ConcurrentDictionary<string, string> GameUserConnections = new();
    private static readonly ConcurrentDictionary<string, GameRoom> GameRooms = new();
    private static readonly ConcurrentDictionary<string, object> GameRoomLocks = new();
    
    private static readonly ConcurrentDictionary<string, Faction> HostFaction = new();

    public class GameRoom
    {
        public string RoomId { get; set; } = Guid.NewGuid().ToString("N");
        public string Username1 { get; set; } = string.Empty;
        public string UserTag1 { get; set; } = string.Empty;
        public int SessionId1 { get; set; } = -1;
        public string Username2 { get; set; } = string.Empty;
        public string UserTag2 { get; set; } = string.Empty;
        public int SessionId2 { get; set; } = -1;
        public bool Started { get; set; } = false;
    }
    
    public SignalRHub(AppDbContext context,
        TokenValidator tokenValidator,
        WebSocketService webSocketService,
        ApiService apiService,
        MailService mailService,
        ILogger<SignalRHub> logger)
    {
        _context = context;
        _tokenValidator = tokenValidator;
        _webSocketService = webSocketService;
        _apiService = apiService;
        _mailService = mailService;
        _logger = logger;
    }

    public async Task HeartBeat(string userTag)
    {
        var user = _context.User.FirstOrDefault(u => u.UserTag == userTag);
        if (user == null) return;
        user.LastPingTime = DateTime.UtcNow;
        await _context.SaveChangesExtendedAsync();
    }
    
    public async Task JoinLobby(string token)
    {
        var userId = _tokenValidator.Authorize(token);
        var userTag = _context.User.FirstOrDefault(u => u.UserId == userId)?.UserTag;
        if (userTag == null)
        {
            _logger.LogError("User not found for token: {Token}", token);
            throw new HubException("Unauthorized");
        }
        
        LobbyConnectionUsers.TryAdd(Context.ConnectionId, userTag);
        LobbyUserConnections.TryAdd(userTag, Context.ConnectionId);
        
        // Check Game Group
        if (GameUserRooms.TryRemove(userTag, out var roomId))
        {
            if (GameRooms.TryGetValue(roomId, out var room))
            {
                // User was in a game room, remove them from the group
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, room.RoomId);
                GameUserConnections.TryRemove(userTag, out _);
                GameConnectionRooms.TryRemove(Context.ConnectionId, out _);
            }
        }
        
        await Groups.AddToGroupAsync(Context.ConnectionId, "Lobby");
        _logger.LogInformation($"User {userTag} connected. Conn={Context.ConnectionId}");
    }

    public async Task LeaveLobby()
    {
        LobbyConnectionUsers.TryRemove(Context.ConnectionId, out var userTag);
        
        if (userTag != null)
        {
            LobbyUserConnections.TryRemove(userTag, out _);
        }
        
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Lobby");
        _logger.LogInformation($"User {userTag} disconnected from lobby.");
    }
    
    public async Task JoinGame(string token, Faction faction)
    {
        var userId = _tokenValidator.Authorize(token);
        var user = _context.User.FirstOrDefault(u => u.UserId == userId);
        if (user == null)
        {
            _logger.LogError("User not found for token: {Token}", token);
            throw new HubException("Unauthorized");
        }
        
        var room = new GameRoom { Username1 = user.UserName, UserTag1 = user.UserTag };
        GameRooms.TryAdd(room.RoomId, room);
        GameUserRooms.TryAdd(user.UserTag, room.RoomId);
        GameUserConnections.TryAdd(user.UserTag, Context.ConnectionId);
        GameConnectionRooms.TryAdd(Context.ConnectionId, room.RoomId);
        _logger.LogInformation($"{Context.ConnectionId} joined {room.RoomId}");
        HostFaction.TryAdd(room.RoomId, faction);
        
        await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);
    }

    public async Task LeaveGame()
    {
        var myConn = Context.ConnectionId;

        if (!GameConnectionRooms.TryGetValue(myConn, out var roomId))
        {
            _logger.LogWarning("LeaveGame: No room found for conn={Conn}", myConn);
            return;
        }

        if (!GameRooms.TryGetValue(roomId, out var room))
        {
            GameConnectionRooms.TryRemove(myConn, out _);
            _logger.LogWarning("LeaveGame: Room {RoomId} not found for conn={Conn}", roomId, myConn);
            return;
        }

        var hostTag = room.UserTag1;
        var guestTag = room.UserTag2;
        var isHost = !string.IsNullOrEmpty(hostTag) 
                     && GameUserConnections.TryGetValue(hostTag, out var hostConn) 
                     && hostConn == myConn;
        if (isHost)
        {
            // Host left the game, need to clean up the room
            // Notify guest first
            if (!string.IsNullOrEmpty(guestTag) && GameUserConnections.TryGetValue(guestTag, out var guestConn))
            {
                await Groups.RemoveFromGroupAsync(guestConn, roomId);
                await Clients.Client(guestConn).SendAsync("GameRoomClosedByHost");
                
                GameUserRooms.TryRemove(guestTag, out _);
                GameUserConnections.TryRemove(guestTag, out _);
                GameConnectionRooms.TryRemove(guestConn, out _);
            }
            
            // Now remove the host
            await Groups.RemoveFromGroupAsync(myConn, roomId);

            if (!string.IsNullOrEmpty(hostTag))
            {
                GameUserRooms.TryRemove(hostTag, out _);
                GameUserConnections.TryRemove(hostTag, out _);
            }
            GameConnectionRooms.TryRemove(myConn, out _);
            
            GameRooms.TryRemove(roomId, out _);
            HostFaction.TryRemove(roomId, out _);
            GameRoomLocks.TryRemove(roomId, out _);

            await Clients.Caller.SendAsync("GameRoomClosed");
            return;
        }
        
        if (!isHost)
        {
            // Guest left the game
            await Groups.RemoveFromGroupAsync(myConn, roomId);

            if (!string.IsNullOrEmpty(guestTag))
            {
                GameUserRooms.TryRemove(guestTag, out _);
                GameUserConnections.TryRemove(guestTag, out _);
            }
            GameConnectionRooms.TryRemove(myConn, out _);
            room.Username2 = string.Empty;
            
            await Clients.OthersInGroup(roomId).SendAsync("GuestLeftGameRoom");
            await Clients.Caller.SendAsync("LeftGameRoom");
            return;
        }
        
        // [Mapping Error] If we reach here, it means the user is neither host nor guest, which should not happen.
        await Groups.RemoveFromGroupAsync(myConn, roomId);
        GameConnectionRooms.TryRemove(myConn, out _);
    }

    public async Task SwitchDeckOnFriendlyMatch(string token, Faction faction)
    {
        var userId = _tokenValidator.Authorize(token);
        if (userId == -1) throw new HubException("Unauthorized");
        
        var userTag = _context.User.AsNoTracking().FirstOrDefault(u => u.UserId == userId)?.UserTag;
        if (userTag == null) throw new HubException("User not found");

        if (GameUserRooms.TryGetValue(userTag, out var roomId))
        {
            var deckInfo = _webSocketService.GetDeckInfo(userId, faction);
            if (deckInfo == null) throw new HubException("Deck not found");
            
            await Clients.OthersInGroup(roomId).SendAsync("SwitchDeck", deckInfo);
        }
    }
    
    public async Task SwitchFactionOnFriendlyMatch(Faction faction)
    {
        // 룸/호스트 검증
        if (!GameConnectionRooms.TryGetValue(Context.ConnectionId, out var roomId))
            throw new HubException("Room not found");
        if (!GameRooms.TryGetValue(roomId, out var room))
            throw new HubException("Game room not found");

        var hostTag = room.UserTag1;
        if (string.IsNullOrWhiteSpace(hostTag))
            throw new HubException("Host not found");
        if (!GameUserConnections.TryGetValue(hostTag, out var hostConnId))
            throw new HubException("Host not found");
        if (Context.ConnectionId != hostConnId)
            throw new HubException("Only host can switch faction");

        // 호스트의 덱 정보 가져오기
        if (!HostFaction.ContainsKey(roomId))
            throw new HubException("Host not found in HostFaction");

        HostFaction.AddOrUpdate(roomId, _ => faction, (_, __) => faction);

        var tags = new[] { room.UserTag1, room.UserTag2 }
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .ToArray();

        var users = _context.User.AsNoTracking()
            .Where(u => tags.Contains(u.UserTag))
            .Select(u => new { u.UserTag, u.UserId })
            .ToDictionary(u => u.UserTag, u => (int?)u.UserId);

        if (!users.TryGetValue(room.UserTag1, out var hostId) || hostId is null)
            throw new HubException("Host not found");

        var hostDeckInfo = _webSocketService.GetDeckInfo(hostId.Value, faction);

        DeckInfo? guestDeckInfo = null;
        if (!string.IsNullOrWhiteSpace(room.UserTag2) &&
            users.TryGetValue(room.UserTag2, out var guestId) &&
            guestId is { } gid)
        {
            var guestFaction = faction == Faction.Sheep ? Faction.Wolf : Faction.Sheep;
            guestDeckInfo = _webSocketService.GetDeckInfo(gid, guestFaction);
        }

        var tasks = new List<Task>(2);
        if (guestDeckInfo != null)
        {
            tasks.Add(Clients.OthersInGroup(roomId).SendAsync("SwitchFaction", guestDeckInfo, hostDeckInfo, true));
        }

        tasks.Add(Clients.Caller.SendAsync("SwitchFaction", hostDeckInfo, guestDeckInfo, false));
        await Task.WhenAll(tasks);
    }
    
    public async Task<LoadInvitableFriendPacketResponse> LoadFriends(LoadInvitableFriendPacketRequired required)
    {
        var userId = _tokenValidator.Authorize(required.AccessToken);
        var friendList = await _context.Friend.AsNoTracking()
            .Where(f => (f.UserId == userId || f.FriendId == userId) && f.Status == FriendStatus.Accepted)
            .ToListAsync();
        var res = new LoadInvitableFriendPacketResponse
        {
            InvitableFriends = new List<FriendUserInfo>(),
            Others = new List<FriendUserInfo>(),
            LoadInvitableFriendOk = false
        };
        
        foreach (var friend in friendList)
        {
            var friendId = friend.UserId == userId ? friend.FriendId : friend.UserId;
            var friendUser = _context.User.AsNoTracking().FirstOrDefault(u => u.UserId == friendId);
            var friendStat = _context.UserStats.AsNoTracking().FirstOrDefault(us => us.UserId == friendId);
            if (friendUser == null || friendStat == null) continue;

            var friendUserInfo = new FriendUserInfo
            {
                UserName = friendUser.UserName,
                UserTag = friendUser.UserTag,
                Level = friendStat.UserLevel,
                RankPoint = friendStat.RankPoint,
                Act = friendUser.Act,
            };
            
            if (LobbyUserConnections.ContainsKey(friendUser.UserTag))
            {
                res.InvitableFriends.Add(friendUserInfo);
            }
            else
            {
                res.Others.Add(friendUserInfo);
            }
        }
        
        res.LoadInvitableFriendOk = true;
        return res;
    }
    
    public async Task HandleInviteFriendlyMatch(InviteFriendlyMatchPacketRequired required)
    {
        var userId = _tokenValidator.Authorize(required.AccessToken);
        if (userId == -1) throw new HubException("Unauthorized");
        
        var myName = _context.User.AsNoTracking().FirstOrDefault(u => u.UserId == userId)?.UserName;
        var myTag = _context.User.AsNoTracking().FirstOrDefault(u => u.UserId == userId)?.UserTag;
        var myFullName = $"{myName} #{myTag}";
        
        if (LobbyUserConnections.TryGetValue(required.InviteeTag, out var friendConnectId))
        {
            // Invitee 에게 메일 보내고, 메일 알림
            var inviteeId = _context.User.FirstOrDefault(u => u.UserTag == required.InviteeTag)?.UserId;
            if (inviteeId == null)
            {
                _logger.LogError($"User {required.InviteeTag} not found.");
                return;
            }

            var mail = _mailService.WriteInvitationMail(inviteeId.Value, myFullName);
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
        if (required == null) throw new HubException("BadRequest");
        
        var inviteeId = _tokenValidator.Authorize(required.AccessToken);
        if (inviteeId == -1) throw new HubException("Unauthorized");
        
        var invitee = _context.User.AsNoTracking().FirstOrDefault(u => u.UserId == inviteeId);
        var invitationMail = _context.Mail.AsNoTracking().FirstOrDefault(m => m.MailId == required.MailId);
        if (invitee == null || invitationMail?.Sender == null) throw new HubException("BadRequest");
        
        var inviteeTag = invitee.UserTag;
        var inviterTag = Util.Util.ExtractUserTag(invitationMail.Sender);

        if (required.Accept == false)
        {
            if (GameUserConnections.TryGetValue(inviterTag, out var inviterConnectId))
            {
                var res = new AcceptInvitationPacketResponse { AcceptInvitationOk = false };
                await Clients.Client(inviterConnectId).SendAsync("RejectInvitation", res);
            }   
            
            return;
        }

        if (GameUserRooms.TryGetValue(inviterTag, out var roomId) && GameRooms.TryGetValue(roomId, out var room))
        {
            if (GameUserRooms.TryGetValue(inviteeTag, out _))
            {
                await Clients.Caller.SendAsync("InvitationFailed", "notify_already_in_game_room");
                return;
            }

            if (!string.IsNullOrEmpty(room.UserTag2) && room.UserTag2 != inviteeTag)
            {
                await Clients.Caller.SendAsync("InvitationFailed", "notify_game_room_full");
                return;
            }

            room.Username2 = invitee.UserName;
            room.UserTag2 = inviteeTag;
            GameUserRooms.TryAdd(inviteeTag, roomId);
            GameUserConnections.TryAdd(inviteeTag, Context.ConnectionId);
            GameConnectionRooms.TryAdd(Context.ConnectionId, roomId);
            
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

            if (HostFaction.TryGetValue(roomId, out var hostFaction))
            {
                if (!GameUserConnections.TryGetValue(inviterTag, out var inviterConnectId))
                {
                    throw new HubException("Inviter not found in GameUserConnections");
                }
                
                // 상대의 덱 정보를 보내야 하므로 상대의 태그를 인자로 넘겨준다
                var inviteeFaction = hostFaction == Faction.Sheep ? Faction.Wolf : Faction.Sheep;
                var responseInviter = _webSocketService.CreateAcceptInvitationPacket(inviteeTag, hostFaction);
                var responseInvitee = _webSocketService.CreateAcceptInvitationPacket(inviterTag, inviteeFaction);
                var inviterTask = Clients.Client(inviterConnectId).SendAsync("GameRoomJoined", responseInviter);
                var inviteeTask = Clients.Client(Context.ConnectionId).SendAsync("JoinGameRoom", responseInvitee);
                
                await Task.WhenAll(inviterTask, inviteeTask);
            }
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
        
        var friendId = _context.User.FirstOrDefault(u => u.UserTag == required.FriendUserTag)?.UserId;
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
                var friendRelations = _context.Friend
                    .FirstOrDefault(f => (f.UserId == userId && f.FriendId == friendId)
                                         || (f.UserId == friendId && f.FriendId == userId));

                if (friendRelations != null)
                {
                    switch (friendRelations.Status)
                    {
                        case FriendStatus.Pending:
                            friendRelations.Status = FriendStatus.Accepted;
                            res.FriendStatus = FriendStatus.Accepted;
                            break;
                        case FriendStatus.Blocked:
                            res.FriendStatus = FriendStatus.Blocked;
                            break;
                        case FriendStatus.Accepted:
                            res.FriendStatus = FriendStatus.Accepted;
                            break;
                        case FriendStatus.None:
                            res.FriendStatus = FriendStatus.None;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    var receiverId = friendId.Value;
                    (userId, friendId) = userId > friendId ? (friendId, userId) : (userId, friendId);
                    
                    var newFriend = new Friend
                    {
                        UserId = userId.Value,
                        FriendId = friendId.Value,
                        RequestReceiverId = receiverId,
                        CreatedAt = DateTime.Now,
                        Status = FriendStatus.Pending
                    };
                    _context.Friend.Add(newFriend);
                    res.FriendStatus = FriendStatus.Pending;
                }

                await _context.SaveChangesExtendedAsync();
                await transaction.CommitAsync();
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Friend Request Transaction Error: {e.Message}");
                throw;
            }
        });
        
        if (LobbyConnectionUsers.TryGetValue(required.FriendUserTag, out var connectId))
        {
            await Clients.Client(connectId).SendAsync("FriendRequestNotification", res);
        }
        else
        {
            _logger.LogInformation("Friend is not in lobby.");
        }

        res.FriendRequestOk = true;
        return res;
    }

    public async Task StartFriendlyMatch(string userTag)
    {
        if (!GameUserRooms.TryGetValue(userTag, out var roomId))
            throw new HubException("Room not found");
        
        if (!GameRooms.TryGetValue(roomId, out var room))
            throw new HubException("Room not found");
        
        if (string.IsNullOrEmpty(room.UserTag1) || string.IsNullOrEmpty(room.UserTag2)) 
            throw new HubException("Both players are required to start the match");
        
        await Clients.Group(room.RoomId).SendAsync("StartSession");
    }

    public async Task GetSessionId(int sessionId)
    {
        // 룸/호스트 검증
        if (!GameConnectionRooms.TryGetValue(Context.ConnectionId, out var roomId))
            throw new HubException("Room not found");
        if (!GameRooms.TryGetValue(roomId, out var room))
            throw new HubException("Game room not found");
        if (!GameUserConnections.TryGetValue(room.UserTag1, out var hostConnId))
            throw new HubException("Host not found");
    
        var isHost = Context.ConnectionId == hostConnId;
        var start = false;
        var locker = GameRoomLocks.GetOrAdd(roomId, _ => new object());
        lock (locker)
        {
            if (isHost)
                room.SessionId1 = sessionId;
            else
                room.SessionId2 = sessionId;

            if (!room.Started && room.SessionId1 != -1 && room.SessionId2 != -1)
            {
                room.Started = true;   // 여기서 시작권 획득
                start = true;
            }
        }
        
        _logger.LogInformation($"friendly match start {start}");
        if (start)
        {
            _logger.LogInformation("Both players have sent their session IDs. Starting the match.");
            if (HostFaction.TryGetValue(roomId, out var hostFaction))
            {
                var packet = _webSocketService.CreateMatchPacket(hostFaction, room);
                var task = await _apiService
                    .SendRequestToSocketAsync<FriendlyMatchPacketResponse>("friendlyMatch", packet, HttpMethod.Post);
                if (task != null)
                {
                    await Clients.Group(roomId).SendAsync("MatchFailed", task.IsSuccess);
                }
            }
            
            room.Started = false;
        }
    }

    public async Task<Tuple<bool, AcceptInvitationPacketResponse>> ReEntryFriendlyMatch(string userTag)
    {
        _logger.LogInformation("ReEntry Method");
        if (!GameUserRooms.TryGetValue(userTag, out var roomId))
            throw new HubException("Room not found");

        if (!GameRooms.TryGetValue(roomId, out var room))
            throw new HubException("Game room not found");
        
        // Connection 갱신
        var connId = Context.ConnectionId;
        GameUserConnections.AddOrUpdate(userTag, connId, (_, __) => connId);
        GameConnectionRooms.AddOrUpdate(connId, roomId, (_, __) => roomId);
        
        // 그룹 재참여
        await Groups.AddToGroupAsync(connId, roomId);

        // 호스트 여부 계산 후 반환
        var isHost = string.Equals(room.UserTag1, userTag, StringComparison.Ordinal);
        var res = new AcceptInvitationPacketResponse();
        if (!HostFaction.TryGetValue(roomId, out var hostFaction))
            return new Tuple<bool, AcceptInvitationPacketResponse>(isHost, res);
        
        if (isHost)
        {
            res = _webSocketService.CreateAcceptInvitationPacket(room.UserTag2, hostFaction);
            _logger.LogInformation($"Re-entry: {userTag} is host in room {roomId}", userTag, roomId);
        }
        else
        {
            res = _webSocketService.CreateAcceptInvitationPacket(room.UserTag1, 
                hostFaction == Faction.Sheep ? Faction.Wolf : Faction.Sheep);
            _logger.LogInformation($"Re-entry: {userTag} is guest in room {roomId}", userTag, roomId);
        }

        return new Tuple<bool, AcceptInvitationPacketResponse>(isHost, res);
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (LobbyConnectionUsers.TryRemove(Context.ConnectionId, out var userTag))
        {
            var user = _context.User.FirstOrDefault(u => u.UserTag == userTag);
            if (user != null)
            {
                user.Act = UserAct.Offline;
                user.LastPingTime = DateTime.UtcNow;
                LobbyUserConnections.TryRemove(userTag, out _);
                
                await _context.SaveChangesExtendedAsync();
            }
            
            _logger.LogInformation($"User {userTag} disconnected. Conn={Context.ConnectionId}");
        }

        await base.OnDisconnectedAsync(exception);
    }
}