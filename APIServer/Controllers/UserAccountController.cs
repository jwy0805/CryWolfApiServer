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
    private readonly UserService _userService;
    private readonly TokenService _tokenService;
    private readonly TokenValidator _tokenValidator;
    private readonly ConfigService _configService;
    private readonly ILogger<UserAccountController> _logger;
    
    public UserAccountController(
        AppDbContext context, 
        UserService userService, 
        TokenService tokenService, 
        TokenValidator validator,
        ConfigService configService,
        ILogger<UserAccountController> logger)
    {
        _context = context;
        _userService = userService;
        _tokenService = tokenService;
        _tokenValidator = validator;
        _configService = configService;
        _logger = logger;
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
            var hashedPassword = _tokenService.HashPassword(required.Password);
            var verificationLink = Environment.GetEnvironmentVariable("ENVIRONMENT") switch
            {
                "Dev" => $"https://hamonstudio.net/verify/{token}",
                _ => $"https://localhost:7270/verify/{token}"
            };
            
            tempUserDb.Add(new TempUser
            {
                TempUserAccount = required.UserAccount,
                TempPassword = hashedPassword,
                IsVerified = false,
                CreatedAt = DateTime.UtcNow
            });

            var dbTask = _context.SaveChangesExtendedAsync();
            var emailTask = _userService.SendVerificationEmail(required.UserAccount, verificationLink);
            
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

    [HttpPut]
    [Route("LoginApple")]
    public async Task<IActionResult> LoginApple([FromBody] LoginApplePacketRequired required)
    {
        var res = new LoginApplePacketResponse();
        var appleBundleId = _configService.GetAppleBundleId();
        if (appleBundleId == string.Empty)
        {
            Console.WriteLine("Apple Bundle ID is not set.");
            res.LoginOk = false;
            return Ok(res);
        }
        
        var sub = await _tokenValidator.ValidateAndExtractAccountFromAppleToken(required.IdToken, appleBundleId);
        if (sub == string.Empty)
        {
            res.LoginOk = false;
            return Ok(res);
        }
        
        var user = _context.User.AsNoTracking().FirstOrDefault(u => u.UserAccount == sub);
        
        // Check if the user exists in the database
        if (user == null)
        {
            await _userService.CreateAccount(sub);
            user = _context.User.AsNoTracking().FirstOrDefault(u => u.UserAccount == sub);
            if (user == null)
            {
                res.LoginOk = false;
                return Ok(res);
            }
        }
        
        var tokens = _tokenService.GenerateTokens(user.UserId);
        res.LoginOk = true;
        res.AccessToken = tokens.AccessToken;
        res.RefreshToken = tokens.RefreshToken;
        
        return Ok(res);
    }
    
    [HttpPut]
    [Route("LoginGoogle")]
    public async Task<IActionResult> LoginGoogle([FromBody] LoginGooglePacketRequired required)
    {
        var res = new LoginGooglePacketResponse();
        var audience = _configService.GetGoogleClientId();
        if (audience == string.Empty)
        {
            Console.WriteLine("Google Client ID is not set.");
            res.LoginOk = false;
            return Ok(res);
        }
        
        var sub = await _tokenValidator.ValidateAndExtractAccountFromGoogleToken(required.IdToken, audience);
        if (sub == string.Empty)
        {
            res.LoginOk = false;
            return Ok(res);
        }
        
        var user = _context.User.AsNoTracking().FirstOrDefault(u => u.UserAccount == sub);

        // Check if the user exists in the database
        if (user == null)
        {
            await _userService.CreateAccount(sub);
            Console.WriteLine("Create Account");
            user = _context.User.AsNoTracking().FirstOrDefault(u => u.UserAccount == sub);
            
            if (user == null)
            {
                res.LoginOk = false;
                return Ok(res);
            }
        }

        var tokens = _tokenService.GenerateTokens(user.UserId);
        res.LoginOk = true;
        res.AccessToken = tokens.AccessToken;
        res.RefreshToken = tokens.RefreshToken;
        
        return Ok(res);
    }
    
    [HttpPost]
    [Route("RefreshToken")]
    public IActionResult RefreshToken([FromBody] RefreshTokenRequired request)
    {
        try
        {
            var tokens = _tokenValidator.RefreshAccessToken(request.RefreshToken);
            var response = new RefreshTokenResponse
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
        
        var user = _context.User.AsNoTracking()
            .FirstOrDefault(user => user.UserId == userId);
        var userStat = _context.UserStats.AsNoTracking()
            .FirstOrDefault(userStat => userStat.UserId == userId);
        var userMatch = _context.UserMatch.AsNoTracking()
            .FirstOrDefault(um => um.UserId == userId);
        var userTutorial = _context.UserTutorial.AsNoTracking()
            .Where(ut => ut.UserId == userId).ToList();
        if (user == null || userStat == null || userMatch == null)
        {
            Console.WriteLine("LoadUserInfo Null Error");
            return NotFound();
        }
        
        var exp = _context.Exp.AsNoTracking()
            .FirstOrDefault(exp => exp.Level == userStat.UserLevel);
        var winRate = userMatch.WinRankMatch + userMatch.LoseRankMatch > 0
            ? (int)(userMatch.WinRankMatch / (float)(userMatch.WinRankMatch + userMatch.LoseRankMatch) * 100) : 0;
        if (exp == null)
        {
            Console.WriteLine("Load Exp Error");
            return NotFound();
        }
            
        res.UserInfo = new UserInfo
        {
            UserName = user.UserName,
            Level = userStat.UserLevel,
            Exp = userStat.Exp,
            ExpToLevelUp = exp.Exp,
            RankPoint = userStat.RankPoint,
            HighestRankPoint = userStat.HighestRankPoint,
            Victories = userMatch.WinRankMatch,
            WinRate = winRate,
            Gold = userStat.Gold,
            Spinel = userStat.Spinel,
            BattleTutorialDone = userTutorial.First(ut => ut.TutorialType == TutorialType.BattleWolf).Done &&
                                 userTutorial.First(ut => ut.TutorialType == TutorialType.BattleSheep).Done,
            CollectionTutorialDone = userTutorial.First(ut => ut.TutorialType == TutorialType.Collection).Done,
            ReinforceTutorialDone = userTutorial.First(ut => ut.TutorialType == TutorialType.Reinforce).Done,
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
        
        var userStat = _context.UserStats.AsNoTracking()
            .FirstOrDefault(userStat => userStat.UserId == required.UserId);
        var userMatch = _context.UserMatch.AsNoTracking()
            .FirstOrDefault(um => um.UserId == required.UserId);
        var userTutorial = _context.UserTutorial.AsNoTracking()
            .Where(ut => ut.UserId == required.UserId).ToList();
        if (userStat == null || userMatch == null)
        {
            Console.WriteLine("LoadUserInfo Null Error");
            return NotFound();
        }
        
        var exp = _context.Exp.AsNoTracking()
            .FirstOrDefault(exp => exp.Level == userStat.UserLevel);
        var winRate = userMatch.WinRankMatch + userMatch.LoseRankMatch > 0
            ? (int)(userMatch.WinRankMatch / (float)(userMatch.WinRankMatch + userMatch.LoseRankMatch) * 100) : 0;
        if (exp == null)
        {
            Console.WriteLine("Load Exp Error");
            return NotFound();
        }
            
        res.UserInfo = new UserInfo
        {
            UserName = user.UserName,
            Level = userStat.UserLevel,
            Exp = userStat.Exp,
            ExpToLevelUp = exp.Exp,
            RankPoint = userStat.RankPoint,
            HighestRankPoint = userStat.HighestRankPoint,
            Victories = userMatch.WinRankMatch,
            WinRate = winRate,
            Gold = userStat.Gold,
            Spinel = userStat.Spinel,
            BattleTutorialDone = userTutorial.First(ut => ut.TutorialType == TutorialType.BattleWolf).Done &&
                                userTutorial.First(ut => ut.TutorialType == TutorialType.BattleSheep).Done,
            CollectionTutorialDone = userTutorial.First(ut => ut.TutorialType == TutorialType.Collection).Done,
            ReinforceTutorialDone = userTutorial.First(ut => ut.TutorialType == TutorialType.Reinforce).Done,
        };
        
        var tokens = _tokenService.GenerateTokens(user.UserId);
        res.AccessToken = tokens.AccessToken;
        res.RefreshToken = tokens.RefreshToken;
        res.LoadTestUserOk = true;
        
        return Ok(res);
    }
    
    [HttpPut]
    [Route("UpdateUserInfo")]
    public async Task<IActionResult> UpdateUserInfo([FromBody] UpdateUserInfoPacketRequired required)
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
        
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                userStat.UserLevel = userInfo.Level;
                userStat.Exp = userInfo.Exp;
                userStat.Gold = userInfo.Gold;
                userStat.Spinel = userInfo.Spinel;
                userStat.RankPoint = userInfo.RankPoint;
                await _context.SaveChangesExtendedAsync();
                res.UpdateUserInfoOk = true;
                await transaction.CommitAsync();
            }
            catch (Exception e)
            {
                res.UpdateUserInfoOk = false;
                await transaction.RollbackAsync();
            }
        });
        
        return Ok(res);
    }

    [HttpPut]
    [Route("UpdateTutorial")]
    public async Task<IActionResult> UpdateTutorial([FromBody] UpdateTutorialRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var res = new UpdateTutorialResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null) return Unauthorized();

        var userTutorial = _context.UserTutorial.FirstOrDefault(ut => ut.UserId == userId);
        if (userTutorial == null) return NotFound();
        
        var strategy = _context.Database.CreateExecutionStrategy();
        
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                userTutorial.Done = required.Done;
                foreach (var tutorialType in required.TutorialTypes)
                {
                    userTutorial.TutorialType = tutorialType;
                }
                await _context.SaveChangesExtendedAsync();
                res.UpdateTutorialOk = true;
                await transaction.CommitAsync();
            }
            catch (Exception e)
            {
                res.UpdateTutorialOk = false;
                await transaction.RollbackAsync();
            }
        });

        return Ok(res);
    }
}
