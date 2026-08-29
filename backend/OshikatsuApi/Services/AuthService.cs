using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.DTOs;
using OshikatsuApi.Entities;
using OshikatsuApi.Exceptions;

namespace OshikatsuApi.Services;

/// <inheritdoc />
public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;

    public AuthService(AppDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    /// <inheritdoc />
    public async Task<AuthResponseDto> RegisterAsync(string email, string password)
    {
        // メールアドレスの重複チェック（大文字小文字を区別しない）
        var exists = await _db.Users
            .AnyAsync(u => u.Email.ToLower() == email.ToLower());

        if (exists)
        {
            throw new DuplicateEmailException();
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Email = email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var (token, _, expiresAt) = _tokenService.GenerateToken(user.Id, user.Email);
        return new AuthResponseDto { Token = token, ExpiresAt = expiresAt };
    }

    /// <inheritdoc />
    public async Task<AuthResponseDto?> LoginAsync(string email, string password)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLower());

        if (user is null)
        {
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return null;
        }

        var (token, _, expiresAt) = _tokenService.GenerateToken(user.Id, user.Email);
        return new AuthResponseDto { Token = token, ExpiresAt = expiresAt };
    }

    /// <inheritdoc />
    public async Task LogoutAsync(string jti, DateTime expiresAt)
    {
        await _tokenService.RevokeTokenAsync(jti, expiresAt);
    }
}
