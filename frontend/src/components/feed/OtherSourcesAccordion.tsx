import { useState } from "react";
import type { OtherSource } from "../../types/feed";

interface OtherSourcesAccordionProps {
  /** まとめた他媒体の件数 */
  count: number;
  /** 他媒体のリンク一覧 */
  sources: OtherSource[];
}

/**
 * 転載記事の他媒体リストを開閉式で表示するコンポーネント（FEAT-035）。
 * count が 0 のときは何も表示しない。
 */
export function OtherSourcesAccordion({
  count,
  sources,
}: OtherSourcesAccordionProps) {
  const [isOpen, setIsOpen] = useState(false);

  if (count === 0) return null;

  return (
    <div className="other-sources">
      <button
        type="button"
        className="other-sources__toggle"
        onClick={() => setIsOpen((prev) => !prev)}
        aria-expanded={isOpen}
      >
        ほか {count} 媒体
        <span className="other-sources__arrow" aria-hidden="true">
          {isOpen ? "▲" : "▼"}
        </span>
      </button>
      {isOpen && (
        <ul className="other-sources__list">
          {sources.map((src) => (
            <li key={src.sourceUrl}>
              <a
                href={src.sourceUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="other-sources__link"
              >
                {src.sourceName ?? src.sourceUrl}
              </a>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
