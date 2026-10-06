import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { SourceTypeFilter } from "../components/feed/SourceTypeFilter";
import type { SourceTypeValue } from "../components/feed/SourceTypeFilter";

describe("SourceTypeFilter", () => {
  // ── 正常系 ────────────────────────────────────────────────────────────

  it("すべてのチップが表示される", () => {
    render(
      <SourceTypeFilter selected={undefined} onChange={() => undefined} />,
    );

    expect(screen.getByRole("radio", { name: /すべて/ })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: /ニュース/ })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: /YouTube/ })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: /X投稿/ })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: /イベント/ })).toBeInTheDocument();
  });

  it("selected=undefined のとき「すべて」がアクティブ", () => {
    render(
      <SourceTypeFilter selected={undefined} onChange={() => undefined} />,
    );

    const allBtn = screen.getByRole("radio", { name: /すべて/ });
    expect(allBtn).toHaveAttribute("aria-checked", "true");
  });

  it("selected='youtube' のとき「YouTube」がアクティブ", () => {
    render(<SourceTypeFilter selected="youtube" onChange={() => undefined} />);

    const ytBtn = screen.getByRole("radio", { name: /YouTube/ });
    expect(ytBtn).toHaveAttribute("aria-checked", "true");

    const allBtn = screen.getByRole("radio", { name: /すべて/ });
    expect(allBtn).toHaveAttribute("aria-checked", "false");
  });

  it("チップをクリックすると onChange が呼ばれる", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();

    render(<SourceTypeFilter selected={undefined} onChange={onChange} />);

    await user.click(screen.getByRole("radio", { name: /YouTube/ }));

    expect(onChange).toHaveBeenCalledOnce();
    expect(onChange).toHaveBeenCalledWith("youtube");
  });

  it("「すべて」をクリックすると undefined が渡される", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();

    render(<SourceTypeFilter selected="news" onChange={onChange} />);

    await user.click(screen.getByRole("radio", { name: /すべて/ }));

    expect(onChange).toHaveBeenCalledWith(undefined);
  });

  it("「ニュース」をクリックすると 'news' が渡される", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();

    render(<SourceTypeFilter selected={undefined} onChange={onChange} />);

    await user.click(screen.getByRole("radio", { name: /ニュース/ }));

    expect(onChange).toHaveBeenCalledWith("news");
  });

  it("「X投稿」をクリックすると 'x_post' が渡される", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();

    render(<SourceTypeFilter selected={undefined} onChange={onChange} />);

    await user.click(screen.getByRole("radio", { name: /X投稿/ }));

    expect(onChange).toHaveBeenCalledWith("x_post");
  });

  it("「イベント」をクリックすると 'event' が渡される", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();

    render(<SourceTypeFilter selected={undefined} onChange={onChange} />);

    await user.click(screen.getByRole("radio", { name: /イベント/ }));

    expect(onChange).toHaveBeenCalledWith("event");
  });

  // ── 異常系 ────────────────────────────────────────────────────────────

  it("onChange が呼ばれなければ選択状態は変わらない", () => {
    // selected が変わらないと aria-checked も変わらない（controlled component）
    const { rerender } = render(
      <SourceTypeFilter selected={undefined} onChange={() => undefined} />,
    );

    const allBtn = screen.getByRole("radio", { name: /すべて/ });
    expect(allBtn).toHaveAttribute("aria-checked", "true");

    // selected を変えずに rerender しても変わらない
    rerender(
      <SourceTypeFilter selected={undefined} onChange={() => undefined} />,
    );
    expect(allBtn).toHaveAttribute("aria-checked", "true");
  });

  // ── 境界値 ─────────────────────────────────────────────────────────────

  it("selected='event' のとき「イベント」のみアクティブで他は非アクティブ", () => {
    render(<SourceTypeFilter selected="event" onChange={() => undefined} />);

    const eventBtn = screen.getByRole("radio", { name: /イベント/ });
    expect(eventBtn).toHaveAttribute("aria-checked", "true");

    const nonActiveTypes: SourceTypeValue[] = [
      undefined,
      "news",
      "youtube",
      "x_post",
    ];
    nonActiveTypes.forEach((val) => {
      const label =
        val === undefined
          ? /すべて/
          : val === "news"
            ? /ニュース/
            : val === "youtube"
              ? /YouTube/
              : /X投稿/;
      const btn = screen.getByRole("radio", { name: label });
      expect(btn).toHaveAttribute("aria-checked", "false");
    });
  });

  it("グループに aria-label が設定されている", () => {
    render(
      <SourceTypeFilter selected={undefined} onChange={() => undefined} />,
    );

    expect(
      screen.getByRole("group", { name: "ソースタイプで絞り込む" }),
    ).toBeInTheDocument();
  });
});
