import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it } from "vitest";
import { ProtectedRoute } from "../components/ProtectedRoute";
import { AuthProvider } from "../context/AuthContext";

function renderWithRouter(initialPath: string) {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={[initialPath]}>
        <Routes>
          <Route path="/auth" element={<div>認証ページ</div>} />
          <Route
            path="/"
            element={
              <ProtectedRoute>
                <div>保護されたコンテンツ</div>
              </ProtectedRoute>
            }
          />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  );
}

describe("ProtectedRoute", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  // --- 正常系 ---
  it("認証済みの場合は子コンポーネントを表示する", () => {
    const futureDate = new Date(Date.now() + 3600000).toISOString();
    localStorage.setItem("auth_token", "valid-token");
    localStorage.setItem("auth_expires_at", futureDate);

    renderWithRouter("/");

    expect(screen.getByText("保護されたコンテンツ")).toBeInTheDocument();
  });

  // --- 異常系 ---
  it("未認証の場合は /auth にリダイレクトする", () => {
    renderWithRouter("/");

    expect(screen.getByText("認証ページ")).toBeInTheDocument();
    expect(screen.queryByText("保護されたコンテンツ")).not.toBeInTheDocument();
  });

  // --- 境界値 ---
  it("トークンはあるが有効期限切れの場合は /auth にリダイレクトする", () => {
    const pastDate = new Date(Date.now() - 1000).toISOString();
    localStorage.setItem("auth_token", "expired-token");
    localStorage.setItem("auth_expires_at", pastDate);

    renderWithRouter("/");

    expect(screen.getByText("認証ページ")).toBeInTheDocument();
    expect(screen.queryByText("保護されたコンテンツ")).not.toBeInTheDocument();
  });

  it("トークンはあるが有効期限情報がない場合は /auth にリダイレクトする", () => {
    localStorage.setItem("auth_token", "token-without-expiry");

    renderWithRouter("/");

    expect(screen.getByText("認証ページ")).toBeInTheDocument();
  });
});
