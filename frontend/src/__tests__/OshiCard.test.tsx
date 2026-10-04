import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { OshiCard } from "../components/dashboard/OshiCard";
import type { OshiDashboardCard } from "../types/dashboard";

const cardWithAll: OshiDashboardCard = {
  oshiId: 1,
  oshiName: "推しA",
  latestNews: {
    id: 10,
    title: "最新ニュースのタイトル",
    sourceUrl: "https://example.com/news/1",
    collectedAt: "2026-08-01T10:00:00+09:00",
  },
  latestProgram: {
    id: 20,
    programName: "テスト番組",
    broadcaster: "テストTV",
    sourceUrl: "https://example.com/prog/1",
    collectedAt: "2026-08-01T09:00:00+09:00",
  },
};

const cardNoNews: OshiDashboardCard = {
  oshiId: 2,
  oshiName: "推しB",
  latestNews: null,
  latestProgram: {
    id: 21,
    programName: "番組B",
    broadcaster: null,
    sourceUrl: "https://example.com/prog/2",
    collectedAt: "2026-08-01T08:00:00+09:00",
  },
};

const cardNoProgram: OshiDashboardCard = {
  oshiId: 3,
  oshiName: "推しC",
  latestNews: {
    id: 11,
    title: "ニュースC",
    sourceUrl: "https://example.com/news/2",
    collectedAt: "2026-08-01T07:00:00+09:00",
  },
  latestProgram: null,
};

const cardEmpty: OshiDashboardCard = {
  oshiId: 4,
  oshiName: "推しD",
  latestNews: null,
  latestProgram: null,
};

function renderCard(card: OshiDashboardCard) {
  return render(
    <MemoryRouter>
      <OshiCard card={card} />
    </MemoryRouter>,
  );
}

