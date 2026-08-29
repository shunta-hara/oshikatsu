namespace OshikatsuApi.DTOs;

/// <summary>ページネーション付きレスポンス汎用DTO</summary>
public class PaginatedResponseDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0
        ? (int)Math.Ceiling((double)TotalCount / PageSize)
        : 0;
}
