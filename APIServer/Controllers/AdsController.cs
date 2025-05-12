using ApiServer.DB;
using ApiServer.Providers;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly CachedDataProvider _cachedDataProvider;
    
    public AdsController(AppDbContext context, CachedDataProvider cachedDataProvider)
    {
        _context = context;
        _cachedDataProvider = cachedDataProvider;
    }

    [HttpPut]
    [Route("CheckDailyProduct")]
    public async Task<IActionResult> CheckDailyProduct()
    {
        return Ok();
    }

    [HttpPut]
    [Route("RefreshDailyProduct")]
    public async Task<IActionResult> RefreshDailyProduct()
    {
        return Ok();
    }
}