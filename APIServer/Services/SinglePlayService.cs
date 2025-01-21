using ApiServer.DB;

namespace ApiServer.Services;

public class SinglePlayService
{
    private readonly AppDbContext _context;
    
    public List<StageEnemy> StageEnemies { get; set; }
    public List<StageReward> StageRewards { get; set; }
    
    public SinglePlayService(AppDbContext context)
    {
        _context = context;
        
        StageEnemies = _context.StageEnemy.ToList();
        StageRewards = _context.StageReward.ToList();
    }
}