using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ApiServer.DB;
using ApiServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ApiServer.Services;

public class TokenValidator
{
    private readonly string _secret;
    private readonly AppDbContext _dbContext;
    private readonly TokenService _tokenService;
    private readonly ILogger<TokenValidator> _logger;
    
    private static readonly ConfigurationManager<OpenIdConnectConfiguration> ConfigManagerGoogle = 
        new("https://accounts.google.com/.well-known/openid-configuration", 
            new OpenIdConnectConfigurationRetriever()
        );
    
    private static readonly ConfigurationManager<OpenIdConnectConfiguration> ConfigManagerApple = 
        new("https://appleid.apple.com/.well-known/openid-configuration", 
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever()
        );

    public TokenValidator(string secret, AppDbContext dbContext, TokenService tokenService, ILogger<TokenValidator> logger)
    {
        _secret = secret;
        _dbContext = dbContext;
        _tokenService = tokenService;
        _logger = logger;
    }

    public int Authorize(string accessToken)
    {
        var principal = ValidateToken(accessToken);
        if (principal == null) return -1; // Unauthorized
        
        var userId = GetUserIdFromAccessToken(principal);
        
        return userId ?? -1; // Return user ID or -1 if not found
    }
    
    public int? GetUserIdFromAccessToken(ClaimsPrincipal principal)
    {
        var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        if (userIdClaim == null) return null;
        return int.Parse(userIdClaim.Value);
    }
    
    public string GetEmailFromToken(ClaimsPrincipal principal)
    {
        var emailClaim = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email);
        return emailClaim?.Value ?? string.Empty;
    }

    /// <summary>
    /// Extract the "sub" claim(user's unique identifier) after validating the Apple ID token.
    /// </summary>
    /// <param name="tokenId">Google ID Token from client</param>
    /// <param name="validClientId">Valid Client Id (typically, Apple Client ID)</param>
    /// <returns>"sub" Value when validated successfully, or null</returns>
    public async Task<string> ValidateAndExtractAccountFromAppleToken(string tokenId, string validClientId)
    {
        try
        {
            // Get OpenIdConnectConfiguration
            var openIdConfig = await ConfigManagerApple.GetConfigurationAsync(CancellationToken.None);
            
            // Set Token Validation Parameters
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = new[] { "https://appleid.apple.com" },
                ValidateAudience = true,
                ValidAudience = validClientId, // My Apple Client ID
                ValidateLifetime = true,         // Validate token expiration
                IssuerSigningKeys = openIdConfig.SigningKeys  // The list of public keys from Apple
            };

            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(tokenId, validationParameters, out var validatedToken);
            
            var jwtToken = validatedToken as JwtSecurityToken;
            var subClaim = jwtToken?.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
            
            return subClaim?.Value ?? string.Empty;
        }
        catch (Exception e)
        {
            _logger.LogWarning("Apple token validation failed: " + e.Message);
            throw;
        }
    }

    /// <summary>
    /// Extract the "sub" claim(user's unique identifier) after validating the Google ID token.
    /// </summary>
    /// <param name="tokenId">Google ID Token from client</param>
    /// <param name="validAudience">Valid Audience Value (typically, Google Client ID)</param>
    /// <returns>"sub" Value when validated successfully, or null</returns>
    public async Task<string> ValidateAndExtractAccountFromGoogleToken(string tokenId, string validAudience)
    {
        // Get Google's open ID config public keys
        var openIdConfig = await ConfigManagerGoogle.GetConfigurationAsync(CancellationToken.None);

        // Set Token Validation Parameters
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            // Possible issuers are "accounts.google.com" or "https://accounts.google.com"
            ValidIssuers = new[] { "accounts.google.com", "https://accounts.google.com" },
            ValidateAudience = true,
            ValidAudience = validAudience, // My Google Client ID
            ValidateLifetime = true,         // Validate token expiration
            IssuerSigningKeys = openIdConfig.SigningKeys  // The list of public keys from Google
        };
        
        var handler = new JwtSecurityTokenHandler();

        try
        {
            handler.ValidateToken(tokenId, validationParameters, out var validatedToken);
            var jwtToken = validatedToken as JwtSecurityToken;
            var subClaim = jwtToken?.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
            return subClaim?.Value ?? string.Empty;
        }
        catch (Exception e)
        {
            _logger.LogWarning("Token validation failed: " + e.Message);
            return string.Empty;
        }
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
            }, out var validatedToken);

            if (validatedToken.ValidTo < DateTime.UtcNow)
            {
                throw new SecurityTokenExpiredException("Token has expired"); // null 반환
            }
            
            foreach (var claim in principal.Claims)
            {
                _logger.LogInformation($"Validate / Type: {claim.Type}, Value: {claim.Value}");
            }
            
            return principal;
        }
        catch (Exception e)
        {
            _logger.LogWarning("Token validation failed: " + e.Message);
            return null;
        }
    }
    
    private int? ValidateRefreshToken(string refreshToken)
    {
        var hashedToken = _tokenService.HashToken(refreshToken);
        var storedToken = _dbContext.RefreshToken.SingleOrDefault(rt => rt.Token == hashedToken);
        if (storedToken == null || storedToken.ExpiresAt < DateTime.UtcNow) return null;
        
        return storedToken.UserId;
    }
    
    public async Task<(string AccessToken, string RefreshToken)> RefreshAccessToken(
        string refreshToken, ClientType clientType)
    {
        var userId = ValidateRefreshToken(refreshToken);
        if (userId == null)
        {
            throw new SecurityTokenException("Invalid refresh token");
        }

        return await _tokenService.GenerateTokensAsync(userId.Value, clientType);
    }
    
    public bool VerifyPassword(string password, string hashedPassword)
    {
        return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
    }
}