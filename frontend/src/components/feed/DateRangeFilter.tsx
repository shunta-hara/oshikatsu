import { useState } from "react";

interface DateRangeFilterProps {
  /** 適用ボタン押下またはクリア時に呼ばれる。
   *  from / to は ISO 8601 UTC 文字列（例: "2026-08-01T00:00:00.000Z"）、
   *  未指定の場合は undefined。
   */
  onApply: (from: string | undefined, to: string | undefined) => void;
}

/**
 * 日付範囲フィルタコンポーネント。
 * 開始日・終了日を入力して「期間を適用」を押すと onApply が呼ばれる。
 * 終了日が開始日より前の場合はバリデーションエラーを表示し、onApply は呼ばれない。
 * 「クリア」を押すと onApply(undefined, undefined) が呼ばれる。
 */
export function DateRangeFilter({ onApply }: DateRangeFilterProps) {
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [error, setError] = useState<string | null>(null);

  const handleApply = () => {
    // 両方入力済みで終了日が開始日より前の場合はエラー
    if (fromDate && toDate && fromDate > toDate) {
      setError("終了日は開始日以降の日付を指定してください");
      return;
    }

    setError(null);

    // YYYY-MM-DD を ISO 8601 UTC 文字列へ変換する
    //   from: 当日の開始（00:00:00 UTC）
    //   to:   当日の終了（23:59:59.999 UTC）
    const fromIso = fromDate
      ? new Date(fromDate + "T00:00:00Z").toISOString()
      : undefined;
    const toIso = toDate
      ? new Date(toDate + "T23:59:59.999Z").toISOString()
      : undefined;

    onApply(fromIso, toIso);
  };

  const handleClear = () => {
    setFromDate("");
    setToDate("");
    setError(null);
    onApply(undefined, undefined);
  };

  return (
    <div>
      <label htmlFor="date-filter-from">開始日</label>
      <input
        id="date-filter-from"
        type="date"
        value={fromDate}
        onChange={(e) => setFromDate(e.target.value)}
      />
      <label htmlFor="date-filter-to">終了日</label>
      <input
        id="date-filter-to"
        type="date"
        value={toDate}
        onChange={(e) => setToDate(e.target.value)}
      />
      <button type="button" onClick={handleApply}>
        期間を適用
      </button>
      <button type="button" onClick={handleClear}>
        クリア
      </button>
      {error && (
        <div role="alert" aria-live="polite">
          {error}
        </div>
      )}
    </div>
  );
}
