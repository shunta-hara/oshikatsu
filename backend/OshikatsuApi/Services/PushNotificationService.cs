using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;

namespace OshikatsuApi.Services;

/// <summary>
/// 全ユーザーへの Web Push 通知送信ジョブ実装（FEAT-045/FEAT-052）。
/// <para>
/// 直近の通知送信以降に収集された新着ニュース・出演情報があるユーザーを検出し、
/// タレント名と新着件数を含む Push 通知を送信する。
/// 収集失敗が3サイクル連続した場合は警告 Push を全購読者へ送信する（重複送信なし）。
/// 410 Gone の endpoint は自動削除する。
/// 送信失敗（410 Gone 以外）は最大 3 回リトライ（指数バックオフ）する。
/// </para>
/// </summary>
public class PushNotificationService : IPushNotificationService
{
    private const int MaxRetries = 3;

    private readonly AppDbContext _db;
    private readonly IWebPushClient _webPushClient;
    private readonly ICollectorJobLogService _jobLogService;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(
        AppDbContext db,
        IWebPushClient webPushClient,
        ICollectorJobLogService jobLogService,
        ILogger<PushNotificationService> logger)
    {
        _db = db;
        _webPushClient = webPushClient;
        _jobLogService = jobLogService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PushNotifyResult> NotifyAllUsersAsync(CancellationToken cancellationToken = default)
    {
        int successCount = 0;
        int failureCount = 0;
        int skipCount = 0;
        int goneRemovedCount = 0;
        int warningSentCount = 0;

        // ── 収集失敗警告チェック（FEAT-052）─────────────────────────────────
        // 3サイクル連続失敗（初回検知のみ）の場合、全購読者へ警告 Push を送信する
        var alerts = await _jobLogService.GetActiveFailureAlertsAsync();
        if (alerts.IsOverallJobFailing || alerts.FailingTalents.Count > 0)
        {
            var warningPayload = BuildWarningPayload(alerts);
            var allSubscriptions = await _db.PushSubscriptions.ToListAsync(cancellationToken);

            _logger.LogWarning(
                "収集失敗アラート検知: 全体失敗={Overall}, 失敗タレント数={TalentCount}。{SubCount} 件の購読へ警告 Push を送信します。",
                alerts.IsOverallJobFailing, alerts.FailingTalents.Count, allSubscriptions.Count);

            foreach (var sub in allSubscriptions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sendResult = await SendWithRetryAsync(sub.Endpoint, sub.P256dh, sub.Auth, warningPayload, cancellationToken);
                switch (sendResult)
                {
                    case SendResult.Success:
                        warningSentCount++;
                        break;
                    case SendResult.Gone:
                        goneRemovedCount++;
                        break;
                    case SendResult.Failure:
                        failureCount++;
                        break;
                }
            }

            _logger.LogInformation(
                "警告 Push 送信完了: 成功={WarningSent}, Gone削除={Gone}",
                warningSentCount, goneRemovedCount);
        }

        // 購読を持つ全ユーザー ID を取得
        var userIds = await _db.PushSubscriptions
            .Select(ps => ps.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var userId in userIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 対象ユーザーの全購読を取得
            var subscriptions = await _db.PushSubscriptions
                .Where(ps => ps.UserId == userId)
                .ToListAsync(cancellationToken);

            if (subscriptions.Count == 0)
                continue;

            // 直近の通知送信時刻 = 購読の中で最も古い LastNotifiedAt（null は未通知）
            var lastNotifiedAt = subscriptions
                .Select(ps => ps.LastNotifiedAt)
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
                .DefaultIfEmpty(DateTimeOffset.MinValue)
                .Min();

            // 直近通知以降の新着件数をタレント別に集計
            var talentNewsCountMap = await GetNewItemCountsByTalentAsync(userId, lastNotifiedAt, cancellationToken);

            if (talentNewsCountMap.Count == 0)
            {
                _logger.LogDebug("ユーザー {UserId}: 新着なし → スキップ", userId);
                skipCount++;
                continue;
            }

            // 通知本文を生成（「推し名：N件の新着」形式）
            var notificationPayload = BuildNotificationPayload(talentNewsCountMap);
            var now = DateTimeOffset.UtcNow;

            // 全購読に送信
            foreach (var sub in subscriptions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sendResult = await SendWithRetryAsync(sub.Endpoint, sub.P256dh, sub.Auth, notificationPayload, cancellationToken);

                switch (sendResult)
                {
                    case SendResult.Success:
                        sub.LastNotifiedAt = now;
                        successCount++;
                        break;
                    case SendResult.Gone:
                        goneRemovedCount++;
                        break;
                    case SendResult.Failure:
                        failureCount++;
                        break;
                }
            }

            // LastNotifiedAt 更新を保存（Gone 削除は SendWithRetryAsync 内で実行済み）
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Push 通知ジョブ完了: 成功={Success}, 失敗={Failure}, スキップ={Skip}, Gone削除={Gone}, 警告送信={Warning}",
            successCount, failureCount, skipCount, goneRemovedCount, warningSentCount);

        return new PushNotifyResult(successCount, failureCount, skipCount, goneRemovedCount)
        {
            WarningSentCount = warningSentCount,
        };
    }

    // ── プライベートヘルパー ─────────────────────────────────────────────────────

    private enum SendResult { Success, Failure, Gone }

    /// <summary>
    /// 指定ユーザーに紐づくタレントごとに直近通知以降の新着件数を集計する。
    /// ニュースアイテムと出演番組の合計件数を返す。
    /// </summary>
    private async Task<Dictionary<string, int>> GetNewItemCountsByTalentAsync(
        int userId,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        // ユーザーが登録しているタレント ID → 表示名 を取得
        var talentMap = await _db.UserOshis
            .Where(uo => uo.UserId == userId)
            .Join(_db.Talents, uo => uo.TalentId, t => t.Id, (uo, t) => new { t.Id, t.DisplayName })
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        if (talentMap.Count == 0)
            return new Dictionary<string, int>();

        var talentIds = talentMap.Keys.ToList();

        // 新着ニュース件数（talent_id ベース）
        var newsCounts = await _db.NewsItems
            .Where(n => n.TalentId.HasValue
                     && talentIds.Contains(n.TalentId.Value)
                     && n.CollectedAt > since)
            .GroupBy(n => n.TalentId!.Value)
            .Select(g => new { TalentId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // 新着出演番組件数（talent_id ベース）
        var programCounts = await _db.ProgramAppearances
            .Where(p => p.TalentId.HasValue
                     && talentIds.Contains(p.TalentId.Value)
                     && p.CollectedAt > since)
            .GroupBy(p => p.TalentId!.Value)
            .Select(g => new { TalentId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // タレント名 → 合計件数 に変換
        var result = new Dictionary<string, int>();
        foreach (var item in newsCounts)
        {
            if (talentMap.TryGetValue(item.TalentId, out var name))
                result[name] = result.GetValueOrDefault(name) + item.Count;
        }
        foreach (var item in programCounts)
        {
            if (talentMap.TryGetValue(item.TalentId, out var name))
                result[name] = result.GetValueOrDefault(name) + item.Count;
        }

        return result;
    }

    // 日本語を Unicode エスケープせず JSON 出力するためのオプション
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    /// <summary>
    /// 収集失敗警告ペイロード JSON を生成する（FEAT-052）。
    /// 例: { "title": "収集エラーのお知らせ", "body": "山田花子の収集が連続して失敗しています（3サイクル連続）" }
    /// </summary>
    private static string BuildWarningPayload(FailureAlertSummary alerts)
    {
        string body;
        if (alerts.IsOverallJobFailing && alerts.FailingTalents.Count > 0)
        {
            var talentNames = string.Join("、", alerts.FailingTalents.Select(t => t.TalentName));
            body = $"情報収集と{talentNames}の収集が連続して失敗しています（3サイクル連続）。確認をお願いします。";
        }
        else if (alerts.IsOverallJobFailing)
        {
            body = "情報収集が連続して失敗しています（3サイクル連続）。確認をお願いします。";
        }
        else
        {
            var talentNames = string.Join("、", alerts.FailingTalents.Select(t => t.TalentName));
            body = $"{talentNames}の収集が連続して失敗しています（3サイクル連続）。";
        }

        return JsonSerializer.Serialize(new
        {
            title = "収集エラーのお知らせ",
            body,
        }, JsonOptions);
    }

    /// <summary>
    /// 通知ペイロード JSON を生成する。
    /// 例: { "title": "推し活 新着情報", "body": "山田花子: 3件の新着, 佐藤太郎: 1件の新着" }
    /// </summary>
    private static string BuildNotificationPayload(Dictionary<string, int> talentCountMap)
    {
        var lines = talentCountMap
            .OrderByDescending(kv => kv.Value)
            .Select(kv => $"{kv.Key}: {kv.Value}件の新着");

        var body = string.Join(", ", lines);

        return JsonSerializer.Serialize(new
        {
            title = "推し活 新着情報",
            body,
        }, JsonOptions);
    }

    /// <summary>
    /// 指数バックオフ付きリトライで Push 通知を送信する。
    /// 成功: Success, 410 Gone（購読削除済み）: Gone, リトライ上限: Failure
    /// </summary>
    private async Task<SendResult> SendWithRetryAsync(
        string endpoint,
        string p256dh,
        string auth,
        string payload,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await _webPushClient.SendNotificationAsync(endpoint, p256dh, auth, payload, cancellationToken);
                _logger.LogDebug("Push 送信成功: {Endpoint} (試行 {Attempt}/{Max})", endpoint, attempt, MaxRetries);
                return SendResult.Success;
            }
            catch (WebPushGoneException)
            {
                // 410 Gone: 購読を削除してリトライしない
                _logger.LogInformation("Push endpoint が無効 (410 Gone)。購読を削除します: {Endpoint}", endpoint);
                await RemoveSubscriptionAsync(endpoint, cancellationToken);
                return SendResult.Gone;
            }
            catch (WebPushTransientException ex)
            {
                if (attempt >= MaxRetries)
                {
                    _logger.LogWarning(ex, "Push 送信失敗（リトライ上限超過）: {Endpoint}", endpoint);
                    return SendResult.Failure;
                }

                // 指数バックオフ: 1s → 2s → 4s
                var delayMs = (int)Math.Pow(2, attempt - 1) * 1000;
                _logger.LogWarning(ex, "Push 送信失敗（試行 {Attempt}/{Max}）。{Delay}ms 後にリトライ: {Endpoint}",
                    attempt, MaxRetries, delayMs, endpoint);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        return SendResult.Failure;
    }

    /// <summary>endpoint に一致する購読を削除する</summary>
    private async Task RemoveSubscriptionAsync(string endpoint, CancellationToken cancellationToken)
    {
        var sub = await _db.PushSubscriptions
            .FirstOrDefaultAsync(ps => ps.Endpoint == endpoint, cancellationToken);

        if (sub is not null)
        {
            _db.PushSubscriptions.Remove(sub);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
