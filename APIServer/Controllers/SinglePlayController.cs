using ApiServer.DB;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SinglePlayController: ControllerBase
{
    private readonly AppDbContext _context;
    private readonly SinglePlayService _singlePlayService;
    private readonly ApiService _apiService;
    private readonly RewardService _rewardService;
    private readonly TokenValidator _tokenValidator;
    private readonly ILogger<SinglePlayController> _logger;
    
    public SinglePlayController(
        AppDbContext context,
        SinglePlayService singlePlayService,
        ApiService apiService,
        RewardService rewardService, 
        TokenValidator tokenValidator,
        ILogger<SinglePlayController> logger)
    {
        _context = context;
        _singlePlayService = singlePlayService;
        _apiService = apiService;
        _rewardService = rewardService;
        _tokenValidator = tokenValidator;
        _logger = logger;
    }
    
    [HttpPost("LoadStageInfo")]
    public IActionResult LoadStageInfo([FromBody] LoadStageInfoPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
    
        var res = new LoadStageInfoPacketResponse();
        var userIdInAccessToken = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userIdInAccessToken == null) return Unauthorized();
        
        var userId = userIdInAccessToken.Value;
        var stageInfoList = _singlePlayService.StageInfos;
        var userStage = _context.UserStage.AsNoTracking()
            .Where(userStage => userStage.UserId == userId).ToList();

        List<UserStageInfo> userStageInfo = new();
        if (userStage.Count != 0)
        {
            userStageInfo = userStage
            .Select(us => new UserStageInfo
            {
                UserId = userId,
                StageId = us.StageId,
                StageLevel = us.StageLevel,
                StageStar = us.StageStar,
                IsCleared = us.IsCleared,
                IsAvailable = us.IsAvailable
            }).ToList();
        }

        res.UserStageInfos = userStageInfo;
        
        res.StageEnemyInfos = stageInfoList.Select(si => new StageEnemyInfo
        {
            StageId = si.StageId,
            UnitIds = si.StageEnemy.Select(se => se.UnitId).ToList()
        }).ToList();
        
        res.StageRewardInfos = stageInfoList.Select(si => new StageRewardInfo
        {
            StageId = si.StageId,
            RewardProducts = si.StageReward.Select(sr => new SingleRewardInfo
            {
                ItemId = sr.ProductId,
                ProductType = sr.ProductType,
                Star = sr.Star,
                Count = sr.Count,
            }).ToList()
        }).ToList();
        
        res.LoadStageInfoOk = true;
        
        return Ok(res);
    }
    
    [HttpPut("StartGame")]
    public async Task<IActionResult> StartGame([FromBody] ChangeActPacketSingleRequired required)
    {
        _logger.LogInformation("[StartGame] method called");
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null)
        {
            return Unauthorized();
        }
        
        var userIdNull = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userIdNull == null)
        {
            return Unauthorized();
        }
        
        var res = new ChangeActPacketSingleResponse();
        var userId = userIdNull.Value;
        var user = _context.User.FirstOrDefault(u => u.UserId == userId);
        var battleSetting = _context.BattleSetting.AsNoTracking().FirstOrDefault(bs => bs.UserId == userId);
        var deck = _context.Deck.AsNoTracking()
            .FirstOrDefault(d => d.UserId == userId && d.Faction == required.Faction && d.LastPicked);
        var userStage = _context.UserStage.AsNoTracking()
            .FirstOrDefault(us => us.UserId == userId && us.StageId == required.StageId);
        
        if (user == null || battleSetting == null || deck == null)
        {
            _logger.LogInformation("[StartGame] User or BattleSetting or Deck or UserStage not found on single play start");
            return NotFound();
        }

        if (userStage == null || userStage.StageId != required.StageId)
        {
            _logger.LogInformation($"[StartGame] Stage id mismatch - suspected cheating user - {userId}");
            return NotFound();            
        }

        user.Act = UserAct.InSingleGame;
        await _context.SaveChangesExtendedAsync();

        var deckUnits = _context.DeckUnit.AsNoTracking()
            .Where(du => du.DeckId == deck.DeckId)
            .Select(du => du.UnitId).ToArray();
        var stageInfo = _singlePlayService.StageInfos.FirstOrDefault(si => si.StageId == userStage.StageId);
        
        if (stageInfo == null)
        {
            _logger.LogInformation("[StartGame] StageInfo not found");
            return NotFound();
        }
        
        var singlePlayPacket = new SinglePlayStartPacketRequired
        {
            UserId = userId,
            UserFaction = required.Faction,
            UnitIds = deckUnits,
            CharacterId = battleSetting.CharacterId,
            AssetId = required.Faction == Faction.Sheep ? battleSetting.SheepId : battleSetting.EnchantId,
            EnemyUnitIds = stageInfo.StageEnemy.Select(se => se.UnitId).ToArray(),
            EnemyCharacterId = stageInfo.CharacterId,
            EnemyAssetId = stageInfo.AssetId,
            MapId = _singlePlayService.StageInfos.First(si => si.StageId == userStage.StageId).MapId,
            SessionId = required.SessionId,
            StageId = required.StageId
        };

        _logger.LogInformation($"[StartGame] {required.SessionId} {singlePlayPacket.CharacterId}, {singlePlayPacket.EnemyCharacterId}");
        await _apiService.SendRequestToSocketAsync<SinglePlayStartPacketRequired>(
            "singlePlay", singlePlayPacket, HttpMethod.Post);
        
        res.ChangeOk = true;
        
        return Ok(res);
    }
}