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

    // [HttpPost]
    // [Route("GetNotice")]
    // public async Task<IActionResult> GetNotice([FromBody] GetNoticeRequired required)
    // {
    //     var principal = _tokenValidator.ValidateToken(required.AccessToken);
    //     if (principal == null) return Unauthorized();
    //
    //     var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
    //     if (userId == null) return Unauthorized();
    //
    //     var userExists = await _context.User.AsNoTracking().AnyAsync(u => u.UserId == userId.Value);
    //     if (!userExists) return Unauthorized();
    //
    //     var lang = required.LanguageCode.ToLower();
    //     var now = DateTime.UtcNow;
    //     
    //     var noticeEntities = await _context.EventNotice.AsNoTracking()
    //         .Where(en =>
    //             en.NoticeType == NoticeType.Notice && en.IsActive && 
    //             (en.StartAt == null || en.StartAt <= now) && (en.EndAt   == null || en.EndAt   >= now))
    //         .Include(en => en.Localizations)
    //         .OrderByDescending(en => en.IsPinned)
    //         .ThenByDescending(en => en.CreatedAt)
    //         .Take(20)
    //         .ToListAsync();
    //     
    //     var notices = noticeEntities.Select(en =>
    //         {
    //             var localization = en.Localizations.FirstOrDefault(enl => enl.LanguageCode == lang);
    //             if (localization == null) return new NoticeInfo();
    //             return new NoticeInfo
    //             {
    //                 EventNoticeId = en.EventNoticeId,
    //                 NoticeType = en.NoticeType,
    //                 Title = localization.Title,
    //                 Content = localization.Content,
    //                 IsPinned = en.IsPinned,
    //                 CreatedAt = en.CreatedAt
    //             };
    //         })
    //         .ToList();
    //
    //     var response = new GetNoticeResponse
    //     {
    //         GetNoticeOk = true,
    //         NoticeInfos = notices
    //     };
    //     
    //     return Ok(response);
    // }
    //
    // [HttpPost]
    // [Route("GetEvent")]
    // public async Task<IActionResult> GetEvent([FromBody] GetEventRequired required)
    // {
    //     var principal = _tokenValidator.ValidateToken(required.AccessToken);
    //     if (principal == null) return Unauthorized();
    //
    //     var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
    //     if (userId == null) return Unauthorized();
    //
    //     var userExists = await _context.User.AsNoTracking().AnyAsync(u => u.UserId == userId.Value);
    //     if (!userExists) return Unauthorized();
    //
    //     var lang = required.LanguageCode.Trim().ToLowerInvariant();
    //     if (string.IsNullOrWhiteSpace(lang)) lang = "en";
    //     
    //     var now = DateTime.UtcNow;
    //
    //     var eventDefs = await _context.EventDefinition.AsNoTracking()
    //         .Where(e => e.IsActive
    //                     && (e.StartAt == null || e.StartAt <= now) && (e.EndAt == null || e.EndAt >= now))
    //         .OrderByDescending(e => e.StartAt ?? DateTime.MinValue)
    //         .ThenByDescending(e => e.CreatedAt)
    //         .Take(20)
    //         .Select(ed => new
    //         {
    //             ed.EventId, ed.EventKey, ed.Version,
    //             ed.StartAt, ed.EndAt, ed.RepeatType, ed.RepeatTimezone, ed.CreatedAt
    //         })
    //         .ToListAsync();
    //
    //     if (eventDefs.Count == 0)
    //     {
    //         return Ok(new GetEventListResponse
    //         {
    //             Ok = true,
    //             ServerNowUtc = now,
    //             Events = new List<EventList>()
    //         });
    //     }
    //
    //     var eventIds = eventDefs.Select(x => x.EventId).ToList();
    //     var noticeCandidates = await _context.EventNotice.AsNoTracking()
    //         .Where(n =>
    //             n.NoticeType == NoticeType.Event &&
    //             n.IsActive &&
    //             n.EventId != null &&
    //             eventIds.Contains(n.EventId.Value) &&
    //             (n.StartAt == null || n.StartAt <= now) &&
    //             (n.EndAt == null || n.EndAt >= now))
    //         .Select(n => new
    //         {
    //             EventId = n.EventId!.Value,
    //             n.EventNoticeId,
    //             n.IsPinned,
    //             n.CreatedAt,
    //
    //             LocRequested = n.Localizations
    //                 .Where(l => l.LanguageCode == lang)
    //                 .Select(l => new { l.Title, l.Content })
    //                 .FirstOrDefault(),
    //
    //             LocEn = n.Localizations
    //                 .Where(l => l.LanguageCode == "en")
    //                 .Select(l => new { l.Title, l.Content })
    //                 .FirstOrDefault(),
    //
    //             LocAny = n.Localizations
    //                 .Select(l => new { l.Title, l.Content })
    //                 .FirstOrDefault()
    //         })
    //         .ToListAsync();
    //     
    //     var tiers = await _context.EventRewardTier.AsNoTracking()
    //         .Where(t => t.IsActive && eventIds.Contains(t.EventId))
    //         .Select(t => new { t.EventId, t.Tier, t.MinEventVersion, t.MaxEventVersion, t.RewardJson })
    //         .ToListAsync();
    //     
    //     var tiersByEventId = tiers.GroupBy(t => t.EventId)
    //         .ToDictionary(grouping => grouping.Key, grouping => grouping.OrderBy(x => x.Tier).ToList());
    //
    //     var list = new List<EventList>(eventDefs.Count);
    //
    //     foreach (var e in eventDefs)
    //     {
    //         var preview = new List<RewardInfo>();
    //         if (tiersByEventId.TryGetValue(e.EventId, out var tList))
    //         {
    //             foreach (var t in tList)
    //             {
    //                 if (t.MinEventVersion > e.Version) continue;
    //                 if (t.MaxEventVersion.HasValue && t.MaxEventVersion.Value < e.Version) continue;
    //
    //                 try
    //                 {
    //                     var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<List<RewardInfo>>(t.RewardJson);
    //                     if (parsed != null)
    //                     {
    //                         preview.AddRange(parsed);
    //                     }
    //                 }
    //                 catch (Exception ex)
    //                 {
    //                     _logger.LogWarning(ex, 
    //                         "Invalid RewardJson. EventId={EventId}, Tier={Tier}", e.EventId, t.Tier);
    //                 }
    //
    //                 if (preview.Count > 3) break;
    //             }
    //         }
    //         
    //         list.Add(new EventList
    //         {
    //             EventId = e.EventId,
    //             EventKey = e.EventKey,
    //             Version = e.Version,
    //             StartAtUtc = e.StartAt,
    //             EndAtUtc = e.EndAt,
    //             RepeatType = e.RepeatType,
    //             RepeatTimezone = e.RepeatTimezone,
    //             PreviewRewards = preview,
    //             Priority = 0
    //         });
    //     }
    //     
    //     list = list
    //         .OrderByDescending(x => x.Priority)
    //         .ThenByDescending(x => x.StartAtUtc ?? DateTime.MinValue)
    //         .ThenByDescending(x => x.EventId)
    //         .ToList();
    //     
    //     var res = new GetEventListResponse
    //     {
    //         Ok = true,
    //         ServerNowUtc = now,
    //         Events = list
    //     };
    //     
    //     return Ok(res);
    // }
}