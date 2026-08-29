interface FeedPaginationProps {
  currentPage: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

/**
 * フィード用ページネーションコントロール。
 * totalPages が 1 以下の場合は何も描画しない。
 */
export function FeedPagination({
  currentPage,
  totalPages,
  onPageChange,
}: FeedPaginationProps) {
  if (totalPages <= 1) return null;

  return (
    <nav aria-label="ページネーション">
      <button
        onClick={() => onPageChange(currentPage - 1)}
        disabled={currentPage <= 1}
        aria-label="前のページ"
      >
        前へ
      </button>
      <span aria-current="page">
        {currentPage} / {totalPages} ページ
      </span>
      <button
        onClick={() => onPageChange(currentPage + 1)}
        disabled={currentPage >= totalPages}
        aria-label="次のページ"
      >
        次へ
      </button>
    </nav>
  );
}
