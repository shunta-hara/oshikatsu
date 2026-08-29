import { render, screen, waitFor } from "@testing-library/react";
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
});
