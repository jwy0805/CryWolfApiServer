using ApiServer.DB;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Services;

public class SinglePlayService
{
    private readonly AppDbContext _context;

    public List<StageInfo> StageInfos { get; set; } = new();
    
    public SinglePlayService(AppDbContext context)
    {
        _context = context;
        
        List<StageEnemy> stageEnemies = _context.StageEnemy.ToList();
        List<StageReward> stageRewards = _context.StageReward.ToList();
        
        foreach (var stage in _context.Stage.AsNoTracking())
        {
            var stageInfo = new StageInfo
            {
                StageId = stage.StageId,
                StageLevel = stage.StageLevel,
                UserFaction = stage.UserFaction,
                AssetId = stage.AssetId,
                CharacterId = stage.CharacterId,
                MapId = stage.MapId,
                StageEnemy = stageEnemies.FindAll(se => se.StageId == stage.StageId),
                StageReward = stageRewards.FindAll(sr => sr.StageId == stage.StageId)
            };
            StageInfos.Add(stageInfo);
        }
    }
}