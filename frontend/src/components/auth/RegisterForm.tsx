import { type ChangeEvent, type FormEvent, useState } from "react";
import { z } from "zod";
import { register } from "../../api/auth";
import { ApiError } from "../../api/client";
import { useAuth } from "../../context/AuthContext";

const registerSchema = z
  .object({
    email: z.string().email("有効なメールアドレスを入力してください"),
    password: z.string().min(8, "パスワードは8文字以上で入力してください"),
    confirmPassword: z.string(),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "パスワードが一致しません",
    path: ["confirmPassword"],
  });

type RegisterFormData = z.infer<typeof registerSchema>;

interface FieldErrors {
  email?: string;
  password?: string;
  confirmPassword?: string;
}

export function RegisterForm() {
  const { saveAuth } = useAuth();
  const [formData, setFormData] = useState<RegisterFormData>({
    email: "",
    password: "",
    confirmPassword: "",
  });
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [apiError, setApiError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const handleChange = (e: ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    // 入力変更でフィールドエラーをクリア
    setFieldErrors((prev) => ({ ...prev, [name]: undefined }));
    setApiError(null);
  };

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setApiError(null);
    setFieldErrors({});

    const result = registerSchema.safeParse(formData);
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
      const auth = await register(result.data.email, result.data.password);
      saveAuth({ token: auth.token, expiresAt: auth.expiresAt });
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 409) {
          setApiError("このメールアドレスはすでに登録されています");
        } else {
          setApiError(err.message);
        }
      } else {
        setApiError(
          "登録に失敗しました。しばらくしてからもう一度お試しください",
        );
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} noValidate aria-label="新規登録フォーム">
      <div>
        <label htmlFor="register-email">メールアドレス</label>
        <input
          id="register-email"
          name="email"
          type="email"
          value={formData.email}
          onChange={handleChange}
          disabled={isLoading}
          aria-describedby={
            fieldErrors.email ? "register-email-error" : undefined
          }
          aria-invalid={fieldErrors.email !== undefined}
        />
        {fieldErrors.email && (
          <span id="register-email-error" role="alert">
            {fieldErrors.email}
          </span>
        )}
      </div>

      <div>
        <label htmlFor="register-password">パスワード</label>
        <input
          id="register-password"
          name="password"
          type="password"
          value={formData.password}
          onChange={handleChange}
          disabled={isLoading}
          aria-describedby={
            fieldErrors.password ? "register-password-error" : undefined
          }
          aria-invalid={fieldErrors.password !== undefined}
        />
        {fieldErrors.password && (
          <span id="register-password-error" role="alert">
            {fieldErrors.password}
          </span>
        )}
      </div>

      <div>
        <label htmlFor="register-confirm-password">パスワード（確認）</label>
        <input
          id="register-confirm-password"
          name="confirmPassword"
          type="password"
          value={formData.confirmPassword}
          onChange={handleChange}
          disabled={isLoading}
          aria-describedby={
            fieldErrors.confirmPassword ? "register-confirm-error" : undefined
          }
          aria-invalid={fieldErrors.confirmPassword !== undefined}
        />
        {fieldErrors.confirmPassword && (
          <span id="register-confirm-error" role="alert">
            {fieldErrors.confirmPassword}
          </span>
        )}
      </div>

      {apiError && (
        <div role="alert" aria-live="assertive">
          {apiError}
        </div>
      )}

      <button type="submit" disabled={isLoading}>
        {isLoading ? "登録中..." : "新規登録"}
      </button>
    </form>
  );
}
