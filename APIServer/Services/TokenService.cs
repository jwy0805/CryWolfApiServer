using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ApiServer.DB;
using ApiServer.DB;
using Microsoft.IdentityModel.Tokens;

namespace ApiServer.Services;

public class TokenService
{
    private readonly string _secret;
    private readonly TimeSpan _accessTokenLifetime;
    private readonly TimeSpan _refreshTokenLifetime;
    private readonly AppDbContext _dbContext;

    public TokenService(string secret, AppDbContext dbContext)
    {
        _secret = secret;
        _accessTokenLifetime = TimeSpan.FromMinutes(60);
        _refreshTokenLifetime = TimeSpan.FromHours(24);
        _dbContext = dbContext;
    }

    public (string AccessToken, string RefreshToken) GenerateTokens(int userId)
    {
        var accessToken = GenerateAccessToken(userId);
        var refreshToken = GenerateRefreshToken();
        SaveRefreshToken(userId, refreshToken);
        
        return (accessToken, refreshToken);
    }

    private string GenerateAccessToken(int userId)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var claim in claims)
        {
            Console.WriteLine($"GenerateAccessToken / Type: {claim.Type}, Value: {claim.Value}");
        }
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "CryWolf",
            audience: "CryWolf",
            claims: claims,
            expires: DateTime.UtcNow.Add(_accessTokenLifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateEmailVerificationToken(string email)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var token = new JwtSecurityToken(
            issuer: "CryWolf",
            audience: "CryWolf",
            claims: claims,
            expires: DateTime.UtcNow.Add(_accessTokenLifetime),
            signingCredentials: credentials);
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    
    private string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        using var randomNumberGenerator = RandomNumberGenerator.Create();
        randomNumberGenerator.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private void SaveRefreshToken(int userId, string refreshToken)
    {
        var hashedToken = HashToken(refreshToken);
        var refreshTokenEntity = new RefreshToken()
        {
            UserId = userId,
            Token = hashedToken,
            ExpiresAt = DateTime.UtcNow.Add(_refreshTokenLifetime),
            CreatedAt = DateTime.UtcNow,
            UpdateAt = DateTime.UtcNow
        };

        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        _dbContext.SaveChanges();
    }

    public string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }
    
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }
}