import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { OtherSourcesAccordion } from "../components/feed/OtherSourcesAccordion";
import type { OtherSource } from "../types/feed";

const sourcesWithName: OtherSource[] = [
  { sourceName: "Yahoo!ニュース", sourceUrl: "https://news.yahoo.co.jp/1" },
  {
    sourceName: "Infoseek ニュース",
    sourceUrl: "https://news.infoseek.co.jp/1",
  },
];

const sourcesNoName: OtherSource[] = [
  { sourceName: null, sourceUrl: "https://example.com/news/1" },
];

describe("OtherSourcesAccordion", () => {
  // ── 正常系 ────────────────────────────────────────────────────────────

  it("count が 0 のときは何も表示しない", () => {
    const { container } = render(
      <OtherSourcesAccordion count={0} sources={[]} />,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it("count が 1 以上のとき「ほか N 媒体」ボタンが表示される", () => {
    render(<OtherSourcesAccordion count={2} sources={sourcesWithName} />);
    expect(
      screen.getByRole("button", { name: /ほか 2 媒体/ }),
    ).toBeInTheDocument();
  });

  it("初期状態ではリストは折りたたまれている（aria-expanded=false）", () => {
    render(<OtherSourcesAccordion count={2} sources={sourcesWithName} />);
    expect(screen.getByRole("button")).toHaveAttribute(
      "aria-expanded",
      "false",
    );
  });

  it("ボタンをクリックするとリストが展開され各媒体のリンクが表示される", async () => {
    const user = userEvent.setup();
    render(<OtherSourcesAccordion count={2} sources={sourcesWithName} />);

    await user.click(screen.getByRole("button"));

    expect(
      screen.getByRole("link", { name: "Yahoo!ニュース" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "Infoseek ニュース" }),
    ).toBeInTheDocument();
  });

  it("展開後にボタンをもう一度クリックするとリストが折りたたまれる", async () => {
    const user = userEvent.setup();
    render(<OtherSourcesAccordion count={2} sources={sourcesWithName} />);

    await user.click(screen.getByRole("button"));
    expect(screen.getAllByRole("link")).toHaveLength(2);

    await user.click(screen.getByRole("button"));
    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });

  it("展開時に aria-expanded が true になる", async () => {
    const user = userEvent.setup();
    render(<OtherSourcesAccordion count={2} sources={sourcesWithName} />);

    await user.click(screen.getByRole("button"));
    expect(screen.getByRole("button")).toHaveAttribute("aria-expanded", "true");
  });

  // ── リンク属性 ────────────────────────────────────────────────────────

  it("展開したリンクは新しいタブで開く（target=_blank）", async () => {
    const user = userEvent.setup();
    render(<OtherSourcesAccordion count={2} sources={sourcesWithName} />);

    await user.click(screen.getByRole("button"));

    const link = screen.getByRole("link", { name: "Yahoo!ニュース" });
    expect(link).toHaveAttribute("target", "_blank");
  });

  it("展開したリンクは rel=noopener noreferrer を持つ", async () => {
    const user = userEvent.setup();
    render(<OtherSourcesAccordion count={2} sources={sourcesWithName} />);

    await user.click(screen.getByRole("button"));

    const link = screen.getByRole("link", { name: "Yahoo!ニュース" });
    expect(link).toHaveAttribute("rel", "noopener noreferrer");
  });

  it("展開したリンクの href が sourceUrl になっている", async () => {
    const user = userEvent.setup();
    render(<OtherSourcesAccordion count={2} sources={sourcesWithName} />);

    await user.click(screen.getByRole("button"));

    const link = screen.getByRole("link", { name: "Yahoo!ニュース" });
    expect(link).toHaveAttribute("href", "https://news.yahoo.co.jp/1");
  });

  // ── 境界値 ─────────────────────────────────────────────────────────────

  it("sourceName が null のときは sourceUrl をリンクテキストとして表示する", async () => {
    const user = userEvent.setup();
    render(<OtherSourcesAccordion count={1} sources={sourcesNoName} />);

    await user.click(screen.getByRole("button"));

    expect(
      screen.getByRole("link", { name: "https://example.com/news/1" }),
    ).toBeInTheDocument();
  });

  it("count=1 のとき「ほか 1 媒体」と表示される", () => {
    render(<OtherSourcesAccordion count={1} sources={sourcesNoName} />);
    expect(
      screen.getByRole("button", { name: /ほか 1 媒体/ }),
    ).toBeInTheDocument();
  });
});
