interface NotificationBadgeProps {
  /** 表示する件数。0 以下の場合はバッジを描画しない */
  count: number;
}

/**
 * 未読件数バッジコンポーネント。
 * count が 1 以上の場合のみバッジを表示する。0 以下の場合は何も描画しない。
 */
export function NotificationBadge({ count }: NotificationBadgeProps) {
  if (count <= 0) {
    return null;
  }

  return (
    <span
      aria-label={`未読 ${count} 件`}
      role="status"
      style={{
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        minWidth: "1.25rem",
        height: "1.25rem",
        padding: "0 0.25rem",
        borderRadius: "9999px",
        backgroundColor: "#e53e3e",
        color: "#fff",
        fontSize: "0.75rem",
        fontWeight: "bold",
        lineHeight: 1,
        marginLeft: "0.5rem",
      }}
    >
      {count}
    </span>
  );
}
