import { buildGguideUrl, buildJcomUrl } from "../../utils/tvGuideUrls";

interface TvGuideLinksProps {
  /** 番組表サイトの検索クエリに使う推しの名前 */
  oshiName: string;
}

/**
 * 推しの名前で外部番組表サイトを検索するリンクを表示するコンポーネント。
 * Gガイド・J:COM TV ガイドの2つのリンクを提供する。
 * データの取得・保存は行わず、URL 生成のみ行う。
 */
export function TvGuideLinks({ oshiName }: TvGuideLinksProps) {
  return (
    <div className="tv-guide-links">
      <span className="tv-guide-links__label">番組表で探す：</span>
      <a
        href={buildGguideUrl(oshiName)}
        target="_blank"
        rel="noopener noreferrer"
        className="tv-guide-links__link"
      >
        Gガイド
      </a>
      <a
        href={buildJcomUrl(oshiName)}
        target="_blank"
        rel="noopener noreferrer"
        className="tv-guide-links__link"
      >
        J:COM
      </a>
    </div>
  );
}