describe("OshiCard", () => {
  // --- 正常系 ---
  it("推し名が表示される", () => {
    renderCard(cardWithAll);
    expect(screen.getByText("推しA")).toBeInTheDocument();
  });

  it("最新ニュースのタイトルが表示される", () => {
    renderCard(cardWithAll);
    expect(screen.getByText("最新ニュースのタイトル")).toBeInTheDocument();
  });

  it("最新番組名が表示される", () => {
    renderCard(cardWithAll);
    expect(screen.getByText("テスト番組")).toBeInTheDocument();
  });

  it("推し名はoshiIdクエリパラメータ付きのリンクになっている", () => {
    renderCard(cardWithAll);
    const link = screen.getByRole("link", { name: "推しA" });
    expect(link).toHaveAttribute("href", "/?oshiId=1");
  });

  it("ニュースリンクは sourceUrl に向いている", () => {
    renderCard(cardWithAll);
    const newsLink = screen.getByRole("link", {
      name: "最新ニュースのタイトル",
    });
    expect(newsLink).toHaveAttribute("href", "https://example.com/news/1");
  });

  it("ニュースリンクは新しいタブで開く（target=_blank）", () => {
    renderCard(cardWithAll);
    const newsLink = screen.getByRole("link", {
      name: "最新ニュースのタイトル",
    });
    expect(newsLink).toHaveAttribute("target", "_blank");
  });

  it("番組リンクは sourceUrl に向いている", () => {
    renderCard(cardWithAll);
    const programLink = screen.getByRole("link", { name: "テスト番組" });
    expect(programLink).toHaveAttribute("href", "https://example.com/prog/1");
  });

  it("番組リンクは新しいタブで開く（target=_blank）", () => {
    renderCard(cardWithAll);
    const programLink = screen.getByRole("link", { name: "テスト番組" });
    expect(programLink).toHaveAttribute("target", "_blank");
  });

  // --- 境界値：ニュースなし ---
  it("latestNews が null のとき「ニュースなし」が表示される", () => {
    renderCard(cardNoNews);
    expect(screen.getByText("ニュースなし")).toBeInTheDocument();
  });

  it("latestNews が null のときニュースリンクは表示されない", () => {
    renderCard(cardNoNews);
    expect(
      screen.queryByRole("link", { name: /ニュース/ }),
    ).not.toBeInTheDocument();
  });

  // --- 境界値：番組なし ---
  it("latestProgram が null のとき「出演情報なし」が表示される", () => {
    renderCard(cardNoProgram);
    expect(screen.getByText("出演情報なし")).toBeInTheDocument();
  });

  it("latestProgram が null のとき番組リンクは表示されない", () => {
    renderCard(cardNoProgram);
    expect(
      screen.queryByRole("link", { name: /番組/ }),
    ).not.toBeInTheDocument();
  });

  // --- 境界値：ニュースも番組もなし ---
  it("ニュースも出演情報もないとき両方の「なし」メッセージが表示される", () => {
    renderCard(cardEmpty);
    expect(screen.getByText("ニュースなし")).toBeInTheDocument();
    expect(screen.getByText("出演情報なし")).toBeInTheDocument();
  });

  // --- 異常系：他の推しIDのリンクと混同しない ---
  it("oshiId=2 のカードは /?oshiId=2 にリンクする", () => {
    renderCard(cardNoNews);
    const link = screen.getByRole("link", { name: "推しB" });
    expect(link).toHaveAttribute("href", "/?oshiId=2");
  });

  // --- 媒体名・他媒体（FEAT-035, FEAT-036） ---
  it("latestNews に sourceName があるとき媒体名が表示される", () => {
    const card: OshiDashboardCard = {
      oshiId: 1,
      oshiName: "推しA",
      latestNews: {
        id: 10,
        title: "最新ニュース",
        sourceUrl: "https://example.com/news/1",
        collectedAt: "2026-08-01T10:00:00+09:00",
        sourceName: "スポニチ",
        otherSourceCount: 0,
        otherSources: [],
      },
      latestProgram: null,
    };
    renderCard(card);
    expect(screen.getByText("スポニチ")).toBeInTheDocument();
  });

  it("latestNews の sourceName が null のとき媒体名が表示されない", () => {
    const card: OshiDashboardCard = {
      oshiId: 1,
      oshiName: "推しA",
      latestNews: {
        id: 10,
        title: "最新ニュース",
        sourceUrl: "https://example.com/news/1",
        collectedAt: "2026-08-01T10:00:00+09:00",
        sourceName: null,
        otherSourceCount: 0,
        otherSources: [],
      },
      latestProgram: null,
    };
    renderCard(card);
    expect(
      document.querySelector(".oshi-card__source-name"),
    ).not.toBeInTheDocument();
  });

  it("latestNews に otherSourceCount > 0 のとき「ほか N 媒体」ボタンが表示される", () => {
    const card: OshiDashboardCard = {
      oshiId: 1,
      oshiName: "推しA",
      latestNews: {
        id: 10,
        title: "最新ニュース",
        sourceUrl: "https://example.com/news/1",
        collectedAt: "2026-08-01T10:00:00+09:00",
        sourceName: "スポニチ",
        otherSourceCount: 2,
        otherSources: [
          {
            sourceName: "Yahoo!ニュース",
            sourceUrl: "https://news.yahoo.co.jp/1",
          },
          {
            sourceName: "Infoseek",
            sourceUrl: "https://news.infoseek.co.jp/1",
          },
        ],
      },
      latestProgram: null,
    };
    renderCard(card);
    expect(
      screen.getByRole("button", { name: /ほか 2 媒体/ }),
    ).toBeInTheDocument();
  });

  it("latestProgram に sourceName があるとき媒体名が表示される", () => {
    const card: OshiDashboardCard = {
      oshiId: 1,
      oshiName: "推しA",
      latestNews: null,
      latestProgram: {
        id: 20,
        programName: "テスト番組",
        broadcaster: null,
        sourceUrl: "https://example.com/prog/1",
        collectedAt: "2026-08-01T09:00:00+09:00",
        sourceName: "ステージナタリー",
        otherSourceCount: 0,
        otherSources: [],
      },
    };
    renderCard(card);
    expect(screen.getByText("ステージナタリー")).toBeInTheDocument();
  });
});
