import { useState } from "react";
import { NavLink, useNavigate } from "react-router-dom";
import { logout } from "../api/auth";
import { useAuth } from "../context/AuthContext";

/**
 * 認証済み画面共通のナビゲーションバー。
 * ダッシュボード・ブックマーク一覧へのリンクとログアウトボタンを含む。
 */
export function NavBar() {
  const { clearAuth } = useAuth();
  const navigate = useNavigate();
  const [isLoggingOut, setIsLoggingOut] = useState(false);

  const handleLogout = async () => {
    setIsLoggingOut(true);
    try {
      await logout();
    } catch {
      // API エラー・ネットワークエラーのいずれでもクライアント側トークンを削除する
    } finally {
      clearAuth();
      navigate("/auth", { replace: true });
    }
  };

  return (
    <nav className="navbar" aria-label="メインナビゲーション">
      <ul className="navbar__links">
        <li>
          <NavLink to="/" end className="navbar__link">
            ダッシュボード
          </NavLink>
        </li>
        <li>
          <NavLink to="/bookmarks" className="navbar__link">
            ブックマーク
          </NavLink>
        </li>
      </ul>
      <button
        className="navbar__logout"
        onClick={() => void handleLogout()}
        disabled={isLoggingOut}
      >
        {isLoggingOut ? "ログアウト中..." : "ログアウト"}
      </button>
    </nav>
  );
}
