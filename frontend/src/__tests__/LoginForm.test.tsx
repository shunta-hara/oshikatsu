import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { LoginForm } from "../components/auth/LoginForm";
import * as authApi from "../api/auth";
import { ApiError } from "../api/client";
import { AuthProvider } from "../context/AuthContext";

// API モジュールをモック
vi.mock("../api/auth");
const mockLogin = vi.mocked(authApi.login);

function renderWithProvider() {
  return render(
    <AuthProvider>
      <LoginForm />
    </AuthProvider>,
  );
}

describe("LoginForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  // --- 正常系 ---
  it("正しい認証情報でログインするとトークンが保存される", async () => {
    const user = userEvent.setup();
    const futureDate = new Date(Date.now() + 3600000).toISOString();
    mockLogin.mockResolvedValueOnce({
      token: "test-token",
      expiresAt: futureDate,
    });

    renderWithProvider();

    await user.type(
      screen.getByLabelText("メールアドレス"),
      "user@example.com",
    );
    await user.type(screen.getByLabelText("パスワード"), "password123");
    await user.click(screen.getByRole("button", { name: "ログイン" }));

    await waitFor(() => {
      expect(localStorage.getItem("auth_token")).toBe("test-token");
    });
  });

  it("ローディング中はボタンのテキストが変わる", async () => {
    const user = userEvent.setup();
    // 応答が遅い API をシミュレート
    mockLogin.mockImplementationOnce(() => new Promise(() => undefined));

    renderWithProvider();

    await user.type(
      screen.getByLabelText("メールアドレス"),
      "user@example.com",
    );
    await user.type(screen.getByLabelText("パスワード"), "password123");
    await user.click(screen.getByRole("button", { name: "ログイン" }));

    expect(
      screen.getByRole("button", { name: "ログイン中..." }),
    ).toBeDisabled();
  });

  // --- 異常系 ---
  it("401 エラー時に「メールアドレスまたはパスワードが正しくありません」と表示する", async () => {
    const user = userEvent.setup();
    mockLogin.mockRejectedValueOnce(
      new ApiError(401, "Invalid email or password."),
    );

    renderWithProvider();

    await user.type(
      screen.getByLabelText("メールアドレス"),
      "user@example.com",
    );
    await user.type(screen.getByLabelText("パスワード"), "wrongpassword");
    await user.click(screen.getByRole("button", { name: "ログイン" }));

    await waitFor(() => {
      expect(
        screen.getByText("メールアドレスまたはパスワードが正しくありません"),
      ).toBeInTheDocument();
    });
  });

  it("ネットワークエラー時にフォールバックエラーメッセージを表示する", async () => {
    const user = userEvent.setup();
    mockLogin.mockRejectedValueOnce(new Error("Network error"));

    renderWithProvider();

    await user.type(
      screen.getByLabelText("メールアドレス"),
      "user@example.com",
    );
    await user.type(screen.getByLabelText("パスワード"), "password123");
    await user.click(screen.getByRole("button", { name: "ログイン" }));

    await waitFor(() => {
      expect(
        screen.getByText(
          "ログインに失敗しました。しばらくしてからもう一度お試しください",
        ),
      ).toBeInTheDocument();
    });
  });

  // --- バリデーション（クライアント側） ---
  it("空のメールアドレスでサブミットするとバリデーションエラーが表示される", async () => {
    const user = userEvent.setup();
    renderWithProvider();

    await user.type(screen.getByLabelText("パスワード"), "password123");
    await user.click(screen.getByRole("button", { name: "ログイン" }));

    expect(
      screen.getByText("有効なメールアドレスを入力してください"),
    ).toBeInTheDocument();
    expect(mockLogin).not.toHaveBeenCalled();
  });

  it("空のパスワードでサブミットするとバリデーションエラーが表示される", async () => {
    const user = userEvent.setup();
    renderWithProvider();

    await user.type(
      screen.getByLabelText("メールアドレス"),
      "user@example.com",
    );
    await user.click(screen.getByRole("button", { name: "ログイン" }));

    expect(
      screen.getByText("パスワードを入力してください"),
    ).toBeInTheDocument();
    expect(mockLogin).not.toHaveBeenCalled();
  });

  // --- 境界値 ---
  it("無効なメール形式ではAPIを呼ばない", async () => {
    const user = userEvent.setup();
    renderWithProvider();

    await user.type(screen.getByLabelText("メールアドレス"), "not-an-email");
    await user.type(screen.getByLabelText("パスワード"), "password123");
    await user.click(screen.getByRole("button", { name: "ログイン" }));

    expect(mockLogin).not.toHaveBeenCalled();
    expect(
      screen.getByText("有効なメールアドレスを入力してください"),
    ).toBeInTheDocument();
  });
});
