using ApiServer.DB;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RelationController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenValidator _tokenValidator;
    private readonly ILogger<RelationController> _logger;
    
    public RelationController(AppDbContext context, TokenValidator tokenValidator, ILogger<RelationController> logger)
    {
        _context = context;
        _tokenValidator = tokenValidator;
        _logger = logger;
    }
    
    [HttpPost]
    [Route("GetFriendList")]
    public IActionResult GetFriendList([FromBody] FriendListPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        var res = new FriendListPacketResponse();

        if (userId == null)
        {
            res.FriendList = new List<FriendUserInfo>();
            res.FriendListOk = false;
            return Ok(res);
        }
        
        var friendsList = _context.Friends.AsNoTracking()
            .Where(friend => friend.UserId == userId && friend.Status == FriendStatus.Accepted)
            .Join(_context.User,
                friend => friend.FriendId,
                user => user.UserId,
                (friends, user) => new { friends, user })
            .Join(_context.UserStats,
                combined => combined.user.UserId,
                userStat => userStat.UserId,
                (combined, userStat) => new FriendUserInfo
                {
                    UserName = combined.user.UserName,
                    Level = userStat.UserLevel,
                    Act = combined.user.Act,
                    RankPoint = userStat.RankPoint,
                })
            .ToList();
        
        res.FriendList = friendsList;
        res.FriendListOk = true;
        
        return Ok(res);
    }

    [HttpPost]
    [Route("LoadPendingFriends")]
    public IActionResult LoadPendingFriends([FromBody] LoadPendingFriendPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null)
        {
            _logger.LogWarning("Requested Username Unauthorized");
            return Unauthorized();
        }
        
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null)
        {
            _logger.LogWarning("Requested Username Unauthorized");
            return Unauthorized();
        }
        
        var pendingFriends = _context.Friends.AsNoTracking()
            .Where(friend => friend.FriendId == userId && friend.Status == FriendStatus.Pending)
            .Join(
                _context.User,
                friend => friend.UserId,
                user => user.UserId,
                (friend, user) => new { friend, user })
            .Join(
                _context.UserStats,
                combined => combined.user.UserId,
                userStat => userStat.UserId,
                (combined, userStat) => new FriendUserInfo
                {
                    UserName = combined.user.UserName,
                    Level = userStat.UserLevel,
                    RankPoint = userStat.RankPoint,
                    FriendStatus = FriendStatus.Pending,
                })
            .ToList();
        
        var res = new LoadPendingFriendPacketResponse
        {
            PendingFriendList = pendingFriends,
            LoadPendingFriendOk = true,
        };
        
        return Ok(res);
    }

    [HttpPut]
    [Route("AcceptFriend")]
    public IActionResult AcceptFriend([FromBody] AcceptFriendPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null)
        {
            _logger.LogWarning("SearchUsername Unauthorized");
            return Unauthorized();
        }
        
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null)
        {
            _logger.LogWarning("SearchUsername Unauthorized");
            return Unauthorized();
        }
        
        var friendId = _context.User.AsNoTracking()
            .FirstOrDefault(user => user.UserName == required.FriendUsername)?.UserId;
        if (friendId == null)
        {
            _logger.LogWarning("SearchUsername Unauthorized");
            return Ok(new AcceptFriendPacketResponse { AcceptFriendOk = false });
        }
        
        var friend = _context.Friends
            .FirstOrDefault(friend =>
                friend.UserId == friendId && friend.FriendId == userId && friend.Status == FriendStatus.Pending);
        if (friend == null)
        {
            _logger.LogWarning("SearchUsername Unauthorized");
            return Ok(new AcceptFriendPacketResponse { AcceptFriendOk = false });
        }
        
        if (required.Accept)
        {
            friend.Status = FriendStatus.Accepted;
            _context.Friends.Update(friend);
            
            var newFriend = new Friends
            {
                UserId = userId.Value,
                FriendId = friendId.Value,
                Status = FriendStatus.Accepted,
                CreatedAt = DateTime.Now,
            };
            
            _context.Friends.Add(newFriend);
            _context.SaveChanges();
            
            return Ok(new AcceptFriendPacketResponse
            {
                AcceptFriendOk = true,
                Accept = true
            });
        }
        else
        {
            _context.Friends.Remove(friend);
            _context.SaveChanges();
            
            return Ok(new AcceptFriendPacketResponse
            {
                AcceptFriendOk = true,
                Accept = false
            });
        }
    }
    
    [HttpPost]
    [Route("SearchUsername")]
    public IActionResult SearchUsername([FromBody] SearchUsernamePacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null)
        {
            _logger.LogWarning("SearchUsername Unauthorized");
            return Unauthorized();
        }
        
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        var res = new SearchUsernamePacketResponse
        {
            FriendUserInfos = new List<FriendUserInfo>()
        };

        var friendUsers = _context.User.AsNoTracking()
            .Where(user => user.UserId != userId && user.UserName.Contains(required.Username))
            .Take(10);

        var friendUserStats = friendUsers
            .GroupJoin(
                _context.UserStats.AsNoTracking(),
                friendUser => friendUser.UserId,
                friendUserStat => friendUserStat.UserId,
                (friendUser, friendUserStats) => new { friendUser, friendUserStats })
            .SelectMany(
                temp => temp.friendUserStats.DefaultIfEmpty(),
                (temp, friendUserStat) => new { temp.friendUser, friendUserStat });
        
        var friendUserInfoList = friendUserStats
            .Select(result => new
            {
                result.friendUser,
                result.friendUserStat,
                Friend = _context.Friends.AsNoTracking().FirstOrDefault(friend 
                    => friend.UserId == result.friendUser.UserId 
                       && friend.FriendId == userId 
                       && friend.Status != FriendStatus.Blocked)
            })
            .ToList()
            .Select(result => new FriendUserInfo
            {
                UserName = result.friendUser.UserName,
                Level = result.friendUserStat?.UserLevel ?? 0,
                RankPoint = result.friendUserStat?.RankPoint ?? 0,
                FriendStatus = result.Friend?.Status ?? FriendStatus.None
            })
            .ToList();
        
        if (friendUserInfoList.Count == 0)
        {
            res.FriendUserInfos = new List<FriendUserInfo>();
            res.SearchUsernameOk = false;
            return Ok(res);
        }
        
        res.FriendUserInfos = friendUserInfoList;
        res.SearchUsernameOk = true;
        return Ok(res);
    }

    [HttpPut]
    [Route("DeleteFriend")]
    public IActionResult DeleteFriend([FromBody] FriendRequestPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null)
        {
            _logger.LogWarning("Requested Username Unauthorized");
            return Unauthorized();
        }
        
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null)
        {
            _logger.LogWarning("Requested Username Unauthorized");
            return Unauthorized();
        }
        
        var friendId = _context.User.AsNoTracking()
            .FirstOrDefault(user => user.UserName == required.FriendUsername)?.UserId;
        if (friendId == null)
        {
            _logger.LogWarning("Requested Username Unauthorized");
            return Ok(new FriendRequestPacketResponse
            {
                FriendRequestOk = false,
                FriendStatus = FriendStatus.None,
            });
        }

        var friendRelations = _context.Friends
            .Where(f => (f.UserId == userId && f.FriendId == friendId) 
                        || (f.UserId == friendId && f.FriendId == userId))
            .ToList();
        var friendRelation1 = friendRelations
            .FirstOrDefault(friend => friend.UserId == userId && friend.FriendId == friendId);
        var friendRelation2 = friendRelations
            .FirstOrDefault(friend => friend.UserId == friendId && friend.FriendId == userId);
        if (friendRelation1 == null || friendRelation2 == null)
        {
            _logger.LogWarning("Requested Username Unauthorized");
            return Ok(new FriendRequestPacketResponse
            {
                FriendRequestOk = false,
                FriendStatus = FriendStatus.None,
            });
        }

        if (required.CurrentFriendStatus == FriendStatus.Blocked)
        {
            friendRelation1.Status = FriendStatus.Blocked;
            friendRelation2.Status = FriendStatus.None;
            _context.Friends.Update(friendRelation1);
            _context.Friends.Update(friendRelation2);
        }
        
        if (required.CurrentFriendStatus == FriendStatus.None)
        {
            _context.Friends.Remove(friendRelation1);
            _context.Friends.Remove(friendRelation2);
        }
        
        _context.SaveChanges();
        
        return Ok(new FriendRequestPacketResponse
        {
            FriendRequestOk = true,
            FriendStatus = required.CurrentFriendStatus,
        });
    }
}