using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

/// <summary>PushNotificationService のユニットテスト</summary>
public class PushNotificationServiceTests
{
    // ── ヘルパー ──────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static PushNotificationService CreateSut(AppDbContext db, IWebPushClient? pushClient = null)
    {
        pushClient ??= Substitute.For<IWebPushClient>();
        return new PushNotificationService(db, pushClient, NullLogger<PushNotificationService>.Instance);
    }

    private static User CreateUser(AppDbContext db)
    {
        var user = new User
        {
            Email = $"user_{Guid.NewGuid()}@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static Talent CreateTalent(AppDbContext db, string name = "山田花子")
    {
        var talent = new Talent
        {
            DisplayName = name,
            NormalizedName = name.ToLower(),
            CreatedAt = DateTime.UtcNow,
        };
        db.Talents.Add(talent);
        db.SaveChanges();
        return talent;
    }

    private static void AttachTalentToUser(AppDbContext db, User user, Talent talent)
    {
        db.UserOshis.Add(new UserOshi
        {
            UserId = user.Id,
            TalentId = talent.Id,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    private static OshikatsuApi.Entities.PushSubscription AddSubscription(
        AppDbContext db,
        User user,
        string? endpoint = null,
        DateTimeOffset? lastNotifiedAt = null)
    {
        var sub = new OshikatsuApi.Entities.PushSubscription
        {
            UserId = user.Id,
            Endpoint = endpoint ?? $"https://fcm.example.com/{Guid.NewGuid()}",
            P256dh = "p256dh_key",
            Auth = "auth_secret",
            CreatedAt = DateTimeOffset.UtcNow,
            LastNotifiedAt = lastNotifiedAt,
        };
        db.PushSubscriptions.Add(sub);
        db.SaveChanges();
        return sub;
    }

    private static NewsItem AddNewsItem(AppDbContext db, Talent talent, DateTimeOffset collectedAt)
    {
        var item = new NewsItem
        {
            TalentId = talent.Id,
            Title = $"記事_{Guid.NewGuid()}",
            SourceUrl = $"https://example.com/{Guid.NewGuid()}",
            CollectedAt = collectedAt,
            CreatedAt = collectedAt,
        };
        db.NewsItems.Add(item);
        db.SaveChanges();
        return item;
    }

    private static ProgramAppearance AddProgram(AppDbContext db, Talent talent, DateTimeOffset collectedAt)
    {
        var item = new ProgramAppearance
        {
            TalentId = talent.Id,
            ProgramName = $"番組_{Guid.NewGuid()}",
            SourceUrl = $"https://example.com/{Guid.NewGuid()}",
            CollectedAt = collectedAt,
            CreatedAt = collectedAt,
        };
        db.ProgramAppearances.Add(item);
        db.SaveChanges();
        return item;
    }

    // ── 正常系: スキップ ──────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyAllUsersAsync_NoPushSubscriptions_Returns0Counts()
    {
        using var db = CreateDb();
        var sut = CreateSut(db);

        var result = await sut.NotifyAllUsersAsync();

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal(0, result.SkipCount);
        Assert.Equal(0, result.GoneRemovedCount);
    }

    [Fact]
    public async Task NotifyAllUsersAsync_UserWithSubscriptionButNoNewItems_Skips()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var talent = CreateTalent(db);
        AttachTalentToUser(db, user, talent);
        AddSubscription(db, user, lastNotifiedAt: DateTimeOffset.UtcNow);
        // 新着なし

        var pushClient = Substitute.For<IWebPushClient>();
        var sut = CreateSut(db, pushClient);

        var result = await sut.NotifyAllUsersAsync();

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.SkipCount);
        await pushClient.DidNotReceiveWithAnyArgs()
            .SendNotificationAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task NotifyAllUsersAsync_UserWithNoTalents_Skips()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        // タレント登録なし
        AddSubscription(db, user);

        var pushClient = Substitute.For<IWebPushClient>();
        var sut = CreateSut(db, pushClient);

        var result = await sut.NotifyAllUsersAsync();

        Assert.Equal(1, result.SkipCount);
        await pushClient.DidNotReceiveWithAnyArgs()
            .SendNotificationAsync(default!, default!, default!, default!, default);
    }

    // ── 正常系: 送信 ─────────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyAllUsersAsync_UserWithNewNewsItem_SendsPush()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var talent = CreateTalent(db);
        AttachTalentToUser(db, user, talent);
        var sub = AddSubscription(db, user, lastNotifiedAt: DateTimeOffset.UtcNow.AddHours(-2));
        AddNewsItem(db, talent, collectedAt: DateTimeOffset.UtcNow.AddMinutes(-30));

        var pushClient = Substitute.For<IWebPushClient>();
        var sut = CreateSut(db, pushClient);

        var result = await sut.NotifyAllUsersAsync();

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.SkipCount);
        await pushClient.Received(1).SendNotificationAsync(
            sub.Endpoint, sub.P256dh, sub.Auth, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAllUsersAsync_NewProgram_AlsoSendsPush()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var talent = CreateTalent(db);
        AttachTalentToUser(db, user, talent);
        AddSubscription(db, user, lastNotifiedAt: DateTimeOffset.UtcNow.AddHours(-2));
        AddProgram(db, talent, collectedAt: DateTimeOffset.UtcNow.AddMinutes(-30));

        var pushClient = Substitute.For<IWebPushClient>();
        var sut = CreateSut(db, pushClient);

        var result = await sut.NotifyAllUsersAsync();

        Assert.Equal(1, result.SuccessCount);
        await pushClient.Received(1).SendNotificationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAllUsersAsync_UserNeverNotified_TreatsAllItemsAsNew()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var talent = CreateTalent(db);
        AttachTalentToUser(db, user, talent);
        // LastNotifiedAt = null（初回）
        AddSubscription(db, user, lastNotifiedAt: null);
        AddNewsItem(db, talent, collectedAt: DateTimeOffset.UtcNow.AddDays(-1));

        var pushClient = Substitute.For<IWebPushClient>();
        var sut = CreateSut(db, pushClient);

        var result = await sut.NotifyAllUsersAsync();

        Assert.Equal(1, result.SuccessCount);
    }

    [Fact]
    public async Task NotifyAllUsersAsync_Payload_ContainsTalentNameAndCount()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var talent = CreateTalent(db, "田中たろう");
        AttachTalentToUser(db, user, talent);
        AddSubscription(db, user, lastNotifiedAt: null);
        AddNewsItem(db, talent, collectedAt: DateTimeOffset.UtcNow.AddMinutes(-10));
        AddNewsItem(db, talent, collectedAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        string? capturedPayload = null;
        var pushClient = Substitute.For<IWebPushClient>();
        pushClient
            .SendNotificationAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Do<string>(p => capturedPayload = p),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = CreateSut(db, pushClient);
        await sut.NotifyAllUsersAsync();

        Assert.NotNull(capturedPayload);
        Assert.Contains("田中たろう", capturedPayload);
        Assert.Contains("2", capturedPayload); // 2件
    }

    [Fact]
    public async Task NotifyAllUsersAsync_MultipleSubscriptions_SendsToAll()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var talent = CreateTalent(db);
        AttachTalentToUser(db, user, talent);
        AddSubscription(db, user, endpoint: "https://fcm.example.com/sub1");
        AddSubscription(db, user, endpoint: "https://fcm.example.com/sub2");
        AddNewsItem(db, talent, collectedAt: DateTimeOffset.UtcNow.AddMinutes(-10));

        var pushClient = Substitute.For<IWebPushClient>();
        var sut = CreateSut(db, pushClient);

        var result = await sut.NotifyAllUsersAsync();

        Assert.Equal(2, result.SuccessCount);
        await pushClient.Received(2).SendNotificationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAllUsersAsync_Success_UpdatesLastNotifiedAt()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var talent = CreateTalent(db);
        AttachTalentToUser(db, user, talent);
        var sub = AddSubscription(db, user, lastNotifiedAt: null);
        AddNewsItem(db, talent, collectedAt: DateTimeOffset.UtcNow.AddMinutes(-10));

        var sut = CreateSut(db);
        var before = DateTimeOffset.UtcNow;
        await sut.NotifyAllUsersAsync();

        var updated = await db.PushSubscriptions.FindAsync(sub.Id);
        Assert.NotNull(updated);
        Assert.NotNull(updated.LastNotifiedAt);
        Assert.True(updated.LastNotifiedAt >= before);
    }

    // ── 正常系: 410 Gone ─────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyAllUsersAsync_GoneEndpoint_RemovesSubscription()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var talent = CreateTalent(db);
        AttachTalentToUser(db, user, talent);
        var sub = AddSubscription(db, user);
        AddNewsItem(db, talent, collectedAt: DateTimeOffset.UtcNow.AddMinutes(-10));

        var pushClient = Substitute.For<IWebPushClient>();
        pushClient
            .SendNotificationAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new WebPushGoneException("410 Gone"));

