namespace OshikatsuApi.DTOs;

/// <summary>マルチ推しダッシュボード用の推しカードDTO（推し名・最新ニュース・最新番組を含む）</summary>
public class OshiDashboardCardDto
{
    public int OshiId { get; set; }
    public string OshiName { get; set; } = null!;
    public NewsItemSummaryDto? LatestNews { get; set; }
    public ProgramAppearanceSummaryDto? LatestProgram { get; set; }
}
