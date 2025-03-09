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
        
        var res = new ReinforceResultPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);

        if (userId != null)
        {
            var unit = (UnitId)required.UnitInfo.Id;
            var unitsToBeDeleted = required.UnitList;
            var userUnits = _context.UserUnit
                .Where(uu => uu.UserId == userId);
            var materialsToBeDeleted = _context.UnitMaterial.AsNoTracking()
                .Where(um => um.UnitId == unit)
                .Join(_context.Material,
                    um => um.MaterialId, m => m.MaterialId,
                    (um, m) => new OwnedMaterialInfo
                    {
                        MaterialInfo = new MaterialInfo
                        {
                            Id = (int)um.MaterialId,
                            Class = m.Class
                        },
                        Count = um.Count
                    }).ToList();
            
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Verity if the user has enough materials and units
                    foreach (var material in materialsToBeDeleted)
                    {
                        var userMaterial = _context.UserMaterial
                            .FirstOrDefault(um => 
                                um.UserId == userId && um.MaterialId == (MaterialId)material.MaterialInfo.Id);
                        
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
                            res.ReinforceResultOk = false;
                            res.Error = 1;
                            await transaction.RollbackAsync();
                        }
                    }

                    foreach (var unitInfo in unitsToBeDeleted)
                    {
                        var userUnit = userUnits.FirstOrDefault(uu => uu.UnitId == (UnitId)unitInfo.Id);
                        
                        if (userUnit != null)
                        {
                            userUnit.Count--;
                            if (userUnit.Count == 0)
                            {
                                _context.UserUnit.Remove(userUnit);
                            }
                        }
                        else
                        {
                            res.ReinforceResultOk = false;
                            res.Error = 1;
                            await transaction.RollbackAsync();
                        }
                    }
                    
                    // Reinforce Card if the user has enough materials and units
                    var reinforcePointDb = _context.ReinforcePoint.AsNoTracking();
                    var reinforcePoint = reinforcePointDb
                        .FirstOrDefault(rp => rp.Class == required.UnitInfo.Class && rp.Level == required.UnitInfo.Level + 1);
                    
                    if (reinforcePoint == null)
                    {
                        res.ReinforceResultOk = false;
                        res.Error = 2;
                        await transaction.RollbackAsync();
                    }
                    else
                    {
                        var denominator = reinforcePoint.Constant;
                        var numerator = unitsToBeDeleted
                            .Where(info => reinforcePointDb
                                .Any(rp => rp.Class == info.Class && rp.Level == info.Level))
                            .Sum(info => reinforcePointDb
                                .First(rp => rp.Class == info.Class && rp.Level == info.Level).Constant);
                        var random = new Random();
                    
                        if (numerator / (float)denominator > random.NextDouble())
                        {
                            var newUnitId = (UnitId)required.UnitInfo.Id + 1;
                            var userUnit = userUnits.FirstOrDefault(uu => uu.UnitId == newUnitId);
                        
                            if (userUnit != null)
                            {
                                userUnit.Count++;
                            }
                            else
                            {
                                _context.UserUnit.Add(new UserUnit
                                {
                                    UserId = (int)userId,
                                    UnitId = newUnitId,
                                    Count = 1
                                });
                            }

                            res.IsSuccess = true;
                        }
                    
                        await _context.SaveChangesExtendedAsync();
                        await transaction.CommitAsync();
                        res.ReinforceResultOk = true;
                    }
                }
                catch (Exception e)
                {
                    res.ReinforceResultOk = false;
                    await transaction.RollbackAsync();
                }
            });
        }
        else
        {
            res.ReinforceResultOk = false;
        }
        
        return Ok(res);
    }
}