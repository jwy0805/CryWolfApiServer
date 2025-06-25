using ApiServer.DB;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MailController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserService _userService;
    private readonly RewardService _rewardService;
    private readonly TokenService _tokenService;
    private readonly TokenValidator _tokenValidator;
    private readonly ILogger<UserAccountController> _logger;
    
    public MailController(
        AppDbContext context, 
        UserService userService, 
        RewardService rewardService,
        TokenService tokenService, 
        TokenValidator validator,
        ILogger<UserAccountController> logger)
    {
        _context = context;
        _userService = userService;
        _rewardService = rewardService;
        _tokenService = tokenService;
        _tokenValidator = validator;
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
    [Route("GetMail")]
    public async Task<IActionResult> GetMail([FromBody] LoadPendingMailPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var userIdNull = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userIdNull == null) return Unauthorized();
        
        // Left join <Mail, Product>
        var userId = userIdNull.Value;
        var mailInfos = await (
            from mail in _context.Mail.AsNoTracking()
            where mail.UserId == userId && mail.Expired == false
            join prod in _context.Product.AsNoTracking()
                on mail.ProductId equals prod.ProductId into prodGroup
            from prod in prodGroup.DefaultIfEmpty()
            select new MailInfo
            {
                MailId = mail.MailId,
                Type = mail.Type,
                SentAt = mail.CreatedAt,
                ExpiresAt = mail.ExpiresAt,
                ProductId = mail.ProductId ?? 0,
                ProductCategory = prod != null ? prod.Category : ProductCategory.None,
                Claimed = mail.Claimed,
                Message = mail.Message ?? string.Empty,
                Sender = mail.Sender  ?? "Cry Wolf"
            }
        ).ToListAsync();
        
        var res = new LoadPendingMailPacketResponse
        {
            LoadPendingMailOk = true,
            PendingMailList   = mailInfos
        };

        return Ok(res);
    }

    [HttpPut]
    [Route("ClaimMail")]
    public async Task<IActionResult> ClaimMail([FromBody] ClaimMailPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var userIdNull = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userIdNull == null) return Unauthorized();
        
        var userId = userIdNull.Value;
        var mail = await _context.Mail.FirstOrDefaultAsync(m => m.MailId == required.MailId && m.UserId == userId);
        if (mail == null) return NotFound();
        
        var res = new ClaimMailPacketResponse();
        
        switch (mail.Type)
        {
            case MailType.Notice:
                mail.Claimed = true;
                res.ClaimMailOk = true;
                res.IsProductMail = false;
                break;
            case MailType.Invite:
                mail.Claimed = true;
                mail.Expired = true;
                res.ClaimMailOk = true;
                res.IsProductMail = false;
                break;
            case MailType.Product:
                res.ClaimMailOk = true;
                res.IsProductMail = true;
                break;
            case MailType.None:
            default:
                return NotFound();
        }
        
        await _context.SaveChangesAsync();
        
        return Ok(res);
    }

    [HttpDelete]
    [Route("DeleteReadMail")]
    public async Task<IActionResult> DeleteReadMail([FromBody] DeleteReadMailPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var userIdNull = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userIdNull == null) return Unauthorized();
        
        var userId = userIdNull.Value;
        var mails = await _context.Mail.Where(m => m.UserId == userId && m.Claimed).ToListAsync();
        if (mails.Count == 0) return Ok(new DeleteReadMailPacketResponse { DeleteReadMailOk = true });
        
        _context.Mail.RemoveRange(mails);
        await _context.SaveChangesAsync();
        return Ok(new DeleteReadMailPacketResponse { DeleteReadMailOk = true });
    }
}