using System.Text.RegularExpressions;
using ApiServer.DB;
using ApiServer.Providers;
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
    private readonly CachedDataProvider _cachedDataProvider;
    private readonly ILogger<UserAccountController> _logger;
    private readonly IConfiguration _config;
    
    public UserAccountController(
        AppDbContext context, 
        UserService userService, 
        TokenService tokenService, 
        TokenValidator validator,
        CachedDataProvider cachedDataProvider,
        ILogger<UserAccountController> logger,
        IConfiguration config)
    {
        _context = context;
        _userService = userService;
        _tokenService = tokenService;
        _tokenValidator = validator;
        _cachedDataProvider = cachedDataProvider;
        _logger = logger;
        _config = config;
    }
    
    /// <summary>
    /// Error Code 1 -> Account Already Exists.
    /// Error Code 2 -> Password Invalid
    /// </summary>
    [HttpPost]
    [Route("ValidateNewAccount")]
    public async Task<IActionResult> ValidateNewAccount([FromBody] ValidateNewAccountPacketRequired required)
    {
        const string allowedSpecialCharacters = "!@#$%^&*()-_=+[]{};:,.?";
        var approved = false;
        var res = new ValidateNewAccountPacketResponse();
        var tempUserDb = _context.TempUser;
        var account = _context.UserAuth
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
                "Prod" => $"https://hamonstudio.net/verify/{token}",
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
        var userAuth = _context.UserAuth
            .AsNoTracking()
            .FirstOrDefault(u => u.UserAccount == required.UserAccount);

        if (userAuth == null)
        {
            res.LoginOk = false;
        }
        else
        {
            var user = _context.User.FirstOrDefault(u => u.UserId == userAuth.UserId);
            if (user == null)
            {
                res.LoginOk = false;
            }
            else
            {
                if (string.IsNullOrEmpty(userAuth.PasswordHash))
                {
                    res.LoginOk = false;
                    return res;
                }
                
                if (_tokenValidator.VerifyPassword(required.Password, userAuth.PasswordHash) == false)
                {
                    res.LoginOk = false;
                    return res;
                }
            
                var tokens = _tokenService.GenerateTokens(userAuth.UserId);
                res.AccessToken = tokens.AccessToken;
                res.RefreshToken = tokens.RefreshToken;
                res.LoginOk = true;
                user.State = UserState.Activate;
                user.Act = UserAct.InLobby;
                _context.SaveChangesExtended();
            }
        }
        
        return res;
    }
    
    [HttpPost]
    [Route("LoginApple")]
    public async Task<IActionResult> LoginApple([FromBody] LoginApplePacketRequired required)
    {
        var res = new LoginApplePacketResponse();
        var appleBundleId = _config["BundleId"];
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
        
        var userAuth = _context.UserAuth.FirstOrDefault(u => u.UserAccount == sub);
        
        // Check if the user exists in the database
        if (userAuth == null)
        {
            await _userService.CreateAccount(sub, AuthProvider.Apple);
            userAuth = _context.UserAuth.FirstOrDefault(u => u.UserAccount == sub);
            if (userAuth == null)
            {
                res.LoginOk = false;
                return Ok(res);
            }
        }
        
        var tokens = _tokenService.GenerateTokens(userAuth.UserId);
        res.LoginOk = true;
        res.AccessToken = tokens.AccessToken;
        res.RefreshToken = tokens.RefreshToken;
        
        var user = _context.User.FirstOrDefault(u => u.UserId == userAuth.UserId);
        if (user == null)
        {
            res.LoginOk = false;
            return Ok(res);
        }
        
        user.State = UserState.Activate;
        user.Act = UserAct.InLobby;
        await _context.SaveChangesExtendedAsync();
        
        return Ok(res);
    }
    
    [HttpPost]
    [Route("LoginGoogle")]
    public async Task<IActionResult> LoginGoogle([FromBody] LoginGooglePacketRequired required)
    {
        var res = new LoginGooglePacketResponse();
        var audience = _config["Google:ClientId"];
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
        
        var userAuth = _context.UserAuth.FirstOrDefault(u => u.UserAccount == sub);

        // Check if the user exists in the database
        if (userAuth == null)
        {
            await _userService.CreateAccount(sub, AuthProvider.Google);
            Console.WriteLine("Create Account");
            userAuth = _context.UserAuth.FirstOrDefault(u => u.UserAccount == sub);
            
            if (userAuth == null)
            {
                res.LoginOk = false;
                return Ok(res);
            }
        }

        var tokens = _tokenService.GenerateTokens(userAuth.UserId);
        res.LoginOk = true;
        res.AccessToken = tokens.AccessToken;
        res.RefreshToken = tokens.RefreshToken;
        
        var user = _context.User.FirstOrDefault(u => u.UserId == userAuth.UserId);
        if (user == null)
        {
            res.LoginOk = false;
            return Ok(res);
        }
        
        user.State = UserState.Activate;
        user.Act = UserAct.InLobby;
        await _context.SaveChangesExtendedAsync();
        
        return Ok(res);
    }

    [HttpPost]
    [Route("LoginGuest")]
    public async Task<IActionResult> LoginGuest([FromBody] LoginGuestPacketRequired required)
    {
        var res = new LoginGuestPacketResponse();
        var user = _context.UserAuth.AsNoTracking().FirstOrDefault(u => u.UserAccount == required.GuestId);
        
        if (user == null)
        {
            await _userService.CreateAccount(required.GuestId, AuthProvider.Guest);
            user = _context.UserAuth.AsNoTracking().FirstOrDefault(u => u.UserAccount == required.GuestId);
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
    [Route("LoginFromWeb")]
    public IActionResult LoginFromWeb([FromBody] LoginUserAccountPacketRequired required)
    {
        var userAuth = _context.UserAuth
            .AsNoTracking()
            .FirstOrDefault(u => u.UserAccount == required.UserAccount);

        if (userAuth == null)
            return Unauthorized(new { message = "Invalid email or password" });

        var user = _context.User
            .AsNoTracking()
            .FirstOrDefault(u => u.UserId == userAuth.UserId);

        if (user == null)
            return Unauthorized(new { message = "Invalid email or password" });

        if (string.IsNullOrEmpty(userAuth.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password" });

        if (_tokenValidator.VerifyPassword(required.Password, userAuth.PasswordHash) == false)
            return Unauthorized(new { message = "Invalid email or password" });

        var tokens = _tokenService.GenerateTokens(userAuth.UserId);

        SetAuthCookies(tokens.AccessToken, tokens.RefreshToken);
        
        return Ok(new { success = true, userName = user.UserName, userTag = user.UserTag, userRole = user.Role });
    }
    
    [HttpPost]
    [Route("LoginGoogleFromWeb")]
    public async Task<IActionResult> LoginGoogleFromWeb([FromBody] LoginGooglePacketRequired required)
    {
        var res = new LoginGooglePacketResponse();

        var audience = _config["Google:Web:ClientId"]; // Web용 Client ID
        if (string.IsNullOrEmpty(audience))
        {
            Console.WriteLine("Google Client ID is not set.");
            res.LoginOk = false;
            return Ok(new { success = false });
        }

        var sub = await _tokenValidator.ValidateAndExtractAccountFromGoogleToken(required.IdToken, audience);
        if (string.IsNullOrEmpty(sub))
            return Ok(new { success = false });

        var userAuth = _context.UserAuth.FirstOrDefault(u => u.UserAccount == sub);
        if (userAuth == null)
        {
            await _userService.CreateAccount(sub, AuthProvider.Google);
            userAuth = _context.UserAuth.FirstOrDefault(u => u.UserAccount == sub);
            if (userAuth == null)
                return Ok(new { success = false });
        }

        var user = _context.User
            .AsNoTracking()
            .FirstOrDefault(u => u.UserId == userAuth.UserId);

        if (user == null)
            return Unauthorized(new { message = "Invalid email or password" });
        
        var tokens = _tokenService.GenerateTokens(userAuth.UserId);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        };

        Response.Cookies.Append("access_token", tokens.AccessToken, cookieOptions);
        Response.Cookies.Append("refresh_token", tokens.RefreshToken, cookieOptions);

        return Ok(new { success = true, userName = user.UserName, userTag = user.UserTag, userRole = user.Role });
    }
    
    [HttpPost]
    [Route("LoginAppleFromWeb")]
    public async Task<IActionResult> LoginAppleFromWeb([FromBody] LoginApplePacketRequired required)
    {
        var res = new LoginGooglePacketResponse();

        var audience = _config["Google:ClientId"]; // Web용 Client ID
        if (string.IsNullOrEmpty(audience))
        {
            Console.WriteLine("Google Client ID is not set.");
            res.LoginOk = false;
            return Ok(new { success = false });
        }

        var sub = await _tokenValidator.ValidateAndExtractAccountFromAppleToken(required.IdToken, audience);
        if (string.IsNullOrEmpty(sub))
            return Ok(new { success = false });

        var userAuth = _context.UserAuth.FirstOrDefault(u => u.UserAccount == sub);
        if (userAuth == null)
        {
            await _userService.CreateAccount(sub, AuthProvider.Google);
            userAuth = _context.UserAuth.FirstOrDefault(u => u.UserAccount == sub);
            if (userAuth == null)
                return Ok(new { success = false });
        }
        
        var user = _context.User
            .AsNoTracking()
            .FirstOrDefault(u => u.UserId == userAuth.UserId);

        if (user == null)
            return Unauthorized(new { message = "Invalid email or password" });

        var tokens = _tokenService.GenerateTokens(userAuth.UserId);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        };

        Response.Cookies.Append("access_token", tokens.AccessToken, cookieOptions);
        Response.Cookies.Append("refresh_token", tokens.RefreshToken, cookieOptions);

        return Ok(new { success = true, userName = user.UserName, userTag = user.UserTag, userRole = user.Role });
    }
    
    private void SetAuthCookies(string accessToken, string refreshToken)
    {
        var accessCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,                 
            SameSite = SameSiteMode.Lax,
            Expires  = DateTimeOffset.UtcNow.AddMinutes(60)
        };
        Response.Cookies.Append("access_token", accessToken, accessCookieOptions);

        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.Lax,
            Expires  = DateTimeOffset.UtcNow.AddHours(24)
        };
        Response.Cookies.Append("refresh_token", refreshToken, refreshCookieOptions);
    }

    private void ClearAuthCookies()
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.Lax,
            Path     = "/"
        };
        
        Response.Cookies.Delete("access_token", options);
        Response.Cookies.Delete("refresh_token", options);
    }

    [HttpGet]
    [Route("KeepInfoFromWeb")]
    public IActionResult MeFromWeb()
    {
        const string accessCookieName = "access_token";

        if (!Request.Cookies.TryGetValue(accessCookieName, out var accessToken) ||
            string.IsNullOrWhiteSpace(accessToken))
        {
            // 프론트에서 callApiWithRefresh가 이 401을 보고 refresh 시도함
            return Unauthorized(new { message = "No access token" });
        }

        var principal = _tokenValidator.ValidateToken(accessToken);
        if (principal == null)
        {
            return Unauthorized(new { message = "Invalid or expired token" });
        }

        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null)
        {
            return Unauthorized(new { message = "Invalid token payload" });
        }

        var user = _context.User
            .AsNoTracking()
            .FirstOrDefault(u => u.UserId == userId.Value);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        return Ok(new
        {
            success = true,
            userName = user.UserName,
            userTag = user.UserTag,
            userRole = user.Role
        });
    }
    
    [HttpPost]
    [Route("PolicyAgreed")]
    public async Task<IActionResult> PolicyAgreed([FromBody] PolicyAgreedPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var res = new PolicyAgreedPacketResponse();
        var userIdNull = _tokenValidator.GetUserIdFromAccessToken(principal);
        var userId = userIdNull ?? 0;
        var userAuth = _context.UserAuth.FirstOrDefault(ua => ua.UserId == userId);
        if (userAuth == null)
        {
            Console.WriteLine("User Not Found");
            return NotFound();
        }

        userAuth.PolicyAgreed = true;
        res.PolicyAgreedOk = true;
        
        await _context.SaveChangesAsync();
        
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
    [Route("RefreshTokenFromWeb")]
    public IActionResult RefreshTokenFromWeb()
    {
        if (!Request.Cookies.TryGetValue("refresh_token", out var refreshToken) ||
            string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogInformation("RefreshFromWeb: No refresh token cookie found.");
            ClearAuthCookies();
            return Unauthorized(new { message = "No Refresh Token " });
        }

        try
        {
            var tokens = _tokenValidator.RefreshAccessToken(refreshToken);
            SetAuthCookies(tokens.AccessToken, tokens.RefreshToken);
            return Ok(new { success = true });
        }
        catch (SecurityTokenException ex)
        {
            // 유효하지 않거나 만료된 리프레시 토큰
            _logger.LogWarning(ex, "RefreshFromWeb: invalid refresh token");
            ClearAuthCookies();
            return Unauthorized(new { message = "Invalid refresh token" });
        }
        catch (Exception ex)
        {
            // 예기치 않은 오류
            _logger.LogError(ex, "RefreshFromWeb: unexpected error");
            ClearAuthCookies();
            return StatusCode(500, new { message = "Refresh failed" });
        }
    }
    
    [HttpPost]
    [Route("LoadUserInfo")]
    public IActionResult LoadUserInfo([FromBody] LoadUserInfoPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null) return Unauthorized();
        
        var user = _context.User.AsNoTracking()
            .FirstOrDefault(user => user.UserId == userId);
        var userAuth = _context.UserAuth.AsNoTracking()
            .FirstOrDefault(userAuth => userAuth.UserId == userId);
        var userStat = _context.UserStats.AsNoTracking()
            .FirstOrDefault(userStat => userStat.UserId == userId);
        var userMatch = _context.UserMatch.AsNoTracking()
            .FirstOrDefault(um => um.UserId == userId);
        var userTutorial = _context.UserTutorial.AsNoTracking()
            .Where(ut => ut.UserId == userId).ToList();
        if (user == null || userAuth == null || userStat == null || userMatch == null)
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
            
        var userSubscription = _context.UserSubscription.AsNoTracking()
            .Where(userSubscription => userSubscription.UserId == userId);
        var subscriptionInfos = new List<SubscriptionInfo>();
        if (userSubscription.Any() == false)
        {
            subscriptionInfos = userSubscription
                .Where(us => us.ExpiresAtUtc > DateTime.UtcNow)
                .Select(us => new SubscriptionInfo
                {
                    SubscriptionType = us.SubscriptionType,
                    ExpiresAt = us.ExpiresAtUtc,
                    StartAt = us.CreatedAtUtc,
                }).ToList();
        }
        
        var res = new LoadUserInfoPacketResponse
        {
            UserInfo = new UserInfo
            {
                UserAccount = userAuth.UserAccount,
                UserName = user.UserName,
                UserTag = user.UserTag,
                UserRole = user.Role,
                Level = userStat.UserLevel,
                Exp = userStat.Exp,
                ExpToLevelUp = exp.Exp,
                RankPoint = userStat.RankPoint,
                HighestRankPoint = userStat.HighestRankPoint,
                Victories = userMatch.WinRankMatch,
                WinRate = winRate,
                Gold = userStat.Gold,
                Spinel = userStat.Spinel,
                NameInitialized = user.NameInitialized,
                Subscriptions = subscriptionInfos,
            },
            UserTutorialInfo = new UserTutorialInfo
            {
                WolfTutorialDone = userTutorial.First(ut => ut.TutorialType == TutorialType.BattleWolf).Done,
                SheepTutorialDone = userTutorial.First(ut => ut.TutorialType == TutorialType.BattleSheep).Done,
                ChangeFactionTutorialDone = userTutorial.First(ut => ut.TutorialType == TutorialType.ChangeFaction).Done,
                CollectionTutorialDone = userTutorial.First(ut => ut.TutorialType == TutorialType.Collection).Done,
                ReinforceTutorialDone = userTutorial.First(ut => ut.TutorialType == TutorialType.Reinforce).Done,
            },
            ExpTable = _cachedDataProvider.GetExpSnapshots(),
            LoadUserInfoOk = true
        };

        return Ok(res);
    }

    [HttpGet]
    [Route("LoadUserInfoFromWeb")]
    public IActionResult LoadUserInfoFromWeb()
    {
        var accessToken = Request.Cookies["access_token"];
        if (string.IsNullOrEmpty(accessToken)) return Unauthorized();

        var userId = _tokenValidator.Authorize(accessToken);
        if (userId <= 0) return Unauthorized();

        var user = _context.User
            .AsNoTracking()
            .FirstOrDefault(u => u.UserId == userId);
        if (user == null) return NotFound();

        var res = new LoadUserInfoPacketResponse
        {
            UserInfo = new UserInfo
            {
                UserName = user.UserName,
                UserTag = user.UserTag,
                UserRole = user.Role
            }
        };

        return Ok(res);
    }
    
    [HttpPost]
    [Route("LoadTestUser")]
    public IActionResult LoadTestUser([FromBody] LoadTestUserPacketRequired required)
    {
        var res = new LoadTestUserPacketResponse();
        var userId = required.UserId;
        
        var user = _context.User.AsNoTracking()
            .FirstOrDefault(user => user.UserId == userId);
        var userAuth = _context.UserAuth.AsNoTracking()
            .FirstOrDefault(userAuth => userAuth.UserId == userId);
        var userStat = _context.UserStats.AsNoTracking()
            .FirstOrDefault(userStat => userStat.UserId == userId);
        var userMatch = _context.UserMatch.AsNoTracking()
            .FirstOrDefault(um => um.UserId == userId);
        var userTutorial = _context.UserTutorial.AsNoTracking()
            .Where(ut => ut.UserId == userId).ToList();
        if (user == null || userAuth == null || userStat == null || userMatch == null)
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
        
        var userSubscription = _context.UserSubscription.AsNoTracking()
            .Where(userSubscription => userSubscription.UserId == userId);
        var subscriptionInfos = new List<SubscriptionInfo>();
        if (userSubscription.Any() == false)
        {
            subscriptionInfos = userSubscription
                .Where(us => us.ExpiresAtUtc > DateTime.UtcNow)
                .Select(us => new SubscriptionInfo
                {
                    SubscriptionType = us.SubscriptionType,
                    ExpiresAt = us.ExpiresAtUtc,
                    StartAt = us.CreatedAtUtc,
                }).ToList();
        }
            
        res.UserInfo = new UserInfo
        {
            UserAccount = userAuth.UserAccount,
            UserName = user.UserName,
            UserTag = user.UserTag,
            UserRole = user.Role,
            Level = userStat.UserLevel,
            Exp = userStat.Exp,
            ExpToLevelUp = exp.Exp,
            RankPoint = userStat.RankPoint,
            HighestRankPoint = userStat.HighestRankPoint,
            Victories = userMatch.WinRankMatch,
            WinRate = winRate,
            Gold = userStat.Gold,
            Spinel = userStat.Spinel,
            NameInitialized = user.NameInitialized,
            Subscriptions = subscriptionInfos,
        };
        
        res.UserTutorialInfo = new UserTutorialInfo
        {
            WolfTutorialDone = userTutorial.First(ut => ut.TutorialType == TutorialType.BattleWolf).Done,
            SheepTutorialDone = userTutorial.First(ut => ut.TutorialType == TutorialType.BattleSheep).Done,
            ChangeFactionTutorialDone = userTutorial.First(ut => ut.TutorialType == TutorialType.ChangeFaction).Done,
            CollectionTutorialDone = userTutorial.First(ut => ut.TutorialType == TutorialType.Collection).Done,
            ReinforceTutorialDone = userTutorial.First(ut => ut.TutorialType == TutorialType.Reinforce).Done,
        };
        
        var tokens = _tokenService.GenerateTokens(user.UserId);
        res.AccessToken = tokens.AccessToken;
        res.RefreshToken = tokens.RefreshToken;
        res.ExpTable = _cachedDataProvider.GetExpSnapshots();
        res.LoadTestUserOk = true;
        
        return Ok(res);
    }

    /// <summary>
    /// ErrorCode 1 -> UserName Already Exists / 
    /// ErrorCode 2 -> Invalid UserName
    /// </summary>
    [HttpPut]
    [Route("UpdateUsername")]
    public async Task<IActionResult> UpdateUsername([FromBody] UpdateNamePacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var userIdN = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userIdN == null) return Unauthorized();  
        
        var userId = userIdN.Value;
        var res = new UpdateNamePacketResponse();
        var user = await _context.User.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return NotFound();
        
        var userNameExists = await _context.User.AsNoTracking().AnyAsync(u => u.UserName == required.NewName);
        if (userNameExists)
        {
            res.ChangeNameOk = false;
            res.ErrorCode = 1;
            return Ok(res);
        }

        if (Util.Util.IsValidUsername(required.NewName) == false)
        {
            res.ChangeNameOk = false;
            res.ErrorCode = 2;
            return Ok(res);
        }
        
        user.UserName = required.NewName;
        user.NameInitialized = true;
        await _context.SaveChangesExtendedAsync();
        
        res.ChangeNameOk = true;
        res.ErrorCode = 0;
        
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

        var userTutorial = _context.UserTutorial.Where(ut => ut.UserId == userId).ToList();
        if (userTutorial.Any() == false) return NotFound();
        
        var strategy = _context.Database.CreateExecutionStrategy();
        
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var type in required.TutorialTypes)
                {
                    var tutorial = userTutorial.FirstOrDefault(ut => ut.TutorialType == type);
                    if (tutorial != null)
                    {
                        tutorial.Done = required.Done;
                    }
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

    [HttpDelete]
    [Route("Logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var userIdNull = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userIdNull == null) return Unauthorized();
        
        var userId = userIdNull.Value;
        var res = new LogoutPacketResponse();
        var user = _context.User.FirstOrDefault(u => u.UserId == userId);
        if (user == null) return NotFound();

        var refreshTokens = _context.RefreshTokens.Where(rt => rt.UserId == userId);
        
        _context.RefreshTokens.RemoveRange(refreshTokens);
        user.Act = UserAct.Offline;
        
        await _context.SaveChangesExtendedAsync();
        res.LogoutOk = true;
        
        return Ok(res);
    }

    [HttpPost]
    [Route("LogoutFromWeb")]
    public async Task<IActionResult> LogoutFromWeb()
    {
        string? accessToken = null;
        string? refreshToken = null;
        
        if (Request.Cookies.TryGetValue("access_token", out var at))
        {
            accessToken = at;
        }

        if (Request.Cookies.TryGetValue("refresh_token", out var rt))
        {
            refreshToken = rt;
        }

        int? userId = null;

        // Extract user ID from access token if available
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            var principal = _tokenValidator.ValidateToken(accessToken);
            if (principal != null)
            {
                userId = _tokenValidator.GetUserIdFromAccessToken(principal);
            }
        }
        
        // Remove refresh tokens from database
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var hashed = _tokenService.HashToken(refreshToken);
            var query = _context.RefreshTokens.Where(rtEntity => rtEntity.Token == hashed);

            if (userId.HasValue)
            {
                query = query.Where(rtEntity => rtEntity.UserId == userId.Value);
            }

            var tokens = await query.ToListAsync();
            if (tokens.Count > 0)
            {
                _context.RefreshTokens.RemoveRange(tokens);
            }
        }
        
        // Update user status to offline
        if (userId.HasValue)
        {
            var user = await _context.User.FirstOrDefaultAsync(u => u.UserId == userId.Value);

            if (user != null)
            {
                user.Act = UserAct.Offline;
            }
        }

        await _context.SaveChangesExtendedAsync();
        
        ClearAuthCookies();

        return Ok(new { success = true });
    }
    
    [HttpDelete]
    [Route("DeleteAccount")]
    public async Task<IActionResult> DeleteAccountHard([FromBody] DeleteUserAccountPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var userIdNull = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userIdNull == null) return Unauthorized();
        
        var userId = userIdNull.Value;
        var user = _context.User.AsNoTracking().FirstOrDefault(u => u.UserId == userId);
        if (user == null) return NotFound();
        
        var res = new DeleteUserAccountPacketResponse();
        
        _context.User.Remove(user);
        await _context.SaveChangesAsync();
        
        res.DeleteOk = true;
        
        return Ok(res);
    }
    
    [HttpDelete]
    [Route("DeleteAccountHard")]
    public async Task<IActionResult> DeleteAccountHard([FromBody] DeleteUserAccountHardPacketRequired required)
    {
        if (required.AdminPassword != _config["AdminPassword"])
        {
            return Unauthorized(new { message = "Invalid Request." });
        }
        
        var user = await _context.User.FirstOrDefaultAsync(u => u.UserId == required.UserId);
        if (user == null) return NotFound();
        
        var res = new DeleteUserAccountHardPacketResponse();

        _context.User.Remove(user);
        await _context.SaveChangesAsync();
        
        res.DeleteOk = true;

        return Ok(res);
    }
}
