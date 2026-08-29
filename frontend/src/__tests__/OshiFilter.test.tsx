import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { OshiFilter } from "../components/feed/OshiFilter";
import type { OshiResponse } from "../types/oshi";

function makeOshi(id: number, name: string): OshiResponse {
  return { id, name, createdAt: "2026-07-01T00:00:00+09:00" };
}

describe("OshiFilter", () => {
  // ── 正常系 ─────────────────────────────────────────────────────────────

  it("「全員」オプションがデフォルトで存在する", () => {
    render(<OshiFilter oshis={[]} selectedOshiId={null} onChange={vi.fn()} />);

    expect(screen.getByRole("combobox")).toBeInTheDocument();
    expect(screen.getByText("全員")).toBeInTheDocument();
  });

  it("推し一覧がオプションとして表示される", () => {
    const oshis = [makeOshi(1, "推しA"), makeOshi(2, "推しB")];

    render(
      <OshiFilter oshis={oshis} selectedOshiId={null} onChange={vi.fn()} />,
    );

    expect(screen.getByText("推しA")).toBeInTheDocument();
    expect(screen.getByText("推しB")).toBeInTheDocument();
  });

  it("selectedOshiId が null のとき「全員」が選択状態になる", () => {
    const oshis = [makeOshi(1, "推しA")];

    render(
      <OshiFilter oshis={oshis} selectedOshiId={null} onChange={vi.fn()} />,
    );

    expect(screen.getByRole("combobox")).toHaveValue("");
  });

  it("selectedOshiId が指定されているとき該当推しが選択状態になる", () => {
    const oshis = [makeOshi(1, "推しA"), makeOshi(2, "推しB")];

    render(<OshiFilter oshis={oshis} selectedOshiId={2} onChange={vi.fn()} />);

    expect(screen.getByRole("combobox")).toHaveValue("2");
  });

  it("推しを選択すると onChange が数値IDで呼ばれる", async () => {
    const user = userEvent.setup();
    const handleChange = vi.fn();
    const oshis = [makeOshi(1, "推しA"), makeOshi(2, "推しB")];

    render(
      <OshiFilter
        oshis={oshis}
        selectedOshiId={null}
        onChange={handleChange}
      />,
    );

    await user.selectOptions(screen.getByRole("combobox"), "推しA");

    expect(handleChange).toHaveBeenCalledWith(1);
  });

  it("「全員」を選択すると onChange が null で呼ばれる", async () => {
    const user = userEvent.setup();
    const handleChange = vi.fn();
    const oshis = [makeOshi(1, "推しA")];

    render(
      <OshiFilter oshis={oshis} selectedOshiId={1} onChange={handleChange} />,
    );

    await user.selectOptions(screen.getByRole("combobox"), "全員");

    expect(handleChange).toHaveBeenCalledWith(null);
  });

  // ── 異常系 ─────────────────────────────────────────────────────────────

  it("推し一覧が空でもエラーなく「全員」だけ表示される", () => {
    render(<OshiFilter oshis={[]} selectedOshiId={null} onChange={vi.fn()} />);

    const options = screen.getAllByRole("option");
    expect(options).toHaveLength(1);
    expect(options[0]).toHaveTextContent("全員");
  });

  // ── 境界値 ─────────────────────────────────────────────────────────────

  it("推しが1人のとき正しく表示される", () => {
    const oshis = [makeOshi(99, "唯一の推し")];

    render(
      <OshiFilter oshis={oshis} selectedOshiId={null} onChange={vi.fn()} />,
    );

    expect(screen.getByText("唯一の推し")).toBeInTheDocument();
    const options = screen.getAllByRole("option");
    expect(options).toHaveLength(2); // 「全員」 + 推し1人
  });
});
