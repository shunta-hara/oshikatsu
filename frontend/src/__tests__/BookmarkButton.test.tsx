import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { BookmarkButton } from "../components/feed/BookmarkButton";
import * as bookmarkApi from "../api/bookmark";
import { ApiError } from "../api/client";

vi.mock("../api/bookmark");
const mockAddBookmark = vi.mocked(bookmarkApi.addBookmark);
const mockRemoveBookmark = vi.mocked(bookmarkApi.removeBookmark);

describe("BookmarkButton", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  // ── 初期表示 ──────────────────────────────────────────────────────────────

  it("initialIsBookmarked=false のとき「ブックマーク」と表示される", () => {
    render(<BookmarkButton newsItemId={1} initialIsBookmarked={false} />);
    expect(
      screen.getByRole("button", { name: "ブックマークに追加" }),
    ).toBeInTheDocument();
  });

  it("initialIsBookmarked=true のとき「ブックマーク済み」と表示される", () => {
    render(<BookmarkButton newsItemId={1} initialIsBookmarked={true} />);
    expect(
      screen.getByRole("button", { name: "ブックマークを解除" }),
    ).toBeInTheDocument();
  });

  it("initialIsBookmarked 未指定のとき「ブックマーク」と表示される", () => {
    render(<BookmarkButton newsItemId={1} />);
    expect(
      screen.getByRole("button", { name: "ブックマークに追加" }),
    ).toBeInTheDocument();
  });

  it("ブックマーク済み状態のとき aria-pressed=true が設定されている", () => {
    render(<BookmarkButton newsItemId={1} initialIsBookmarked={true} />);
    const button = screen.getByRole("button");
    expect(button).toHaveAttribute("aria-pressed", "true");
  });

  it("未ブックマーク状態のとき aria-pressed=false が設定されている", () => {
    render(<BookmarkButton newsItemId={1} initialIsBookmarked={false} />);
    const button = screen.getByRole("button");
    expect(button).toHaveAttribute("aria-pressed", "false");
  });

  // ── 正常系: ブックマーク登録 ─────────────────────────────────────────────

  it("未ブックマーク状態でクリックすると addBookmark が呼ばれる", async () => {
    mockAddBookmark.mockResolvedValue(undefined);
    render(<BookmarkButton newsItemId={42} initialIsBookmarked={false} />);

    fireEvent.click(screen.getByRole("button"));

    await waitFor(() => {
      expect(mockAddBookmark).toHaveBeenCalledWith(42);
    });
  });

  it("ブックマーク登録成功後はボタンが「ブックマーク済み」に変わる", async () => {
    mockAddBookmark.mockResolvedValue(undefined);
    render(<BookmarkButton newsItemId={1} initialIsBookmarked={false} />);

    fireEvent.click(screen.getByRole("button"));

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "ブックマークを解除" }),
      ).toBeInTheDocument();
    });
  });

  // ── 正常系: ブックマーク解除 ─────────────────────────────────────────────

  it("ブックマーク済み状態でクリックすると removeBookmark が呼ばれる", async () => {
    mockRemoveBookmark.mockResolvedValue(undefined);
    render(<BookmarkButton newsItemId={42} initialIsBookmarked={true} />);

    fireEvent.click(screen.getByRole("button"));

    await waitFor(() => {
      expect(mockRemoveBookmark).toHaveBeenCalledWith(42);
    });
  });

  it("ブックマーク解除成功後はボタンが「ブックマーク」に変わる", async () => {
    mockRemoveBookmark.mockResolvedValue(undefined);
    render(<BookmarkButton newsItemId={1} initialIsBookmarked={true} />);

    fireEvent.click(screen.getByRole("button"));

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "ブックマークに追加" }),
      ).toBeInTheDocument();
    });
  });

  // ── 異常系 ────────────────────────────────────────────────────────────────

  it("409 (Already Bookmarked) を受け取ったときはブックマーク済み状態になる", async () => {
    mockAddBookmark.mockRejectedValue(new ApiError(409, "Already bookmarked."));
    render(<BookmarkButton newsItemId={1} initialIsBookmarked={false} />);

    fireEvent.click(screen.getByRole("button"));

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "ブックマークを解除" }),
      ).toBeInTheDocument();
    });
  });

  it("404 (Bookmark Not Found) を受け取ったときは未ブックマーク状態になる", async () => {
    mockRemoveBookmark.mockRejectedValue(
      new ApiError(404, "Bookmark not found."),
    );
    render(<BookmarkButton newsItemId={1} initialIsBookmarked={true} />);

    fireEvent.click(screen.getByRole("button"));

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "ブックマークに追加" }),
      ).toBeInTheDocument();
    });
  });

  it("その他のエラー時は状態が変わらない", async () => {
    mockAddBookmark.mockRejectedValue(new ApiError(500, "Server Error"));
    render(<BookmarkButton newsItemId={1} initialIsBookmarked={false} />);

    fireEvent.click(screen.getByRole("button"));

    await waitFor(() => {
      // ローディングが終わった後も「ブックマーク」のまま
      expect(screen.getByRole("button")).not.toBeDisabled();
    });
    expect(
      screen.getByRole("button", { name: "ブックマークに追加" }),
    ).toBeInTheDocument();
  });

  // ── 境界値 ────────────────────────────────────────────────────────────────

  it("ローディング中はボタンが無効化される", () => {
    // 解決しない Promise でローディング状態を維持
    mockAddBookmark.mockImplementation(() => new Promise(() => undefined));
    render(<BookmarkButton newsItemId={1} initialIsBookmarked={false} />);

    fireEvent.click(screen.getByRole("button"));

    expect(screen.getByRole("button")).toBeDisabled();
  });
});
