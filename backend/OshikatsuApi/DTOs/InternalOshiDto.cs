namespace OshikatsuApi.DTOs;

/// <summary>collector 内部 API 用: タレント情報レスポンス（GET /api/v1/internal/oshis）</summary>
public class InternalOshiDto
{
    /// <summary>タレントの ID（talents.id）</summary>
    public int TalentId { get; set; }

    /// <summary>タレントの表示名</summary>
    public string Name { get; set; } = null!;
}
