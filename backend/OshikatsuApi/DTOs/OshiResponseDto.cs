namespace OshikatsuApi.DTOs;

/// <summary>推し情報レスポンス</summary>
public class OshiResponseDto
{
    /// <summary>推しの ID</summary>
    public int Id { get; set; }

    /// <summary>推しの名前</summary>
    public string Name { get; set; } = null!;

    /// <summary>登録日時（UTC）</summary>
    public DateTime CreatedAt { get; set; }
}
