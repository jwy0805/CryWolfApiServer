using ApiServer.DB;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;

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
    
    public SinglePlayController(
        AppDbContext context,
        SinglePlayService singlePlayService,
        ApiService apiService,
        RewardService rewardService, 
        TokenValidator tokenValidator)
    {
        _context = context;
        _singlePlayService = singlePlayService;
        _apiService = apiService;
        _rewardService = rewardService;
        _tokenValidator = tokenValidator;
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
        var userStage = _context.UserStage.Where(userStage => userStage.UserId == userId).ToList();
        var stageEnemies = _singlePlayService.StageEnemies;
        var stageRewards = _singlePlayService.StageRewards;

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
        
        var stageEnemyInfo = stageEnemies
            .GroupBy(se => se.StageId)
            .Select(group => new StageEnemyInfo
            {
                StageId = group.Key,
                UnitIds = group.Select(se => se.UnitId).ToList()
            }).ToList();
     
        var stageRewardInfo = stageRewards
            .GroupBy(sr => sr.StageId)
            .Select(group => new StageRewardInfo
            {
                StageId = group.Key,
                RewardProducts = group.Select(sr => new SingleRewardInfo
                {
                    ItemId = sr.ProductId,
                    ProductType = sr.ProductType,
                    Star = sr.Star,
                    Count = sr.Count,
                }).ToList()
            }).ToList();
        
        res.UserStageInfos = userStageInfo;
        res.StageEnemyInfos = stageEnemyInfo;
        res.StageRewardInfos = stageRewardInfo;
        res.LoadStageInfoOk = true;
        
        return Ok(res);
    }
    
    [HttpPut("StartGame")]
    public async Task<IActionResult> StartGame([FromBody] ChangeActPacketSingleRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var userIdInAccessToken = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userIdInAccessToken == null) return Unauthorized();
        
        var res = new ChangeActPacketSingleResponse();
        var userId = userIdInAccessToken.Value;
        var user = _context.User.FirstOrDefault(u => u.UserId == userId);
        var battleSetting = _context.BattleSetting.FirstOrDefault(bs => bs.UserId == userId);
        var deck = _context.Deck
            .FirstOrDefault(d => d.UserId == userId && d.Faction == required.Faction && d.LastPicked);
        var userStage = _context.UserStage
            .FirstOrDefault(userStage => userStage.UserId == userId && userStage.StageId == required.StageId);

        if (user == null || battleSetting == null || deck == null || userStage == null)
        {
            res.ChangeOk = false;
            return NotFound();
        }
        
        var userInfo = new
        {
            User = user,
            BattleSetting = battleSetting,
            Deck = deck
        };
        
        await _context.SaveChangesExtendedAsync();

        var singlePlayPacket = new SinglePlayStartPacketRequired
        {
            UserId = userId,
            Faction = required.Faction,
            MapId = 1, // Modify this when the game has multiple maps
            SessionId = required.SessionId,
            StageId = required.StageId
        };

        await _apiService.SendRequestToSocketAsync<SinglePlayStartPacketRequired>(
            "singlePlay", singlePlayPacket, HttpMethod.Post);
        
        return Ok(res);
    }
}