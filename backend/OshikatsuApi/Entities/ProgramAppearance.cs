namespace OshikatsuApi.Entities;

/// <summary>
/// 出演番組エンティティ（program_appearances テーブル）
/// </summary>
public class ProgramAppearance
{
    public int Id { get; set; }

    /// <summary>紐づく推しID（後方互換のため残存）</summary>
    public int OshiId { get; set; }

    /// <summary>紐づくタレントID（talents テーブルへの外部キー）</summary>
    public int? TalentId { get; set; }

    /// <summary>番組名</summary>
    public string ProgramName { get; set; } = null!;

    /// <summary>放送局（任意）</summary>
    public string? Broadcaster { get; set; }

    /// <summary>放送日時（取得できた場合）</summary>
    public DateTimeOffset? AiredAt { get; set; }

    /// <summary>
    /// 放送日時が推定値かどうかのフラグ。
    /// true = 取得できなかったため収集日時で代替、false = 実際の放送日時
    /// </summary>
    public bool IsAiredAtEstimated { get; set; }

    /// <summary>収集元URL（ソースURL）</summary>
    public string SourceUrl { get; set; } = null!;

    /// <summary>collector が収集した日時（UTC）</summary>
    public DateTimeOffset CollectedAt { get; set; }

    /// <summary>媒体名（例: Weverse）。collector が RSS から抽出する。任意。</summary>
    public string? SourceName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Oshi Oshi { get; set; } = null!;
    public Talent? Talent { get; set; }
}
