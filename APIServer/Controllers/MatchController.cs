using ApiServer.DB;
using ApiServer.Providers;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MatchController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ApiService _apiService;
    private readonly TokenService _tokenService;
    private readonly TokenValidator _tokenValidator;
    private readonly MatchService _matchService;
    private readonly RewardService _rewardService;
    private readonly ProductClaimService _claimService;
    private readonly CachedDataProvider _cachedDataProvider;
    private readonly ILogger<MatchController> _logger;
    
    public MatchController(
        AppDbContext context, 
        ApiService apiService,
        TokenService tokenService, 
        TokenValidator tokenValidator, 
        MatchService matchService,
        RewardService rewardService,
        ProductClaimService claimService,
        CachedDataProvider cachedDataProvider,
        ILogger<MatchController> logger)
    {
        _context = context;
        _apiService = apiService;
        _tokenService = tokenService;
        _tokenValidator = tokenValidator;
        _matchService = matchService;
        _rewardService = rewardService;
        _claimService = claimService;
        _cachedDataProvider = cachedDataProvider;
        _logger = logger;
    }
    
    [HttpPut]
    [Route("TestMatchMaking")]
    public async Task<IActionResult> TestMatchMaking([FromBody] ChangeActTestPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var res = new ChangeActPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        var faction = required.Faction;
        if (userId == null) return Unauthorized();

        var user = _context.User.FirstOrDefault(u => u.UserId == userId);
        var userStats = _context.UserStats.FirstOrDefault(us => us.UserId == userId);
        var battleSetting = _context.BattleSetting.FirstOrDefault(bs => bs.UserId == userId);
        var deck = _context.Deck
            .FirstOrDefault(d => d.UserId == userId && d.Faction == faction && d.LastPicked);

        var deckUnits = Array.Empty<UnitId>();
        if (deck != null)
        {
            deckUnits = _context.DeckUnit
                .Where(du => du.DeckId == deck.DeckId)
                .Select(du => du.UnitId)
                .ToArray();
        }

        var userInfo = new
        {
            User = user,
            UserStats = userStats,
            BattleSetting = battleSetting,
            Deck = deck,
            DeckUnits = deckUnits
        };

        if (userInfo.User == null 
            || userInfo.UserStats == null
            || userInfo.BattleSetting == null 
            || userInfo.Deck == null
            || userInfo.DeckUnits.Length != 6)
        {
            res.ChangeOk = false;
            return NotFound();
        }

        // 사용자 액션 업데이트
        userInfo.User.Act = UserAct.MatchMaking;
        res.ChangeOk = true;
        await _context.SaveChangesExtendedAsync();
        
        // MatchMakingServer에 Test NPC 정보 전달
        var matchPacket = new MatchMakingPacketRequired
        {
            Test = true,
            UserId = userInfo.User.UserId,
            SessionId = required.SessionId,
            UserName = userInfo.User.UserName,
            Faction = required.Faction,
            RankPoint = userInfo.UserStats.RankPoint,
            RequestTime = DateTime.Now,
            MapId = required.MapId,
            CharacterId = userInfo.BattleSetting.CharacterId,
            AssetId = required.Faction == Faction.Sheep 
                ? userInfo.BattleSetting.SheepId 
                : userInfo.BattleSetting.EnchantId,
            UnitIds = userInfo.DeckUnits,
            Achievements = new List<int>()
        };

        await _apiService
            .SendRequestAsync<MatchMakingPacketResponse>("MatchMaking/Match", matchPacket, HttpMethod.Post);

        return Ok(res);
    }

    [HttpPost]
    [Route("GetQueueCounts")]
    public GetQueueCountsPacketResponse GetQueueCounts([FromBody] GetQueueCountsPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null)
        {
            return new GetQueueCountsPacketResponse { GetQueueCountsOk = false };
        }

        var res = new GetQueueCountsPacketResponse
        {
            GetQueueCountsOk = true,
            QueueCountsSheep = _cachedDataProvider.QueueCountsSheep,
            QueueCountsWolf = _cachedDataProvider.QueueCountsWolf,
        };

        return res;
    }

    [HttpPost]
    [Route("ReportQueueCounts")]
    public ReportQueueCountsResponse ReportQueueCounts([FromBody] ReportQueueCountsRequired required)
    {
        _cachedDataProvider.QueueCountsSheep = required.SheepQueueCount;
        _cachedDataProvider.QueueCountsWolf = required.WolfQueueCount;

        var res = new ReportQueueCountsResponse
        {
            ReportQueueCountsOk = true,
        };

        return res;
    }
    
    [HttpPut]
    [Route("ChangeActByMatchMaking")]
    public async Task<IActionResult> ChangeActByMatchMaking([FromBody] ChangeActPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var res = new ChangeActPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        var faction = required.Faction;
        if (userId == null) return Unauthorized();

        var user = _context.User.FirstOrDefault(u => u.UserId == userId);
        var userStats = _context.UserStats.FirstOrDefault(us => us.UserId == userId);
        var battleSetting = _context.BattleSetting.FirstOrDefault(bs => bs.UserId == userId);
        var deck = _context.Deck
            .FirstOrDefault(d => d.UserId == userId && d.Faction == faction && d.LastPicked);

        var deckUnits = Array.Empty<UnitId>();
        if (deck != null)
        {
            deckUnits = _context.DeckUnit
                .Where(du => du.DeckId == deck.DeckId)
                .Select(du => du.UnitId)
                .ToArray();
        }

        var userInfo = new
        {
            User = user,
            UserStats = userStats,
            BattleSetting = battleSetting,
            Deck = deck,
            DeckUnits = deckUnits
        };

        if (userInfo.User == null 
            || userInfo.UserStats == null
            || userInfo.BattleSetting == null 
            || userInfo.Deck == null
            || userInfo.DeckUnits.Length != 6)
        {
            res.ChangeOk = false;
            return NotFound();
        }

        // 사용자 액션 업데이트
        userInfo.User.Act = UserAct.MatchMaking;
        res.ChangeOk = true;
        await _context.SaveChangesExtendedAsync();
        
        // MatchMakingServer에 유저 정보 전달
        var matchPacket = new MatchMakingPacketRequired
        {
            UserId = userInfo.User.UserId,
            SessionId = required.SessionId,
            UserName = userInfo.User.UserName,
            Faction = required.Faction,
            RankPoint = userInfo.UserStats.RankPoint,
            RequestTime = DateTime.Now,
            MapId = required.MapId,
            CharacterId = userInfo.BattleSetting.CharacterId,
            AssetId = required.Faction == Faction.Sheep 
                ? userInfo.BattleSetting.SheepId 
                : userInfo.BattleSetting.EnchantId,
            UnitIds = userInfo.DeckUnits,
            Achievements = new List<int>()
        };
        
        await _apiService
            .SendRequestAsync<MatchMakingPacketResponse>("MatchMaking/Match", matchPacket, HttpMethod.Post);
        
        return Ok(res);
    }

    [HttpPut]
    [Route("ChangeActByTutorial")]
    public async Task<IActionResult> ChangeActByTutorial([FromBody] ChangeActPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var res = new ChangeActPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        var faction = required.Faction;
        if (userId == null) return Unauthorized();
        
        var user = _context.User.FirstOrDefault(u => u.UserId == userId);
        var battleSetting = _context.BattleSetting.FirstOrDefault(bs => bs.UserId == userId);
        var deck = _context.Deck
            .FirstOrDefault(d => d.UserId == userId && d.Faction == faction && d.LastPicked);
        
        var deckUnits = Array.Empty<UnitId>();
        if (deck != null)
        {
            deckUnits = _context.DeckUnit
                .Where(du => du.DeckId == deck.DeckId)
                .Select(du => du.UnitId)
                .ToArray();
        }

        var userInfo = new
        {
            User = user,
            BattleSetting = battleSetting,
            Deck = deck,
            DeckUnits = deckUnits
        };

        if (userInfo.User == null 
            || userInfo.BattleSetting == null 
            || userInfo.Deck == null
            || userInfo.DeckUnits.Length != 6)
        {
            res.ChangeOk = false;
            _logger.LogWarning("start tutorial error");
            return NotFound();
        }

        // 사용자 액션 업데이트
        userInfo.User.Act = UserAct.InTutorial;
        res.ChangeOk = true;
        await _context.SaveChangesExtendedAsync();

        var tutorialPacket = new TutorialStartPacketRequired
        {
            UserId = userInfo.User.UserId,
            SessionId = required.SessionId,
            UserFaction = required.Faction,
            MapId = required.MapId,
            CharacterId = userInfo.BattleSetting.CharacterId,
            AssetId = required.Faction == Faction.Sheep 
                ? userInfo.BattleSetting.SheepId 
                : userInfo.BattleSetting.EnchantId,
            EnemyCharacterId = 2001,
            EnemyAssetId = required.Faction == Faction.Sheep ? 1001 : 901,
            UnitIds = userInfo.DeckUnits
        };
        
        await _apiService
            .SendRequestToSocketAsync<TutorialStartPacketResponse>("tutorial", tutorialPacket, HttpMethod.Post);
        
        return Ok(res);
    }

    [HttpPost]
    [Route("SetMatchInfo")]
    public IActionResult SetMatchInfo([FromBody] SendMatchInfoPacketRequired required)
    {
        _matchService
            .AddMatchInfo(required.SheepUserId, required.SheepSessionId, required.WolfUserId, required.WolfSessionId);
        var res = new SendMatchInfoPacketResponse { SendMatchInfoOk = true };
        return Ok(res);
    }

    [HttpPut]
    [Route("RankGameReward")]
    public async Task<IActionResult> RankGamReward([FromBody] RankGameRewardPacketRequired required)
    {
        if (required.WinUserId == -1 || required.LoseUserId == -1) return Ok();
        
        // Rank Game, change user match info, stat
        var winUser = _context.User.FirstOrDefault(u => u.UserId == required.WinUserId);
        var loseUser = _context.User.FirstOrDefault(u => u.UserId == required.LoseUserId);
        var winUserStats = _context.UserStats.FirstOrDefault(us => us.UserId == required.WinUserId);
        var loseUserStats = _context.UserStats.FirstOrDefault(us => us.UserId == required.LoseUserId);
        var winUserMatchInfo = _context.UserMatch.FirstOrDefault(um => um.UserId == required.WinUserId);
        var loseUserMatchInfo = _context.UserMatch.FirstOrDefault(um => um.UserId == required.LoseUserId);
        
        if (loseUser == null || loseUserStats == null || loseUserMatchInfo == null) return NotFound();
        if (winUser == null || winUserStats == null || winUserMatchInfo == null) return NotFound();

        winUser.Act = UserAct.InLobby;
        winUserStats.RankPoint += required.WinRankPoint;
        loseUser.Act = UserAct.InLobby;
        loseUserStats.RankPoint -= required.LoseRankPoint;

        if (winUserStats.RankPoint > winUserStats.HighestRankPoint)
        {
            winUserStats.HighestRankPoint = winUserStats.RankPoint;
        }

        winUserMatchInfo.WinRankMatch += 1;
        loseUserMatchInfo.LoseRankMatch += 1;

        var winnerRewardsList = GetWinnerRewards(required.WinUserId, winUserStats.RankPoint, required.WinRankPoint);
        var loserRewardsList = GetLoserRewards(required.LoseUserId, loseUserStats.RankPoint, required.LoseRankPoint);
        var res = new RankGameRewardPacketResponse
        {
            GetGameRewardOk = true,
            WinnerRewards = winnerRewardsList,
            LoserRewards = loserRewardsList 
        };
    
        await Task.WhenAll(
            AddGameRewards(winUser.UserId, winnerRewardsList),
            AddGameRewards(loseUser.UserId, loserRewardsList));
        await _context.SaveChangesExtendedAsync();
    
        return Ok(res);  
    }

    [HttpPut]
    [Route("SingleGameReward")]
    public async Task<IActionResult> GetSingleGameReward([FromBody] SingleGameRewardPacketRequired required)
    {
        var user = _context.User.FirstOrDefault(u => u.UserId == required.UserId);
        var userStats = _context.UserStats.FirstOrDefault(us => us.UserId == required.UserId);
        var userStage = _context.UserStage
            .FirstOrDefault(us => us.UserId == required.UserId && us.StageId == required.StageId);

        if (user == null || userStats == null || userStage == null) return NotFound();
        
        user.Act = UserAct.InLobby;
        var res = new SingleGameRewardPacketResponse();

        if (required.Star > 0)
        {
            // win
            var prevStar = userStage.StageStar;
            var prevRewards = _rewardService.GetSingleRewards(required.StageId, prevStar);
            var newStar = required.Star;
            var newRewards = _rewardService.GetSingleRewards(required.StageId, newStar);
            var prevKeys = prevRewards
                .Select(r => (r.ItemId, r.ProductType, r.Star))
                .ToHashSet();
            var rewards = newRewards
                .Where(r => !prevKeys.Contains((r.ItemId, r.ProductType, r.Star)))
                .ToList();            
            var rewardInfo = rewards.Select(reward => new RewardInfo
            {
                ItemId = reward.ItemId,
                ProductType = reward.ProductType,
                Count = reward.Count
            }).ToList();
            
            res.Rewards = rewards;
            userStage.StageStar = newStar;
            await AddGameRewards(user.UserId, rewardInfo);
            
            // Add Next Stage on DB
            if (required.StageId != 1009 || required.StageId != 5009)
            {
                var nextStage = new UserStage
                {
                    UserId = user.UserId,
                    StageId = required.StageId + 1,
                    StageLevel = _context.Stage.AsNoTracking()
                        .FirstOrDefault(s => s.StageId == required.StageId + 1)!.StageLevel,
                    StageStar = 0,
                    IsCleared = false,
                    IsAvailable = true
                };
                _logger.LogInformation($"NextStage: {nextStage.StageId}, UserId: {nextStage.UserId} at {DateTime.Now}");
                _context.UserStage.Add(nextStage);
            }
            
            _context.SaveChangesExtended();
        }
        
        res.GetGameRewardOk = true;
        
        return Ok(res);
    }

    [HttpPut]
    [Route("TutorialReward")]
    public IActionResult GetTutorialGameReward([FromBody] TutorialRewardPacketRequired required)
    {
        var user = _context.User.AsNoTracking().FirstOrDefault(u => u.UserId == required.UserId);
        var userUnit = _context.UserUnit;
        var userUnits = userUnit
            .Where(u => u.UserId == required.UserId)
            .Select(u => u.UnitId).ToList();
        if (user == null) return NotFound();
        user.Act = UserAct.InLobby;
        
        var res = new TutorialRewardPacketResponse();
        var reward = AddTutorialRewardUnit(required.Faction, userUnits, required.UserId);
        
        res.GetGameRewardOk = true;
        res.Rewards = new List<RewardInfo> { reward };
        
        return Ok(res);
    }

    private RewardInfo AddTutorialRewardUnit(Faction faction, List<UnitId> userUnits, int userId)
    {
        var knightUnits = _context.Unit.AsNoTracking()
            .Where(u => u.Class == UnitClass.Knight && u.Level == 3).ToList();
        var rewardUnit = knightUnits
            .Where(ku => ku.Faction == faction)
            .Select(ku => ku.UnitId).ToList()
            .Except(userUnits).FirstOrDefault();

        UserUnit newUnit;
        if (rewardUnit == UnitId.UnknownUnit)
        {
            newUnit = new UserUnit
            {
                UserId = userId,
                UnitId = faction == Faction.Wolf ? UnitId.Cactus : UnitId.Bloom,
                Count = 1,
            };
        }
        else
        {
            newUnit = new UserUnit
            {
                UserId = userId,
                UnitId = rewardUnit - 1,
                Count = 1,
            };
        }
        
        var userUnit = _context.UserUnit.FirstOrDefault(uu => uu.UserId == userId && uu.UnitId == newUnit.UnitId);

        Console.WriteLine($"unit : id - {userUnit?.UnitId}, count - {userUnit?.Count}");

        if (userUnit == null)
        {
            _context.UserUnit.Add(newUnit);
        }
        else
        {
            userUnit.Count++;
        }

        _context.SaveChangesExtended();
        
        return new RewardInfo
        {
            ItemId = (int)newUnit.UnitId,
            ProductType = ProductType.Unit,
            Count = 1
        };
    }
    
    public List<RewardInfo> GetWinnerRewards(int userId, int rankPoint, int rankPointBefore)
    {
        return _rewardService.GetRankRewards(userId, rankPoint, rankPointBefore, true);
    }
    
    public List<RewardInfo> GetLoserRewards(int userId, int rankPoint, int rankPointBefore)
    {
        return _rewardService.GetRankRewards(userId, rankPoint, rankPointBefore, false);
    }

    private async Task AddGameRewards(int userId, List<RewardInfo> rewards)
    {
        var productList = _cachedDataProvider.GetProducts();
        var userMail = _context.Mail;

        // Selectable or random product will be sent to mailbox, other products immediately added to user inventory.
        foreach (var reward in rewards)
        {
            if (reward.ProductType == ProductType.Container)
            {
                for (int i = 0; i < reward.Count; i++)
                {
                    var mail = new Mail
                    {
                        UserId = userId,
                        Claimed = false,
                        Expired = false,
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddDays(30),
                        Type = MailType.Product,
                        ProductId = reward.ItemId,
                        ProductCode = productList.First(p => p.ProductId == reward.ItemId).ProductCode,
                        Sender = "cry wolf"
                    };
                    
                    userMail.Add(mail);
                }
            }
            else
            {
                var productComposition = new ProductComposition
                {
                    ProductId = reward.ItemId,
                    ProductType = reward.ProductType,
                    Count = reward.Count,
                };
                
                await _claimService.StoreProductAsync(userId, productComposition);
                await _context.SaveChangesExtendedAsync();
            }
        }
        
        var userMaterial = _context.UserMaterial;
        foreach (var reward in rewards.Where(r => r.ProductType == ProductType.Material))
        {
            var existingMaterial = userMaterial
                .FirstOrDefault(um => um.UserId == userId && (int)um.MaterialId == reward.ItemId);

            if (existingMaterial != null)
            {
                existingMaterial.Count += reward.Count;
            }
            else
            {
                userMaterial.Add(new UserMaterial
                {
                    UserId = userId,
                    MaterialId = (MaterialId)reward.ItemId,
                    Count = reward.Count,
                });
            }
        }
    }
    
    [HttpPut]
    [Route("CancelMatchMaking")]
    public async Task<IActionResult> CancelMatchMaking([FromBody] CancelMatchPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var res = new CancelMatchPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null) return Unauthorized();

        var user = _context.User
            .FirstOrDefault(user => user.UserId == userId);
        if (user == null) return NotFound();
        user.Act = UserAct.InLobby;
        
        // Remove user from match making queue
        var cancelPacket = new MatchCancelPacketRequired { UserId = user.UserId };
        await _apiService
            .SendRequestAsync<MatchCancelPacketResponse>("MatchMaking/CancelMatch", cancelPacket, HttpMethod.Post);
        
        res.CancelOk = await _context.SaveChangesExtendedAsync();
        _logger.LogInformation("Cancel match making for user {UserId}", user.UserId);
        return Ok(res);
    }

    [HttpPost]
    [Route("GetRankPoint")]
    public IActionResult GetRankPoint([FromBody] GetRankPointPacketRequired required)
    {
        // TODO: Temp
        var res = new GetRankPointPacketResponse
        {
            WinPointSheep = 10,
            WinPointWolf = 10,
            LosePointSheep = 10,
            LosePointWolf = 10
        };
        
        return Ok(res);
    }
    
    [HttpPut]
    [Route("Surrender")]
    public async Task<IActionResult> GameEndedBySurrender([FromBody] SurrenderPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var userMatchDb = _context.UserMatch;
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal) ?? 0;
        if (userId == 0) return Unauthorized();
        
        var user = _context.User.FirstOrDefault(u => u.UserId == userId);
        if (user == null) return NotFound();

        var matchInfo = _matchService.FindMatchInfo(userId);
        if (matchInfo == null) return NotFound();
        _matchService.RemoveMatchInfo(userId);
        
        var resultPacket = new GameResultPacketRequired
        {
            UserId = userId,
            IsWin = false
        };
        
        await _apiService.SendRequestToSocketAsync<GameResultPacketResponse>("surrender", resultPacket, HttpMethod.Post);
        user.Act = UserAct.InLobby;
        
        var res = new SurrenderPacketResponse { SurrenderOk = await _context.SaveChangesExtendedAsync() };
        _logger.LogInformation("Surrender game {UserId}", user.UserId);
        return Ok(res);
    }

    [HttpPost]
    [Route("SessionDisconnect")]
    public IActionResult SessionDisconnect([FromBody] SessionDisconnectPacketRequired required)
    {
        _matchService.RemoveMatchInfo(required.UserId);
        var res = new SessionDisconnectPacketResponse { SessionDisconnectOk = true };
        return Ok(res);
    }
}