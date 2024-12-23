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
            .Where(mail => mail.UserId == userId)
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
        
        var res = new ClaimMailPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal) ?? 0;
        if (userId == 0)
        {
            res.ClaimMailOk = false;
            return Ok(res);
        }
        
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Lock the mail row 'for update', other transactions will wait until this transaction is committed
                var mail = _context.Mail
                    .FromSqlRaw("SELECT * FROM Mail WHERE MailId = {0} FOR UPDATE", required.MailId)
                    .FirstOrDefault();
                
                if (mail == null || mail.Claimed)
                {
                    res.ClaimMailOk = false;
                    return;
                }
                
                var compositions = _context.ProductComposition
                    .Where(pc => pc.ProductId == mail.ProductId)
                    .ToList();
                
                foreach (var product in compositions
                             .Select(composition => 
                                 _rewardService.ClaimFinalProducts(composition.CompositionId))
                                .SelectMany(productList => productList))
                {
                    _rewardService.ClaimPurchasedProduct(userId, product);
                }

                mail.Claimed = true;
                await _context.SaveChangesExtendedAsync();
                await transaction.CommitAsync();
                res.ClaimMailOk = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await transaction.RollbackAsync();
                res.ClaimMailOk = false;
            }
        });
        
        return Ok(res);
    }
}