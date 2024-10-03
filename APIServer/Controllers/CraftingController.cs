using AccountServer.DB;
using AccountServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CraftingController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenService _tokenService;
    private readonly TokenValidator _tokenValidator;
    
    public CraftingController(AppDbContext context, TokenService tokenService, TokenValidator validator)
    {
        _context = context;
        _tokenService = tokenService;
        _tokenValidator = validator;
    }

    [HttpPost]
    [Route("LoadMaterials")]
    public IActionResult LoadMaterials([FromBody] LoadMaterialsPacketRequired required)
    {
        var principal = _tokenValidator.ValidateAccessToken(required.AccessToken);
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
                    (unitMaterial, material) => new MaterialInfo
                    {
                        Id = (int)unitMaterial.MaterialId,
                        Class = material.Class,
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
    public IActionResult CraftCard([FromBody] CraftCardPacketRequired required)
    {
        var principal = _tokenValidator.ValidateAccessToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var res = new CraftCardPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        
        if (userId != null)
        {
            var materialsToBeDeleted = required.Materials;
            var unitToBeCrafted = required.UnitId;
            var unitCount = required.Count;
            
            res.CraftCardOk = true;
        }
        else
        {
            res.CraftCardOk = false;
        }

        return Ok(res);
    }
}