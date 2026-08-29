using System.ComponentModel.DataAnnotations;

namespace OshikatsuApi.DTOs;

/// <summary>ユーザー登録リクエスト</summary>
public class RegisterRequestDto
{
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string Email { get; set; } = null!;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = null!;
}
