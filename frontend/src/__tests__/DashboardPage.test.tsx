import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { DashboardPage } from "../pages/DashboardPage";
import * as authApi from "../api/auth";
import * as oshiApi from "../api/oshi";
import * as feedApi from "../api/feed";
import * as collectorApi from "../api/collector";
import { ApiError } from "../api/client";
import { AuthProvider } from "../context/AuthContext";

vi.mock("../api/auth");
vi.mock("../api/oshi");
vi.mock("../api/feed");
vi.mock("../api/collector");
const mockLogout = vi.mocked(authApi.logout);
const mockGetOshis = vi.mocked(oshiApi.getOshis);
const mockGetNews = vi.mocked(feedApi.getNews);
const mockGetPrograms = vi.mocked(feedApi.getPrograms);
const mockGetCollectorStatus = vi.mocked(collectorApi.getCollectorStatus);

const emptyNewsPage = {
  items: [] as [],
  totalCount: 0,
  page: 1,
  pageSize: 20,
  totalPages: 0,
};

const emptyProgramsPage = {
  items: [] as [],
  totalCount: 0,
  page: 1,
  pageSize: 20,
  totalPages: 0,
};

function renderWithProvider(initialUrl = "/") {
  const futureDate = new Date(Date.now() + 3600000).toISOString();
  localStorage.setItem("auth_token", "valid-token");
  localStorage.setItem("auth_expires_at", futureDate);

  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={[initialUrl]}>
        <Routes>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/auth" element={<div>認証ページ</div>} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  );
}

