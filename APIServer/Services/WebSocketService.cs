using ApiServer.DB;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Services;

public class WebSocketService
{
    private readonly AppDbContext _context;
    private readonly TokenValidator _tokenValidator;
    private readonly ILogger<RewardService> _logger;
    
    public WebSocketService(AppDbContext context, TokenValidator tokenValidator, ILogger<RewardService> logger)
    {
        _context = context;
        _tokenValidator = tokenValidator;
        _logger = logger;
    }
    
    public AcceptInvitationPacketResponse CreateAcceptInvitationPacket(string userTag, Faction myFaction)
    {
        var user = _context.User.AsNoTracking().FirstOrDefault(u => u.UserTag == userTag);
        if (user == null)
        {
            _logger.LogError($"User {userTag} not found.");
            return new AcceptInvitationPacketResponse { AcceptInvitationOk = false };
        }
        
        var userStats = _context.UserStats.AsNoTracking().FirstOrDefault(s => s.UserId == user.UserId);
        if (userStats == null)
        {
            _logger.LogError($"UserStats {userTag} not found.");
            return new AcceptInvitationPacketResponse { AcceptInvitationOk = false };
        }
        
        var userInfo = new UserInfo
        {
            UserName = user.UserName,
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
        
        _logger.LogError($"DeckInfo {user.UserName} not found.");
        return new AcceptInvitationPacketResponse { AcceptInvitationOk = false };
    }
    
    public DeckInfo? GetDeckInfo(int userId, Faction faction)
    {
        return _context.Deck.AsNoTracking()
            .Where(d => d.UserId == userId && d.Faction == faction && d.LastPicked)
            .Select(d => new DeckInfo
            {
                DeckId = d.DeckId,
                UnitInfo = _context.DeckUnit.AsNoTracking()
                    .Where(du => du.DeckId == d.DeckId)
                    .Join(_context.Unit.AsNoTracking(),
                        du => du.UnitId,
                        u  => u.UnitId,
                        (du, u) => u)
                    .OrderBy(u => u.Class)          
                    .Select(unit => new UnitInfo
                    {
                        Id      = (int)unit.UnitId,
                        Class   = unit.Class,         
                        Level   = unit.Level,
                        Species = (int)unit.Species,
                        Role    = unit.Role,
                        Faction = unit.Faction,
                        Region  = unit.Region
                    })
                    .ToArray(),
                DeckNumber = d.DeckNumber,
                Faction    = (int)d.Faction,
                LastPicked = d.LastPicked
            })
            .FirstOrDefault();
    }

    public FriendlyMatchPacketRequired CreateMatchPacket(Faction hostFaction, SignalRHub.SignalRHub.GameRoom room)
    {
        var user1 = _context.User.AsNoTracking()
            .FirstOrDefault(u => u.UserName == room.Username1);
        var user2 = _context.User.AsNoTracking()
            .FirstOrDefault(u => u.UserName == room.Username2);
        if (user1 == null || user2 == null)
        {
            _logger.LogError("One of the users in the room not found.");
            return new FriendlyMatchPacketRequired();
        }
        
        user1.Act = UserAct.InMultiGame;
        user2.Act = UserAct.InMultiGame;
        _context.SaveChangesExtended();

        var userId1 = user1.UserId;
        var userId2 = user2.UserId;
        var battleSetting1 = _context.BattleSetting.AsNoTracking()
            .FirstOrDefault(bs => bs.UserId == userId1);
        var battleSetting2 = _context.BattleSetting.AsNoTracking()
            .FirstOrDefault(bs => bs.UserId == userId2);
        var deck1 = _context.Deck.AsNoTracking()
            .FirstOrDefault(d => d.UserId == userId1 && d.Faction == hostFaction && d.LastPicked);
        var deck2 = _context.Deck.AsNoTracking()
            .FirstOrDefault(d => d.UserId == userId2 && d.Faction != hostFaction && d.LastPicked);
        if (battleSetting1 == null || battleSetting2 == null || deck1 == null || deck2 == null)
        {
            _logger.LogError("Information not found for one of the players in the room.");
            return new FriendlyMatchPacketRequired();
        }
        
        var deckUnits1 = _context.DeckUnit.AsNoTracking()
            .Where(du => du.DeckId == deck1.DeckId)
            .Select(du => du.UnitId).ToArray();
        var deckUnits2 = _context.DeckUnit.AsNoTracking()
            .Where(du => du.DeckId == deck2.DeckId)
            .Select(du => du.UnitId).ToArray();
        
        if (hostFaction == Faction.Sheep)
        {
            return new FriendlyMatchPacketRequired
            {
                SheepUserId = userId1,
                SheepUserName = room.Username1,
                SheepSessionId = room.SessionId1,
                WolfUserId = userId2,
                WolfUserName = room.Username2,
                WolfSessionId = room.SessionId2,
                MapId = 1,
                SheepCharacterId = (CharacterId)battleSetting1.CharacterId,
                WolfCharacterId = (CharacterId)battleSetting2.CharacterId,
                SheepId = (SheepId)battleSetting1.SheepId,
                EnchantId = (EnchantId)battleSetting2.EnchantId,
                SheepUnitIds = deckUnits1,
                WolfUnitIds = deckUnits2,
            };
        }
        else
        {
            return new FriendlyMatchPacketRequired
            {
                SheepUserId = userId2,
                SheepUserName = room.Username2,
                SheepSessionId = room.SessionId2,
                WolfUserId = userId1,
                WolfUserName = room.Username1,
                WolfSessionId = room.SessionId1,
                MapId = 1,
                SheepCharacterId = (CharacterId)battleSetting2.CharacterId,
                WolfCharacterId = (CharacterId)battleSetting1.CharacterId,
                SheepId = (SheepId)battleSetting2.SheepId,
                EnchantId = (EnchantId)battleSetting1.EnchantId,
                SheepUnitIds = deckUnits2,
                WolfUnitIds = deckUnits1,
            };
        }
    }
}