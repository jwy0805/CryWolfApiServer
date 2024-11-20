using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ApiServer.DB;
using ApiServer.Services;
using Microsoft.IdentityModel.Tokens;

namespace ApiServer.Services;

public class TokenValidator
{
    private readonly string _secret;
    private readonly AppDbContext _dbContext;
    private readonly TokenService _tokenService;

    public TokenValidator(string secret, AppDbContext dbContext, TokenService tokenService)
    {
        _secret = secret;
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    public int? GetUserIdFromAccessToken(ClaimsPrincipal principal)
    {
        // foreach (var claim in principal.Claims)
        // {
        //     Console.WriteLine($"GetUserId / Type: {claim.Type}, Value: {claim.Value}");
        // }
        var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        if (userIdClaim == null) return null;
        return int.Parse(userIdClaim.Value);
    }
    
    public string GetEmailFromToken(ClaimsPrincipal principal)
    {
        var emailClaim = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email);
        return emailClaim?.Value ?? string.Empty;
    }
    
    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secret);

        try
        {
            tokenHandler.InboundClaimTypeMap.Clear();
            
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = "CryWolf",
                ValidAudience = "CryWolf",
                IssuerSigningKey = new SymmetricSecurityKey(key),
                NameClaimType = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Sub,
                RoleClaimType = ClaimTypes.Role
            }, out SecurityToken validatedToken);

            if (validatedToken.ValidTo < DateTime.UtcNow)
            {
                throw new SecurityTokenExpiredException("Token has expired"); // null 반환
            }
            
            foreach (var claim in principal.Claims)
            {
                Console.WriteLine($"Validate / Type: {claim.Type}, Value: {claim.Value}");
            }
            
            return principal;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }
    
    public int? ValidateRefreshToken(string refreshToken)
    {
        var hashedToken = _tokenService.HashToken(refreshToken);
        var storedToken = _dbContext.RefreshTokens.SingleOrDefault(rt => rt.Token == hashedToken);
        if (storedToken == null || storedToken.ExpiresAt < DateTime.UtcNow) return null;
        
        return storedToken.UserId;
    }
    
    public (string AccessToken, string RefreshToken) RefreshAccessToken(string refreshToken)
    {
        var userId = ValidateRefreshToken(refreshToken);
        if (userId == null)
        {
            throw new SecurityTokenException("Invalid refresh token");
        }

        return _tokenService.GenerateTokens(userId.Value);
    }
    
    public bool VerifyPassword(string password, string hashedPassword)
    {
        return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
    }
}