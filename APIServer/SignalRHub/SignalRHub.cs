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

    private static readonly ConcurrentDictionary<string, string> LobbyUsers = new();
    
    public SignalRHub(ILogger<SignalRHub> logger, AppDbContext context, TokenValidator tokenValidator)
    {
        _logger = logger;
        _context = context;
        _tokenValidator = tokenValidator;
    }

    public async Task JoinLobby(string userName)
    {
        LobbyUsers.TryAdd(Context.ConnectionId, userName);
        await Groups.AddToGroupAsync(Context.ConnectionId, "Lobby");
    }
    
    public async Task LeaveLobby()
    {
        LobbyUsers.TryRemove(Context.ConnectionId, out _);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Lobby");
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
        
        if (LobbyUsers.TryGetValue(required.FriendUsername, out var connectId))
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