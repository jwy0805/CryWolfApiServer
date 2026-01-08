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
    
    [HttpPost("CreateNotice")]
    public async Task<IActionResult> CreateNotice([FromBody] CreateEventNoticeRequired required)
    {
        if (required.StartAt == null || required.EndAt == null)
        {
            return BadRequest(new { success = false, message = "StartAt/EndAt is required." });
        }
        
        var startAtUtc = required.StartAt.Value.UtcDateTime;
        var endAtUtc = required.EndAt.Value.UtcDateTime;
        if (endAtUtc <= startAtUtc)
        {
            return BadRequest(new { success = false, message = "EndAt must be greater than StartAt." });
        }
        
        var rewards = required.Rewards ?? new List<RewardInfo>();
        if (rewards.Any(reward => reward.Count <= 0))
        {
            return BadRequest(new { success = false, message = "Reward count must be greater than 0." });
        }
        
        var noticeType = rewards.Count > 0 ? NoticeType.Event : NoticeType.Notice;

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var eventNotice = new EventNotice
            {
                NoticeType = noticeType,
                IsPinned = required.IsPinned,
                IsActive = true,
                StartAt = startAtUtc,
                EndAt = endAtUtc,
                CreatedAt = DateTime.UtcNow,
            };

            foreach (var loc in required.Localizations)
            {
                eventNotice.Localizations.Add(new EventNoticeLocalization
                {
                    LanguageCode = loc.LanguageCode.ToLowerInvariant(),
                    Title = loc.Title,
                    Content = loc.Content
                });
            }  

            foreach (var reward in rewards)
            {
               
            }
            
            _context.EventNotice.Add(eventNotice);
            await _context.SaveChangesExtendedAsync();
            await transaction.CommitAsync();
            
            return Ok(new { success = true, id = eventNotice.EventNoticeId });
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { success = false, message = "CreateNotice failed.", detail = e.Message });
        }
    }
}