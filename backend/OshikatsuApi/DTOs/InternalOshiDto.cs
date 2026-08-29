namespace OshikatsuApi.DTOs;

/// <summary>collector 内部 API 用: 推し情報レスポンス</summary>
public class InternalOshiDto
{
    /// <summary>推しの ID</summary>
    public int Id { get; set; }

    /// <summary>推しの名前</summary>
    public string Name { get; set; } = null!;
}
