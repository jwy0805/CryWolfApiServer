using System.Text.Json;
using ApiServer.DB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminController> _logger;
    
    public AdminController(AppDbContext context, IConfiguration config, ILogger<AdminController> logger)
    {
        _context = context;
        _config = config;
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
    public async Task<IActionResult> PublishNotice([FromBody] PublishNoticeRequired required)
    {
        if (required.Localizations == null || required.Localizations.Count == 0)
            return BadRequest(new { success = false, message = "Localizations is required." });

        // 둘 다 null 또는 둘 다 값
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

        // localization 검증 + 언어 중복 방지
        var seenLang = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var loc in required.Localizations)
        {
            var lang = NormalizeLang(loc.LanguageCode);
            if (!seenLang.Add(lang))
                return BadRequest(new { success = false, message = $"Duplicate localization language: {lang}." });

            var title = (loc.Title ?? "").Trim();
            var content = (loc.Content ?? "").Trim();

            if (string.IsNullOrWhiteSpace(title))
                return BadRequest(new { success = false, message = $"Title is required. (lang={lang})" });
            if (string.IsNullOrWhiteSpace(content))
                return BadRequest(new { success = false, message = $"Content is required. (lang={lang})" });
        }

        var notice = new Notice
        {
            IsPinned = required.IsPinned,
            IsActive = true,
            StartAt = startUtc,
            EndAt = endUtc,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = null, // admin userId 필요하면 주입
        };

        foreach (var loc in required.Localizations)
        {
            notice.Localizations.Add(new NoticeLocalization
            {
                LanguageCode = NormalizeLang(loc.LanguageCode),
                Title = (loc.Title ?? "").Trim(),
                Content = (loc.Content ?? "").Trim(),
            });
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            int noticeId = 0;

            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.Notice.Add(notice);
                    await _context.SaveChangesExtendedAsync();
                    await tx.CommitAsync();
                    noticeId = notice.NoticeId;
                }
                catch (Exception exInner)
                {
                    _logger.LogError(exInner, "PublishNotice: error during transaction, rolling back");
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return Ok(new { success = true, noticeId });
        }
        catch (DbUpdateException dbEx)
        {
            return Conflict(new
            {
                success = false,
                message = "PublishNotice failed due to DB constraint.",
                detail = dbEx.InnerException?.Message ?? dbEx.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PublishNotice failed.");
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

            if (t.MinEventVersion <= 0)
                return BadRequest(new { success = false, message = $"MinEventVersion must be > 0. (tier={t.Tier})" });

            if (t.MaxEventVersion.HasValue && t.MaxEventVersion.Value < t.MinEventVersion)
                return BadRequest(new { success = false, message = $"MaxEventVersion must be >= MinEventVersion. (tier={t.Tier})" });
        }

        // localization 검증 + 언어 중복 방지
        var seenLang = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var loc in required.Localizations)
        {
            var lang = NormalizeLang(loc.LanguageCode);
            if (!seenLang.Add(lang))
                return BadRequest(new { success = false, message = $"Duplicate localization language: {lang}." });

            var title = (loc.Title ?? "").Trim();
            var content = (loc.Content ?? "").Trim();

            if (string.IsNullOrWhiteSpace(title))
                return BadRequest(new { success = false, message = $"Title is required. (lang={lang})" });
            if (string.IsNullOrWhiteSpace(content))
                return BadRequest(new { success = false, message = $"Content is required. (lang={lang})" });
        }

        // (선택) timezone 문자열 정리
        var repeatTimezone = string.IsNullOrWhiteSpace(required.RepeatTimeZone) ? "UTC" : required.RepeatTimeZone.Trim();

        // UX 사전 체크 (최종 방어는 unique index)
        var exists = await _context.Event.AsNoTracking()
            .AnyAsync(e => e.EventKey == eventKey);
        if (exists)
            return Conflict(new { success = false, message = "Event with the same EventKey already exists." });

        var ev = new Event
        {
            EventKey = eventKey,
            IsActive = true,
            StartAt = startUtc,
            EndAt = endUtc,
            RepeatType = required.RepeatType,
            RepeatTimezone = repeatTimezone,
            Version = required.Version,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = null,

            IsPinned = required.IsPinned,
            Priority = required.Priority,
        };

        foreach (var loc in required.Localizations)
        {
            ev.Localizations.Add(new EventLocalization
            {
                LanguageCode = NormalizeLang(loc.LanguageCode),
                Title = (loc.Title ?? "").Trim(),
                Content = (loc.Content ?? "").Trim() + "\n \n \n \n \n",
            });
        }

        foreach (var tier in required.Tiers.OrderBy(x => x.Tier))
        {
            ev.RewardTiers.Add(new EventRewardTier
            {
                Tier = tier.Tier,
                IsActive = true,
                ConditionJson = tier.ConditionJson,
                RewardJson = tier.RewardJson,
                MinEventVersion = tier.MinEventVersion,
                MaxEventVersion = tier.MaxEventVersion,
            });
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            int eventId = 0;

            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.Event.Add(ev);
                    await _context.SaveChangesExtendedAsync();
                    await tx.CommitAsync();
                    eventId = ev.EventId;
                }
                catch (Exception exInner)
                {
                    _logger.LogError(exInner, "PublishEvent: error during transaction, rolling back");
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return Ok(new { success = true, eventId });
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
            _logger.LogError(ex, "PublishEvent failed.");
            return StatusCode(500, new { success = false, message = "PublishEvent failed.", detail = ex.Message });
        }
    }
    
    [HttpPost]
    [Route("DeleteNotice")]
    public async Task<IActionResult> DeleteNotice([FromBody] DeleteNoticePacketRequired required)
    {
        if (required.AdminPassword != _config["Admin:Password"])
            return Unauthorized(new { success = false, message = "Invalid Request." });
        
        var noticeId = required.NoticeId;
        var affected = await _context.Notice
            .Where(n => n.NoticeId == noticeId)
            .ExecuteDeleteAsync();

        if (affected == 0)
            return NotFound(new { success = false, message = $"Notice not found. (noticeId={noticeId})" });

        return Ok(new { success = true, deletedNoticeId = noticeId });
    }

    [HttpPost]
    [Route("DeleteEvent")]
    public async Task<IActionResult> DeleteEvent([FromBody] DeleteEventPacketRequired required)
    {
        if (required.AdminPassword != _config["Admin:Password"])
            return Unauthorized(new { success = false, message = "Invalid Request." });

        var eventId = required.EventId;
        var affected = await _context.Event
            .Where(e => e.EventId == eventId)
            .ExecuteDeleteAsync();

        if (affected == 0)
            return NotFound(new { success = false, message = $"Event not found. (eventId={eventId})" });

        return Ok(new { success = true, deletedEventId = eventId });
    }

    [HttpPost]
    [Route("ClearNotices")]
    public async Task<IActionResult> ClearNotices([FromBody] ClearNoticesPacketRequired required)
    {
        if (required.AdminPassword != _config["Admin:Password"])
            return Unauthorized(new { success = false, message = "Invalid Request." });

        var deleted = await _context.Notice.ExecuteDeleteAsync();
        return Ok(new { success = true, deletedNotices = deleted });
    }

    [HttpPost]
    [Route("ClearEvents")]
    public async Task<IActionResult> ClearEvents([FromBody] ClearEventsPacketRequired required)
    {
        if (required.AdminPassword != _config["Admin:Password"])
            return Unauthorized(new { success = false, message = "Invalid Request." });

        var deleted = await _context.Event.ExecuteDeleteAsync();
        return Ok(new { success = true, deletedEvents = deleted });
    }

    private static string NormalizeLang(string languageCode)
    {
        var lang = (languageCode ?? "").Trim();
        if (string.IsNullOrWhiteSpace(lang)) return "en";
        return lang.ToLowerInvariant();
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