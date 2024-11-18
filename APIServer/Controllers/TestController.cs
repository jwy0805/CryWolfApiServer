using AccountServer.DB;
using AccountServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace AccountServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TestController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ApiService _apiService;
    
    public TestController(AppDbContext context, ApiService apiService)
    {
        _context = context;
        _apiService = apiService;
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
    
    [HttpPost]
    [Route("TestServers")]
    public async Task<IActionResult> TestServers([FromBody] ServerTestRequired required)
    {
        var apiToMatch = new TestApiToMatchRequired { Test = required.Test };
        var apiToSocket = new TestApiToSocketRequired { Test = required.Test };
        var taskMatch = _apiService.SendRequestAsync<TestApiToMatchResponse>("Test", apiToMatch, HttpMethod.Post);
        var taskSocket = _apiService.SendRequestToSocketAsync<TestApiToSocketResponse>("Test", apiToSocket, HttpMethod.Post);
        
        await Task.WhenAll(taskMatch, taskSocket);
        
        if (taskMatch.Result == null || taskSocket.Result == null)
        {
            return BadRequest();
        }

        var res = new ServerTestResponse
        {
            MatchTestOk = taskMatch.Result.TestOk,
            SocketTestOk = taskSocket.Result.TestOk
        };
        
        return Ok(res);
    }
}