using ApiServer.DB;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CraftingController : ControllerBase
{
    private readonly ILogger<CraftingController> _logger;
    private readonly AppDbContext _context;
    private readonly TokenService _tokenService;
    private readonly TokenValidator _tokenValidator;
    
    public CraftingController(
        ILogger<CraftingController> logger,
        AppDbContext context,
        TokenService tokenService,
        TokenValidator validator)
    {
        _logger = logger;
        _context = context;
        _tokenService = tokenService;
        _tokenValidator = validator;
    }

    [HttpPost]
    [Route("LoadMaterials")]
    public IActionResult LoadMaterials([FromBody] LoadMaterialsPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var res = new LoadMaterialsPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);

        if (userId != null)
        {
            var materialsForCrafting = _context.UnitMaterial.AsNoTracking()
                .Where(unitMaterial => unitMaterial.UnitId == required.UnitId)
                .Join(_context.Material,
                    unitMaterial => unitMaterial.MaterialId,
                    material => material.MaterialId,
                    (unitMaterial, material) => new OwnedMaterialInfo
                    {
                        MaterialInfo = new MaterialInfo
                        {
                            Id = (int)unitMaterial.MaterialId,
                            Class = material.Class
                        },
                        Count = unitMaterial.Count
                    }).ToList();
            
            res.CraftingMaterialList = materialsForCrafting;
            res.LoadMaterialsOk = true;
        }
        else
        {
            res.LoadMaterialsOk = false;
        }

        return Ok(res);
    }

    [HttpPut]
    [Route("CraftCard")]
    public async Task<IActionResult> CraftCard([FromBody] CraftCardPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var res = new CraftCardPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        
        if (userId != null)
        {
            var materialsToBeDeleted = required.Materials;
            var unitToBeCrafted = required.UnitId;
            var unitCount = required.Count;
            var userMaterials = _context.UserMaterial
                .Where(um => um.UserId == userId)
                .Where(um => materialsToBeDeleted
                    .Select(info => info.MaterialInfo.Id).ToList().Contains((int)um.MaterialId))
                .ToList();
            var userUnit = _context.UserUnit
                .Where(uu => uu.UserId == userId)
                .FirstOrDefault(uu => uu.UnitId == unitToBeCrafted);

            var strategy = _context.Database.CreateExecutionStrategy();
            
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    foreach (var material in materialsToBeDeleted)
                    {
                        var userMaterial = userMaterials
                            .FirstOrDefault(um => (int)um.MaterialId == material.MaterialInfo.Id);
                        if (userMaterial != null && userMaterial.Count >= material.Count)
                        {
                            userMaterial.Count -= material.Count;
                            if (userMaterial.Count == 0)
                            {
                                _context.UserMaterial.Remove(userMaterial);
                            }
                        }
                        else
                        {
                            res.CraftCardOk = false;
                            res.Error = 1;
                            await transaction.RollbackAsync();
                        }
                    }

                    if (userUnit == null)
                    {
                        _context.UserUnit.Add(new UserUnit
                        {
                            UserId = (int)userId,
                            UnitId = unitToBeCrafted,
                            Count = unitCount
                        });
                    }
                    else
                    {
                        userUnit.Count += unitCount;
                    }

                    await _context.SaveChangesExtendedAsync();
                    await transaction.CommitAsync();
                    res.CraftCardOk = true;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception.Message);
                    res.CraftCardOk = false;
                    await transaction.RollbackAsync();
                }
            });
        }
        else
        {
            res.CraftCardOk = false;
        }

        return Ok(res);
    }

    [HttpPut]
    [Route("ReinforceCard")]
    public async Task<IActionResult> ReinforceCard([FromBody] ReinforceResultPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var userIdNullable = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userIdNullable == null) return Unauthorized();

        int userId = userIdNullable.Value;

        var res = new ReinforceResultPacketResponse
        {
            ReinforceResultOk = false,
            IsSuccess = false,
            Error = 0
        };

        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            // 실패 처리 헬퍼: 반드시 rollback + 즉시 종료(= 이후 로직 진행 금지)
            async Task FailAndReturn(int errorCode)
            {
                res.ReinforceResultOk = false;
                res.IsSuccess = false;
                res.Error = errorCode;
                await tx.RollbackAsync();
            }

            try
            {
                var baseUnitId = (UnitId)required.UnitInfo.Id;
                var baseUnitClass = required.UnitInfo.Class;
                var baseUnitLevel = required.UnitInfo.Level;

                // 서버 기준 목표 레벨(다음 레벨)
                int targetLevel = baseUnitLevel + 1;

                // -----------------------------
                // 1) 강화 재료(머티리얼) 요구량 로드
                // -----------------------------
                var materialsToConsume = await _context.UnitMaterial.AsNoTracking()
                    .Where(um => um.UnitId == baseUnitId)
                    .Select(um => new
                    {
                        MaterialId = um.MaterialId,
                        Count = um.Count
                    })
                    .ToListAsync();

                // -----------------------------
                // 2) 희생 유닛 리스트 (required.UnitList)
                //    - 여기엔 "재료 유닛들"만 들어온다고 가정
                // -----------------------------
                var unitsToConsume = required.UnitList ?? new List<UnitInfo>();
                // (Class, Level)별 개수 집계 (중복카드가 있으면 개수만큼 반영)
                var unitConsumeGroups = unitsToConsume
                    .GroupBy(u => new { u.Class, u.Level, u.Id })
                    .Select(g => new { g.Key.Class, g.Key.Level, UnitId = (UnitId)g.Key.Id, Count = g.Count() })
                    .ToList();

                // -----------------------------
                // 3) ReinforcePoint 로드 (번역 실패 방지 버전)
                //    - DB에서는 IN으로 "superset"만 가져오고
                //    - (Class,Level) 정확 매칭은 메모리 HashSet에서 처리
                // -----------------------------
                var candidateKeys = unitsToConsume
                    .Select(u => new { u.Class, u.Level })
                    .Distinct()
                    .ToList();

                var candClasses = candidateKeys.Select(k => k.Class).Distinct().ToList();
                var candLevels  = candidateKeys.Select(k => k.Level).Distinct().ToList();

                var reinforcePointsRaw = await _context.ReinforcePoint.AsNoTracking()
                    .Where(rp =>
                        (rp.Class == baseUnitClass && rp.Level == targetLevel) ||
                        (candClasses.Contains(rp.Class) && candLevels.Contains(rp.Level)))
                    .ToListAsync();

                // 메모리에서 (Class,Level) 정확 페어로 필터
                var candidateSet = candidateKeys
                    .Select(k => (k.Class, k.Level))
                    .ToHashSet();

                var reinforcePoints = reinforcePointsRaw
                    .Where(rp =>
                        (rp.Class == baseUnitClass && rp.Level == targetLevel) ||
                        candidateSet.Contains((rp.Class, rp.Level)))
                    .ToList();

                // 목표 ReinforcePoint(분모)
                var targetRp = reinforcePoints
                    .FirstOrDefault(rp => rp.Class == baseUnitClass && rp.Level == targetLevel);

                if (targetRp == null)
                {
                    // 강화 테이블 이상(목표 레벨 상수 없음)
                    await FailAndReturn(errorCode: 2);
                    return;
                }

                // 희생 카드 상수 lookup
                var rpDict = reinforcePoints
                    .GroupBy(rp => (rp.Class, rp.Level))
                    .ToDictionary(g => g.Key, g => g.First().Constant);

                // -----------------------------
                // 4) 유저 보유 검증 + 차감 (동일 트랜잭션)
                // -----------------------------

                // 4-A) 머티리얼 차감
                if (materialsToConsume.Count > 0)
                {
                    var matIds = materialsToConsume.Select(m => m.MaterialId).Distinct().ToList();

                    var userMaterials = await _context.UserMaterial
                        .Where(um => um.UserId == userId && matIds.Contains(um.MaterialId))
                        .ToListAsync();

                    foreach (var reqMat in materialsToConsume)
                    {
                        var row = userMaterials.FirstOrDefault(x => x.MaterialId == reqMat.MaterialId);
                        if (row == null || row.Count < reqMat.Count)
                        {
                            await FailAndReturn(errorCode: 1); // 재료 부족
                            return;
                        }

                        row.Count -= reqMat.Count;
                        if (row.Count <= 0)
                            _context.UserMaterial.Remove(row);
                    }
                }

                // 4-B) 강화 원본 유닛 1장 차감 (성공 시 상위 유닛 1장 지급)
                //     -> "원본을 소모하지 않는 강화"면 여기 블록을 제거하면 됨.
                var baseUserUnit = await _context.UserUnit
                    .FirstOrDefaultAsync(uu => uu.UserId == userId && uu.UnitId == baseUnitId);

                if (baseUserUnit == null || baseUserUnit.Count <= 0)
                {
                    await FailAndReturn(errorCode: 1); // 원본 유닛 부족
                    return;
                }

                baseUserUnit.Count -= 1;
                if (baseUserUnit.Count <= 0)
                    _context.UserUnit.Remove(baseUserUnit);

                // 4-C) 희생 유닛 차감
                if (unitConsumeGroups.Count > 0)
                {
                    var consumeUnitIds = unitConsumeGroups.Select(x => x.UnitId).Distinct().ToList();

                    var userUnits = await _context.UserUnit
                        .Where(uu => uu.UserId == userId && consumeUnitIds.Contains(uu.UnitId))
                        .ToListAsync();

                    foreach (var g in unitConsumeGroups)
                    {
                        var row = userUnits.FirstOrDefault(uu => uu.UnitId == g.UnitId);
                        if (row == null || row.Count < g.Count)
                        {
                            await FailAndReturn(errorCode: 1); // 희생 유닛 부족
                            return;
                        }

                        row.Count -= g.Count;
                        if (row.Count <= 0)
                            _context.UserUnit.Remove(row);
                    }
                }

                // -----------------------------
                // 5) 강화 확률 계산
                // -----------------------------
                int denominator = targetRp.Constant;

                // numerator = Σ(희생카드 constant * 개수)
                int numerator = 0;
                foreach (var g in unitsToConsume.GroupBy(u => (u.Class, u.Level)))
                {
                    if (rpDict.TryGetValue(g.Key, out var c))
                    {
                        numerator += c * g.Count();
                    }
                }

                double p = denominator <= 0 ? 0.0 : (double)numerator / denominator;
                if (p < 0) p = 0;
                if (p > 1) p = 1;

                var rng = new Random();
                bool success = rng.NextDouble() < p;

                // -----------------------------
                // 6) 성공 시 상위 유닛 1장 지급
                // -----------------------------
                if (success)
                {
                    var newUnitId = (UnitId)(required.UnitInfo.Id + 1);

                    var upgraded = await _context.UserUnit
                        .FirstOrDefaultAsync(uu => uu.UserId == userId && uu.UnitId == newUnitId);

                    if (upgraded == null)
                    {
                        _context.UserUnit.Add(new UserUnit
                        {
                            UserId = userId,
                            UnitId = newUnitId,
                            Count = 1
                        });
                    }
                    else
                    {
                        upgraded.Count += 1;
                    }

                    res.IsSuccess = true;
                }
                else
                {
                    res.IsSuccess = false;
                }

                await _context.SaveChangesExtendedAsync();
                await tx.CommitAsync();

                res.ReinforceResultOk = true;
                res.Error = 0;
            }
            catch
            {
                try { await tx.RollbackAsync(); } catch { /* ignore */ }
                res.ReinforceResultOk = false;
                res.IsSuccess = false;
                res.Error = 999; // 서버 내부 예외
            }
        });

        return Ok(res);
    }
}