describe("DashboardPage", () => {
  beforeEach(() => {
    // resetAllMocks: mockOnce キューも含めてリセット（clearAllMocks だとキューが残る）
    vi.resetAllMocks();
    localStorage.clear();
    // デフォルトで空の推し一覧を返す
    mockGetOshis.mockResolvedValue([]);
    // デフォルトで空のフィードを返す
    mockGetNews.mockResolvedValue(emptyNewsPage);
    mockGetPrograms.mockResolvedValue(emptyProgramsPage);
    // デフォルトで収集ジョブ未実行を返す
    mockGetCollectorStatus.mockResolvedValue({
      latestNewsJob: null,
      latestProgramJob: null,
    });
  });

  // --- 正常系 ---
  it("ダッシュボードのタイトルが表示される", async () => {
    mockLogout.mockResolvedValue(undefined);
    renderWithProvider();
    expect(screen.getByText("oshikatsu ダッシュボード")).toBeInTheDocument();
    // getOshis の非同期完了を待つ
    await waitFor(() => {
      expect(mockGetOshis).toHaveBeenCalledOnce();
    });
  });

  it("ログアウトボタンをクリックするとトークンが削除されて認証ページに遷移する", async () => {
    const user = userEvent.setup();
    mockLogout.mockResolvedValue(undefined);

    renderWithProvider();

    await user.click(screen.getByRole("button", { name: "ログアウト" }));

    await waitFor(() => {
      expect(screen.getByText("認証ページ")).toBeInTheDocument();
    });
    expect(localStorage.getItem("auth_token")).toBeNull();
  });

  // --- 異常系 ---
  it("ログアウト API エラー時でもトークンが削除されて認証ページに遷移する", async () => {
    const user = userEvent.setup();
    mockLogout.mockRejectedValue(new ApiError(500, "Internal Server Error"));

    renderWithProvider();

    await user.click(screen.getByRole("button", { name: "ログアウト" }));

    await waitFor(() => {
      expect(screen.getByText("認証ページ")).toBeInTheDocument();
    });
    expect(localStorage.getItem("auth_token")).toBeNull();
  });

  it("ネットワークエラー時でもトークンが削除されて認証ページへ遷移する", async () => {
    const user = userEvent.setup();
    mockLogout.mockRejectedValue(new Error("Network error"));

    renderWithProvider();

    await user.click(screen.getByRole("button", { name: "ログアウト" }));

    await waitFor(() => {
      expect(screen.getByText("認証ページ")).toBeInTheDocument();
    });
    expect(localStorage.getItem("auth_token")).toBeNull();
  });

  it("推し一覧の取得に失敗したときエラーメッセージが表示される", async () => {
    mockGetOshis.mockRejectedValue(new ApiError(500, "Internal Server Error"));

    renderWithProvider();

    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
    });
  });

  // --- 境界値 ---
  it("ローディング中はボタンが無効になる", async () => {
    // 解決しない Promise でローディング状態を維持する
    // fireEvent を使用: userEvent はすべてのマイクロタスクを flush するため
    // 非同期ハンドラの中間状態テストには fireEvent が適切
    mockLogout.mockImplementation(() => new Promise<void>(() => undefined));

    renderWithProvider();

    // 推し一覧の取得完了を待つ
    await waitFor(() => {
      expect(mockGetOshis).toHaveBeenCalledOnce();
    });

    fireEvent.click(screen.getByRole("button", { name: "ログアウト" }));

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "ログアウト中..." }),
      ).toBeDisabled();
    });
  });

  it("空の推し一覧のとき「まだ推しが登録されていません」が表示される", async () => {
    mockGetOshis.mockResolvedValue([]);

    renderWithProvider();

    await waitFor(() => {
      expect(
        screen.getByText(
          "まだ推しが登録されていません。上のフォームから追加してください。",
        ),
      ).toBeInTheDocument();
    });
  });

  // --- OshiFilter ---
  it("OshiFilter の「推しで絞り込み」セレクトが表示される", async () => {
    renderWithProvider();

    await waitFor(() => {
      expect(
        screen.getByRole("combobox", { name: "推しで絞り込み" }),
      ).toBeInTheDocument();
    });
  });

  it("推しが登録されているとき OshiFilter にオプションが表示される", async () => {
    mockGetOshis.mockResolvedValue([
      { id: 1, name: "推しA", createdAt: "2026-07-01T00:00:00+09:00" },
    ]);

    renderWithProvider();

    // OshiList と OshiFilter の両方に「推しA」が現れるため getAllByText を使う
    await waitFor(() => {
      expect(screen.getAllByText("推しA").length).toBeGreaterThan(0);
    });
  });

  it("URLに oshiId クエリパラメータがあるとき推しが選択状態になる", async () => {
    mockGetOshis.mockResolvedValue([
      { id: 42, name: "推しB", createdAt: "2026-07-01T00:00:00+09:00" },
    ]);

    renderWithProvider("/?oshiId=42");

    await waitFor(() => {
      expect(
        screen.getByRole("combobox", { name: "推しで絞り込み" }),
      ).toHaveValue("42");
    });
  });

  it("OshiFilter で推しを選択するとフィードが該当推しIDで再取得される", async () => {
    const user = userEvent.setup();
    mockGetOshis.mockResolvedValue([
      { id: 5, name: "推しC", createdAt: "2026-07-01T00:00:00+09:00" },
    ]);

    renderWithProvider();

    // OshiList と OshiFilter の両方に「推しC」が現れるため getAllByText を使う
    await waitFor(() => {
      expect(screen.getAllByText("推しC").length).toBeGreaterThan(0);
    });

    await user.selectOptions(
      screen.getByRole("combobox", { name: "推しで絞り込み" }),
      "推しC",
    );

    await waitFor(() => {
      // oshiId=5 で getNews が呼ばれていることを確認
      expect(mockGetNews).toHaveBeenCalledWith(1, 20, 5);
    });
  });

  // --- CollectorStatus ---
  it("収集ジョブ状況セクションのタイトルが表示される", async () => {
    renderWithProvider();

    await waitFor(() => {
      expect(screen.getByText("収集ジョブ状況")).toBeInTheDocument();
    });
  });

  it("収集ジョブが実行済みのとき実行情報が表示される", async () => {
    mockGetCollectorStatus.mockResolvedValue({
      latestNewsJob: {
        id: 1,
        jobType: "news",
        executedAt: "2026-07-17T10:00:00+09:00",
        successCount: 8,
        failureCount: 1,
        createdAt: "2026-07-17T10:00:00+09:00",
      },
      latestProgramJob: null,
    });

    renderWithProvider();

    await waitFor(() => {
      expect(screen.getByText(/成功: 8件/)).toBeInTheDocument();
    });
  });

  it("収集ジョブが未実行のとき「未実行」が表示される", async () => {
    mockGetCollectorStatus.mockResolvedValue({
      latestNewsJob: null,
      latestProgramJob: null,
    });

    renderWithProvider();

    await waitFor(() => {
      expect(screen.getAllByText("未実行")).toHaveLength(2);
    });
  });
});
