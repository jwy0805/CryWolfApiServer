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
        if (required.ProductId != 0)
        {
            var product = await _context.Product.AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductId == required.ProductId);
            productCode = product?.ProductCode ?? string.Empty;
        }

        var utcNow = DateTime.UtcNow;
        var mails = users.Select(userId => new Mail
        {
            UserId      = userId,
            Type        = required.Type,
            CreatedAt   = utcNow,
            ExpiresAt   = utcNow.AddDays(30),
            ProductId   = required.ProductId == 0 ? null : required.ProductId,
            ProductCode = productCode,
            Message     = required.Message
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
    public IActionResult GetMail([FromBody] LoadPendingMailPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var res = new LoadPendingMailPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal) ?? 0;
        if (userId == 0)
        {
            res.LoadPendingMailOk = false;
            return Ok(res);
        }
        
        var mails = _context.Mail.AsNoTracking()
            .Where(mail => mail.UserId == userId && mail.Expired == false)
            .ToList();
        
        res.LoadPendingMailOk = true;
        res.PendingMailList = mails.Select(mail => new MailInfo
        {
            MailId = mail.MailId,
            Type = mail.Type,
            SentAt = mail.CreatedAt,
            ExpiresAt = mail.ExpiresAt,
            ProductId = mail.ProductId ?? 0,
            Claimed = mail.Claimed,
            Message = mail.Message ?? "",
            Sender = mail.Sender ?? "Cry Wolf"
        }).ToList();

        foreach (var mailInfo in res.PendingMailList)
        {
            mailInfo.ProductCategory = _context.Product
                .FirstOrDefault(p => p.ProductId == mailInfo.ProductId)?.Category ?? ProductCategory.None;
        }

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
        // var strategy = _context.Database.CreateExecutionStrategy();
        //
        // await strategy.ExecuteAsync(async () =>
        // {
        //     await using var transaction = await _context.Database.BeginTransactionAsync();
        //     try
        //     {
        //         // Lock the mail row 'for update', other transactions will wait until this transaction is committed
        //         var mail = _context.Mail
        //             .FromSqlRaw("SELECT * FROM Mail WHERE MailId = {0} AND UserId = {1} FOR UPDATE"
        //                 , required.MailId, userId)
        //             .FirstOrDefault();
        //         
        //         if (mail == null || mail.Claimed)
        //         {
        //             res.ClaimMailOk = false;
        //             return;
        //         }
        //
        //         var compositions = _context.ProductComposition
        //             .Where(pc => pc.ProductId == mail.ProductId)
        //             .ToList();
        //         
        //         foreach (var product in compositions
        //                      .Select(composition => 
        //                          _rewardService.ClaimFinalProducts(composition.CompositionId))
        //                         .SelectMany(productList => productList))
        //         {
        //             _rewardService.ClaimPurchasedProduct(userId, product);
        //         }
        //
        //         mail.Claimed = true;
        //         await _context.SaveChangesExtendedAsync();
        //         await transaction.CommitAsync();
        //         res.ClaimMailOk = true;
        //     }
        //     catch (Exception e)
        //     {
        //         Console.WriteLine(e);
        //         await transaction.RollbackAsync();
        //         res.ClaimMailOk = false;
        //     }
        // });
        
        return Ok(res);
    }
}