using AccountServer.DB;
using Microsoft.AspNetCore.Mvc;

namespace AccountServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TestController : ControllerBase
{
    private readonly AppDbContext _context;
    
    public TestController(AppDbContext context)
    {
        _context = context;
    }
    
    [HttpPost]
    [Route("Test")]
    public IActionResult Test([FromBody] TestRequired required)
    {
        var unit = _context.Unit.FirstOrDefault(unit => unit.UnitId == (UnitId)required.UnitId);
        var res = new TestResponse();

        if (unit != null)
        {
            res.TestOk = true;
            res.UnitName = unit.UnitId.ToString();
        }
        else
        {
            res.TestOk = false;
        }
        
        return Ok(res);
    }
}