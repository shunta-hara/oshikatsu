import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { FeedPagination } from "../components/feed/FeedPagination";

describe("FeedPagination", () => {
  // ── 正常系 ──────────────────────────────────────────────────────────────

  it("複数ページある場合にページネーションが表示される", () => {
    const onPageChange = vi.fn();
    render(
      <FeedPagination
        currentPage={1}
        totalPages={3}
        onPageChange={onPageChange}
      />,
    );

    expect(screen.getByRole("navigation")).toBeInTheDocument();
    expect(screen.getByLabelText("前のページ")).toBeInTheDocument();
    expect(screen.getByLabelText("次のページ")).toBeInTheDocument();
  });

  it("現在のページ番号と総ページ数が表示される", () => {
    render(
      <FeedPagination
        currentPage={2}
        totalPages={5}
        onPageChange={vi.fn()}
      />,
    );

    expect(screen.getByText(/2 \/ 5 ページ/)).toBeInTheDocument();
  });

  it("次へボタンをクリックすると次のページ番号で onPageChange が呼ばれる", async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();
    render(
      <FeedPagination
        currentPage={2}
        totalPages={5}
        onPageChange={onPageChange}
      />,
    );

    await user.click(screen.getByLabelText("次のページ"));

    expect(onPageChange).toHaveBeenCalledWith(3);
  });

  it("前へボタンをクリックすると前のページ番号で onPageChange が呼ばれる", async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();
    render(
      <FeedPagination
        currentPage={3}
        totalPages={5}
        onPageChange={onPageChange}
      />,
    );

    await user.click(screen.getByLabelText("前のページ"));

    expect(onPageChange).toHaveBeenCalledWith(2);
  });

  // ── 異常系 ──────────────────────────────────────────────────────────────

  it("1ページ目では前へボタンが無効になる", () => {
    render(
      <FeedPagination
        currentPage={1}
        totalPages={3}
        onPageChange={vi.fn()}
      />,
    );

    expect(screen.getByLabelText("前のページ")).toBeDisabled();
  });

  it("最終ページでは次へボタンが無効になる", () => {
    render(
      <FeedPagination
        currentPage={3}
        totalPages={3}
        onPageChange={vi.fn()}
      />,
    );

    expect(screen.getByLabelText("次のページ")).toBeDisabled();
  });

  // ── 境界値 ───────────────────────────────────────────────────────────────

  it("totalPages が 1 以下の場合は何も描画しない", () => {
    const { container } = render(
      <FeedPagination
        currentPage={1}
        totalPages={1}
        onPageChange={vi.fn()}
      />,
    );

    expect(container.firstChild).toBeNull();
  });

  it("totalPages が 0 の場合は何も描画しない", () => {
    const { container } = render(
      <FeedPagination
        currentPage={1}
        totalPages={0}
        onPageChange={vi.fn()}
      />,
    );

    expect(container.firstChild).toBeNull();
  });

  it("中間ページでは前へ・次へ両方が有効になる", () => {
    render(
      <FeedPagination
        currentPage={2}
        totalPages={5}
        onPageChange={vi.fn()}
      />,
    );

    expect(screen.getByLabelText("前のページ")).not.toBeDisabled();
    expect(screen.getByLabelText("次のページ")).not.toBeDisabled();
  });
});
