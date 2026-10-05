namespace OshikatsuApi.DTOs;

/// <summary>推し情報レスポンス（FEAT-039: id=user_oshis.id, name=talents.display_name）</summary>
public class OshiResponseDto
{
    /// <summary>UserOshi の ID（user_oshis.id）</summary>
    public int Id { get; set; }

    /// <summary>タレントの表示名（talents.display_name）</summary>
    public string Name { get; set; } = null!;

    /// <summary>登録日時（UTC）</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// タレント ID（talents.id）。
    /// GitHub Actions dispatch の inputs に使用するための内部フィールド。
    /// frontend には公開しない（OpenAPI 定義には含めない）。
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int TalentId { get; set; }
}
