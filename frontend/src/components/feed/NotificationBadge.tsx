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
    <span aria-label={`未読 ${count} 件`} role="status" className="badge">
      {count}
    </span>
  );
}
