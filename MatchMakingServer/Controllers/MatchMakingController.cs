using AccountServer.Services;
using MatchMakingServer.DB;
using Microsoft.AspNetCore.Mvc;

namespace MatchMakingServer.Controllers;

[Route("match/[controller]")]
[ApiController]
public class MatchMakingController : ControllerBase
{
    private readonly MatchMakingService _matchMakingService;
    private readonly ILogger<MatchMakingController> _logger;

    public MatchMakingController(MatchMakingService service, ILogger<MatchMakingController> logger)
    {
        _matchMakingService = service;
        _logger = logger;
    }
    
    [HttpPost]
    [Route("Match")]
    public MatchMakingPacketResponse MatchMaking([FromBody] MatchMakingPacketRequired required)
    {
        _matchMakingService.AddMatchRequest(required, required.Test);
        Console.WriteLine($"Match Requested : {required.SessionId}, {required.Test}");
        return new MatchMakingPacketResponse();
    }
    
    [HttpPost]
    [Route("MatchAi")]
    public MatchMakingPacketResponse MatchMakingAi([FromBody] MatchMakingPacketRequired required)
    {
        _matchMakingService.AddMatchRequest(required);
        return new MatchMakingPacketResponse();
    }
    
    [HttpPost]
    [Route("CancelMatch")]
    public MatchCancelPacketResponse CancelMatch([FromBody] MatchCancelPacketRequired required)
    {
        var cancelUserId = _matchMakingService.CancelMatchRequest(required);
        var response = new MatchCancelPacketResponse { UserId = cancelUserId };
        return response;
    }
    
    [HttpPost]
    [Route("Test")]
    public TestApiToMatchResponse Test([FromBody] TestApiToMatchRequired required)
    {
        return new TestApiToMatchResponse { TestOk = required.Test };
    }
}