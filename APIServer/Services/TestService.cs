using ApiServer.DB;

namespace ApiServer.Services;

public class TestService
{
    private readonly Random _random = new();
    
    public int GetRandomNumber(int min, int max) => _random.Next(min, max);
    
    public UnitId[] SetAiUnits(Faction faction)
        => Enumerable.Range(0, 9)
            .Select(i => (UnitId)(faction == Faction.Sheep ? 103 + i * 3 : 503 + i * 3))
            .OrderBy(_ => Random.Shared.Next())
            .Take(6)
            .ToArray();
}