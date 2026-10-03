import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { NavBar } from "../components/NavBar";
import * as authApi from "../api/auth";
import { AuthProvider } from "../context/AuthContext";

vi.mock("../api/auth");
const mockLogout = vi.mocked(authApi.logout);

/**
 * NavBar を AuthProvider + MemoryRouter 内でレンダリングするヘルパー。
 * ログアウト後の遷移先として /auth に「認証ページ」を配置する。
 */
function renderNavBar(initialUrl = "/") {
  const futureDate = new Date(Date.now() + 3600000).toISOString();
  localStorage.setItem("auth_token", "valid-token");
  localStorage.setItem("auth_expires_at", futureDate);

  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={[initialUrl]}>
        <Routes>
          <Route path="/auth" element={<div>認証ページ</div>} />
          <Route path="*" element={<NavBar />} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  );
}

describe("NavBar", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    localStorage.clear();
  });

  // ── 正常系 ────────────────────────────────────────────────────────────────

  it("ダッシュボードへのリンクが表示される", () => {
    mockLogout.mockResolvedValue(undefined);
    renderNavBar();

    expect(
      screen.getByRole("link", { name: "ダッシュボード" }),
    ).toBeInTheDocument();
  });

  it("ブックマークへのリンクが表示される", () => {
    mockLogout.mockResolvedValue(undefined);
    renderNavBar();

    expect(
      screen.getByRole("link", { name: "ブックマーク" }),
    ).toBeInTheDocument();
  });

  it("ログアウトボタンが表示される", () => {
    mockLogout.mockResolvedValue(undefined);
    renderNavBar();

    expect(
      screen.getByRole("button", { name: "ログアウト" }),
    ).toBeInTheDocument();
  });

  it("ダッシュボードリンクが / を指している", () => {
    mockLogout.mockResolvedValue(undefined);
    renderNavBar();

    expect(
      screen.getByRole("link", { name: "ダッシュボード" }),
    ).toHaveAttribute("href", "/");
  });

  it("ブックマークリンクが /bookmarks を指している", () => {
    mockLogout.mockResolvedValue(undefined);
    renderNavBar();

    expect(screen.getByRole("link", { name: "ブックマーク" })).toHaveAttribute(
      "href",
      "/bookmarks",
    );
  });

  it("ログアウトボタンをクリックするとトークンが削除されて認証ページに遷移する", async () => {
    const user = userEvent.setup();
    mockLogout.mockResolvedValue(undefined);

    renderNavBar();

    await user.click(screen.getByRole("button", { name: "ログアウト" }));

    await waitFor(() => {
      expect(screen.getByText("認証ページ")).toBeInTheDocument();
    });
    expect(localStorage.getItem("auth_token")).toBeNull();
  });

  // ── 異常系 ────────────────────────────────────────────────────────────────

  it("ログアウト API エラー時でもトークンが削除されて認証ページに遷移する", async () => {
    const user = userEvent.setup();
    mockLogout.mockRejectedValue(new Error("Internal Server Error"));

    renderNavBar();

    await user.click(screen.getByRole("button", { name: "ログアウト" }));

    await waitFor(() => {
      expect(screen.getByText("認証ページ")).toBeInTheDocument();
    });
    expect(localStorage.getItem("auth_token")).toBeNull();
  });

  it("ネットワークエラー時でもトークンが削除されて認証ページに遷移する", async () => {
    const user = userEvent.setup();
    mockLogout.mockRejectedValue(new Error("Network error"));

    renderNavBar();

    await user.click(screen.getByRole("button", { name: "ログアウト" }));

    await waitFor(() => {
      expect(screen.getByText("認証ページ")).toBeInTheDocument();
    });
    expect(localStorage.getItem("auth_token")).toBeNull();
  });

  // ── 境界値 ────────────────────────────────────────────────────────────────

  it("ログアウト中はボタンが「ログアウト中...」になり無効化される", async () => {
    mockLogout.mockImplementation(() => new Promise<void>(() => undefined));

    renderNavBar();

    fireEvent.click(screen.getByRole("button", { name: "ログアウト" }));

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "ログアウト中..." }),
      ).toBeDisabled();
    });
  });

  it("メインナビゲーションのランドマークが存在する", () => {
    mockLogout.mockResolvedValue(undefined);
    renderNavBar();

    expect(
      screen.getByRole("navigation", { name: "メインナビゲーション" }),
    ).toBeInTheDocument();
  });
});
