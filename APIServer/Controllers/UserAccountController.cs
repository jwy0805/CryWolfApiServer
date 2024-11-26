using ApiServer.DB;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserAccountController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserService _emailService;
    private readonly TokenService _tokenService;
    private readonly TokenValidator _tokenValidator;
    
    public UserAccountController(AppDbContext context, UserService emailService, TokenService tokenService, TokenValidator validator)
    {
        _context = context;
        _emailService = emailService;
        _tokenService = tokenService;
        _tokenValidator = validator;
    }
    
    [HttpPost]
    [Route("ValidateNewAccount")]
    public async Task<IActionResult> ValidateNewAccount([FromBody] ValidateNewAccountPacketRequired required)
    {
        const string allowedSpecialCharacters = "!@#$%^&*()-_=+[]{};:,.?";
        var approved = false;
        var res = new ValidateNewAccountPacketResponse();
        var tempUserDb = _context.TempUser;
        var account = _context.User
            .AsNoTracking()
            .FirstOrDefault(user => user.UserAccount == required.UserAccount);
        
        if (required.Password.Any(char.IsDigit) && required.Password.Any(char.IsLetter) &&
            required.Password.Any(allowedSpecialCharacters.Contains) && required.Password.Length >= 8 && account == null)
        {
            res.ValidateOk = true;
            res.ErrorCode = 0;
            approved = true;
        }
        else
        {
            if (account != null)
            {
                res.ValidateOk = false;
                res.ErrorCode = 1;
            }
            else
            {
                res.ValidateOk = false;
                res.ErrorCode = 2;
            }
        }

        if (approved)
        {
            var token = _tokenService.GenerateEmailVerificationToken(required.UserAccount);
            // var verificationLink = $"https://hamonstudio.net/verify/{token}";
            var verificationLink = $"https://localhost:7270/verify/{token}";
            var hashedPassword = _tokenService.HashPassword(required.Password);
            
            tempUserDb.Add(new TempUser
            {
                TempUserAccount = required.UserAccount,
                TempPassword = hashedPassword,
                IsVerified = false,
                CreatedAt = DateTime.UtcNow
            });

            var dbTask = _context.SaveChangesExtendedAsync();
            var emailTask = _emailService.SendVerificationEmail(required.UserAccount, verificationLink);
            
            await Task.WhenAll(dbTask, emailTask);
        }
        
        return Ok(res);
    }
    
    [HttpPost]
    [Route("Login")]
    public LoginUserAccountPacketResponse Login([FromBody] LoginUserAccountPacketRequired required)
    {
        var res = new LoginUserAccountPacketResponse();
        var user = _context.User
            .AsNoTracking()
            .FirstOrDefault(u => u.UserAccount == required.UserAccount);

        if (user == null)
        {
            res.LoginOk = false;
        }
        else
        {
            if (_tokenValidator.VerifyPassword(required.Password, user.Password) == false)
            {
                res.LoginOk = false;
                return res;
            }
            
            var tokens = _tokenService.GenerateTokens(user.UserId);
            res.AccessToken = tokens.AccessToken;
            res.RefreshToken = tokens.RefreshToken;
            res.LoginOk = true;
            user.State = UserState.Activate;
            user.Act = UserAct.InLobby;
            _context.SaveChangesExtended();
        }
        
        return res;
    }
    
    [HttpPost]
    [Route("RefreshToken")]
    public IActionResult RefreshToken([FromBody] RefreshTokenRequired request)
    {
        try
        {
            var tokens = _tokenValidator.RefreshAccessToken(request.RefreshToken);
            var response = new RefreshTokenResponse()
            {
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken
            };
            return Ok(response);
        }
        catch (SecurityTokenException exception)
        {
            return Unauthorized(new { message = exception.Message });
        }
    }

    [HttpPost]
    [Route("LoadUserInfo")]
    public IActionResult LoadUserInfo([FromBody] LoadUserInfoPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var res = new LoadUserInfoPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null) return Unauthorized();
        
        var user = _context.User
            .AsNoTracking()
            .FirstOrDefault(user => user.UserId == userId);
        var userStat = _context.UserStats
            .AsNoTracking()
            .FirstOrDefault(userStat => userStat.UserId == userId);
        if (user == null || userStat == null) return NotFound();

        res.UserInfo = new UserInfo
        {
            UserName = user.UserName,
            Level = userStat.UserLevel,
            Exp = userStat.Exp,
            Gold = userStat.Gold,
            Spinel = userStat.Spinel,
            RankPoint = userStat.RankPoint,
        };
        
        res.LoadUserInfoOk = true;
        
        return Ok(res);
    }
    
    [HttpPost]
    [Route("LoadTestUser")]
    public IActionResult LoadTestUser([FromBody] LoadTestUserPacketRequired required)
    {
        var res = new LoadTestUserPacketResponse();
        var user = _context.User
            .AsNoTracking()
            .FirstOrDefault(user => user.UserId == required.UserId);
        if (user == null) return NotFound();
        
        var tokens = _tokenService.GenerateTokens(user.UserId);
        res.AccessToken = tokens.AccessToken;
        res.RefreshToken = tokens.RefreshToken;
        
        return Ok(res);
    }
    
    [HttpPost]
    [Route("UpdateUserInfo")]
    public IActionResult UpdateUserInfo([FromBody] UpdateUserInfoPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var res = new UpdateUserInfoPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null) return Unauthorized();
        
        var userInfo = required.UserInfo;
        var userStat = _context.UserStats
            .AsNoTracking()
            .FirstOrDefault(userStat => userStat.UserId == userId);
        if (userStat == null) return NotFound();
        
        using var transaction = _context.Database.BeginTransaction();
        try
        {
            userStat.UserLevel = userInfo.Level;
            userStat.Exp = userInfo.Exp;
            userStat.Gold = userInfo.Gold;
            userStat.Spinel = userInfo.Spinel;
            userStat.RankPoint = userInfo.RankPoint;
            res.UpdateUserInfoOk = _context.SaveChangesExtended();
            transaction.Commit();
        }
        catch (Exception e)
        {
            res.UpdateUserInfoOk = false;
            transaction.Rollback();
            return Ok(res);
        }
        
        return Ok(res);
    }
    
    [HttpPost]
    [Route("SearchUsername")]
    public IActionResult SearchUsername([FromBody] SearchUsernamePacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        var res = new SearchUsernamePacketResponse();
        var friendUser = _context.User
            .AsNoTracking()
            .FirstOrDefault(user => user.UserName == required.Username);
        
        if (friendUser == null)
        {
            res.UserInfo = new UserInfo { UserName = string.Empty };
            res.SearchUsernameOk = false;
            return Ok(res);
        }
        
        var userStat = _context.UserStats
            .AsNoTracking()
            .FirstOrDefault(userStat => userStat.UserId == friendUser.UserId);
        if (userStat == null) return NotFound();

        res.UserInfo = new UserInfo
        {
            UserName = friendUser.UserName,
            Level = userStat.UserLevel,
            Exp = userStat.Exp,
            Gold = userStat.Gold,
            Spinel = userStat.Spinel,
            RankPoint = userStat.RankPoint,
        };
        
        var relation = _context.Friends
            .AsNoTracking()
            .FirstOrDefault(friend => friend.UserId == userId && friend.FriendId == friendUser.UserId);

        if (relation == null)
        {
            res.FriendStatus = FriendStatus.None;
            res.SearchUsernameOk = true;

            return Ok(res);
        }
        
        switch (relation.Status)
        {
            case FriendStatus.Pending:
                res.FriendStatus = FriendStatus.Pending;
                break;
            case FriendStatus.Accepted:
                res.FriendStatus = FriendStatus.Accepted;
                break;
            case FriendStatus.Blocked:
                res.FriendStatus = FriendStatus.Blocked;
                break;
        }
        
        return Ok(res);
    }
}
