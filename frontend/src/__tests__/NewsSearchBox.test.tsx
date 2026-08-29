import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { NewsSearchBox } from "../components/feed/NewsSearchBox";

describe("NewsSearchBox", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  // ── 正常系 ────────────────────────────────────────────────────────────

  it("検索ボックスが表示される", () => {
    render(<NewsSearchBox value="" onSearch={vi.fn()} onClear={vi.fn()} />);
    expect(screen.getByRole("searchbox")).toBeInTheDocument();
  });

  it("「検索」ボタンが表示される", () => {
    render(<NewsSearchBox value="" onSearch={vi.fn()} onClear={vi.fn()} />);
    expect(screen.getByRole("button", { name: "検索" })).toBeInTheDocument();
  });

  it("テキストを入力して検索ボタンをクリックすると onSearch がトリム済みの値で呼ばれる", async () => {
    const user = userEvent.setup();
    const onSearch = vi.fn();
    render(<NewsSearchBox value="" onSearch={onSearch} onClear={vi.fn()} />);

    await user.type(screen.getByRole("searchbox"), "テスト");
    await user.click(screen.getByRole("button", { name: "検索" }));

    expect(onSearch).toHaveBeenCalledOnce();
    expect(onSearch).toHaveBeenCalledWith("テスト");
  });

  it("Enter キーを押すと onSearch が呼ばれる", async () => {
    const user = userEvent.setup();
    const onSearch = vi.fn();
    render(<NewsSearchBox value="" onSearch={onSearch} onClear={vi.fn()} />);

    await user.type(screen.getByRole("searchbox"), "テスト");
    await user.keyboard("{Enter}");

    expect(onSearch).toHaveBeenCalledWith("テスト");
  });

  it("前後空白付きキーワードを入力すると onSearch にトリム済みの値が渡される", async () => {
    const user = userEvent.setup();
    const onSearch = vi.fn();
    render(<NewsSearchBox value="" onSearch={onSearch} onClear={vi.fn()} />);

    await user.type(screen.getByRole("searchbox"), "  テスト  ");
    await user.click(screen.getByRole("button", { name: "検索" }));

    expect(onSearch).toHaveBeenCalledWith("テスト");
  });

  // ── クリアボタン ──────────────────────────────────────────────────────

  it("value が空のときクリアボタンが表示されない", () => {
    render(<NewsSearchBox value="" onSearch={vi.fn()} onClear={vi.fn()} />);
    expect(
      screen.queryByRole("button", { name: "検索をクリア" }),
    ).not.toBeInTheDocument();
  });

  it("value が設定されているときクリアボタンが表示される", () => {
    render(
      <NewsSearchBox value="テスト" onSearch={vi.fn()} onClear={vi.fn()} />,
    );
    expect(
      screen.getByRole("button", { name: "検索をクリア" }),
    ).toBeInTheDocument();
  });

  it("クリアボタンをクリックすると onClear が呼ばれる", async () => {
    const user = userEvent.setup();
    const onClear = vi.fn();
    render(
      <NewsSearchBox value="テスト" onSearch={vi.fn()} onClear={onClear} />,
    );

    await user.click(screen.getByRole("button", { name: "検索をクリア" }));

    expect(onClear).toHaveBeenCalledOnce();
  });

  it("クリアボタンをクリックすると入力フィールドがクリアされる", async () => {
    const user = userEvent.setup();
    render(
      <NewsSearchBox value="テスト" onSearch={vi.fn()} onClear={vi.fn()} />,
    );

    await user.click(screen.getByRole("button", { name: "検索をクリア" }));

    expect(screen.getByRole("searchbox")).toHaveValue("");
  });

  // ── 異常系・境界値 ────────────────────────────────────────────────────

  it("空白のみの入力で検索しても onSearch が呼ばれない", async () => {
    const user = userEvent.setup();
    const onSearch = vi.fn();
    render(<NewsSearchBox value="" onSearch={onSearch} onClear={vi.fn()} />);

    await user.type(screen.getByRole("searchbox"), "   ");
    await user.click(screen.getByRole("button", { name: "検索" }));

    expect(onSearch).not.toHaveBeenCalled();
  });

  it("何も入力せずに検索しても onSearch が呼ばれない", async () => {
    const user = userEvent.setup();
    const onSearch = vi.fn();
    render(<NewsSearchBox value="" onSearch={onSearch} onClear={vi.fn()} />);

    await user.click(screen.getByRole("button", { name: "検索" }));

    expect(onSearch).not.toHaveBeenCalled();
  });
});
