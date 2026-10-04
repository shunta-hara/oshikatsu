import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { TvGuideLinks } from "../components/feed/TvGuideLinks";

describe("TvGuideLinks", () => {
  // ── 正常系 ─────────────────────────────────────────────────────────────

  it("「番組表で探す：」ラベルが表示される", () => {
    render(<TvGuideLinks oshiName="推しA" />);
    expect(screen.getByText("番組表で探す：")).toBeInTheDocument();
  });

  it("Gガイドのリンクが表示される", () => {
    render(<TvGuideLinks oshiName="推しA" />);
    const link = screen.getByRole("link", { name: "Gガイド" });
    expect(link).toBeInTheDocument();
  });

  it("J:COM のリンクが表示される", () => {
    render(<TvGuideLinks oshiName="推しA" />);
    const link = screen.getByRole("link", { name: "J:COM" });
    expect(link).toBeInTheDocument();
  });

  it("Gガイドのリンクが新しいタブで開く設定になっている", () => {
    render(<TvGuideLinks oshiName="推しA" />);
    const link = screen.getByRole("link", { name: "Gガイド" });
    expect(link).toHaveAttribute("target", "_blank");
    expect(link).toHaveAttribute("rel", "noopener noreferrer");
  });

  it("J:COM のリンクが新しいタブで開く設定になっている", () => {
    render(<TvGuideLinks oshiName="推しA" />);
    const link = screen.getByRole("link", { name: "J:COM" });
    expect(link).toHaveAttribute("target", "_blank");
    expect(link).toHaveAttribute("rel", "noopener noreferrer");
  });

  it("Gガイドのリンク href に推しの名前が URLエンコードされて含まれる", () => {
    render(<TvGuideLinks oshiName="鈴木一郎" />);
    const link = screen.getByRole("link", { name: "Gガイド" });
    expect(link).toHaveAttribute(
      "href",
      "https://bangumi.org/search?q=%E9%88%B4%E6%9C%A8%E4%B8%80%E9%83%8E",
    );
  });

  it("J:COM のリンク href に推しの名前が URLエンコードされて含まれる", () => {
    render(<TvGuideLinks oshiName="鈴木一郎" />);
    const link = screen.getByRole("link", { name: "J:COM" });
    expect(link).toHaveAttribute(
      "href",
      "https://tvguide.myjcom.jp/search/event/?keyword=%E9%88%B4%E6%9C%A8%E4%B8%80%E9%83%8E",
    );
  });

  // ── 境界値 ─────────────────────────────────────────────────────────────

  it("空白を含む推し名も URL が正しくエンコードされる", () => {
    render(<TvGuideLinks oshiName="山田 太郎" />);
    const gguide = screen.getByRole("link", { name: "Gガイド" });
    expect(gguide).toHaveAttribute(
      "href",
      "https://bangumi.org/search?q=%E5%B1%B1%E7%94%B0%20%E5%A4%AA%E9%83%8E",
    );
  });

  it("& を含む推し名も URL が正しくエンコードされる", () => {
    render(<TvGuideLinks oshiName="A&B" />);
    const gguide = screen.getByRole("link", { name: "Gガイド" });
    expect(gguide).toHaveAttribute(
      "href",
      "https://bangumi.org/search?q=A%26B",
    );
  });

  it("# を含む推し名も URL が正しくエンコードされる", () => {
    render(<TvGuideLinks oshiName="C#" />);
    const jcom = screen.getByRole("link", { name: "J:COM" });
    expect(jcom).toHaveAttribute(
      "href",
      "https://tvguide.myjcom.jp/search/event/?keyword=C%23",
    );
  });

  it("/ を含む推し名も URL が正しくエンコードされる", () => {
    render(<TvGuideLinks oshiName="X/Y" />);
    const gguide = screen.getByRole("link", { name: "Gガイド" });
    expect(gguide).toHaveAttribute(
      "href",
      "https://bangumi.org/search?q=X%2FY",
    );
  });
});
