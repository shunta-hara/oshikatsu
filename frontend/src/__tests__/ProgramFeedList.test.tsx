import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ProgramFeedList } from "../components/feed/ProgramFeedList";
import * as feedApi from "../api/feed";
import { ApiError } from "../api/client";
import type { ProgramsPaginatedResponse } from "../types/feed";

vi.mock("../api/feed");
const mockGetPrograms = vi.mocked(feedApi.getPrograms);

function makeProgramsPage(
  overrides: Partial<ProgramsPaginatedResponse> = {},
): ProgramsPaginatedResponse {
  return {
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 20,
    totalPages: 0,
    ...overrides,
  };
}

describe("ProgramFeedList", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  // ── 正常系 ────────────────────────────────────────────────────────────

  it("番組名がリンクとして表示される", async () => {
    mockGetPrograms.mockResolvedValue(
      makeProgramsPage({
        items: [
          {
            id: 1,
            oshiId: 1,
            oshiName: "推しA",
            programName: "テスト番組",
            broadcaster: "テレビA",
            airedAt: null,
            isAiredAtEstimated: false,
            sourceUrl: "https://example.com/programs/1",
            collectedAt: "2026-07-16T10:00:00+09:00",
          },
        ],
        totalCount: 1,
        totalPages: 1,
      }),
    );

    render(<ProgramFeedList />);

    await waitFor(() => {
      expect(screen.getByText("テスト番組")).toBeInTheDocument();
    });
  });

  it("出典URLが新しいタブで開くリンクになっている", async () => {
    mockGetPrograms.mockResolvedValue(
      makeProgramsPage({
        items: [
          {
            id: 1,
            oshiId: 1,
            oshiName: "推しA",
            programName: "テスト番組",
            broadcaster: null,
            airedAt: null,
            isAiredAtEstimated: false,
            sourceUrl: "https://example.com/programs/1",
            collectedAt: "2026-07-16T10:00:00+09:00",
          },
        ],
        totalCount: 1,
        totalPages: 1,
      }),
    );

    render(<ProgramFeedList />);

    await waitFor(() => {
      const link = screen.getByRole("link", { name: "テスト番組" });
      expect(link).toHaveAttribute("href", "https://example.com/programs/1");
      expect(link).toHaveAttribute("target", "_blank");
      expect(link).toHaveAttribute("rel", "noopener noreferrer");
    });
  });

  it("推し名と放送局が表示される", async () => {
    mockGetPrograms.mockResolvedValue(
      makeProgramsPage({
        items: [
          {
            id: 1,
            oshiId: 1,
            oshiName: "テスト推し",
            programName: "テスト番組",
            broadcaster: "テレビB",
            airedAt: null,
            isAiredAtEstimated: false,
            sourceUrl: "https://example.com/programs/1",
            collectedAt: "2026-07-16T10:00:00+09:00",
          },
        ],
        totalCount: 1,
        totalPages: 1,
      }),
    );

    render(<ProgramFeedList />);

    await waitFor(() => {
      expect(screen.getByText("テスト推し")).toBeInTheDocument();
      expect(screen.getByText(/テレビB/)).toBeInTheDocument();
    });
  });

  it("放送局が null の場合もエラーなく表示される", async () => {
    mockGetPrograms.mockResolvedValue(
      makeProgramsPage({
        items: [
          {
            id: 1,
            oshiId: 1,
            oshiName: "推しA",
            programName: "番組名のみ",
            broadcaster: null,
            airedAt: null,
            isAiredAtEstimated: false,
            sourceUrl: "https://example.com/programs/1",
            collectedAt: "2026-07-16T10:00:00+09:00",
          },
        ],
        totalCount: 1,
        totalPages: 1,
      }),
    );

    render(<ProgramFeedList />);

    await waitFor(() => {
      expect(screen.getByText("番組名のみ")).toBeInTheDocument();
    });
  });

  it("ページネーションが複数ページある場合に表示される", async () => {
    mockGetPrograms.mockResolvedValue(
      makeProgramsPage({
        items: Array.from({ length: 20 }, (_, i) => ({
          id: i + 1,
          oshiId: 1,
          oshiName: "推しA",
          programName: `番組${i + 1}`,
          broadcaster: null,
          airedAt: null,
          isAiredAtEstimated: false,
          sourceUrl: `https://example.com/${i + 1}`,
          collectedAt: "2026-07-16T10:00:00+09:00",
        })),
        totalCount: 25,
        totalPages: 2,
      }),
    );

    render(<ProgramFeedList />);

    await waitFor(() => {
      expect(screen.getByRole("navigation")).toBeInTheDocument();
    });
  });

  // ── 異常系 ────────────────────────────────────────────────────────────

  it("APIエラー時にエラーメッセージが表示される", async () => {
    mockGetPrograms.mockRejectedValue(new ApiError(500, "Internal Server Error"));

    render(<ProgramFeedList />);

    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
    });
  });

  it("ネットワークエラー時にエラーメッセージが表示される", async () => {
    mockGetPrograms.mockRejectedValue(new Error("Network Error"));

    render(<ProgramFeedList />);

    await waitFor(() => {
      expect(
        screen.getByText("番組情報の取得に失敗しました"),
      ).toBeInTheDocument();
    });
  });

  // ── 境界値 ─────────────────────────────────────────────────────────────

  it("データが0件のとき空状態メッセージが表示される", async () => {
    mockGetPrograms.mockResolvedValue(makeProgramsPage());

    render(<ProgramFeedList />);

    await waitFor(() => {
      expect(
        screen.getByText("まだ出演番組情報がありません"),
      ).toBeInTheDocument();
    });
  });

  it("ローディング中はテキストが表示される", () => {
    mockGetPrograms.mockImplementation(() => new Promise(() => undefined));

    render(<ProgramFeedList />);

    expect(screen.getByText("読み込み中...")).toBeInTheDocument();
  });
});
