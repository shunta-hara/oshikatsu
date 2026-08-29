import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
} from "react";
import type { ReactNode } from "react";
import type { StoredAuth } from "../types/auth";

interface AuthContextValue {
  /** ローカルストレージから読み込んだトークン（未認証時は null） */
  token: string | null;
  /** ログイン成功後にトークンを保存する */
  saveAuth: (auth: StoredAuth) => void;
  /** ログアウト時にトークンを削除する */
  clearAuth: () => void;
  /** 認証済みかどうか（トークンが存在し有効期限内） */
  isAuthenticated: boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

const TOKEN_KEY = "auth_token";
const EXPIRES_KEY = "auth_expires_at";

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(() =>
    localStorage.getItem(TOKEN_KEY),
  );
  const [expiresAt, setExpiresAt] = useState<string | null>(() =>
    localStorage.getItem(EXPIRES_KEY),
  );

  const isAuthenticated =
    token !== null && expiresAt !== null && new Date(expiresAt) > new Date();

  const saveAuth = useCallback((auth: StoredAuth) => {
    localStorage.setItem(TOKEN_KEY, auth.token);
    localStorage.setItem(EXPIRES_KEY, auth.expiresAt);
    setToken(auth.token);
    setExpiresAt(auth.expiresAt);
  }, []);

  const clearAuth = useCallback(() => {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(EXPIRES_KEY);
    setToken(null);
    setExpiresAt(null);
  }, []);

  // 有効期限切れのトークンを自動削除する
  useEffect(() => {
    if (token && expiresAt && new Date(expiresAt) <= new Date()) {
      clearAuth();
    }
  }, [token, expiresAt, clearAuth]);

  return (
    <AuthContext.Provider
      value={{ token, saveAuth, clearAuth, isAuthenticated }}
    >
      {children}
    </AuthContext.Provider>
  );
}

/** AuthContext を取得する。Provider 外で使うと例外を投げる */
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error("useAuth must be used within AuthProvider");
  }
  return ctx;
}
