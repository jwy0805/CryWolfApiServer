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

    [HttpPost]
    [Route("GetDailyProductRefreshTime")]
    public async Task<IActionResult> GetDailyProductRefreshTime([FromBody] GetDailyProductRefreshTimePacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var res = new GetDailyProductRefreshTimePacketResponse();
        var userIdN = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userIdN == null) return BadRequest(res);
        
        var userId = userIdN.Value;
        var userDailyProduct = await _context.UserDailyProduct.Where(udp => udp.UserId == userId).FirstOrDefaultAsync();
        
        if (userDailyProduct == null)
        {
            res.GetRefreshTimeOk = false;
        }
        else
        {
            res.GetRefreshTimeOk = true;
            res.RefreshAt = userDailyProduct.RefreshAt;
        }
        
        return Ok(res);
    }
    
    [HttpPut]
    [Route("RevealDailyProduct")]
    public async Task<IActionResult> CheckDailyProduct([FromBody] RevealDailyProductPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var res = new RevealDailyProductPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null) return BadRequest(res);
        
        var userDailyProduct = await _context.UserDailyProduct
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Slot == required.Slot);
        if (userDailyProduct == null) return BadRequest(res);
        
        userDailyProduct.NeedAds = false;
        _context.UserDailyProduct.Update(userDailyProduct);
        
        await _context.SaveChangesAsync();
        
        res.RevealDailyProductOk = true;
        res.DailyProductInfos = await _dailyProductService.GetDailyProductInfos(userId.Value);
        
        return Ok(res);
    }

    [HttpPut]
    [Route("RefreshDailyProduct")]
    public async Task<IActionResult> RefreshDailyProduct([FromBody] RefreshDailyProductPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var res = new RefreshDailyProductPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null) return BadRequest(res);
        
        var refreshed = await _dailyProductService.RefreshByAdsAsync(userId.Value);
        if (refreshed)
        {
            res.DailyProducts = await _dailyProductService.GetDailyProductInfos(userId.Value);
            res.RefreshDailyProductOk = true;
            res.RefreshTime = DateTime.UtcNow;
        }
        else
        {
            res.RefreshDailyProductOk = false;
        }
        
        return Ok(res);
    }
}