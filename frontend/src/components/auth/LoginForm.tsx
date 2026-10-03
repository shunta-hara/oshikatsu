import { type ChangeEvent, type FormEvent, useState } from "react";
import { z } from "zod";
import { login } from "../../api/auth";
import { ApiError } from "../../api/client";
import { useAuth } from "../../context/AuthContext";

const loginSchema = z.object({
  email: z.string().email("有効なメールアドレスを入力してください"),
  password: z.string().min(1, "パスワードを入力してください"),
});

type LoginFormData = z.infer<typeof loginSchema>;

interface FieldErrors {
  email?: string;
  password?: string;
}

export function LoginForm() {
  const { saveAuth } = useAuth();
  const [formData, setFormData] = useState<LoginFormData>({
    email: "",
    password: "",
  });
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [apiError, setApiError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const handleChange = (e: ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    setFieldErrors((prev) => ({ ...prev, [name]: undefined }));
    setApiError(null);
  };

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setApiError(null);
    setFieldErrors({});

    const result = loginSchema.safeParse(formData);
    if (!result.success) {
      const errors: FieldErrors = {};
      for (const issue of result.error.issues) {
        const field = issue.path[0] as keyof FieldErrors;
        errors[field] = issue.message;
      }
      setFieldErrors(errors);
      return;
    }

    setIsLoading(true);
    try {
      const auth = await login(result.data.email, result.data.password);
      saveAuth({ token: auth.token, expiresAt: auth.expiresAt });
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 401) {
          setApiError("メールアドレスまたはパスワードが正しくありません");
        } else {
          setApiError(err.message);
        }
      } else {
        setApiError(
          "ログインに失敗しました。しばらくしてからもう一度お試しください",
        );
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <form
      onSubmit={handleSubmit}
      noValidate
      aria-label="ログインフォーム"
      className="auth-form"
    >
      <div className="form-field">
        <label htmlFor="login-email">メールアドレス</label>
        <input
          id="login-email"
          name="email"
          type="email"
          value={formData.email}
          onChange={handleChange}
          disabled={isLoading}
          aria-describedby={fieldErrors.email ? "login-email-error" : undefined}
          aria-invalid={fieldErrors.email !== undefined}
        />
        {fieldErrors.email && (
          <span id="login-email-error" role="alert" className="field-error">
            {fieldErrors.email}
          </span>
        )}
      </div>

      <div className="form-field">
        <label htmlFor="login-password">パスワード</label>
        <input
          id="login-password"
          name="password"
          type="password"
          value={formData.password}
          onChange={handleChange}
          disabled={isLoading}
          aria-describedby={
            fieldErrors.password ? "login-password-error" : undefined
          }
          aria-invalid={fieldErrors.password !== undefined}
        />
        {fieldErrors.password && (
          <span id="login-password-error" role="alert" className="field-error">
            {fieldErrors.password}
          </span>
        )}
      </div>

      {apiError && (
        <div role="alert" aria-live="assertive" className="error-message">
          {apiError}
        </div>
      )}

      <button type="submit" disabled={isLoading} className="btn-primary">
        {isLoading ? "ログイン中..." : "ログイン"}
      </button>
    </form>
  );
}
