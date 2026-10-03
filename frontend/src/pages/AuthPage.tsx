import { useState } from "react";
import { Navigate } from "react-router-dom";
import { LoginForm } from "../components/auth/LoginForm";
import { RegisterForm } from "../components/auth/RegisterForm";
import { useAuth } from "../context/AuthContext";

type Tab = "login" | "register";

/**
 * 認証ページ: ログイン / 新規登録 をタブで切り替える。
 * 認証済みの場合はダッシュボードにリダイレクトする。
 */
export function AuthPage() {
  const { isAuthenticated } = useAuth();
  const [activeTab, setActiveTab] = useState<Tab>("login");

  if (isAuthenticated) {
    return <Navigate to="/" replace />;
  }

  return (
    <div className="auth-page">
      <div className="auth-card">
        <h1 className="auth-title">oshikatsu</h1>

        <nav className="auth-tabs" role="tablist" aria-label="認証タブ">
          <button
            className="auth-tab"
            role="tab"
            aria-selected={activeTab === "login"}
            onClick={() => setActiveTab("login")}
          >
            ログイン
          </button>
          <button
            className="auth-tab"
            role="tab"
            aria-selected={activeTab === "register"}
            onClick={() => setActiveTab("register")}
          >
            新規登録
          </button>
        </nav>

        <div role="tabpanel">
          {activeTab === "login" ? <LoginForm /> : <RegisterForm />}
        </div>
      </div>
    </div>
  );
}
