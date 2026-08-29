import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { RegisterForm } from "../components/auth/RegisterForm";
import * as authApi from "../api/auth";
import { ApiError } from "../api/client";
import { AuthProvider } from "../context/AuthContext";

vi.mock("../api/auth");
const mockRegister = vi.mocked(authApi.register);

function renderWithProvider() {
  return render(
    <AuthProvider>
      <RegisterForm />
    </AuthProvider>,
  );
}

describe("RegisterForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  // --- 正常系 ---
  it("正しい情報で登録するとトークンが保存される", async () => {
    const user = userEvent.setup();
    const futureDate = new Date(Date.now() + 3600000).toISOString();
    mockRegister.mockResolvedValueOnce({
      token: "new-token",
      expiresAt: futureDate,
    });

    renderWithProvider();

    await user.type(screen.getByLabelText("メールアドレス"), "new@example.com");
    await user.type(screen.getByLabelText("パスワード"), "password123");
    await user.type(screen.getByLabelText("パスワード（確認）"), "password123");
    await user.click(screen.getByRole("button", { name: "新規登録" }));

    await waitFor(() => {
      expect(localStorage.getItem("auth_token")).toBe("new-token");
    });
    expect(mockRegister).toHaveBeenCalledWith("new@example.com", "password123");
  });

  it("ローディング中はボタンが無効になる", async () => {
    const user = userEvent.setup();
    mockRegister.mockImplementationOnce(() => new Promise(() => undefined));

    renderWithProvider();

    await user.type(screen.getByLabelText("メールアドレス"), "new@example.com");
    await user.type(screen.getByLabelText("パスワード"), "password123");
    await user.type(screen.getByLabelText("パスワード（確認）"), "password123");
    await user.click(screen.getByRole("button", { name: "新規登録" }));

    expect(screen.getByRole("button", { name: "登録中..." })).toBeDisabled();
  });

  // --- 異常系 ---
  it("409 エラー時に「このメールアドレスはすでに登録されています」と表示する", async () => {
    const user = userEvent.setup();
    mockRegister.mockRejectedValueOnce(
      new ApiError(409, "The email address is already registered."),
    );

    renderWithProvider();

    await user.type(
      screen.getByLabelText("メールアドレス"),
      "exists@example.com",
    );
    await user.type(screen.getByLabelText("パスワード"), "password123");
    await user.type(screen.getByLabelText("パスワード（確認）"), "password123");
    await user.click(screen.getByRole("button", { name: "新規登録" }));

    await waitFor(() => {
      expect(
        screen.getByText("このメールアドレスはすでに登録されています"),
      ).toBeInTheDocument();
    });
  });

  it("ネットワークエラー時にフォールバックエラーメッセージを表示する", async () => {
    const user = userEvent.setup();
    mockRegister.mockRejectedValueOnce(new Error("Network error"));

    renderWithProvider();

    await user.type(screen.getByLabelText("メールアドレス"), "new@example.com");
    await user.type(screen.getByLabelText("パスワード"), "password123");
    await user.type(screen.getByLabelText("パスワード（確認）"), "password123");
    await user.click(screen.getByRole("button", { name: "新規登録" }));

    await waitFor(() => {
      expect(
        screen.getByText(
          "登録に失敗しました。しばらくしてからもう一度お試しください",
        ),
      ).toBeInTheDocument();
    });
  });

  // --- バリデーション ---
  it("パスワードが一致しない場合はAPIを呼ばない", async () => {
    const user = userEvent.setup();
    renderWithProvider();

    await user.type(screen.getByLabelText("メールアドレス"), "new@example.com");
    await user.type(screen.getByLabelText("パスワード"), "password123");
    await user.type(
      screen.getByLabelText("パスワード（確認）"),
      "different456",
    );
    await user.click(screen.getByRole("button", { name: "新規登録" }));

    expect(screen.getByText("パスワードが一致しません")).toBeInTheDocument();
    expect(mockRegister).not.toHaveBeenCalled();
  });

  it("無効なメール形式ではAPIを呼ばない", async () => {
    const user = userEvent.setup();
    renderWithProvider();

    await user.type(screen.getByLabelText("メールアドレス"), "not-an-email");
    await user.type(screen.getByLabelText("パスワード"), "password123");
    await user.type(screen.getByLabelText("パスワード（確認）"), "password123");
    await user.click(screen.getByRole("button", { name: "新規登録" }));

    expect(
      screen.getByText("有効なメールアドレスを入力してください"),
    ).toBeInTheDocument();
    expect(mockRegister).not.toHaveBeenCalled();
  });

  // --- 境界値 ---
  it("パスワードが7文字以下の場合はエラーを表示する", async () => {
    const user = userEvent.setup();
    renderWithProvider();

    await user.type(screen.getByLabelText("メールアドレス"), "new@example.com");
    await user.type(screen.getByLabelText("パスワード"), "short");
    await user.type(screen.getByLabelText("パスワード（確認）"), "short");
    await user.click(screen.getByRole("button", { name: "新規登録" }));

    expect(
      screen.getByText("パスワードは8文字以上で入力してください"),
    ).toBeInTheDocument();
    expect(mockRegister).not.toHaveBeenCalled();
  });

  it("パスワードが8文字ちょうどなら登録できる", async () => {
    const user = userEvent.setup();
    const futureDate = new Date(Date.now() + 3600000).toISOString();
    mockRegister.mockResolvedValueOnce({
      token: "new-token",
      expiresAt: futureDate,
    });

    renderWithProvider();

    await user.type(screen.getByLabelText("メールアドレス"), "new@example.com");
    await user.type(screen.getByLabelText("パスワード"), "exactly8");
    await user.type(screen.getByLabelText("パスワード（確認）"), "exactly8");
    await user.click(screen.getByRole("button", { name: "新規登録" }));

    await waitFor(() => {
      expect(mockRegister).toHaveBeenCalledWith("new@example.com", "exactly8");
    });
  });
});
