import { useState } from "react";

interface DateRangeFilterProps {
  /** 適用ボタン押下またはクリア時に呼ばれる。
   *  from / to は ISO 8601 JST 文字列（例: "2026-08-01T00:00:00+09:00"）、
   *  未指定の場合は undefined。
   */
  onApply: (from: string | undefined, to: string | undefined) => void;
}

/**
 * 日付範囲フィルタコンポーネント。
 * 開始日・終了日・「期間を適用」・「クリア」を横並びで表示する（狭い画面では折り返す）。
 * 終了日が開始日より前の場合はバリデーションエラーを表示し、onApply は呼ばれない。
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

    // YYYY-MM-DD を ISO 8601 JST 文字列へ変換する。
    // new Date() は実行環境のタイムゾーンに依存するため使わず、
    // +09:00 オフセットを直接付与した文字列を構築する。
    //   from: 当日の JST 開始（00:00:00+09:00）
    //   to:   当日の JST 終了（23:59:59.999+09:00）
    const fromIso = fromDate ? `${fromDate}T00:00:00+09:00` : undefined;
    const toIso = toDate ? `${toDate}T23:59:59.999+09:00` : undefined;

    onApply(fromIso, toIso);
  };

  const handleClear = () => {
    setFromDate("");
    setToDate("");
    setError(null);
    onApply(undefined, undefined);
  };

  return (
    <div className="date-filter-row">
      <div className="date-filter-field">
        <label htmlFor="date-filter-from">開始日</label>
        <input
          id="date-filter-from"
          type="date"
          value={fromDate}
          onChange={(e) => setFromDate(e.target.value)}
        />
      </div>
      <div className="date-filter-field">
        <label htmlFor="date-filter-to">終了日</label>
        <input
          id="date-filter-to"
          type="date"
          value={toDate}
          onChange={(e) => setToDate(e.target.value)}
        />
      </div>
      <button type="button" className="btn-secondary" onClick={handleApply}>
        期間を適用
      </button>
      <button type="button" className="btn-secondary" onClick={handleClear}>
        クリア
      </button>
      {error && (
        <div role="alert" aria-live="polite" className="field-error">
          {error}
        </div>
      )}
    </div>
  );
}
