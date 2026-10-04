import { Link } from "react-router-dom";
import type { OshiDashboardCard } from "../../types/dashboard";
import { OtherSourcesAccordion } from "../feed/OtherSourcesAccordion";

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
    <article
      className="card oshi-card"
      aria-label={`${card.oshiName}のダッシュボードカード`}
    >
      <Link to={`/?oshiId=${card.oshiId}`} className="oshi-card__name-link">
        {card.oshiName}
      </Link>

      <section
        className="oshi-card__section"
        aria-labelledby={`news-heading-${card.oshiId}`}
      >
        <h4
          id={`news-heading-${card.oshiId}`}
          className="oshi-card__section-title"
        >
          最新ニュース
        </h4>
        {card.latestNews !== null ? (
          <>
            <a
              className="oshi-card__content-link"
              href={card.latestNews.sourceUrl}
              target="_blank"
              rel="noopener noreferrer"
            >
              {card.latestNews.title}
            </a>
            {card.latestNews.sourceName && (
              <span className="oshi-card__source-name">
                {card.latestNews.sourceName}
              </span>
            )}
            <OtherSourcesAccordion
              count={card.latestNews.otherSourceCount ?? 0}
              sources={card.latestNews.otherSources ?? []}
            />
          </>
        ) : (
          <p className="oshi-card__none">ニュースなし</p>
        )}
      </section>

      <section
        className="oshi-card__section"
        aria-labelledby={`program-heading-${card.oshiId}`}
      >
        <h4
          id={`program-heading-${card.oshiId}`}
          className="oshi-card__section-title"
        >
          最新出演情報
        </h4>
        {card.latestProgram !== null ? (
          <>
            <a
              className="oshi-card__content-link"
              href={card.latestProgram.sourceUrl}
              target="_blank"
              rel="noopener noreferrer"
            >
              {card.latestProgram.programName}
            </a>
            {card.latestProgram.sourceName && (
              <span className="oshi-card__source-name">
                {card.latestProgram.sourceName}
              </span>
            )}
            <OtherSourcesAccordion
              count={card.latestProgram.otherSourceCount ?? 0}
              sources={card.latestProgram.otherSources ?? []}
            />
          </>
        ) : (
          <p className="oshi-card__none">出演情報なし</p>
        )}
      </section>
    </article>
  );
}
