using OshikatsuApi.DTOs;

namespace OshikatsuApi.Services;

/// <summary>マルチ推しダッシュボード集計を担う</summary>
public interface IDashboardService
{
    /// <summary>
    /// ユーザーの推し全員分のダッシュボードカードを返す。
    /// 各カードには推し名・最新ニュース1件・最新出演番組1件が含まれる。
    /// N+1 クエリを避けるため、推し・ニュース・番組を 3 本のクエリで一括取得する。
    /// </summary>
    Task<IReadOnlyList<OshiDashboardCardDto>> GetSummaryAsync(int userId);
}
