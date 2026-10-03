import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { OshiCardList } from "../components/dashboard/OshiCardList";
import type { OshiDashboardCard } from "../types/dashboard";

const card1: OshiDashboardCard = {
  oshiId: 1,
  oshiName: "推しA",
  latestNews: {
    id: 10,
    title: "推しAのニュース",
    sourceUrl: "https://example.com/news/1",
    collectedAt: "2026-08-01T10:00:00+09:00",
  },
  latestProgram: null,
};

const card2: OshiDashboardCard = {
  oshiId: 2,
  oshiName: "推しB",
  latestNews: null,
  latestProgram: {
    id: 20,
    programName: "推しBの番組",
    broadcaster: "テストTV",
    sourceUrl: "https://example.com/prog/1",
    collectedAt: "2026-08-01T09:00:00+09:00",
  },
};

function renderList(cards: OshiDashboardCard[]) {
  return render(
    <MemoryRouter>
      <OshiCardList cards={cards} />
    </MemoryRouter>,
  );
}

describe("OshiCardList", () => {
  // --- 境界値：空リスト ---
  it("cards が空のとき案内メッセージが表示される", () => {
    renderList([]);
    expect(
      screen.getByText("推しを登録するとダッシュボードカードが表示されます。"),
    ).toBeInTheDocument();
  });

  it("cards が空のときリストアイテムは表示されない", () => {
    renderList([]);
    expect(screen.queryByRole("listitem")).not.toBeInTheDocument();
  });

  // --- 正常系 ---
  it("cards が1件あるとき推し名が表示される", () => {
    renderList([card1]);
    expect(screen.getByText("推しA")).toBeInTheDocument();
  });

  it("cards が2件あるときそれぞれの推し名が表示される", () => {
    renderList([card1, card2]);
    expect(screen.getByText("推しA")).toBeInTheDocument();
    expect(screen.getByText("推しB")).toBeInTheDocument();
  });

  it("cards の数だけリストアイテムが表示される", () => {
    renderList([card1, card2]);
    expect(screen.getAllByRole("listitem")).toHaveLength(2);
  });

  it("各カードのニュースタイトルが表示される", () => {
    renderList([card1, card2]);
    expect(screen.getByText("推しAのニュース")).toBeInTheDocument();
  });

  it("各カードの番組名が表示される", () => {
    renderList([card1, card2]);
    expect(screen.getByText("推しBの番組")).toBeInTheDocument();
  });

  // --- 異常系：ニュース・番組なしカードが混在 ---
  it("latestNews が null のカードは「ニュースなし」を表示する", () => {
    renderList([card2]);
    expect(screen.getByText("ニュースなし")).toBeInTheDocument();
  });

  it("latestProgram が null のカードは「出演情報なし」を表示する", () => {
    renderList([card1]);
    expect(screen.getByText("出演情報なし")).toBeInTheDocument();
  });
});
