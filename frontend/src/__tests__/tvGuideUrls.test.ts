import { describe, expect, it } from "vitest";
import { buildGguideUrl, buildJcomUrl } from "../utils/tvGuideUrls";

describe("buildGguideUrl", () => {
  // ── 正常系 ─────────────────────────────────────────────────────────────

  it("日本語の推し名を正しくエンコードした URL を返す", () => {
    const url = buildGguideUrl("鈴木一郎");
    expect(url).toBe(
      "https://bangumi.org/search?q=%E9%88%B4%E6%9C%A8%E4%B8%80%E9%83%8E",
    );
  });

  it("空白を含む推し名を正しくエンコードする", () => {
    const url = buildGguideUrl("山田 太郎");
    expect(url).toBe(
      "https://bangumi.org/search?q=%E5%B1%B1%E7%94%B0%20%E5%A4%AA%E9%83%8E",
    );
  });

  it("& を含む推し名を正しくエンコードする", () => {
    const url = buildGguideUrl("A&B");
    expect(url).toBe("https://bangumi.org/search?q=A%26B");
  });

  it("# を含む推し名を正しくエンコードする", () => {
    const url = buildGguideUrl("C#");
    expect(url).toBe("https://bangumi.org/search?q=C%23");
  });

  it("/ を含む推し名を正しくエンコードする", () => {
    const url = buildGguideUrl("X/Y");
    expect(url).toBe("https://bangumi.org/search?q=X%2FY");
  });

  it("記号を複数含む推し名を正しくエンコードする", () => {
    const url = buildGguideUrl("推し♥&A");
    expect(url).toBe(
      "https://bangumi.org/search?q=%E6%8E%A8%E3%81%97%E2%99%A5%26A",
    );
  });

  // ── 境界値 ─────────────────────────────────────────────────────────────

  it("空文字でも URL が壊れない（クエリが空になる）", () => {
    const url = buildGguideUrl("");
    expect(url).toBe("https://bangumi.org/search?q=");
  });

  it("ASCII のみの名前はそのまま使われる", () => {
    const url = buildGguideUrl("TaylorSwift");
    expect(url).toBe("https://bangumi.org/search?q=TaylorSwift");
  });
});

describe("buildJcomUrl", () => {
  // ── 正常系 ─────────────────────────────────────────────────────────────

  it("日本語の推し名を正しくエンコードした URL を返す", () => {
    const url = buildJcomUrl("鈴木一郎");
    expect(url).toBe(
      "https://tvguide.myjcom.jp/search/event/?keyword=%E9%88%B4%E6%9C%A8%E4%B8%80%E9%83%8E",
    );
  });

  it("空白を含む推し名を正しくエンコードする", () => {
    const url = buildJcomUrl("山田 太郎");
    expect(url).toBe(
      "https://tvguide.myjcom.jp/search/event/?keyword=%E5%B1%B1%E7%94%B0%20%E5%A4%AA%E9%83%8E",
    );
  });

  it("& を含む推し名を正しくエンコードする", () => {
    const url = buildJcomUrl("A&B");
    expect(url).toBe("https://tvguide.myjcom.jp/search/event/?keyword=A%26B");
  });

  it("# を含む推し名を正しくエンコードする", () => {
    const url = buildJcomUrl("C#");
    expect(url).toBe("https://tvguide.myjcom.jp/search/event/?keyword=C%23");
  });

  it("/ を含む推し名を正しくエンコードする", () => {
    const url = buildJcomUrl("X/Y");
    expect(url).toBe("https://tvguide.myjcom.jp/search/event/?keyword=X%2FY");
  });

  // ── 境界値 ─────────────────────────────────────────────────────────────

  it("空文字でも URL が壊れない（クエリが空になる）", () => {
    const url = buildJcomUrl("");
    expect(url).toBe("https://tvguide.myjcom.jp/search/event/?keyword=");
  });

  it("ASCII のみの名前はそのまま使われる", () => {
    const url = buildJcomUrl("TaylorSwift");
    expect(url).toBe(
      "https://tvguide.myjcom.jp/search/event/?keyword=TaylorSwift",
    );
  });
});
