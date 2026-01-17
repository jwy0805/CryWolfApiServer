using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ApiServer.DB;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Services;

/// <summary>
/// Refresh Tokens are stored in DB per user and client type. - web, mobile
/// </summary>
public class TokenService
{
    private readonly string _secret;
    private readonly TimeSpan _accessTokenLifetime;
    private readonly TimeSpan _refreshTokenLifetime;
    private readonly AppDbContext _context;

    public TokenService(string secret, AppDbContext dbContext)
    {
        _secret = secret;
        _accessTokenLifetime = TimeSpan.FromMinutes(60);
        _refreshTokenLifetime = TimeSpan.FromDays(90);
        _context = dbContext;
    }

    public async Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(int userId, ClientType clientType)
    {
        var accessToken = GenerateAccessToken(userId);
        var refreshToken = GenerateRefreshToken();

        await SaveRefreshTokenByClientTypeAsync(userId, clientType, refreshToken);

        return (accessToken, refreshToken);
    }

    private async Task SaveRefreshTokenByClientTypeAsync(int userId, ClientType clientType, string refreshToken)
    {
        var nowUtc = DateTime.UtcNow;
        var hashedToken = HashToken(refreshToken);
        var expiresAt = nowUtc.Add(_refreshTokenLifetime);

        // This UPSERT relies on a UNIQUE KEY on (UserId, ClientType).
        await _context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO RefreshToken
    (UserId, ClientType, Token, ExpiresAt, CreatedAt, UpdateAt, RevokedAt)
VALUES
    ({userId}, {(byte)clientType}, {hashedToken}, {expiresAt}, {nowUtc}, {nowUtc}, NULL)
ON DUPLICATE KEY UPDATE
    Token = VALUES(Token),
    ExpiresAt = VALUES(ExpiresAt),
    UpdateAt = VALUES(UpdateAt),
    RevokedAt = NULL;");
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