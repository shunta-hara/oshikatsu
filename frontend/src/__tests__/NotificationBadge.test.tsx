import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { NotificationBadge } from "../components/feed/NotificationBadge";

describe("NotificationBadge", () => {
  // ── 正常系 ────────────────────────────────────────────────────────────

  it("count が 1 のときバッジが表示される", () => {
    render(<NotificationBadge count={1} />);

    expect(screen.getByRole("status")).toBeInTheDocument();
    expect(screen.getByText("1")).toBeInTheDocument();
  });

  it("count が 5 のときバッジに 5 が表示される", () => {
    render(<NotificationBadge count={5} />);

    expect(screen.getByText("5")).toBeInTheDocument();
  });

  it("aria-label に件数が含まれる", () => {
    render(<NotificationBadge count={3} />);

    expect(
      screen.getByRole("status", { name: "未読 3 件" }),
    ).toBeInTheDocument();
  });

  it("大きな件数も正しく表示される", () => {
    render(<NotificationBadge count={99} />);

    expect(screen.getByText("99")).toBeInTheDocument();
  });

  // ── 異常系・境界値 ─────────────────────────────────────────────────────

  it("count が 0 のときバッジを表示しない", () => {
    render(<NotificationBadge count={0} />);

    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });

  it("count が負の値のときバッジを表示しない", () => {
    render(<NotificationBadge count={-1} />);

    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });

  it("count が 0 から 1 に変わるとバッジが表示される", () => {
    const { rerender } = render(<NotificationBadge count={0} />);
    expect(screen.queryByRole("status")).not.toBeInTheDocument();

    rerender(<NotificationBadge count={1} />);
    expect(screen.getByRole("status")).toBeInTheDocument();
  });
});
