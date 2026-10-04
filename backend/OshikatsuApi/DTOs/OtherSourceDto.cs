namespace OshikatsuApi.DTOs;

/// <summary>転載・同一記事の他媒体情報（FEAT-035）</summary>
public class OtherSourceDto
{
    /// <summary>媒体名。取得できない場合は null。</summary>
    public string? SourceName { get; set; }

    /// <summary>収集元 URL</summary>
    public string SourceUrl { get; set; } = null!;
}
