using System.ComponentModel.DataAnnotations;

namespace OshikatsuApi.DTOs;

/// <summary>推し登録リクエスト</summary>
public class CreateOshiRequestDto
{
    /// <summary>推しの名前（1〜100文字）</summary>
    [Required(ErrorMessage = "Name is required.")]
    [MinLength(1, ErrorMessage = "Name must not be empty.")]
    [MaxLength(100, ErrorMessage = "Name must be 100 characters or fewer.")]
    public string Name { get; set; } = null!;
}
