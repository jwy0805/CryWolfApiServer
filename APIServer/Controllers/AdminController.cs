using System.Text.Json;
using ApiServer.DB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<AdminController> _logger;
    
    public AdminController(AppDbContext context, ILogger<AdminController> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    [HttpPost]
    [Route("SendMail")]
    public async Task<IActionResult> SendMail([FromBody] SendMailByAdminPacketRequired required)
    {
        if (required.UserIds.Length == 0)
            return BadRequest("UserIds must not be empty.");

        var users = await _context.User
            .AsNoTracking()
            .Where(u => required.UserIds.Contains(u.UserId))
            .Select(u => u.UserId)
            .ToListAsync();

        if (users.Count == 0)
        {
            _logger.LogWarning("SendMail: no matching users for IDs {UserIds}", required.UserIds);
            return NotFound(new SendMailByAdminPacketResponse { SendMailOk = false });
        }

        var productCode = string.Empty;
        if (required.ProductId != null && required.ProductId != 0)
        {
            var product = await _context.Product.AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductId == required.ProductId);
            productCode = product?.ProductCode ?? string.Empty;
        }

        var utcNow = DateTime.UtcNow;
        var mails = users.Select(userId => new Mail
        {
            UserId = userId,
            Type = required.Type,
            CreatedAt = utcNow,
            ExpiresAt = utcNow.AddDays(30),
            ProductId = required.ProductId == 0 ? null : required.ProductId,
            ProductCode = productCode,
            Message = required.Message,
            Sender = required.Sender,
        }).ToList();

        var response = new SendMailByAdminPacketResponse();
        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.Mail.AddRange(mails);
                    await _context.SaveChangesExtendedAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception exInner)
                {
                    _logger.LogError(exInner, "SendMail: error during transaction, rolling back");
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            response.SendMailOk = true;
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendMail: failed to send mail to users {UserIds}", users);
            response.SendMailOk = false;
            return StatusCode(StatusCodes.Status500InternalServerError, response);
        }
    }
    
    [HttpPost]
    [Route("PublishNotice")]
    public async Task<IActionResult> CreateNotice([FromBody] PublishNoticeRequired required)
    {
        
        if (required.Localizations.Count == 0)
            return BadRequest(new { success = false, message = "Localizations is required." });

        if (required.StartAt.HasValue ^ required.EndAt.HasValue)
            return BadRequest(new { success = false, message = "StartAt and EndAt must be both set or both null." });

        DateTime? startUtc = null;
        DateTime? endUtc = null;

        if (required.StartAt.HasValue && required.EndAt.HasValue)
        {
            startUtc = required.StartAt.Value.UtcDateTime;
            endUtc = required.EndAt.Value.UtcDateTime;

            if (endUtc <= startUtc)
                return BadRequest(new { success = false, message = "EndAt must be greater than StartAt." });
        }
        
        var notice = new EventNotice
        {
            NoticeType = NoticeType.Notice,
            IsPinned = required.IsPinned,
            IsActive = true,
            StartAt = startUtc,
            EndAt = endUtc,
            CreatedAt = DateTime.UtcNow,
        };

        foreach (var loc in required.Localizations)
        {
            var lang = (loc.LanguageCode ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lang)) lang = "en";

            var title = (loc.Title ?? "").Trim();
            var content = (loc.Content ?? "").Trim();

            if (string.IsNullOrWhiteSpace(title))
                return BadRequest(new { success = false, message = $"Title is required. (lang={lang})" });

            if (string.IsNullOrWhiteSpace(content))
                return BadRequest(new { success = false, message = $"Content is required. (lang={lang})" });

            notice.Localizations.Add(new EventNoticeLocalization
            {
                LanguageCode = lang,
                Title = title,
                Content = content
            });
        }

        try
        {
            _context.EventNotice.Add(notice);
            await _context.SaveChangesExtendedAsync();
            return Ok(new { success = true, noticeId = notice.EventNoticeId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "PublishNotice failed.", detail = ex.Message });
        }
    }

    [HttpPost]
    [Route("PublishEvent")]
    public async Task<IActionResult> PublishEvent([FromBody] PublishEventRequired required)
    {
        var eventKey = (required.EventKey ?? "").Trim();
        if (string.IsNullOrWhiteSpace(eventKey))
            return BadRequest(new { success = false, message = "EventKey is required." });

        var startUtc = required.StartAt.UtcDateTime;
        var endUtc = required.EndAt.UtcDateTime;
        if (endUtc <= startUtc)
            return BadRequest(new { success = false, message = "EndAt must be greater than StartAt." });

        if (required.Version <= 0)
            return BadRequest(new { success = false, message = "Version must be greater than 0." });

        if (required.Localizations.Count == 0)
            return BadRequest(new { success = false, message = "Localizations is required." });

        if (required.Tiers.Count == 0)
            return BadRequest(new { success = false, message = "At least one tier is required." });

        // tier 중복 방지
        var dupTier = required.Tiers.GroupBy(t => t.Tier).FirstOrDefault(g => g.Count() > 1);
        if (dupTier != null)
            return BadRequest(new { success = false, message = $"Duplicate tier found: {dupTier.Key}." });

        foreach (var t in required.Tiers)
        {
            if (t.Tier <= 0)
                return BadRequest(new { success = false, message = "Tier must be greater than 0." });

            if (string.IsNullOrWhiteSpace(t.ConditionJson) || !IsValidJson(t.ConditionJson))
                return BadRequest(new { success = false, message = $"Invalid ConditionJson. (tier={t.Tier})" });

            if (string.IsNullOrWhiteSpace(t.RewardJson) || !IsValidJson(t.RewardJson))
                return BadRequest(new { success = false, message = $"Invalid RewardJson. (tier={t.Tier})" });

            if (t.MinEventVersion <= 0)
                return BadRequest(new { success = false, message = $"MinEventVersion must be > 0. (tier={t.Tier})" });

            if (t.MaxEventVersion.HasValue && t.MaxEventVersion.Value < t.MinEventVersion)
                return BadRequest(new { success = false, message = $"MaxEventVersion must be >= MinEventVersion. (tier={t.Tier})" });
        }

        // localization 검증 + 언어 중복 방지(선택이지만 권장: Unique 인덱스와 일치)
        var seenLang = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var loc in required.Localizations)
        {
            var lang = (loc.LanguageCode ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lang)) lang = "en";

            if (!seenLang.Add(lang))
                return BadRequest(new { success = false, message = $"Duplicate localization language: {lang}." });

            var title = (loc.Title ?? "").Trim();
            var content = (loc.Content ?? "").Trim();

            if (string.IsNullOrWhiteSpace(title))
                return BadRequest(new { success = false, message = $"Title is required. (lang={lang})" });
            if (string.IsNullOrWhiteSpace(content))
                return BadRequest(new { success = false, message = $"Content is required. (lang={lang})" });
        }

        // UX용 사전 체크(레이스 컨디션은 Unique 인덱스로 최종 방어)
        var exists = await _context.EventDefinition.AsNoTracking()
            .AnyAsync(e => e.EventKey == eventKey);

        if (exists)
            return Conflict(new { success = false, message = "Event with the same EventKey already exists." });
        
        var repeatTimezone = string.IsNullOrWhiteSpace(required.RepeatTimeZone) ? "UTC" : required.RepeatTimeZone.Trim();
        var eventDef = new EventDefinition
        {
            EventKey = eventKey,
            IsActive = true,
            StartAt = startUtc,
            EndAt = endUtc,
            RepeatType = required.RepeatType,
            RepeatTimezone = repeatTimezone,
            Version = required.Version,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = null, // 필요하면 admin userId 넣기
        };

        // Reward tiers (EventId를 직접 만지지 말고 네비게이션으로 붙인다)
        foreach (var tier in required.Tiers.OrderBy(x => x.Tier))
        {
            eventDef.RewardTiers.Add(new EventRewardTier
            {
                Tier = tier.Tier,
                IsActive = true,
                ConditionJson = tier.ConditionJson,
                RewardJson = tier.RewardJson,
                MinEventVersion = tier.MinEventVersion,
                MaxEventVersion = tier.MaxEventVersion,
            });
        }

        var eventNotice = new EventNotice
        {
            NoticeType = NoticeType.Event,
            IsPinned = false,
            IsActive = true,
            StartAt = startUtc,
            EndAt = endUtc,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = null,
        };

        foreach (var loc in required.Localizations)
        {
            var lang = (loc.LanguageCode ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lang)) lang = "en";

            eventNotice.Localizations.Add(new EventNoticeLocalization
            {
                LanguageCode = lang,
                Title = (loc.Title ?? "").Trim(),
                Content = (loc.Content ?? "").Trim(),
            });
        }

        eventDef.Notices.Add(eventNotice);

        try
        {
            _context.EventDefinition.Add(eventDef);
            await _context.SaveChangesExtendedAsync();
            return Ok(new { success = true, eventId = eventDef.EventId });
        }
        catch (DbUpdateException dbEx)
        {
            return Conflict(new
            {
                success = false,
                message = "PublishEvent failed due to DB constraint.",
                detail = dbEx.InnerException?.Message ?? dbEx.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "PublishEvent failed.",
                detail = ex.Message
            });
        }
    }
    
    private static bool IsValidJson(string json)
    {
        try
        {
            JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}