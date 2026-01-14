using System.Data;
using System.Globalization;
using System.Runtime.CompilerServices;
using ApiServer.DB;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EventController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenValidator _tokenValidator;
    private readonly ProductClaimService _claimService;
    private readonly ILogger<EventController> _logger;

    public EventController(
        AppDbContext context, 
        TokenValidator tokenValidator, 
        ProductClaimService claimService,
        ILogger<EventController> logger)
    {
        _context = context;
        _tokenValidator = tokenValidator;
        _claimService = claimService;
        _logger = logger;
    }

    private async Task<long?> ValidateUserAsync(string accessToken)
    {
        var principal = _tokenValidator.ValidateToken(accessToken);
        if (principal == null) return null;

        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null) return null;

        var exists = await _context.User.AsNoTracking().AnyAsync(u => u.UserId == userId.Value);
        return exists ? userId.Value : null;
    }
    
    private (string Title, string Content) PickLocalization(
        IEnumerable<(string LanguageCode, string Title, string Content)> locs,
        string requestedLang)
    {
        // requested -> en -> any
        var req = locs.FirstOrDefault(x => x.LanguageCode == requestedLang);
        if (!string.IsNullOrEmpty(req.Title) || !string.IsNullOrEmpty(req.Content))
            return (req.Title ?? "", req.Content ?? "");

        var en = locs.FirstOrDefault(x => x.LanguageCode == "en");
        if (!string.IsNullOrEmpty(en.Title) || !string.IsNullOrEmpty(en.Content))
            return (en.Title ?? "", en.Content ?? "");

        var any = locs.FirstOrDefault();
        return (any.Title ?? "", any.Content ?? "");
    }

    [HttpPost]
    [Route("GetNotice")]
    public async Task<IActionResult> GetNotice([FromBody] GetNoticeRequired required)
    {
        var userId = await ValidateUserAsync(required.AccessToken);
        if (userId == null) return Unauthorized();

        var lang = Util.Util.NormalizeLang(required.LanguageCode);
        var now = DateTime.UtcNow;

        // 1) Notice 본문(메타) 먼저
        var notices = await _context.Notice.AsNoTracking()
            .Where(n => n.IsActive
                        && (n.StartAt == null || n.StartAt <= now)
                        && (n.EndAt == null || n.EndAt >= now))
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAt)
            .Take(20)
            .Select(n => new
            {
                n.NoticeId,
                n.IsPinned,
                n.CreatedAt
            })
            .ToListAsync();

        if (notices.Count == 0)
        {
            return Ok(new GetNoticeResponse
            {
                GetNoticeOk = true,
                NoticeInfos = new List<NoticeInfo>()
            });
        }

        var noticeIds = notices.Select(x => x.NoticeId).ToList();
        var locs = await _context.NoticeLocalization.AsNoTracking()
            .Where(l => noticeIds.Contains(l.NoticeId))
            .Select(l => new
            {
                l.NoticeId,
                LanguageCode = l.LanguageCode.ToLower(),
                l.Title,
                l.Content
            })
            .ToListAsync();

        var locByNoticeId = locs
            .GroupBy(x => x.NoticeId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => (x.LanguageCode, x.Title, x.Content)).ToList()
            );

        var result = new List<NoticeInfo>(notices.Count);
        foreach (var n in notices)
        {
            locByNoticeId.TryGetValue(n.NoticeId, out var list);
            list ??= new List<(string, string, string)>();

            var picked = PickLocalization(list, lang);

            result.Add(new NoticeInfo
            {
                NoticeId = n.NoticeId,
                Title = picked.Title,
                Content = picked.Content,
                IsPinned = n.IsPinned,
                CreatedAt = n.CreatedAt
            });
        }

        return Ok(new GetNoticeResponse
        {
            GetNoticeOk = true,
            NoticeInfos = result
        });
    }

    [HttpPost]
    [Route("GetEvent")]
    public async Task<IActionResult> GetEvent([FromBody] GetEventRequired required)
    {
        var userId = await ValidateUserAsync(required.AccessToken);
        if (userId == null) return Unauthorized();

        var lang = Util.Util.NormalizeLang(required.LanguageCode).ToLowerInvariant();
        var fallbackLang = "en";
        var now = DateTime.UtcNow;
        
        var events = await _context.Event.AsNoTracking()
            .Where(e => e.IsActive
                        && (e.StartAt == null || e.StartAt <= now)
                        && (e.EndAt == null || e.EndAt >= now))
            .OrderByDescending(e => e.Priority)
            .ThenByDescending(e => e.IsPinned)
            .ThenByDescending(e => e.StartAt ?? DateTime.MinValue)
            .Take(30)
            .Select(e => new { e.EventId, e.EventKey, e.StartAt, e.EndAt, e.IsPinned, e.Priority })
            .ToListAsync();

        if (events.Count == 0)
        {
            return Ok(new GetEventResponse
            {
                GetEventOk = true,
                ServerNowUtc = now,
                EventInfos = new List<EventInfo>()
            });
        }
        
        var eventIds = events.Select(x => x.EventId).ToList();
        var eventLocs = await _context.EventLocalization.AsNoTracking()
            .Where(l => eventIds.Contains(l.EventId))
            .Where(l => l.LanguageCode.ToLower() == lang || l.LanguageCode.ToLower() == fallbackLang)
            .Select(l => new
            {
                l.EventId,
                LanguageCode = l.LanguageCode.ToLower(),
                l.Title,
                l.Content
            })
            .ToListAsync();

        var locByEventId = eventLocs
            .GroupBy(x => x.EventId)
            .ToDictionary(
                grouping => grouping.Key,
                grouping => grouping.Select(x => (x.LanguageCode, x.Title, x.Content)).ToList()
            );

        var list = new List<EventInfo>(events.Count);

        foreach (var e in events)
        {
            // localization
            locByEventId.TryGetValue(e.EventId, out var locList);
            locList ??= new List<(string, string, string)>();
            var picked = PickLocalization(locList, lang);

            list.Add(new EventInfo
            {
                EventId = e.EventId,
                EventKey = e.EventKey,
                StartAtUtc = e.StartAt,
                EndAtUtc = e.EndAt,
                IsPinned = e.IsPinned,
                Priority = e.Priority,
                Title = picked.Title,
                Content = picked.Content,
            });
        }

        return Ok(new GetEventResponse
        {
            GetEventOk = true,
            ServerNowUtc = now,
            EventInfos = list
        });
    }

    [HttpPost]
    [Route(("GetEventProgress"))]
    public async Task<ActionResult> GetEventProgress([FromBody] GetEventProgressRequired required)
    {
        var userId = await ValidateUserAsync(required.AccessToken);
        if (userId == null) return Unauthorized();

        var nowUtc = DateTime.UtcNow;
        var lang = Util.Util.NormalizeLang(required.LanguageCode).ToLowerInvariant();
        var fallbackLang = "en";
        
        var ev = await _context.Event.AsNoTracking()
            .Where(e => e.EventId == required.EventId
                        && e.IsActive
                        && (e.StartAt == null || e.StartAt <= nowUtc)
                        && (e.EndAt == null || e.EndAt >= nowUtc))
            .Select(e => new
            {
                e.EventId, e.EventKey, e.Version, e.StartAt, e.EndAt, e.RepeatType, e.RepeatTimezone
            })
            .SingleOrDefaultAsync();

        if (ev == null)
            return NotFound();

        // cycleKey 계산 (RepeatType/Timezone 기반)
        var cycleKey = ComputeCycleKey(ev.RepeatTimezone, ev.RepeatType, nowUtc);
        var locs = await _context.EventLocalization.AsNoTracking()
            .Where(l => l.EventId == ev.EventId)
            .Where(l => l.LanguageCode.ToLower() == lang || l.LanguageCode.ToLower() == fallbackLang)
            .Select(l => new
            {
                LanguageCode = l.LanguageCode.ToLower(),
                l.Title,
                l.Content
            })
            .ToListAsync();
        
        var picked = PickLocalization(
            locs.Select(x => (x.LanguageCode, x.Title, x.Content)).ToList(), lang);
        var title = string.IsNullOrWhiteSpace(picked.Title) ? ev.EventKey : picked.Title;
        var content = picked.Content ?? "";
        
        var progressValue = await _context.UserEventProgress.AsNoTracking()
            .Where(uep => uep.UserId == userId && uep.EventId == ev.EventId && uep.CycleKey == cycleKey)
            .Select(p => (int?)p.ProgressValue)
            .SingleOrDefaultAsync() ?? 0;

        var tiers = await _context.EventRewardTier.AsNoTracking()
            .Where(t => t.IsActive
                        && t.EventId == ev.EventId
                        && t.MinEventVersion <= ev.Version
                        && (t.MaxEventVersion == null || t.MaxEventVersion >= ev.Version))
            .OrderBy(t => t.Tier)
            .Select(t => new
            {
                t.Tier, t.ConditionJson, t.RewardJson, t.MinEventVersion, t.MaxEventVersion
            })
            .ToListAsync();
        
        var claimedTiers = await _context.UserEventClaim.AsNoTracking()
            .Where(uec => uec.UserId == userId && uec.EventId == ev.EventId && uec.CycleKey == cycleKey)
            .Select(uec => uec.Tier)
            .ToListAsync();
        
        var claimedSet = claimedTiers.Count == 0 ? null : claimedTiers.ToHashSet();
        var tierInfos = new List<TierInfo>(tiers.Count);
        foreach (var tier in tiers)
        {
            var requiredValue = ExtractRequiredValueFromConditionJson(tier.ConditionJson);
            var isClaimed = claimedSet != null && claimedSet.Contains(tier.Tier);
            var isClaimable = !isClaimed && requiredValue > 0 && progressValue >= requiredValue;
            
            tierInfos.Add(new TierInfo
            {
                Tier = tier.Tier,
                ConditionJson = string.IsNullOrWhiteSpace(tier.ConditionJson) ? "{}" : tier.ConditionJson,
                RewardJson = string.IsNullOrWhiteSpace(tier.RewardJson) ? "{}" : tier.RewardJson,
                MinEventVersion = tier.MinEventVersion,
                MaxEventVersion = tier.MaxEventVersion,
                IsClaimed = isClaimed,
                IsClaimable = isClaimable
            });
        }
        
        return Ok(new GetEventProgressResponse
        {
            GetEventProgressOk = true,
            EventId = ev.EventId,
            EventKey = ev.EventKey,
            Title = title,
            Content = content,
            CycleKey = cycleKey,
            ProgressValue = progressValue,
            TierInfos = tierInfos
        });
    }

    [HttpPut]
    [Route("SendEventProgress")]
    public async Task<IActionResult> SendEventProgress([FromBody] SendEventProgressPacketRequired required)
    {
        if (required.UserIds.Count == 0) return BadRequest("No userIds provided");

        var eventKey = (required.EventKey ?? "").Trim();
        if (string.IsNullOrWhiteSpace(eventKey)) return BadRequest("EventKey is required");

        var userIds = required.UserIds.Where(x => x > 0).Distinct().ToArray();
        if (userIds.Length == 0) return BadRequest("No valid userIds provided");
        
        var nowUtc = DateTime.UtcNow;
        var ev = await _context.Event.AsNoTracking()
            .Where(e => e.EventKey == eventKey)
            .Select(e => new
            {
                e.EventId, e.EventKey, e.IsActive, e.StartAt, e.EndAt, e.RepeatType, e.RepeatTimezone, e.Version
            })
            .SingleOrDefaultAsync();    
        
        if (ev == null) return NotFound($"Event with key '{eventKey}' not found");
        if (!ev.IsActive) 
            return Ok(new SendEventProgressPacketResponse { SendEventProgressOk = true });
        if (ev.StartAt.HasValue && nowUtc < ev.StartAt.Value)
            return Ok(new SendEventProgressPacketResponse { SendEventProgressOk = true });
        if (ev.EndAt.HasValue && nowUtc >= ev.EndAt.Value)
            return Ok(new SendEventProgressPacketResponse { SendEventProgressOk = true });
        
        var cycleKey = ComputeCycleKey(ev.RepeatTimezone, ev.RepeatType, nowUtc);
        var counterKeyStr = required.CounterKey.ToString();
        
        var tierConditions = await _context.EventRewardTier.AsNoTracking()
            .Where(t => t.EventId == ev.EventId
                        && t.IsActive
                        && t.MinEventVersion <= ev.Version
                        && (t.MaxEventVersion == null || t.MaxEventVersion >= ev.Version))
            .Select(t => t.ConditionJson)
            .ToListAsync();

        var matchedRequiredValues = new List<int>(capacity: 4);

        foreach (var conditionJson in tierConditions)
        {
            if (!TryParseCounterCondition(conditionJson, out var condition)) continue;

            if (!condition.Type.Equals("counter", StringComparison.OrdinalIgnoreCase)) continue;
            if (!condition.CounterKey.Equals(counterKeyStr, StringComparison.OrdinalIgnoreCase)) continue;
            if (condition.Value <= 0) continue;

            matchedRequiredValues.Add(condition.Value);
        }

        if (matchedRequiredValues.Count == 0)
        {
            _logger.LogWarning("SendEventProgress no-op. EventKey={EventKey}, CounterKey={CounterKey}",
                eventKey, required.CounterKey);

            return Ok(new SendEventProgressPacketResponse { SendEventProgressOk = true });
        }
        
        // 증분 방식
        // - RoomId > 0 : 보통 누적(+1) (친선/랭크/전투 등)
        // - RoomId == 0: 보통 1회성 (디스코드 버튼 클릭 등)
        // - 단, 해당 CounterKey에 대해 value>1 티어가 존재하면 누적으로 전환(룸 없는 누적 이벤트 대응)
        var hasMultiStepTier = matchedRequiredValues.Any(v => v > 1);
        var isIncrement = required.RoomId > 0 || hasMultiStepTier;
        
        try
        {
            // 5) 유저별 upsert
            foreach (var uid in userIds)
            {
                if (isIncrement)
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO UserEventProgress (UserId, EventId, CycleKey, ProgressValue, UpdatedAt)
VALUES ({(long)uid}, {ev.EventId}, {cycleKey}, 1, UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE
  ProgressValue = ProgressValue + 1,
  UpdatedAt = UTC_TIMESTAMP(6);
");
                }
                else
                {
                    // 1회성(멱등): 최소 1 보장
                    await _context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO UserEventProgress (UserId, EventId, CycleKey, ProgressValue, UpdatedAt)
VALUES ({(long)uid}, {ev.EventId}, {cycleKey}, 1, UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE
  ProgressValue = GREATEST(ProgressValue, 1),
  UpdatedAt = UTC_TIMESTAMP(6);
");
                }
            }

            return Ok(new SendEventProgressPacketResponse { SendEventProgressOk = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SendEventProgress failed. EventKey={EventKey}, CounterKey={CounterKey}, RoomId={RoomId}, Users={UserCount}",
                eventKey, required.CounterKey, required.RoomId, userIds.Length);

            return StatusCode(500, new SendEventProgressPacketResponse { SendEventProgressOk = false });
        }
    }

    [HttpPost]
    [Route("ClaimEventReward")]
    public async Task<IActionResult> ClaimEventReward([FromBody] ClaimEventRewardRequired required)
    {
        var userId = await ValidateUserAsync(required.AccessToken);
        if (userId == null) return Unauthorized();
        if (required.EventId <= 0) return BadRequest("Invalid EventId");
        if (required.Tier <= 0) return BadRequest("Invalid Tier");
        
        var nowUtc = DateTime.UtcNow;
        var ev = await _context.Event.AsNoTracking()
            .Where(e => e.EventId == required.EventId
                        && e.IsActive
                        && (e.StartAt == null || e.StartAt <= nowUtc)
                        && (e.EndAt == null || e.EndAt >= nowUtc))
            .Select(e => new
            {
                e.EventId, e.EventKey, e.Version, e.StartAt, e.EndAt, e.RepeatType, e.RepeatTimezone
            })
            .SingleOrDefaultAsync();
        
        if (ev == null) return NotFound();
        
        var cycleKey = ComputeCycleKey(ev.RepeatTimezone, ev.RepeatType, nowUtc);

        var tierDef = await _context.EventRewardTier.AsNoTracking()
            .Where(tier => tier.EventId == ev.EventId
                           && tier.IsActive
                           && tier.Tier == required.Tier
                           && tier.MinEventVersion <= ev.Version
                           && (tier.MaxEventVersion == null || tier.MaxEventVersion >= ev.Version))
            .Select(tier => new
            {
                tier.Tier, tier.ConditionJson, tier.RewardJson, tier.MinEventVersion, tier.MaxEventVersion
            })
            .SingleOrDefaultAsync();
        
        if (tierDef == null) return NotFound("Tier not found");
        
        var requiredValue = ExtractRequiredValueFromConditionJson(tierDef.ConditionJson);
        if (requiredValue <= 0) return BadRequest("Invalid tier condition");
        
        var rewardSnapshot = string.IsNullOrWhiteSpace(tierDef.RewardJson) ? "{}" : tierDef.RewardJson;
        List<RewardInfo>? rewardItems;
        try
        {
            rewardItems = JsonConvert.DeserializeObject<List<RewardInfo>>(rewardSnapshot);
        }
        catch
        {
            return StatusCode(500, new ClaimEventRewardResponse
            {
                ClaimOk = false,
                EventId = ev.EventId,
                Tier = required.Tier,
                CycleKey = cycleKey,
                Error = "Invalid reward data"
            });
        }

        if (rewardItems == null || rewardItems.Count == 0)
        {
            return StatusCode(500, new ClaimEventRewardResponse
            {
                ClaimOk = false,
                EventId = ev.EventId,
                Tier = required.Tier,
                CycleKey = cycleKey,
                Error = "Invalid reward data"
            });
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            ClaimEventRewardResponse? response = null;
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var claimTransactionId = Guid.NewGuid().ToString("N");
                var inserted = await _context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT IGNORE INTO UserEventClaim
    (UserId, EventId, Tier, CycleKey, ClaimTxId, ClaimedAt, EventVersionAtClaim, RewardSnapshotJson)
VALUES
    ({(long)userId}, {ev.EventId}, {required.Tier}, {cycleKey},
     {claimTransactionId}, UTC_TIMESTAMP(6), {ev.Version}, {rewardSnapshot});
");
                
                if (inserted == 0)
                {
                    var already = await _context.UserEventClaim.AsNoTracking()
                        .AnyAsync(c => c.UserId == (long)userId
                                       && c.EventId == ev.EventId
                                       && c.Tier == required.Tier
                                       && c.CycleKey == cycleKey);
                    
                    response = new ClaimEventRewardResponse
                    {
                        ClaimOk = already,     
                        AlreadyClaimed = already,
                        EventId = ev.EventId,
                        Tier = required.Tier,
                        CycleKey = cycleKey,
                        Error = already ? "" : "Not claimable"
                    };
                    
                    await transaction.CommitAsync();
                    return;
                };
                
                foreach (var item in rewardItems)
                {
                    if (item.ProductType == ProductType.Container)
                    {
                        for (var i = 0; i < item.Count; i++)
                        {
                            _context.Mail.Add(new Mail
                            {
                                UserId = (int)userId,
                                Type = MailType.Product,
                                ProductId = item.ItemId,
                                Claimed = false
                            });
                        }
                    }
                    else
                    {
                        var composition = new ProductComposition
                        {
                            ProductId = 0,
                            CompositionId = item.ItemId,
                            ProductType = item.ProductType,
                            Count = item.Count,
                        };

                        await _claimService.StoreProductAsync((int)userId, composition);
                    }
                }

                await _context.SaveChangesExtendedAsync();
                await transaction.CommitAsync();
                
                response = new ClaimEventRewardResponse
                {
                    ClaimOk = true,
                    AlreadyClaimed = false,
                    EventId = ev.EventId,
                    Tier = required.Tier,
                    CycleKey = cycleKey
                };
            });
            
            return Ok(response ?? new ClaimEventRewardResponse
            {
                ClaimOk = false,
                EventId = ev.EventId,
                Tier = required.Tier,
                CycleKey = cycleKey,
                Error = "Unknown"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClaimEventReward failed. UserId={UserId}, EventId={EventId}, Tier={Tier}",
                userId, required.EventId, required.Tier);

            return StatusCode(500, new ClaimEventRewardResponse
            {
                ClaimOk = false,
                EventId = ev.EventId,
                Tier = required.Tier,
                CycleKey = cycleKey,
                Error = "Claim failed"
            });
        }
    }

    private int ExtractRequiredValueFromConditionJson(string? conditionJson)
    {
        if (string.IsNullOrWhiteSpace(conditionJson)) return 0;

        try
        {
            var condition = JsonConvert.DeserializeObject<CounterCondition>(conditionJson);
            if (condition == null) return 0;
            if (!condition.Type.Equals("counter", StringComparison.OrdinalIgnoreCase)) return 0;
            return condition.Value > 0 ? condition.Value : 0;
        }
        catch 
        {
            return 0;
        }
    }
    
    private sealed class CounterCondition
    {
        public string Type { get; set; } = "";
        public string CounterKey { get; set; } = "";
        public int Value { get; set; }
    }
    
    private bool TryParseCounterCondition(string? json, out CounterCondition cond)
    {
        cond = new CounterCondition();
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            var parsed = JsonConvert.DeserializeObject<CounterCondition>(json);
            if (parsed == null) return false;
            cond = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private string ComputeCycleKey(string repeatTimeZone, EventRepeatType repeatType, DateTime nowUtc)
    {
        var tz = ResolveTimeZone(repeatTimeZone);
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), tz);

        return repeatType switch
        {
            EventRepeatType.None    => "default",
            EventRepeatType.Daily   => local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            // ISO 8601 week: 월요일 시작, 1주차 규칙 포함
            EventRepeatType.Weekly  => $"{ISOWeek.GetYear(local)}-W{ISOWeek.GetWeekOfYear(local):00}",
            EventRepeatType.Monthly => local.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            _ => "default"
        };
    }

    private TimeZoneInfo ResolveTimeZone(string? tzId)
    {
        if (string.IsNullOrWhiteSpace(tzId) || tzId.Equals("UTC", StringComparison.OrdinalIgnoreCase))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(tzId.Trim());
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }
}
 