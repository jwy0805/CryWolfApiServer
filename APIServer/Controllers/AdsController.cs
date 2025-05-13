using ApiServer.DB;
using ApiServer.Providers;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenValidator _tokenValidator;
    private readonly IDailyProductService _dailyProductService;
    
    public AdsController(AppDbContext context, TokenValidator tokenValidator, IDailyProductService dailyProductService)
    {
        _context = context;
        _tokenValidator = tokenValidator;
        _dailyProductService = dailyProductService;
    }

    [HttpPut]
    [Route("CheckDailyProduct")]
    public async Task<IActionResult> CheckDailyProduct(CheckDailyProductPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var res = new CheckDailyProductPacketResponse();
        var userIdN = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userIdN == null) return BadRequest(res);
        
        var userId = userIdN.Value;
        var userDailyProduct = await _context.UserDailyProduct
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Slot == required.Slot);
        if (userDailyProduct == null) return BadRequest(res);
        
        userDailyProduct.NeedAds = false;
        _context.UserDailyProduct.Update(userDailyProduct);
        
        await _context.SaveChangesAsync();
        res.CheckDailyProductOk = true;
        
        return Ok(res);
    }

    [HttpPut]
    [Route("RefreshDailyProduct")]
    public async Task<IActionResult> RefreshDailyProduct(RefreshDailyProductPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var res = new RefreshDailyProductPacketResponse();
        var userIdN = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userIdN == null) return BadRequest(res);
        
        var userId = userIdN.Value;
        var refreshed = await _dailyProductService.RefreshByAdsAsync(userId);
        if (refreshed)
        {
            var products = _context.Product.AsNoTracking().ToList();
            var compositions = _context.ProductComposition.AsNoTracking().ToList();
            var probabilities = _context.CompositionProbability.AsNoTracking().ToList();
            res.DailyProducts = await _dailyProductService.GetDailyProductInfos(
                userId, products, compositions, probabilities);
            res.RefreshDailyProductOk = true;
        }
        else
        {
            res.RefreshDailyProductOk = false;
        }
        
        return Ok(res);
    }
}