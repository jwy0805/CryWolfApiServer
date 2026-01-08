using ApiServer.DB;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EventController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenValidator _tokenValidator;
    private readonly ILogger<EventController> _logger;
    
    public EventController(AppDbContext context, TokenValidator tokenValidator, ILogger<EventController> logger)
    {
        _context = context;
        _tokenValidator = tokenValidator;
        _logger = logger;
    }

    [HttpPost]
    [Route("GetNotice")]
    public async Task<IActionResult> GetNotice([FromBody] GetNoticeRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null) return Unauthorized();

        var userExists = await _context.User.AsNoTracking().AnyAsync(u => u.UserId == userId.Value);
        if (!userExists) return Unauthorized();

        var lang = required.LanguageCode.ToLower();
        var now = DateTime.UtcNow;
        
        var noticeEntities = await _context.EventNotice.AsNoTracking()
            .Where(en =>
                en.NoticeType == NoticeType.Notice && en.IsActive && 
                (en.StartAt == null || en.StartAt <= now) && (en.EndAt   == null || en.EndAt   >= now))
            .Include(en => en.Localizations)
            .OrderByDescending(en => en.IsPinned)
            .ThenByDescending(en => en.CreatedAt)
            .Take(20)
            .ToListAsync();
        
        var notices = noticeEntities.Select(en =>
            {
                var localization = en.Localizations.FirstOrDefault(enl => enl.LanguageCode == lang);
                if (localization == null) return new NoticeInfo();
                return new NoticeInfo
                {
                    EventNoticeId = en.EventNoticeId,
                    NoticeType = en.NoticeType,
                    Title = localization.Title,
                    Content = localization.Content,
                    IsPinned = en.IsPinned,
                    CreatedAt = en.CreatedAt
                };
            })
            .ToList();

        var response = new GetNoticeResponse
        {
            GetNoticeOk = true,
            NoticeInfos = notices
        };
        
        return Ok(response);
    }

    [HttpPost]
    [Route("GetEvent")]
    public async Task<IActionResult> GetEvent([FromBody] GetEventRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null) return Unauthorized();

        var userExists = await _context.User.AsNoTracking().AnyAsync(u => u.UserId == userId.Value);
        if (!userExists) return Unauthorized();

        var lang = required.LanguageCode.ToLower();
        var now = DateTime.UtcNow;
        
        var eventEntities = await _context.EventNotice.AsNoTracking()
            .Where(en =>
                en.NoticeType == NoticeType.Event && en.IsActive && 
                (en.StartAt == null || en.StartAt <= now) && (en.EndAt   == null || en.EndAt   >= now))
            .Include(en => en.Localizations)
            // .Include(en => en.Rewards)
            .OrderByDescending(en => en.IsPinned)
            .ThenByDescending(en => en.CreatedAt)
            .Take(20)
            .ToListAsync();

        var events = eventEntities.Select(en => 
            {
                var localization = en.Localizations.FirstOrDefault(enl => enl.LanguageCode == lang);
                if (localization == null) return new EventInfo();
                return new EventInfo
                {
                    NoticeInfo = new NoticeInfo
                    {
                        EventNoticeId = en.EventNoticeId,
                        NoticeType = en.NoticeType,
                        Title = localization.Title,
                        Content = localization.Content,
                        IsPinned = en.IsPinned,
                        CreatedAt = en.CreatedAt
                    },
                    
                    // Rewards = en.Rewards.Select(enr => new RewardInfo
                    // {
                    //     ItemId = enr.ItemId,
                    //     ProductType = enr.ProductType,
                    //     Count = enr.Count
                    // }).ToList()
                };
            })
            .ToList();
        
        var response = new GetEventResponse 
        {
            GetEventOk = true,
            EventInfos = events
        };
        
        return Ok(response);
    }
}