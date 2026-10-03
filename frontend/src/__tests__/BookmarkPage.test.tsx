import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { BookmarkPage } from "../pages/BookmarkPage";
import * as bookmarkApi from "../api/bookmark";
import * as authApi from "../api/auth";
import { ApiError } from "../api/client";
import { AuthProvider } from "../context/AuthContext";
import type { BookmarksPaginatedResponse } from "../types/bookmark";

vi.mock("../api/bookmark");
vi.mock("../api/auth");
const mockGetBookmarks = vi.mocked(bookmarkApi.getBookmarks);
// BookmarkButton 内で addBookmark/removeBookmark が呼ばれる可能性があるためモック
vi.mocked(bookmarkApi.addBookmark).mockResolvedValue(undefined);
vi.mocked(bookmarkApi.removeBookmark).mockResolvedValue(undefined);
vi.mocked(authApi.logout).mockResolvedValue(undefined);

function makeBookmarkPage(
  overrides: Partial<BookmarksPaginatedResponse> = {},
): BookmarksPaginatedResponse {
  return {
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 20,
    totalPages: 0,
    ...overrides,
  };
}

const sampleItem = {
  id: 1,
  newsItemId: 10,
  oshiId: 1,
  oshiName: "推しA",
  title: "ブックマークされたニュース",
  summary: null,
  sourceUrl: "https://example.com/news/1",
  publishedAt: null,
  isPublishedAtEstimated: false,
  collectedAt: "2026-08-30T10:00:00+09:00",
  bookmarkedAt: "2026-08-30T12:00:00+09:00",
};

function renderPage() {
  const futureDate = new Date(Date.now() + 3600000).toISOString();
  localStorage.setItem("auth_token", "valid-token");
  localStorage.setItem("auth_expires_at", futureDate);

  return render(
    <AuthProvider>
      <MemoryRouter>
        <Routes>
          <Route path="*" element={<BookmarkPage />} />
          <Route path="/auth" element={<div>認証ページ</div>} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  );
}

describe("BookmarkPage", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    localStorage.clear();
    vi.mocked(bookmarkApi.addBookmark).mockResolvedValue(undefined);
    vi.mocked(bookmarkApi.removeBookmark).mockResolvedValue(undefined);
    vi.mocked(authApi.logout).mockResolvedValue(undefined);
  });

  // ── 正常系 ────────────────────────────────────────────────────────────────

  it("ページ見出しが表示される", async () => {
    mockGetBookmarks.mockResolvedValue(makeBookmarkPage());

    renderPage();

    expect(screen.getByText("ブックマーク一覧")).toBeInTheDocument();
  });

  it("ブックマーク記事のタイトルが表示される", async () => {
    mockGetBookmarks.mockResolvedValue(
      makeBookmarkPage({
        items: [sampleItem],
        totalCount: 1,
        totalPages: 1,
      }),
    );

    renderPage();

    await waitFor(() => {
      expect(
        screen.getByText("ブックマークされたニュース"),
      ).toBeInTheDocument();
    });
  });

  it("ブックマーク記事の出典URLがリンクとして新しいタブで開く", async () => {
    mockGetBookmarks.mockResolvedValue(
      makeBookmarkPage({
        items: [sampleItem],
        totalCount: 1,
        totalPages: 1,
      }),
    );

    renderPage();

    await waitFor(() => {
      const link = screen.getByRole("link", {
        name: "ブックマークされたニュース",
      });
      expect(link).toHaveAttribute("href", "https://example.com/news/1");
      expect(link).toHaveAttribute("target", "_blank");
    });
  });

  it("各ブックマーク記事にブックマーク済みボタンが表示される", async () => {
    mockGetBookmarks.mockResolvedValue(
      makeBookmarkPage({
        items: [sampleItem],
        totalCount: 1,
        totalPages: 1,
      }),
    );

    renderPage();

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "ブックマークを解除" }),
      ).toBeInTheDocument();
    });
  });

  it("複数件あるとき全件表示される", async () => {
    mockGetBookmarks.mockResolvedValue(
      makeBookmarkPage({
        items: [
          { ...sampleItem, id: 1, newsItemId: 10, title: "記事1" },
          { ...sampleItem, id: 2, newsItemId: 11, title: "記事2" },
        ],
        totalCount: 2,
        totalPages: 1,
      }),
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("記事1")).toBeInTheDocument();
      expect(screen.getByText("記事2")).toBeInTheDocument();
    });
  });

  it("ページネーションが表示される", async () => {
    mockGetBookmarks.mockResolvedValue(
      makeBookmarkPage({
        items: [sampleItem],
        totalCount: 25,
        totalPages: 2,
      }),
    );

    renderPage();

    await waitFor(() => {
      expect(
        screen.getByRole("navigation", { name: "ページネーション" }),
      ).toBeInTheDocument();
    });
  });

  it("次のページへ遷移できる", async () => {
    const user = userEvent.setup();
    const page1Data = makeBookmarkPage({
      items: [{ ...sampleItem, title: "1ページ目" }],
      totalCount: 25,
      totalPages: 2,
      page: 1,
    });
    const page2Data = makeBookmarkPage({
      items: [{ ...sampleItem, id: 2, newsItemId: 11, title: "2ページ目" }],
      totalCount: 25,
      totalPages: 2,
      page: 2,
    });
    mockGetBookmarks
      .mockResolvedValueOnce(page1Data)
      .mockResolvedValueOnce(page2Data);

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("1ページ目")).toBeInTheDocument();
    });

    await user.click(screen.getByLabelText("次のページ"));

    await waitFor(() => {
      expect(screen.getByText("2ページ目")).toBeInTheDocument();
    });
  });

  // ── 異常系 ────────────────────────────────────────────────────────────────

  it("APIエラー時にエラーメッセージが表示される", async () => {
    mockGetBookmarks.mockRejectedValue(
      new ApiError(500, "Internal Server Error"),
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
    });
  });

  it("ネットワークエラー時にエラーメッセージが表示される", async () => {
    mockGetBookmarks.mockRejectedValue(new Error("Network Error"));

    renderPage();

    await waitFor(() => {
      expect(
        screen.getByText("ブックマークの取得に失敗しました"),
      ).toBeInTheDocument();
    });
  });

  // ── 境界値 ────────────────────────────────────────────────────────────────

  it("ブックマークが0件のとき空状態メッセージが表示される", async () => {
    mockGetBookmarks.mockResolvedValue(makeBookmarkPage());

    renderPage();

    await waitFor(() => {
      expect(
        screen.getByText("ブックマークした記事はありません"),
      ).toBeInTheDocument();
    });
  });

  it("ローディング中はテキストが表示される", () => {
    mockGetBookmarks.mockImplementation(() => new Promise(() => undefined));

    renderPage();

    expect(screen.getByText("読み込み中...")).toBeInTheDocument();
  });
});
