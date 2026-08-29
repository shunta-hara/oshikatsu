namespace OshikatsuApi.DTOs;

/// <summary>未読件数レスポンス DTO（ニュース＋出演番組の合計）</summary>
public class UnreadCountResponseDto
{
    /// <summary>前回閲覧以降に追加されたニュース・出演番組の合計件数</summary>
    public int UnreadCount { get; set; }
}
