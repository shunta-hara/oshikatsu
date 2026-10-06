namespace OshikatsuApi.DTOs;

/// <summary>POST /api/v1/internal/push/notify へのリクエスト DTO（オプション）</summary>
public class PushNotifyRequestDto
{
    /// <summary>
    /// カスタムメッセージ（設定時は全購読者へブロードキャスト送信）。
    /// 未設定時は通常の新着通知ジョブを実行する。
    /// DB 容量警告など管理者からのシステム通知に使用する（FEAT-053）。
    /// </summary>
    public string? CustomMessage { get; set; }
}
