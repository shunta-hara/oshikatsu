import { useState } from "react";
import { deleteOshi } from "../../api/oshi";
import { ApiError } from "../../api/client";
import type { OshiResponse } from "../../types/oshi";

interface OshiListProps {
  oshis: OshiResponse[];
  /** 削除成功時に呼ばれるコールバック */
  onOshiDeleted: (id: number) => void;
}

export function OshiList({ oshis, onOshiDeleted }: OshiListProps) {
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [deleteErrors, setDeleteErrors] = useState<Record<number, string>>({});

  if (oshis.length === 0) {
    return (
      <p>まだ推しが登録されていません。上のフォームから追加してください。</p>
    );
  }

  const handleDelete = async (oshi: OshiResponse) => {
    if (!window.confirm(`「${oshi.name}」を削除しますか？`)) {
      return;
    }

    setDeletingId(oshi.id);
    setDeleteErrors((prev) => {
      const next = { ...prev };
      delete next[oshi.id];
      return next;
    });

    try {
      await deleteOshi(oshi.id);
      onOshiDeleted(oshi.id);
    } catch (err) {
      let message =
        "削除に失敗しました。しばらくしてからもう一度お試しください";
      if (err instanceof ApiError) {
        message = err.message;
      }
      setDeleteErrors((prev) => ({ ...prev, [oshi.id]: message }));
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <ul aria-label="推し一覧">
      {oshis.map((oshi) => (
        <li key={oshi.id}>
          <span>{oshi.name}</span>
          {deleteErrors[oshi.id] && (
            <span role="alert">{deleteErrors[oshi.id]}</span>
          )}
          <button
            onClick={() => void handleDelete(oshi)}
            disabled={deletingId === oshi.id}
            aria-label={`${oshi.name}を削除`}
          >
            {deletingId === oshi.id ? "削除中..." : "削除"}
          </button>
        </li>
      ))}
    </ul>
  );
}
