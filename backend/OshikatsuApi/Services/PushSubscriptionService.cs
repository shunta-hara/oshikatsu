using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;

namespace OshikatsuApi.Services;

/// <inheritdoc />
public class PushSubscriptionService : IPushSubscriptionService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public PushSubscriptionService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <inheritdoc />
    public Task<string> GetVapidPublicKeyAsync()
    {
        var key = _config["VapidSettings:PublicKey"] ?? string.Empty;
        return Task.FromResult(key);
    }

    /// <inheritdoc />
    public async Task UpsertSubscriptionAsync(int userId, string endpoint, string p256dh, string auth)
    {
        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(ps => ps.Endpoint == endpoint);

        if (existing is not null)
        {
            // 同一 endpoint の再登録: 鍵情報とユーザーを更新する（上書き）
            existing.UserId = userId;
            existing.P256dh = p256dh;
            existing.Auth = auth;
        }
        else
        {
            _db.PushSubscriptions.Add(new PushSubscription
            {
                UserId = userId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        await _db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteSubscriptionAsync(int userId, string endpoint)
    {
        var subscription = await _db.PushSubscriptions
            .FirstOrDefaultAsync(ps => ps.UserId == userId && ps.Endpoint == endpoint);

        if (subscription is not null)
        {
            _db.PushSubscriptions.Remove(subscription);
            await _db.SaveChangesAsync();
        }
        // 存在しない場合は冪等として何もしない
    }
}
