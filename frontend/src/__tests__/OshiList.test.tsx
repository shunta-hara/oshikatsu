import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { OshiList } from "../components/oshi/OshiList";
import * as oshiApi from "../api/oshi";
import { ApiError } from "../api/client";
import type { OshiResponse } from "../types/oshi";

vi.mock("../api/oshi");
const mockDeleteOshi = vi.mocked(oshiApi.deleteOshi);

const oshi1: OshiResponse = {
  id: 1,
  name: "鈴木一郎",
  createdAt: "2024-01-01T00:00:00Z",
};
const oshi2: OshiResponse = {
  id: 2,
  name: "田中花子",
  createdAt: "2024-01-02T00:00:00Z",
};

describe("OshiList", () => {
  const onOshiDeleted = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    // window.confirm のデフォルトはキャンセル（false）
    vi.spyOn(window, "confirm").mockReturnValue(false);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  function renderList(oshis: OshiResponse[]) {
    return render(<OshiList oshis={oshis} onOshiDeleted={onOshiDeleted} />);
  }

  // --- 境界値：空リスト ---
  it("空の配列が渡されると「まだ推しが登録されていません」が表示される", () => {
    renderList([]);
    expect(
      screen.getByText(
        "まだ推しが登録されていません。上のフォームから追加してください。",
      ),
    ).toBeInTheDocument();
  });

  // --- 正常系 ---
  it("推しが渡されると名前が一覧に表示される", () => {
    renderList([oshi1, oshi2]);
    expect(screen.getByText("鈴木一郎")).toBeInTheDocument();
    expect(screen.getByText("田中花子")).toBeInTheDocument();
  });

  it("各推しに削除ボタンが表示される", () => {
    renderList([oshi1, oshi2]);
    expect(
      screen.getByRole("button", { name: "鈴木一郎を削除" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "田中花子を削除" }),
    ).toBeInTheDocument();
  });

  it("削除確認OKで deleteOshi が呼ばれ onOshiDeleted がコールバックされる", async () => {
    const user = userEvent.setup();
    vi.spyOn(window, "confirm").mockReturnValue(true);
    mockDeleteOshi.mockResolvedValueOnce(undefined);

    renderList([oshi1, oshi2]);

    await user.click(screen.getByRole("button", { name: "鈴木一郎を削除" }));

    await waitFor(() => {
      expect(mockDeleteOshi).toHaveBeenCalledWith(oshi1.id);
    });
    expect(onOshiDeleted).toHaveBeenCalledWith(oshi1.id);
  });

  it("削除確認キャンセルでは deleteOshi が呼ばれない", async () => {
    const user = userEvent.setup();
    vi.spyOn(window, "confirm").mockReturnValue(false);

    renderList([oshi1]);

    await user.click(screen.getByRole("button", { name: "鈴木一郎を削除" }));

    expect(mockDeleteOshi).not.toHaveBeenCalled();
    expect(onOshiDeleted).not.toHaveBeenCalled();
  });

  // --- 異常系 ---
  it("削除 API エラー時にエラーメッセージが表示される", async () => {
    const user = userEvent.setup();
    vi.spyOn(window, "confirm").mockReturnValue(true);
    mockDeleteOshi.mockRejectedValueOnce(
      new ApiError(500, "Internal Server Error"),
    );

    renderList([oshi1]);

    await user.click(screen.getByRole("button", { name: "鈴木一郎を削除" }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
    });
    expect(onOshiDeleted).not.toHaveBeenCalled();
  });

  it("ネットワークエラー時にフォールバックエラーメッセージが表示される", async () => {
    const user = userEvent.setup();
    vi.spyOn(window, "confirm").mockReturnValue(true);
    mockDeleteOshi.mockRejectedValueOnce(new Error("Network error"));

    renderList([oshi1]);

    await user.click(screen.getByRole("button", { name: "鈴木一郎を削除" }));

    await waitFor(() => {
      expect(
        screen.getByText(
          "削除に失敗しました。しばらくしてからもう一度お試しください",
        ),
      ).toBeInTheDocument();
    });
  });

  it("削除中はボタンが無効になる", async () => {
    const user = userEvent.setup();
    vi.spyOn(window, "confirm").mockReturnValue(true);
    // 解決しない Promise でローディング状態を維持
    mockDeleteOshi.mockImplementationOnce(() => new Promise(() => undefined));

    renderList([oshi1]);

    await user.click(screen.getByRole("button", { name: "鈴木一郎を削除" }));

    // aria-label はローディング中も "鈴木一郎を削除" のまま変わらないため
    // aria-label でボタンを取得し disabled 状態を確認する
    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "鈴木一郎を削除" }),
      ).toBeDisabled();
    });
  });

  it("403 エラー時にAPIのエラーメッセージが表示される", async () => {
    const user = userEvent.setup();
    vi.spyOn(window, "confirm").mockReturnValue(true);
    mockDeleteOshi.mockRejectedValueOnce(
      new ApiError(403, "You do not have permission to delete this oshi."),
    );

    renderList([oshi1]);

    await user.click(screen.getByRole("button", { name: "鈴木一郎を削除" }));

    await waitFor(() => {
      expect(
        screen.getByText("You do not have permission to delete this oshi."),
      ).toBeInTheDocument();
    });
  });
});
