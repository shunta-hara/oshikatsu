import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { DateRangeFilter } from "../components/feed/DateRangeFilter";

describe("DateRangeFilter", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  // ── 正常系 ────────────────────────────────────────────────────────────

  it("開始日と終了日の入力フィールドが表示される", () => {
    render(<DateRangeFilter onApply={vi.fn()} />);

    expect(screen.getByLabelText("開始日")).toBeInTheDocument();
    expect(screen.getByLabelText("終了日")).toBeInTheDocument();
  });

  it("「期間を適用」と「クリア」ボタンが表示される", () => {
    render(<DateRangeFilter onApply={vi.fn()} />);

    expect(
      screen.getByRole("button", { name: "期間を適用" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "クリア" })).toBeInTheDocument();
  });

  it("何も入力せずに適用するとonApplyがundefined, undefinedで呼ばれる", async () => {
    const user = userEvent.setup();
    const onApply = vi.fn();
    render(<DateRangeFilter onApply={onApply} />);

    await user.click(screen.getByRole("button", { name: "期間を適用" }));

    expect(onApply).toHaveBeenCalledOnce();
    expect(onApply).toHaveBeenCalledWith(undefined, undefined);
  });

  it("開始日のみ入力して適用するとfromのみが渡される", async () => {
    const user = userEvent.setup();
    const onApply = vi.fn();
    render(<DateRangeFilter onApply={onApply} />);

    fireEvent.change(screen.getByLabelText("開始日"), {
      target: { value: "2026-08-01" },
    });
    await user.click(screen.getByRole("button", { name: "期間を適用" }));

    expect(onApply).toHaveBeenCalledOnce();
    const [from, to] = onApply.mock.calls[0] as [
      string | undefined,
      string | undefined,
    ];
    expect(from).toBe("2026-08-01T00:00:00.000Z");
    expect(to).toBeUndefined();
  });

  it("終了日のみ入力して適用するとtoのみが渡される", async () => {
    const user = userEvent.setup();
    const onApply = vi.fn();
    render(<DateRangeFilter onApply={onApply} />);

    fireEvent.change(screen.getByLabelText("終了日"), {
      target: { value: "2026-08-31" },
    });
    await user.click(screen.getByRole("button", { name: "期間を適用" }));

    expect(onApply).toHaveBeenCalledOnce();
    const [from, to] = onApply.mock.calls[0] as [
      string | undefined,
      string | undefined,
    ];
    expect(from).toBeUndefined();
    expect(to).toBe("2026-08-31T23:59:59.999Z");
  });

  it("開始日と終了日の両方を入力して適用するとfromとtoが渡される", async () => {
    const user = userEvent.setup();
    const onApply = vi.fn();
    render(<DateRangeFilter onApply={onApply} />);

    fireEvent.change(screen.getByLabelText("開始日"), {
      target: { value: "2026-08-01" },
    });
    fireEvent.change(screen.getByLabelText("終了日"), {
      target: { value: "2026-08-31" },
    });
    await user.click(screen.getByRole("button", { name: "期間を適用" }));

    expect(onApply).toHaveBeenCalledOnce();
    const [from, to] = onApply.mock.calls[0] as [
      string | undefined,
      string | undefined,
    ];
    expect(from).toBe("2026-08-01T00:00:00.000Z");
    expect(to).toBe("2026-08-31T23:59:59.999Z");
  });

  // ── 異常系 ────────────────────────────────────────────────────────────

  it("終了日が開始日より前の場合バリデーションエラーが表示されonApplyは呼ばれない", async () => {
    const user = userEvent.setup();
    const onApply = vi.fn();
    render(<DateRangeFilter onApply={onApply} />);

    fireEvent.change(screen.getByLabelText("開始日"), {
      target: { value: "2026-08-31" },
    });
    fireEvent.change(screen.getByLabelText("終了日"), {
      target: { value: "2026-08-01" },
    });
    await user.click(screen.getByRole("button", { name: "期間を適用" }));

    expect(screen.getByRole("alert")).toBeInTheDocument();
    expect(onApply).not.toHaveBeenCalled();
  });

  // ── 境界値 ─────────────────────────────────────────────────────────────

  it("開始日と終了日が同じ日でもバリデーションエラーにならない", async () => {
    const user = userEvent.setup();
    const onApply = vi.fn();
    render(<DateRangeFilter onApply={onApply} />);

    fireEvent.change(screen.getByLabelText("開始日"), {
      target: { value: "2026-08-15" },
    });
    fireEvent.change(screen.getByLabelText("終了日"), {
      target: { value: "2026-08-15" },
    });
    await user.click(screen.getByRole("button", { name: "期間を適用" }));

    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(onApply).toHaveBeenCalledOnce();
  });

  it("クリアボタンを押すとonApplyがundefined, undefinedで呼ばれ入力値もリセットされる", async () => {
    const user = userEvent.setup();
    const onApply = vi.fn();
    render(<DateRangeFilter onApply={onApply} />);

    // まず開始日を入力
    fireEvent.change(screen.getByLabelText("開始日"), {
      target: { value: "2026-08-01" },
    });

    await user.click(screen.getByRole("button", { name: "クリア" }));

    expect(onApply).toHaveBeenCalledWith(undefined, undefined);
    // 入力値がリセットされている
    expect(screen.getByLabelText("開始日")).toHaveValue("");
    expect(screen.getByLabelText("終了日")).toHaveValue("");
  });

  it("バリデーションエラー後にクリアするとエラーが消える", async () => {
    const user = userEvent.setup();
    render(<DateRangeFilter onApply={vi.fn()} />);

    // バリデーションエラーを出す
    fireEvent.change(screen.getByLabelText("開始日"), {
      target: { value: "2026-08-31" },
    });
    fireEvent.change(screen.getByLabelText("終了日"), {
      target: { value: "2026-08-01" },
    });
    await user.click(screen.getByRole("button", { name: "期間を適用" }));
    expect(screen.getByRole("alert")).toBeInTheDocument();

    // クリアするとエラーが消える
    await user.click(screen.getByRole("button", { name: "クリア" }));
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});
