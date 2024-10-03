using AccountServer.Services;
using MatchMakingServer.DB;
using Microsoft.AspNetCore.Mvc;

namespace MatchMakingServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MatchMakingController : ControllerBase
{
    private readonly MatchMakingService _matchMakingService;

    public MatchMakingController(MatchMakingService service)
    {
        _matchMakingService = service;
    }
    
    [HttpPost]
    [Route("Match")]
    public MatchMakingPacketResponse MatchMaking([FromBody] MatchMakingPacketRequired required)
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
}