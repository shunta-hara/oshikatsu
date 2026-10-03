import { type ChangeEvent, type FormEvent, useState } from "react";
import { z } from "zod";
import { createOshi } from "../../api/oshi";
import { ApiError } from "../../api/client";
import type { OshiResponse } from "../../types/oshi";

const addOshiSchema = z.object({
  name: z
    .string()
    .min(1, "推しの名前を入力してください")
    .max(100, "名前は100文字以内で入力してください"),
});

interface OshiFormProps {
  /** 登録成功時に呼ばれるコールバック */
  onOshiAdded: (oshi: OshiResponse) => void;
}

export function OshiForm({ onOshiAdded }: OshiFormProps) {
  const [name, setName] = useState("");
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const handleChange = (e: ChangeEvent<HTMLInputElement>) => {
    setName(e.target.value);
    setFieldError(null);
    setApiError(null);
    setSuccessMessage(null);
  };

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setFieldError(null);
    setApiError(null);
    setSuccessMessage(null);

    const result = addOshiSchema.safeParse({ name });
    if (!result.success) {
      setFieldError(result.error.issues[0].message);
      return;
    }

    setIsLoading(true);
    try {
      const oshi = await createOshi(result.data.name);
      onOshiAdded(oshi);
      setName("");
      setSuccessMessage("登録しました。数分後にニュースが表示されます");
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 422) {
          setApiError("推しの登録は最大20件までです");
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
    <form onSubmit={handleSubmit} aria-label="推し追加フォーム">
      <div>
        <label htmlFor="oshi-name">推しの名前</label>
        <input
          id="oshi-name"
          type="text"
          value={name}
          onChange={handleChange}
          disabled={isLoading}
          placeholder="例：鈴木一郎"
          aria-invalid={fieldError !== null}
          aria-describedby={fieldError ? "oshi-name-error" : undefined}
        />
        {fieldError && (
          <span id="oshi-name-error" role="alert">
            {fieldError}
          </span>
        )}
      </div>

      {apiError && (
        <div role="alert" aria-live="assertive">
          {apiError}
        </div>
      )}

      {successMessage && (
        <div role="status" aria-live="polite">
          {successMessage}
        </div>
      )}

      <button type="submit" disabled={isLoading}>
        {isLoading ? "登録中..." : "追加"}
      </button>
    </form>
  );
}
