using ApiServer.DB;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;

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
    
    public MatchController(
        AppDbContext context, 
        ApiService apiService,
        TokenService tokenService, 
        TokenValidator tokenValidator, 
        MatchService matchService,
        RewardService rewardService)
    {
        _context = context;
        _apiService = apiService;
        _tokenService = tokenService;
        _tokenValidator = tokenValidator;
        _matchService = matchService;
        _rewardService = rewardService;
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
        
        // MatchMakingServer에 유저 정보 전달
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
    public IActionResult RankGamReward([FromBody] GameRewardPacketRequired required)
    {
        if (required.WinUserId == -1)
        {
            // Lose at single mode
            var loseUser = _context.User.FirstOrDefault(u => u.UserId == required.LoseUserId);
            var loseUserStats = _context.UserStats.FirstOrDefault(us => us.UserId == required.LoseUserId);
            var loseUserMatchInfo = _context.UserMatch.FirstOrDefault(um => um.UserId == required.LoseUserId);

            if (loseUser == null || loseUserStats == null || loseUserMatchInfo == null) return NotFound();
            
            loseUserStats.RankPoint -= required.LoseRankPoint;
            var loserRewardsList = GetLoserRewards(required.LoseUserId, loseUserStats.RankPoint, required.LoseRankPoint);
            var res = new GameRewardPacketResponse
            {
                GetGameRewardOk = true,
                WinnerRewards = new List<RewardInfo>(),
                LoserRewards = loserRewardsList
            };

            loseUserStats.Gold += loserRewardsList
                .FirstOrDefault(reward => reward.ProductType == ProductType.Gold)?.Count ?? 0;
            AddMaterialRewards(loseUser.UserId, loserRewardsList);
            _context.SaveChangesExtended();

            return Ok(res);
        }
        else if (required.LoseUserId == -1)
        {
            // Win at single mode
            var winUser = _context.User.FirstOrDefault(u => u.UserId == required.WinUserId);
            var winUserStats = _context.UserStats.FirstOrDefault(us => us.UserId == required.WinUserId);
            var winUserMatchInfo = _context.UserMatch.FirstOrDefault(um => um.UserId == required.WinUserId);

            if (winUser == null || winUserStats == null || winUserMatchInfo == null) return NotFound();
            
            winUserStats.RankPoint += required.WinRankPoint;
            var winnerRewardsList = GetWinnerRewards(required.WinUserId, winUserStats.RankPoint, required.WinRankPoint);
            var res = new GameRewardPacketResponse
            {
                GetGameRewardOk = true,
                WinnerRewards = winnerRewardsList,
                LoserRewards = new List<RewardInfo>(),
            };

            winUserStats.Gold += winnerRewardsList
                .FirstOrDefault(reward => reward.ProductType == ProductType.Gold)?.Count ?? 0;
            AddMaterialRewards(winUser.UserId, winnerRewardsList);
            _context.SaveChangesExtended();
            return Ok(res);
        }
        else
        {
            // Rank Game, change user match info, stat
            var winUser = _context.User.FirstOrDefault(u => u.UserId == required.WinUserId);
            var loseUser = _context.User.FirstOrDefault(u => u.UserId == required.LoseUserId);
            var winUserStats = _context.UserStats.FirstOrDefault(us => us.UserId == required.WinUserId);
            var loseUserStats = _context.UserStats.FirstOrDefault(us => us.UserId == required.LoseUserId);
            var winUserMatchInfo = _context.UserMatch.FirstOrDefault(um => um.UserId == required.WinUserId);
            var loseUserMatchInfo = _context.UserMatch.FirstOrDefault(um => um.UserId == required.LoseUserId);
            
            if (loseUser == null || loseUserStats == null || loseUserMatchInfo == null) return NotFound();
            if (winUser == null || winUserStats == null || winUserMatchInfo == null) return NotFound();

            winUserStats.RankPoint += required.WinRankPoint;
            loseUserStats.RankPoint -= required.LoseRankPoint;

            if (winUserStats.RankPoint > winUserStats.HighestRankPoint)
            {
                winUserStats.HighestRankPoint = winUserStats.RankPoint;
            }

            winUserMatchInfo.WinRankMatch += 1;
            loseUserMatchInfo.LoseRankMatch += 1;

            var winnerRewardsList = GetWinnerRewards(required.WinUserId, winUserStats.RankPoint, required.WinRankPoint);
            var loserRewardsList = GetLoserRewards(required.LoseUserId, loseUserStats.RankPoint, required.LoseRankPoint);
            var res = new GameRewardPacketResponse
            {
                GetGameRewardOk = true,
                WinnerRewards = winnerRewardsList,
                LoserRewards = loserRewardsList 
            };
        
            winUserStats.Gold += winnerRewardsList.FirstOrDefault(reward => reward.ProductType == ProductType.Gold)?.Count ?? 0;
            loseUserStats.Gold += loserRewardsList.FirstOrDefault(reward => reward.ProductType == ProductType.Gold)?.Count ?? 0;
        
            AddMaterialRewards(winUser.UserId, winnerRewardsList);
            AddMaterialRewards(loseUser.UserId, loserRewardsList);
        
            _context.SaveChangesExtended();
        
            return Ok(res);        
        }
    }

    public List<RewardInfo> GetWinnerRewards(int userId, int rankPoint, int rankPointBefore)
    {
        return _rewardService.GetRankRewards(userId, rankPoint, rankPointBefore, true);
    }
    
    public List<RewardInfo> GetLoserRewards(int userId, int rankPoint, int rankPointBefore)
    {
        return _rewardService.GetRankRewards(userId, rankPoint, rankPointBefore, false);
    }

    private void AddMaterialRewards(int userId, List<RewardInfo> rewards)
    {
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
        Console.WriteLine("cancel");
        var cancelPacket = new MatchCancelPacketRequired { UserId = user.UserId };
        await _apiService
            .SendRequestAsync<MatchCancelPacketResponse>("MatchMaking/CancelMatch", cancelPacket, HttpMethod.Post);
        
        res.CancelOk = await _context.SaveChangesExtendedAsync();
        Console.WriteLine("cancel ok");
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
        Console.WriteLine("surrender ok");
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