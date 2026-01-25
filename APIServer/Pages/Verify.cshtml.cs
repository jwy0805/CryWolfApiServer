using ApiServer.DB;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Pages;

public class Verify : PageModel
{
    private readonly AppDbContext _dbContext;
    private readonly UserService _userService;
    private readonly TokenValidator _tokenValidator;
    
    public Verify(AppDbContext dbContext, UserService userService, TokenValidator tokenValidator)
    {
        _dbContext = dbContext;
        _userService = userService;
        _tokenValidator = tokenValidator;
    }
    
    public string Message { get; private set; } = string.Empty;
    public bool IsVerified { get; private set; }

    public async Task<IActionResult> OnGet(string token)
    {
        try
        {
            var principal = _tokenValidator.ValidateToken(token);
            if (principal == null)
            {
                Message = "Invalid Token\nPlease try again.";
                IsVerified = false;
                return Page();
            }
        
            var email = _tokenValidator.GetEmailFromToken(principal);
            if (email == string.Empty)
            {
                Message = "Invalid Email\nPlease try again.";
                IsVerified = false;
                return Page();
            }

            var tempUser = await _dbContext.TempUser.SingleOrDefaultAsync(u => u.TempUserAccount == email);
            if (tempUser == null)
            {
                Message = "User Not Found";
                IsVerified = false;
                return Page();
            }

            var strategy = _dbContext.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync();

                tempUser.IsVerified = IsVerified;

                await _dbContext.SaveChangesAsync();

                var created = await _userService
                    .CreateAccount(tempUser.TempUserAccount, "None", AuthProvider.Direct, tempUser.TempPassword);
                if (created)
                {
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                    
                    IsVerified = true;
                    Message = "Email Verified.\nLogin with your new account.";
                }
                else
                {
                    await transaction.RollbackAsync();

                    Message = "Error: Account Creation Failed";
                    IsVerified = false;
                }
            });
            
            return Page();
        }
        catch (Exception e)
        {
            Message = "Error: " + e.Message;
            IsVerified = false;
            
            return Page();
        }
    }
}