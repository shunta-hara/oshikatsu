import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { NewsFeedList } from "../components/feed/NewsFeedList";
import * as feedApi from "../api/feed";
import { ApiError } from "../api/client";
import type { NewsPaginatedResponse } from "../types/feed";

vi.mock("../api/feed");
const mockGetNews = vi.mocked(feedApi.getNews);

function makeNewsPage(
  overrides: Partial<NewsPaginatedResponse> = {},
): NewsPaginatedResponse {
  return {
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 20,
    totalPages: 0,
    ...overrides,
  };
}

describe("NewsFeedList", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  // ── 正常系 ────────────────────────────────────────────────────────────

  it("ニュースアイテムがタイトルとして表示される", async () => {
    mockGetNews.mockResolvedValue(
      makeNewsPage({
        items: [
          {
            id: 1,
            oshiId: 1,
            oshiName: "推しA",
            title: "テストニュース記事",
            summary: null,
            sourceUrl: "https://example.com/news/1",
            publishedAt: null,
            isPublishedAtEstimated: false,
            collectedAt: "2026-07-16T10:00:00+09:00",
          },
        ],
        totalCount: 1,
        totalPages: 1,
      }),
    );

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(screen.getByText("テストニュース記事")).toBeInTheDocument();
    });
  });

  it("ニュースアイテムの出典URLがリンクとして新しいタブで開く設定になっている", async () => {
    mockGetNews.mockResolvedValue(
      makeNewsPage({
        items: [
          {
            id: 1,
            oshiId: 1,
            oshiName: "推しA",
            title: "テストニュース",
            summary: null,
            sourceUrl: "https://example.com/news/1",
            publishedAt: null,
            isPublishedAtEstimated: false,
            collectedAt: "2026-07-16T10:00:00+09:00",
          },
        ],
        totalCount: 1,
        totalPages: 1,
      }),
    );

    render(<NewsFeedList />);

    await waitFor(() => {
      const link = screen.getByRole("link", { name: "テストニュース" });
      expect(link).toHaveAttribute("href", "https://example.com/news/1");
      expect(link).toHaveAttribute("target", "_blank");
      expect(link).toHaveAttribute("rel", "noopener noreferrer");
    });
  });

  it("推し名と収集日時が表示される", async () => {
    mockGetNews.mockResolvedValue(
      makeNewsPage({
        items: [
          {
            id: 1,
            oshiId: 1,
            oshiName: "テスト推し",
            title: "ニュース",
            summary: null,
            sourceUrl: "https://example.com/news/1",
            publishedAt: null,
            isPublishedAtEstimated: false,
            collectedAt: "2026-07-16T10:00:00+09:00",
          },
        ],
        totalCount: 1,
        totalPages: 1,
      }),
    );

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(screen.getByText("テスト推し")).toBeInTheDocument();
    });
  });

  it("複数件あるとき全アイテムが表示される", async () => {
    mockGetNews.mockResolvedValue(
      makeNewsPage({
        items: [
          {
            id: 1,
            oshiId: 1,
            oshiName: "推しA",
            title: "ニュース1",
            summary: null,
            sourceUrl: "https://example.com/1",
            publishedAt: null,
            isPublishedAtEstimated: false,
            collectedAt: "2026-07-16T10:00:00+09:00",
          },
          {
            id: 2,
            oshiId: 1,
            oshiName: "推しA",
            title: "ニュース2",
            summary: null,
            sourceUrl: "https://example.com/2",
            publishedAt: null,
            isPublishedAtEstimated: false,
            collectedAt: "2026-07-16T09:00:00+09:00",
          },
        ],
        totalCount: 2,
        totalPages: 1,
      }),
    );

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(screen.getByText("ニュース1")).toBeInTheDocument();
      expect(screen.getByText("ニュース2")).toBeInTheDocument();
    });
  });

  it("ページネーションが複数ページある場合に表示される", async () => {
    mockGetNews.mockResolvedValue(
      makeNewsPage({
        items: Array.from({ length: 20 }, (_, i) => ({
          id: i + 1,
          oshiId: 1,
          oshiName: "推しA",
          title: `ニュース${i + 1}`,
          summary: null,
          sourceUrl: `https://example.com/${i + 1}`,
          publishedAt: null,
          isPublishedAtEstimated: false,
          collectedAt: "2026-07-16T10:00:00+09:00",
        })),
        totalCount: 25,
        totalPages: 2,
      }),
    );

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(screen.getByRole("navigation")).toBeInTheDocument();
    });
  });

  it("次のページへ遷移できる", async () => {
    const user = userEvent.setup();
    const page1Data = makeNewsPage({
      items: [
        {
          id: 1,
          oshiId: 1,
          oshiName: "推しA",
          title: "1ページ目のニュース",
          summary: null,
          sourceUrl: "https://example.com/1",
          publishedAt: null,
          isPublishedAtEstimated: false,
          collectedAt: "2026-07-16T10:00:00+09:00",
        },
      ],
      totalCount: 25,
      totalPages: 2,
      page: 1,
    });
    const page2Data = makeNewsPage({
      items: [
        {
          id: 21,
          oshiId: 1,
          oshiName: "推しA",
          title: "2ページ目のニュース",
          summary: null,
          sourceUrl: "https://example.com/21",
          publishedAt: null,
          isPublishedAtEstimated: false,
          collectedAt: "2026-07-15T10:00:00+09:00",
        },
      ],
      totalCount: 25,
      totalPages: 2,
      page: 2,
    });
    mockGetNews
      .mockResolvedValueOnce(page1Data)
      .mockResolvedValueOnce(page2Data);

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(screen.getByText("1ページ目のニュース")).toBeInTheDocument();
    });

    await user.click(screen.getByLabelText("次のページ"));

    await waitFor(() => {
      expect(screen.getByText("2ページ目のニュース")).toBeInTheDocument();
    });
  });

  // ── 異常系 ────────────────────────────────────────────────────────────

  it("APIエラー時にエラーメッセージが表示される", async () => {
    mockGetNews.mockRejectedValue(new ApiError(500, "Internal Server Error"));

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
    });
  });

  it("ネットワークエラー時にエラーメッセージが表示される", async () => {
    mockGetNews.mockRejectedValue(new Error("Network Error"));

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(
        screen.getByText("ニュースの取得に失敗しました"),
      ).toBeInTheDocument();
    });
  });

  // ── 境界値 ─────────────────────────────────────────────────────────────

  it("データが0件のとき空状態メッセージが表示される", async () => {
    mockGetNews.mockResolvedValue(makeNewsPage());

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(
        screen.getByText("まだニュース情報がありません"),
      ).toBeInTheDocument();
    });
  });

  it("ローディング中はテキストが表示される", () => {
    // 解決しない Promise でローディング状態を維持する
    mockGetNews.mockImplementation(() => new Promise(() => undefined));

    render(<NewsFeedList />);

    expect(screen.getByText("読み込み中...")).toBeInTheDocument();
  });

  // ── キーワード検索 ────────────────────────────────────────────────────

  it("検索ボックスが表示される", async () => {
    mockGetNews.mockResolvedValue(makeNewsPage());

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(screen.getByRole("searchbox")).toBeInTheDocument();
    });
  });

  it("キーワードを入力して検索するとクエリ付きで getNews が呼ばれる", async () => {
    const user = userEvent.setup();
    // 初回ロード: 空
    mockGetNews.mockResolvedValueOnce(makeNewsPage());
    // 検索後: 空（0件）
    mockGetNews.mockResolvedValueOnce(makeNewsPage());

    render(<NewsFeedList />);

    // 初回ロード完了を待つ
    await waitFor(() => {
      expect(mockGetNews).toHaveBeenCalledOnce();
    });

    await user.type(screen.getByRole("searchbox"), "テスト");
    await user.click(screen.getByRole("button", { name: "検索" }));

    await waitFor(() => {
      expect(mockGetNews).toHaveBeenCalledWith(
        1,
        20,
        undefined,
        "テスト",
        undefined,
        undefined,
      );
    });
  });

  it("検索結果が0件のとき「該当するニュースが見つかりません」が表示される", async () => {
    const user = userEvent.setup();
    mockGetNews.mockResolvedValue(makeNewsPage());

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(mockGetNews).toHaveBeenCalledOnce();
    });

    await user.type(screen.getByRole("searchbox"), "ヒットしないワード");
    await user.click(screen.getByRole("button", { name: "検索" }));

    await waitFor(() => {
      expect(
        screen.getByText("該当するニュースが見つかりません"),
      ).toBeInTheDocument();
    });
  });

  it("クリア後はクエリなしで getNews が呼ばれ全件一覧に戻る", async () => {
    const user = userEvent.setup();
    // 初回ロード
    mockGetNews.mockResolvedValueOnce(makeNewsPage());
    // 検索後（0件）
    mockGetNews.mockResolvedValueOnce(makeNewsPage());
    // クリア後（全件）
    mockGetNews.mockResolvedValueOnce(
      makeNewsPage({
        items: [
          {
            id: 1,
            oshiId: 1,
            oshiName: "推しA",
            title: "全件表示のニュース",
            summary: null,
            sourceUrl: "https://example.com/1",
            publishedAt: null,
            isPublishedAtEstimated: false,
            collectedAt: "2026-08-29T10:00:00+09:00",
          },
        ],
        totalCount: 1,
        totalPages: 1,
      }),
    );

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(mockGetNews).toHaveBeenCalledOnce();
    });

    // 検索する
    await user.type(screen.getByRole("searchbox"), "テスト");
    await user.click(screen.getByRole("button", { name: "検索" }));

    await waitFor(() => {
      expect(
        screen.getByText("該当するニュースが見つかりません"),
      ).toBeInTheDocument();
    });

    // クリアする
    await user.click(screen.getByRole("button", { name: "検索をクリア" }));

    await waitFor(() => {
      expect(screen.getByText("全件表示のニュース")).toBeInTheDocument();
    });

    // クリア後は query なしで呼ばれる（from/to も undefined）
    expect(mockGetNews).toHaveBeenLastCalledWith(
      1,
      20,
      undefined,
      undefined,
      undefined,
      undefined,
    );
  });

  // ── 日付範囲フィルタ ──────────────────────────────────────────────────

  it("日付範囲フィルタが表示される", async () => {
    mockGetNews.mockResolvedValue(makeNewsPage());

    render(<NewsFeedList />);

    // 開始日・終了日の入力フィールドが存在する
    expect(screen.getByLabelText("開始日")).toBeInTheDocument();
    expect(screen.getByLabelText("終了日")).toBeInTheDocument();
  });

  it("日付範囲を適用するとfrom/to付きでgetNewsが呼ばれる", async () => {
    const user = userEvent.setup();
    // 初回ロード
    mockGetNews.mockResolvedValueOnce(makeNewsPage());
    // 日付フィルタ適用後
    mockGetNews.mockResolvedValueOnce(makeNewsPage());

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(mockGetNews).toHaveBeenCalledOnce();
    });

    fireEvent.change(screen.getByLabelText("開始日"), {
      target: { value: "2026-08-01" },
    });
    fireEvent.change(screen.getByLabelText("終了日"), {
      target: { value: "2026-08-31" },
    });
    await user.click(screen.getByRole("button", { name: "期間を適用" }));

    await waitFor(() => {
      expect(mockGetNews).toHaveBeenLastCalledWith(
        1,
        20,
        undefined,
        undefined,
        "2026-08-01T00:00:00+09:00",
        "2026-08-31T23:59:59.999+09:00",
      );
    });
  });

  it("日付フィルタをクリアするとfrom/toなしでgetNewsが呼ばれる", async () => {
    const user = userEvent.setup();
    mockGetNews.mockResolvedValue(makeNewsPage());

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(mockGetNews).toHaveBeenCalledOnce();
    });

    // 日付を入力してから適用
    fireEvent.change(screen.getByLabelText("開始日"), {
      target: { value: "2026-08-01" },
    });
    await user.click(screen.getByRole("button", { name: "期間を適用" }));

    await waitFor(() => {
      expect(mockGetNews).toHaveBeenCalledTimes(2);
    });

    // クリアする
    await user.click(screen.getByRole("button", { name: "クリア" }));

    await waitFor(() => {
      expect(mockGetNews).toHaveBeenLastCalledWith(
        1,
        20,
        undefined,
        undefined,
        undefined,
        undefined,
      );
    });
  });

  it("日付範囲フィルタで0件のとき「該当するニュースが見つかりません」が表示される", async () => {
    const user = userEvent.setup();
    // 初回ロード（全件）
    mockGetNews.mockResolvedValueOnce(makeNewsPage());
    // 日付フィルタ後（0件）
    mockGetNews.mockResolvedValueOnce(makeNewsPage());

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(mockGetNews).toHaveBeenCalledOnce();
    });

    fireEvent.change(screen.getByLabelText("開始日"), {
      target: { value: "2020-01-01" },
    });
    fireEvent.change(screen.getByLabelText("終了日"), {
      target: { value: "2020-01-31" },
    });
    await user.click(screen.getByRole("button", { name: "期間を適用" }));

    await waitFor(() => {
      expect(
        screen.getByText("該当するニュースが見つかりません"),
      ).toBeInTheDocument();
    });
  });

  // ── 媒体名・他媒体（FEAT-035, FEAT-036） ──────────────────────────────

  it("sourceName があるとき媒体名が表示される", async () => {
    mockGetNews.mockResolvedValue(
      makeNewsPage({
        items: [
          {
            id: 1,
            oshiId: 1,
            oshiName: "推しA",
            title: "テストニュース",
            summary: null,
            sourceUrl: "https://example.com/news/1",
            publishedAt: null,
            isPublishedAtEstimated: false,
            collectedAt: "2026-07-16T10:00:00+09:00",
            sourceName: "スポニチ Sponichi Annex",
            otherSourceCount: 0,
            otherSources: [],
          },
        ],
        totalCount: 1,
        totalPages: 1,
      }),
    );

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(screen.getByText("スポニチ Sponichi Annex")).toBeInTheDocument();
    });
  });

  it("sourceName が null のとき媒体名欄が表示されない", async () => {
    mockGetNews.mockResolvedValue(
      makeNewsPage({
        items: [
          {
            id: 1,
            oshiId: 1,
            oshiName: "推しA",
            title: "テストニュース",
            summary: null,
            sourceUrl: "https://example.com/news/1",
            publishedAt: null,
            isPublishedAtEstimated: false,
            collectedAt: "2026-07-16T10:00:00+09:00",
            sourceName: null,
            otherSourceCount: 0,
            otherSources: [],
          },
        ],
        totalCount: 1,
        totalPages: 1,
      }),
    );

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(screen.getByText("テストニュース")).toBeInTheDocument();
    });

    // 媒体名クラスの要素が存在しない
    expect(
      document.querySelector(".feed-card__source-name"),
    ).not.toBeInTheDocument();
  });

  it("otherSourceCount が 1 以上のとき「ほか N 媒体」ボタンが表示される", async () => {
    mockGetNews.mockResolvedValue(
      makeNewsPage({
        items: [
          {
            id: 1,
            oshiId: 1,
            oshiName: "推しA",
            title: "テストニュース",
            summary: null,
            sourceUrl: "https://example.com/news/1",
            publishedAt: null,
            isPublishedAtEstimated: false,
            collectedAt: "2026-07-16T10:00:00+09:00",
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
        ],
        totalCount: 1,
        totalPages: 1,
      }),
    );

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: /ほか 2 媒体/ }),
      ).toBeInTheDocument();
    });
  });

  it("「ほか N 媒体」を開くと他媒体リンクが新しいタブで表示される", async () => {
    const user = userEvent.setup();
    mockGetNews.mockResolvedValue(
      makeNewsPage({
        items: [
          {
            id: 1,
            oshiId: 1,
            oshiName: "推しA",
            title: "テストニュース",
            summary: null,
            sourceUrl: "https://example.com/news/1",
            publishedAt: null,
            isPublishedAtEstimated: false,
            collectedAt: "2026-07-16T10:00:00+09:00",
            sourceName: "スポニチ",
            otherSourceCount: 1,
            otherSources: [
              {
                sourceName: "Yahoo!ニュース",
                sourceUrl: "https://news.yahoo.co.jp/1",
              },
            ],
          },
        ],
        totalCount: 1,
        totalPages: 1,
      }),
    );

    render(<NewsFeedList />);

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: /ほか 1 媒体/ }),
      ).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: /ほか 1 媒体/ }));

    const link = screen.getByRole("link", { name: "Yahoo!ニュース" });
    expect(link).toHaveAttribute("href", "https://news.yahoo.co.jp/1");
    expect(link).toHaveAttribute("target", "_blank");
    expect(link).toHaveAttribute("rel", "noopener noreferrer");
  });
});
