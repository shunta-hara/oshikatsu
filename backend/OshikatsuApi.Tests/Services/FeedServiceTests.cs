using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

public class FeedServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly FeedService _sut;

    public FeedServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new FeedService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── GetNewsAsync: 正常系 ────────────────────────────────────────────────

    [Fact]
    public async Task GetNewsAsync_ReturnsOnlyUsersOshisNews()
    {
        var user1Oshi = await CreateOshi(userId: 1, name: "推しA");
        var user2Oshi = await CreateOshi(userId: 2, name: "推しB");
        await CreateNewsItem(user1Oshi, "ユーザー1のニュース");
        await CreateNewsItem(user2Oshi, "ユーザー2のニュース");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("ユーザー1のニュース", result.Items[0].Title);
    }

    [Fact]
    public async Task GetNewsAsync_ReturnsOrderedByCollectedAtDesc()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateNewsItem(oshi, "古いニュース", collectedAt: now.AddHours(-2));
        await CreateNewsItem(oshi, "新しいニュース", collectedAt: now.AddHours(-1));
        await CreateNewsItem(oshi, "最新ニュース", collectedAt: now);

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal("最新ニュース", result.Items[0].Title);
        Assert.Equal("古いニュース", result.Items[2].Title);
    }

    [Fact]
    public async Task GetNewsAsync_IncludesOshiName()
    {
        var oshi = await CreateOshi(userId: 1, name: "テスト推し");
        await CreateNewsItem(oshi, "テストニュース");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal("テスト推し", result.Items[0].OshiName);
    }

    [Fact]
    public async Task GetNewsAsync_ReturnsPaginationMetadata()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            await CreateNewsItem(oshi, $"ニュース{i}", collectedAt: now.AddMinutes(-i));
        }

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(1, result.TotalPages);
    }

    // ── GetNewsAsync: 境界値 ────────────────────────────────────────────────

    [Fact]
    public async Task GetNewsAsync_NoNews_ReturnsEmptyResult()
    {
        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetNewsAsync_Pagination_FirstPageHas20Items()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 25; i++)
        {
            await CreateNewsItem(oshi, $"ニュース{i}", collectedAt: now.AddMinutes(-i));
        }

        var page1 = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(2, page1.TotalPages);
    }

    [Fact]
    public async Task GetNewsAsync_Pagination_SecondPageHasRemainingItems()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 25; i++)
        {
            await CreateNewsItem(oshi, $"ニュース{i}", collectedAt: now.AddMinutes(-i));
        }

        var page2 = await _sut.GetNewsAsync(userId: 1, page: 2, pageSize: 20);

        Assert.Equal(5, page2.Items.Count);
    }

    [Fact]
    public async Task GetNewsAsync_MultipleOshis_ReturnsAllUsersNews()
    {
        var oshi1 = await CreateOshi(userId: 1, name: "推しA");
        var oshi2 = await CreateOshi(userId: 1, name: "推しB");
        await CreateNewsItem(oshi1, "推しAのニュース");
        await CreateNewsItem(oshi2, "推しBのニュース");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(2, result.TotalCount);
    }

    // ── GetProgramsAsync: 正常系 ────────────────────────────────────────────

    [Fact]
    public async Task GetProgramsAsync_ReturnsOnlyUsersOshisPrograms()
    {
        var user1Oshi = await CreateOshi(userId: 1, name: "推しA");
        var user2Oshi = await CreateOshi(userId: 2, name: "推しB");
        await CreateProgramAppearance(user1Oshi, "ユーザー1の番組");
        await CreateProgramAppearance(user2Oshi, "ユーザー2の番組");

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("ユーザー1の番組", result.Items[0].ProgramName);
    }

    [Fact]
    public async Task GetProgramsAsync_ReturnsOrderedByCollectedAtDesc()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateProgramAppearance(oshi, "古い番組", collectedAt: now.AddHours(-2));
        await CreateProgramAppearance(oshi, "新しい番組", collectedAt: now.AddHours(-1));
        await CreateProgramAppearance(oshi, "最新の番組", collectedAt: now);

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal("最新の番組", result.Items[0].ProgramName);
        Assert.Equal("古い番組", result.Items[2].ProgramName);
    }

    [Fact]
    public async Task GetProgramsAsync_IncludesOshiName()
    {
        var oshi = await CreateOshi(userId: 1, name: "テスト推し");
        await CreateProgramAppearance(oshi, "テスト番組");

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal("テスト推し", result.Items[0].OshiName);
    }

    // ── GetProgramsAsync: 境界値 ────────────────────────────────────────────

    [Fact]
    public async Task GetProgramsAsync_NoPrograms_ReturnsEmptyResult()
    {
        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetProgramsAsync_Pagination_FirstPageHas20Items()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 25; i++)
        {
            await CreateProgramAppearance(oshi, $"番組{i}", collectedAt: now.AddMinutes(-i));
        }

        var page1 = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(2, page1.TotalPages);
    }

    [Fact]
    public async Task GetProgramsAsync_Pagination_SecondPageHasRemainingItems()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 25; i++)
        {
            await CreateProgramAppearance(oshi, $"番組{i}", collectedAt: now.AddMinutes(-i));
        }

        var page2 = await _sut.GetProgramsAsync(userId: 1, page: 2, pageSize: 20);

        Assert.Equal(5, page2.Items.Count);
    }

    // ── GetNewsAsync: oshiId フィルタ ────────────────────────────────────────

    [Fact]
    public async Task GetNewsAsync_WithOshiId_ReturnsOnlyThatOshisNews()
    {
        var oshi1 = await CreateOshi(userId: 1, name: "推しA");
        var oshi2 = await CreateOshi(userId: 1, name: "推しB");
        await CreateNewsItem(oshi1, "推しAのニュース");
        await CreateNewsItem(oshi2, "推しBのニュース");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, oshiId: oshi1.Id);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("推しAのニュース", result.Items[0].Title);
    }

    [Fact]
    public async Task GetNewsAsync_WithOshiIdNotBelongingToUser_ReturnsEmpty()
    {
        var otherOshi = await CreateOshi(userId: 99, name: "他ユーザーの推し");
        await CreateNewsItem(otherOshi, "他ユーザーのニュース");

        // userId=1 のユーザーが userId=99 の oshiId でフィルタしても空になる
        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, oshiId: otherOshi.Id);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetNewsAsync_WithNullOshiId_ReturnsAllUsersNews()
    {
        var oshi1 = await CreateOshi(userId: 1, name: "推しA");
        var oshi2 = await CreateOshi(userId: 1, name: "推しB");
        await CreateNewsItem(oshi1, "推しAのニュース");
        await CreateNewsItem(oshi2, "推しBのニュース");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, oshiId: null);

        Assert.Equal(2, result.TotalCount);
    }

    // ── GetProgramsAsync: oshiId フィルタ ───────────────────────────────────

    [Fact]
    public async Task GetProgramsAsync_WithOshiId_ReturnsOnlyThatOshisPrograms()
    {
        var oshi1 = await CreateOshi(userId: 1, name: "推しA");
        var oshi2 = await CreateOshi(userId: 1, name: "推しB");
        await CreateProgramAppearance(oshi1, "推しAの番組");
        await CreateProgramAppearance(oshi2, "推しBの番組");

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20, oshiId: oshi2.Id);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("推しBの番組", result.Items[0].ProgramName);
    }

    [Fact]
    public async Task GetProgramsAsync_WithOshiIdNotBelongingToUser_ReturnsEmpty()
    {
        var otherOshi = await CreateOshi(userId: 99, name: "他ユーザーの推し");
        await CreateProgramAppearance(otherOshi, "他ユーザーの番組");

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20, oshiId: otherOshi.Id);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetProgramsAsync_WithNullOshiId_ReturnsAllUsersPrograms()
    {
        var oshi1 = await CreateOshi(userId: 1, name: "推しA");
        var oshi2 = await CreateOshi(userId: 1, name: "推しB");
        await CreateProgramAppearance(oshi1, "推しAの番組");
        await CreateProgramAppearance(oshi2, "推しBの番組");

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20, oshiId: null);

        Assert.Equal(2, result.TotalCount);
    }

    // ── 異常系 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNewsAsync_UserWithNoOshis_ReturnsEmptyResult()
    {
        // 他ユーザーが推しを持っていても、対象ユーザーには返さない
        var otherOshi = await CreateOshi(userId: 99, name: "他ユーザーの推し");
        await CreateNewsItem(otherOshi, "他ユーザーのニュース");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetProgramsAsync_UserWithNoOshis_ReturnsEmptyResult()
    {
        var otherOshi = await CreateOshi(userId: 99, name: "他ユーザーの推し");
        await CreateProgramAppearance(otherOshi, "他ユーザーの番組");

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    // ── GetUnreadCountAsync: 正常系 ─────────────────────────────────────────

    [Fact]
    public async Task GetUnreadCountAsync_LastViewedFeedAtIsNull_ReturnsZero()
    {
        // LastViewedFeedAt が null のユーザー（未閲覧）は 0 を返す
        var user = await CreateUser(userId: 1, lastViewedFeedAt: null);
        var oshi = await CreateOshi(userId: user.Id, name: "推しA");
        await CreateNewsItem(oshi, "新しいニュース");

        var result = await _sut.GetUnreadCountAsync(user.Id);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetUnreadCountAsync_NewsAfterLastViewed_CountsAsUnread()
    {
        var lastViewed = DateTime.UtcNow.AddHours(-1);
        var user = await CreateUser(userId: 1, lastViewedFeedAt: lastViewed);
        var oshi = await CreateOshi(userId: user.Id, name: "推しA");
        // lastViewed より後に収集されたニュース
        await CreateNewsItem(oshi, "新しいニュース", collectedAt: DateTimeOffset.UtcNow);

        var result = await _sut.GetUnreadCountAsync(user.Id);

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task GetUnreadCountAsync_NewsBeforeLastViewed_NotCounted()
    {
        var lastViewed = DateTime.UtcNow;
        var user = await CreateUser(userId: 1, lastViewedFeedAt: lastViewed);
        var oshi = await CreateOshi(userId: user.Id, name: "推しA");
        // lastViewed より前に収集されたニュース
        await CreateNewsItem(oshi, "古いニュース", collectedAt: DateTimeOffset.UtcNow.AddHours(-2));

        var result = await _sut.GetUnreadCountAsync(user.Id);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetUnreadCountAsync_CountsNewsAndProgramsTogether()
    {
        var lastViewed = DateTime.UtcNow.AddHours(-1);
        var user = await CreateUser(userId: 1, lastViewedFeedAt: lastViewed);
        var oshi = await CreateOshi(userId: user.Id, name: "推しA");
        await CreateNewsItem(oshi, "新しいニュース", collectedAt: DateTimeOffset.UtcNow);
        await CreateNewsItem(oshi, "新しいニュース2", collectedAt: DateTimeOffset.UtcNow);
        await CreateProgramAppearance(oshi, "新しい番組", collectedAt: DateTimeOffset.UtcNow);

        var result = await _sut.GetUnreadCountAsync(user.Id);

        Assert.Equal(3, result);
    }

    [Fact]
    public async Task GetUnreadCountAsync_OtherUsersItems_NotCounted()
    {
        var lastViewed = DateTime.UtcNow.AddHours(-1);
        var user = await CreateUser(userId: 1, lastViewedFeedAt: lastViewed);
        // 他ユーザーの推しのニュース（UserId = 999 はこのユーザーとは別）
        var otherOshi = await CreateOshi(userId: 999, name: "他ユーザーの推し");
        await CreateNewsItem(otherOshi, "他ユーザーのニュース", collectedAt: DateTimeOffset.UtcNow);

        var result = await _sut.GetUnreadCountAsync(user.Id);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetUnreadCountAsync_UserWithNoOshis_ReturnsZero()
    {
        var lastViewed = DateTime.UtcNow.AddHours(-1);
        var user = await CreateUser(userId: 1, lastViewedFeedAt: lastViewed);
        // 推しが登録されていない

        var result = await _sut.GetUnreadCountAsync(user.Id);

        Assert.Equal(0, result);
    }

    // ── GetUnreadCountAsync: 境界値 ─────────────────────────────────────────

    [Fact]
    public async Task GetUnreadCountAsync_UserNotFound_ReturnsZero()
    {
        // ユーザーが存在しない場合は 0 を返す
        var result = await _sut.GetUnreadCountAsync(userId: 999);

        Assert.Equal(0, result);
    }

    // ── MarkAsReadAsync: 正常系 ─────────────────────────────────────────────

    [Fact]
    public async Task MarkAsReadAsync_UpdatesLastViewedFeedAt()
    {
        var before = DateTime.UtcNow.AddMinutes(-1);
        var user = await CreateUser(userId: 1, lastViewedFeedAt: null);

        await _sut.MarkAsReadAsync(user.Id);

        var updatedUser = await _db.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser!.LastViewedFeedAt);
        Assert.True(updatedUser.LastViewedFeedAt >= before);
    }

    [Fact]
    public async Task MarkAsReadAsync_AfterMark_UnreadCountBecomesZero()
    {
        var lastViewed = DateTime.UtcNow.AddHours(-2);
        var user = await CreateUser(userId: 1, lastViewedFeedAt: lastViewed);
        var oshi = await CreateOshi(userId: user.Id, name: "推しA");
        await CreateNewsItem(oshi, "新しいニュース", collectedAt: DateTimeOffset.UtcNow.AddHours(-1));

        // mark-as-read 前は未読あり
        var countBefore = await _sut.GetUnreadCountAsync(user.Id);
        Assert.Equal(1, countBefore);

        await _sut.MarkAsReadAsync(user.Id);

        // mark-as-read 後は未読0
        var countAfter = await _sut.GetUnreadCountAsync(user.Id);
        Assert.Equal(0, countAfter);
    }

    // ── GetNewsAsync: キーワード検索 ─────────────────────────────────────────

    [Fact]
    public async Task GetNewsAsync_WithKeywordMatchingTitle_ReturnsMatchingItems()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        await CreateNewsItem(oshi, "Pythonについて");
        await CreateNewsItem(oshi, "Javaの最新情報");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, query: "Python");

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Pythonについて", result.Items[0].Title);
    }

    [Fact]
    public async Task GetNewsAsync_WithKeywordMatchingSummary_ReturnsMatchingItems()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        await CreateNewsItem(oshi, "ニュース1", summary: "Pythonが人気");
        await CreateNewsItem(oshi, "ニュース2", summary: "Javaが話題");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, query: "Python");

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("ニュース1", result.Items[0].Title);
    }

    [Fact]
    public async Task GetNewsAsync_WithKeywordMatchingTitleOrSummary_ReturnsBothMatches()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        await CreateNewsItem(oshi, "Pythonについて", summary: null);
        await CreateNewsItem(oshi, "別の記事", summary: "Python事例紹介");
        await CreateNewsItem(oshi, "Javaの最新情報", summary: null);

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, query: "Python");

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetNewsAsync_WithKeywordNotMatching_ReturnsEmpty()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        await CreateNewsItem(oshi, "Javaの最新情報", summary: null);

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, query: "Python");

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetNewsAsync_WithNullQuery_ReturnsAllItems()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        await CreateNewsItem(oshi, "ニュース1");
        await CreateNewsItem(oshi, "ニュース2");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, query: null);

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetNewsAsync_WithWhitespaceQuery_ReturnsAllItems()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        await CreateNewsItem(oshi, "ニュース1");

        // 空白のみのクエリはフィルタなしとして扱う
        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, query: "   ");

        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetNewsAsync_WithKeywordAndOshiId_FiltersBothCriteria()
    {
        var oshi1 = await CreateOshi(userId: 1, name: "推しA");
        var oshi2 = await CreateOshi(userId: 1, name: "推しB");
        await CreateNewsItem(oshi1, "Pythonニュース");
        await CreateNewsItem(oshi2, "Pythonニュース");

        // oshi1 + "Python" キーワードで絞り込むと oshi1 の1件のみ
        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, oshiId: oshi1.Id, query: "Python");

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(oshi1.Id, result.Items[0].OshiId);
    }

    // ── GetNewsAsync: 日付範囲フィルタ ──────────────────────────────────────

    [Fact]
    public async Task GetNewsAsync_WithFrom_ReturnsOnlyItemsAfterFrom()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateNewsItem(oshi, "古い記事", publishedAt: now.AddDays(-5));
        await CreateNewsItem(oshi, "新しい記事", publishedAt: now.AddDays(-1));

        var from = now.AddDays(-3);
        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, from: from);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("新しい記事", result.Items[0].Title);
    }

    [Fact]
    public async Task GetNewsAsync_WithTo_ReturnsOnlyItemsBeforeTo()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateNewsItem(oshi, "古い記事", publishedAt: now.AddDays(-5));
        await CreateNewsItem(oshi, "新しい記事", publishedAt: now.AddDays(-1));

        var to = now.AddDays(-3);
        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, to: to);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("古い記事", result.Items[0].Title);
    }

    [Fact]
    public async Task GetNewsAsync_WithFromAndTo_ReturnsOnlyItemsInRange()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateNewsItem(oshi, "範囲外(古)", publishedAt: now.AddDays(-10));
        await CreateNewsItem(oshi, "範囲内", publishedAt: now.AddDays(-5));
        await CreateNewsItem(oshi, "範囲外(新)", publishedAt: now.AddDays(-1));

        var from = now.AddDays(-7);
        var to = now.AddDays(-3);
        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, from: from, to: to);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("範囲内", result.Items[0].Title);
    }

    [Fact]
    public async Task GetNewsAsync_WithDateFilter_ExcludesNullPublishedAt()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateNewsItem(oshi, "公開日なし", publishedAt: null);
        await CreateNewsItem(oshi, "公開日あり", publishedAt: now);

        var from = now.AddDays(-1);
        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, from: from);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("公開日あり", result.Items[0].Title);
    }

    [Fact]
    public async Task GetNewsAsync_FromAfterAllItems_ReturnsEmpty()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateNewsItem(oshi, "古い記事", publishedAt: now.AddDays(-10));

        // 全アイテムより後の from → 空
        var from = now.AddDays(1);
        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, from: from);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetNewsAsync_ToBeforeAllItems_ReturnsEmpty()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateNewsItem(oshi, "新しい記事", publishedAt: now);

        // 全アイテムより前の to → 空
        var to = now.AddDays(-10);
        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, to: to);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetNewsAsync_NullFromAndTo_ReturnsAllItems()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateNewsItem(oshi, "記事1", publishedAt: now.AddDays(-5));
        await CreateNewsItem(oshi, "記事2（公開日なし）", publishedAt: null);

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        // フィルタなし → PublishedAt 有無に関わらず全件
        Assert.Equal(2, result.TotalCount);
    }

    // ── GetNewsAsync: JST フィルタ UTC 正規化（FEAT-017）────────────────────

    [Fact]
    public async Task GetNewsAsync_JstFrom_SameResultAsUtcEquivalent()
    {
        // UTC 2026-10-02T11:00:00Z に公開されたニュース
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var utcPublished = new DateTimeOffset(2026, 10, 2, 11, 0, 0, TimeSpan.Zero);
        await CreateNewsItem(oshi, "対象記事", publishedAt: utcPublished);

        // JST from: 2026-10-02T10:00:00+09:00 == 2026-10-02T01:00:00Z（記事より前）
        var jstFrom = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.FromHours(9));
        // 等価 UTC from: 2026-10-02T01:00:00Z
        var utcFrom = jstFrom.ToUniversalTime();

        var resultJst = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, from: jstFrom);
        var resultUtc = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, from: utcFrom);

        // JST フィルタと UTC フィルタは同じ結果を返す
        Assert.Equal(resultUtc.TotalCount, resultJst.TotalCount);
        Assert.Equal(1, resultJst.TotalCount);
    }

    [Fact]
    public async Task GetNewsAsync_JstTo_SameResultAsUtcEquivalent()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var utcPublished = new DateTimeOffset(2026, 10, 2, 11, 0, 0, TimeSpan.Zero);
        await CreateNewsItem(oshi, "対象記事", publishedAt: utcPublished);

        // JST to: 2026-10-02T21:00:00+09:00 == 2026-10-02T12:00:00Z（記事より後）
        var jstTo = new DateTimeOffset(2026, 10, 2, 21, 0, 0, TimeSpan.FromHours(9));
        var utcTo = jstTo.ToUniversalTime();

        var resultJst = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, to: jstTo);
        var resultUtc = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, to: utcTo);

        Assert.Equal(resultUtc.TotalCount, resultJst.TotalCount);
        Assert.Equal(1, resultJst.TotalCount);
    }

    [Fact]
    public async Task GetNewsAsync_JstFrom_ExcludesItemBeforeJstBoundary()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        // 範囲外（古）: 2026-10-01T00:00:00Z
        var oldItem = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        // 範囲内: 2026-10-02T15:00:00Z
        var newItem = new DateTimeOffset(2026, 10, 2, 15, 0, 0, TimeSpan.Zero);
        await CreateNewsItem(oshi, "古い記事", publishedAt: oldItem);
        await CreateNewsItem(oshi, "新しい記事", publishedAt: newItem);

        // JST from: 2026-10-02T00:00:00+09:00 == 2026-10-01T15:00:00Z
        var jstFrom = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.FromHours(9));

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, from: jstFrom);

        // 2026-10-01T00:00:00Z は jstFrom(=2026-10-01T15:00:00Z) より前なので除外
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("新しい記事", result.Items[0].Title);
    }

    // ── GetProgramsAsync: 日付範囲フィルタ ─────────────────────────────────

    [Fact]
    public async Task GetProgramsAsync_WithFrom_ReturnsOnlyItemsAfterFrom()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateProgramAppearance(oshi, "古い番組", airedAt: now.AddDays(-5));
        await CreateProgramAppearance(oshi, "新しい番組", airedAt: now.AddDays(-1));

        var from = now.AddDays(-3);
        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20, from: from);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("新しい番組", result.Items[0].ProgramName);
    }

    [Fact]
    public async Task GetProgramsAsync_WithTo_ReturnsOnlyItemsBeforeTo()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateProgramAppearance(oshi, "古い番組", airedAt: now.AddDays(-5));
        await CreateProgramAppearance(oshi, "新しい番組", airedAt: now.AddDays(-1));

        var to = now.AddDays(-3);
        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20, to: to);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("古い番組", result.Items[0].ProgramName);
    }

    [Fact]
    public async Task GetProgramsAsync_WithDateFilter_ExcludesNullAiredAt()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateProgramAppearance(oshi, "放送日なし", airedAt: null);
        await CreateProgramAppearance(oshi, "放送日あり", airedAt: now);

        var from = now.AddDays(-1);
        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20, from: from);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("放送日あり", result.Items[0].ProgramName);
    }

    [Fact]
    public async Task GetProgramsAsync_WithFromAndTo_ReturnsOnlyItemsInRange()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateProgramAppearance(oshi, "範囲外(古)", airedAt: now.AddDays(-10));
        await CreateProgramAppearance(oshi, "範囲内", airedAt: now.AddDays(-5));
        await CreateProgramAppearance(oshi, "範囲外(新)", airedAt: now.AddDays(-1));

        var from = now.AddDays(-7);
        var to = now.AddDays(-3);
        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20, from: from, to: to);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("範囲内", result.Items[0].ProgramName);
    }

    // ── GetProgramsAsync: JST フィルタ UTC 正規化（FEAT-017）────────────────

    [Fact]
    public async Task GetProgramsAsync_JstFrom_SameResultAsUtcEquivalent()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var utcAiredAt = new DateTimeOffset(2026, 10, 2, 11, 0, 0, TimeSpan.Zero);
        await CreateProgramAppearance(oshi, "対象番組", airedAt: utcAiredAt);

        // JST from: 2026-10-02T10:00:00+09:00 == 2026-10-02T01:00:00Z（番組より前）
        var jstFrom = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.FromHours(9));
        var utcFrom = jstFrom.ToUniversalTime();

        var resultJst = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20, from: jstFrom);
        var resultUtc = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20, from: utcFrom);

        Assert.Equal(resultUtc.TotalCount, resultJst.TotalCount);
        Assert.Equal(1, resultJst.TotalCount);
    }

    [Fact]
    public async Task GetProgramsAsync_JstTo_SameResultAsUtcEquivalent()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var utcAiredAt = new DateTimeOffset(2026, 10, 2, 11, 0, 0, TimeSpan.Zero);
        await CreateProgramAppearance(oshi, "対象番組", airedAt: utcAiredAt);

        // JST to: 2026-10-02T21:00:00+09:00 == 2026-10-02T12:00:00Z（番組より後）
        var jstTo = new DateTimeOffset(2026, 10, 2, 21, 0, 0, TimeSpan.FromHours(9));
        var utcTo = jstTo.ToUniversalTime();

        var resultJst = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20, to: jstTo);
        var resultUtc = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20, to: utcTo);

        Assert.Equal(resultUtc.TotalCount, resultJst.TotalCount);
        Assert.Equal(1, resultJst.TotalCount);
    }

    [Fact]
    public async Task GetProgramsAsync_JstFrom_ExcludesItemBeforeJstBoundary()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var oldItem = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var newItem = new DateTimeOffset(2026, 10, 2, 15, 0, 0, TimeSpan.Zero);
        await CreateProgramAppearance(oshi, "古い番組", airedAt: oldItem);
        await CreateProgramAppearance(oshi, "新しい番組", airedAt: newItem);

        // JST from: 2026-10-02T00:00:00+09:00 == 2026-10-01T15:00:00Z
        var jstFrom = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.FromHours(9));

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20, from: jstFrom);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("新しい番組", result.Items[0].ProgramName);
    }

    // ── ヘルパー ─────────────────────────────────────────────────────────────

    private async Task<OshikatsuApi.Entities.User> CreateUser(int userId, DateTime? lastViewedFeedAt)
    {
        var now = DateTime.UtcNow;
        var user = new OshikatsuApi.Entities.User
        {
            // InMemory DB は Id を自動生成しないため手動で指定しない
            // 代わりに SaveChanges 後の Id を使う
            Email = $"user{userId}@example.com",
            PasswordHash = "hash",
            CreatedAt = now,
            UpdatedAt = now,
            LastViewedFeedAt = lastViewedFeedAt,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Oshi> CreateOshi(int userId, string name)
    {
        var now = DateTime.UtcNow;
        var oshi = new Oshi
        {
            UserId = userId,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Oshis.Add(oshi);
        await _db.SaveChangesAsync();
        return oshi;
    }

    private async Task<NewsItem> CreateNewsItem(
        Oshi oshi,
        string title,
        string? summary = null,
        DateTimeOffset? collectedAt = null,
        DateTimeOffset? publishedAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        var item = new NewsItem
        {
            OshiId = oshi.Id,
            Title = title,
            Summary = summary,
            SourceUrl = $"https://example.com/news/{Guid.NewGuid()}",
            CollectedAt = collectedAt ?? now,
            PublishedAt = publishedAt,
            CreatedAt = now,
        };
        _db.NewsItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    private async Task<ProgramAppearance> CreateProgramAppearance(
        Oshi oshi,
        string programName,
        DateTimeOffset? collectedAt = null,
        DateTimeOffset? airedAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        var item = new ProgramAppearance
        {
            OshiId = oshi.Id,
            ProgramName = programName,
            SourceUrl = $"https://example.com/programs/{Guid.NewGuid()}",
            CollectedAt = collectedAt ?? now,
            AiredAt = airedAt,
            CreatedAt = now,
        };
        _db.ProgramAppearances.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    // ── キーワード検索: 大文字小文字を区別しない（Sprint 25 FEAT-027）──────────

    [Fact]
    public async Task GetNewsAsync_KeywordSearch_LowercaseQueryMatchesUppercaseTitle()
    {
        // "NHK Morning Show" を小文字クエリ "nhk" で検索できる
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        await CreateNewsItem(oshi, "NHK Morning Show");
        await CreateNewsItem(oshi, "Fuji TV Evening News");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, query: "nhk");

        Assert.Single(result.Items);
        Assert.Contains("NHK", result.Items[0].Title);
    }

    [Fact]
    public async Task GetNewsAsync_KeywordSearch_UppercaseQueryMatchesLowercaseTitle()
    {
        // "nhk morning" タイトルを大文字クエリ "NHK" で検索できる
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        await CreateNewsItem(oshi, "nhk morning digest");
        await CreateNewsItem(oshi, "fuji tv report");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, query: "NHK");

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetNewsAsync_KeywordSearch_MixedCaseQueryMatchesMixedCaseTitle()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        await CreateNewsItem(oshi, "NHK朝のニュース");

        // 大文字・小文字・混在の3パターンすべてが同じ1件を返す
        var lower = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, query: "nhk");
        var upper = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, query: "NHK");
        var mixed = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, query: "Nhk");

        Assert.Equal(1, lower.TotalCount);
        Assert.Equal(1, upper.TotalCount);
        Assert.Equal(1, mixed.TotalCount);
    }

    [Fact]
    public async Task GetNewsAsync_KeywordSearch_PercentInQueryTreatedAsLiteral()
    {
        // "%" はワイルドカードではなくリテラルとして扱われる
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        await CreateNewsItem(oshi, "100%成功");      // 一致するはず
        await CreateNewsItem(oshi, "普通の記事タイトル");    // 一致しないはず

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, query: "100%");

        Assert.Single(result.Items);
        Assert.Contains("100%", result.Items[0].Title);
    }

    [Fact]
    public async Task GetNewsAsync_KeywordSearch_UnderscoreInQueryTreatedAsLiteral()
    {
        // "_" はワイルドカードではなくリテラルとして扱われる
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        await CreateNewsItem(oshi, "nhk_morning_news");  // 一致するはず
        await CreateNewsItem(oshi, "nhk morning news");  // 一致しないはず（_ はリテラル）

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, query: "nhk_morning");

        Assert.Single(result.Items);
        Assert.Contains("nhk_morning_news", result.Items[0].Title);
    }
}
