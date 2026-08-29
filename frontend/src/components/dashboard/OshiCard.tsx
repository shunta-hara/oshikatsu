import { Link } from "react-router-dom";
import type { OshiDashboardCard } from "../../types/dashboard";

interface OshiCardProps {
  card: OshiDashboardCard;
}

/**
 * 推し1人分のダッシュボードカード。
 * カードをクリックするとその推しで絞り込んだフィード一覧に遷移する（oshiId クエリパラメータ）。
 * 最新ニュース・最新番組がない場合は「なし」メッセージを表示する。
 */
export function OshiCard({ card }: OshiCardProps) {
  return (
    <article aria-label={`${card.oshiName}のダッシュボードカード`}>
      <Link to={`/?oshiId=${card.oshiId}`}>
        <h3>{card.oshiName}</h3>
      </Link>

      <section aria-labelledby={`news-heading-${card.oshiId}`}>
        <h4 id={`news-heading-${card.oshiId}`}>最新ニュース</h4>
        {card.latestNews !== null ? (
          <a
            href={card.latestNews.sourceUrl}
            target="_blank"
            rel="noopener noreferrer"
          >
            {card.latestNews.title}
          </a>
        ) : (
          <p>ニュースなし</p>
        )}
      </section>

      <section aria-labelledby={`program-heading-${card.oshiId}`}>
        <h4 id={`program-heading-${card.oshiId}`}>最新番組</h4>
        {card.latestProgram !== null ? (
          <a
            href={card.latestProgram.sourceUrl}
            target="_blank"
            rel="noopener noreferrer"
          >
            {card.latestProgram.programName}
          </a>
        ) : (
          <p>番組情報なし</p>
        )}
      </section>
    </article>
  );
}