        var sut = CreateSut(db, pushClient);
        var result = await sut.NotifyAllUsersAsync();

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.GoneRemovedCount);
        Assert.False(await db.PushSubscriptions.AnyAsync(ps => ps.Id == sub.Id));
    }

    [Fact]
    public async Task NotifyAllUsersAsync_GoneEndpoint_DoesNotRetry()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var talent = CreateTalent(db);
        AttachTalentToUser(db, user, talent);
        AddSubscription(db, user);
        AddNewsItem(db, talent, collectedAt: DateTimeOffset.UtcNow.AddMinutes(-10));

        var pushClient = Substitute.For<IWebPushClient>();
        pushClient
            .SendNotificationAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new WebPushGoneException("410 Gone"));

        var sut = CreateSut(db, pushClient);
        await sut.NotifyAllUsersAsync();

        // 410 Gone は1回のみ呼ばれ、リトライしない
        await pushClient.Received(1).SendNotificationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── 正常系: リトライ ─────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyAllUsersAsync_TransientError_RetriesUpToMaxTimes()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var talent = CreateTalent(db);
        AttachTalentToUser(db, user, talent);
        AddSubscription(db, user);
        AddNewsItem(db, talent, collectedAt: DateTimeOffset.UtcNow.AddMinutes(-10));

        var pushClient = Substitute.For<IWebPushClient>();
        pushClient
            .SendNotificationAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new WebPushTransientException("一時的なエラー"));

        var sut = CreateSut(db, pushClient);
        var result = await sut.NotifyAllUsersAsync();

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        // 最大 3 回試みる
        await pushClient.Received(3).SendNotificationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAllUsersAsync_TransientThenSuccess_ReturnsSuccess()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var talent = CreateTalent(db);
        AttachTalentToUser(db, user, talent);
        AddSubscription(db, user);
        AddNewsItem(db, talent, collectedAt: DateTimeOffset.UtcNow.AddMinutes(-10));

        int callCount = 0;
        var pushClient = Substitute.For<IWebPushClient>();
        pushClient
            .SendNotificationAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                if (callCount < 2)
                    return Task.FromException(new WebPushTransientException("一時的なエラー"));
                return Task.CompletedTask;
            });

        var sut = CreateSut(db, pushClient);
        var result = await sut.NotifyAllUsersAsync();

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
    }

    // ── 境界値 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyAllUsersAsync_ItemCollectedBeforeLastNotified_Skips()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var talent = CreateTalent(db);
        AttachTalentToUser(db, user, talent);

        var lastNotified = DateTimeOffset.UtcNow.AddHours(-1);
        AddSubscription(db, user, lastNotifiedAt: lastNotified);
        // 通知以前のアイテムは新着扱いにならない
        AddNewsItem(db, talent, collectedAt: lastNotified.AddMinutes(-30));

        var pushClient = Substitute.For<IWebPushClient>();
        var sut = CreateSut(db, pushClient);

        var result = await sut.NotifyAllUsersAsync();

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.SkipCount);
        await pushClient.DidNotReceiveWithAnyArgs()
            .SendNotificationAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task NotifyAllUsersAsync_MultipleTalents_PayloadContainsAllTalents()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var talent1 = CreateTalent(db, "タレントA");
        var talent2 = CreateTalent(db, "タレントB");
        AttachTalentToUser(db, user, talent1);
        AttachTalentToUser(db, user, talent2);
        AddSubscription(db, user, lastNotifiedAt: null);
        AddNewsItem(db, talent1, collectedAt: DateTimeOffset.UtcNow.AddMinutes(-10));
        AddNewsItem(db, talent2, collectedAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        string? capturedPayload = null;
        var pushClient = Substitute.For<IWebPushClient>();
        pushClient
            .SendNotificationAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Do<string>(p => capturedPayload = p),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = CreateSut(db, pushClient);
        await sut.NotifyAllUsersAsync();

        Assert.NotNull(capturedPayload);
        Assert.Contains("タレントA", capturedPayload);
        Assert.Contains("タレントB", capturedPayload);
    }
}
