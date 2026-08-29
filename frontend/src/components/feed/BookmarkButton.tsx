import { useState } from "react";
import { addBookmark, removeBookmark } from "../../api/bookmark";
import { ApiError } from "../../api/client";

interface BookmarkButtonProps {
  /** ブックマーク対象のニュースアイテムID */
  newsItemId: number;
  /** 初期ブックマーク状態（未指定の場合は false）*/
  initialIsBookmarked?: boolean;
}

/**
 * ニュース記事のブックマークトグルボタン。
 * クリックでブックマーク登録・解除を行い、楽観的UIで状態を即時更新する。
 */
export function BookmarkButton({
  newsItemId,
  initialIsBookmarked = false,
}: BookmarkButtonProps) {
  const [isBookmarked, setIsBookmarked] = useState(initialIsBookmarked);
  const [isLoading, setIsLoading] = useState(false);

  const handleClick = async () => {
    if (isLoading) return;
    setIsLoading(true);

    if (isBookmarked) {
      // ブックマーク解除
      try {
        await removeBookmark(newsItemId);
        setIsBookmarked(false);
      } catch (err) {
        if (err instanceof ApiError && err.status === 404) {
          // すでに解除済み → 状態を合わせる
          setIsBookmarked(false);
        }
        // その他のエラーは状態を維持
      } finally {
        setIsLoading(false);
      }
    } else {
      // ブックマーク登録
      try {
        await addBookmark(newsItemId);
        setIsBookmarked(true);
      } catch (err) {
        if (err instanceof ApiError && err.status === 409) {
          // すでに登録済み → 状態を合わせる
          setIsBookmarked(true);
        }
        // その他のエラーは状態を維持
      } finally {
        setIsLoading(false);
      }
    }
  };

  return (
    <button
      type="button"
      onClick={handleClick}
      disabled={isLoading}
      aria-label={isBookmarked ? "ブックマークを解除" : "ブックマークに追加"}
      aria-pressed={isBookmarked}
    >
      {isBookmarked ? "ブックマーク済み" : "ブックマーク"}
    </button>
  );
}
