/** ソースタイプ絞り込みチップ（FEAT-051） */

/** フィルタの選択値。undefined = すべて */
export type SourceTypeValue =
  undefined | "news" | "youtube" | "x_post" | "event";

interface SourceTypeOption {
  value: SourceTypeValue;
  label: string;
  /** アイコン（emoji または短テキスト） */
  icon: string;
}

const SOURCE_TYPE_OPTIONS: SourceTypeOption[] = [
  { value: undefined, label: "すべて", icon: "📋" },
  { value: "news", label: "ニュース", icon: "📰" },
  { value: "youtube", label: "YouTube", icon: "▶" },
  { value: "x_post", label: "X投稿", icon: "𝕏" },
  { value: "event", label: "イベント", icon: "🎟" },
];

interface SourceTypeFilterProps {
  /** 現在の選択値。undefined = すべて */
  selected: SourceTypeValue;
  /** 選択変更時のコールバック */
  onChange: (value: SourceTypeValue) => void;
}

/**
 * ニュースフィードのソースタイプ絞り込みチップ。
 * 「すべて」「ニュース」「YouTube」「X投稿」「イベント」の選択肢を表示する。
 */
export function SourceTypeFilter({
  selected,
  onChange,
}: SourceTypeFilterProps) {
  return (
    <div
      role="group"
      aria-label="ソースタイプで絞り込む"
      className="source-type-filter"
    >
      {SOURCE_TYPE_OPTIONS.map((opt) => {
        const isActive = selected === opt.value;
        return (
          <button
            key={opt.value ?? "all"}
            type="button"
            role="radio"
            aria-checked={isActive}
            className={`source-type-chip${isActive ? " source-type-chip--active" : ""}`}
            onClick={() => onChange(opt.value)}
          >
            <span className="source-type-chip__icon" aria-hidden="true">
              {opt.icon}
            </span>
            {opt.label}
          </button>
        );
      })}
    </div>
  );
}
