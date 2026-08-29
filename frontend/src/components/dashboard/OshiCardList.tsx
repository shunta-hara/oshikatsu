import type { OshiDashboardCard } from "../../types/dashboard";
import { OshiCard } from "./OshiCard";

interface OshiCardListProps {
  cards: OshiDashboardCard[];
}

/**
 * 推し全員のダッシュボードカードをリスト表示するコンポーネント。
 * 推しが登録されていない場合は案内メッセージを表示する。
 */
export function OshiCardList({ cards }: OshiCardListProps) {
  if (cards.length === 0) {
    return <p>推しを登録するとダッシュボードカードが表示されます。</p>;
  }

  return (
    <ul>
      {cards.map((card) => (
        <li key={card.oshiId}>
          <OshiCard card={card} />
        </li>
      ))}
    </ul>
  );
}
