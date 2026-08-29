using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;
using OshikatsuApi.Settings;

namespace OshikatsuApi.Services;

/// <inheritdoc />
public class TokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;
    private readonly AppDbContext _db;

    public TokenService(IOptions<JwtSettings> jwtOptions, AppDbContext db)
    {
        _jwtSettings = jwtOptions.Value;
        _db = db;
    }

    /// <inheritdoc />
    public (string Token, string Jti, DateTime ExpiresAt) GenerateToken(int userId, string email)
    {
        var jti = Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
        };

        var tokenDescriptor = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        return (tokenString, jti, expiresAt);
    }

    /// <inheritdoc />
    public async Task RevokeTokenAsync(string jti, DateTime expiresAt)
    {
        _db.RevokedTokens.Add(new RevokedToken
        {
            Jti = jti,
            RevokedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
        });
        await _db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<bool> IsTokenRevokedAsync(string jti)
    {
        return await _db.RevokedTokens
            .AnyAsync(t => t.Jti == jti && t.ExpiresAt > DateTime.UtcNow);
    }
}
