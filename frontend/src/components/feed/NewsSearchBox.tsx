import { useState } from "react";

interface NewsSearchBoxProps {
  /** 現在のコミット済み検索キーワード（クリアボタンの表示制御に使用） */
  value: string;
  /** 検索ボタン押下または Enter キーで呼ばれる。空白のみの入力では呼ばれない */
  onSearch: (query: string) => void;
  /** クリアボタン押下で呼ばれる */
  onClear: () => void;
}

/**
 * ニュースのキーワード検索ボックス。
 * 入力欄と「検索」ボタンを横並びで表示する。
 * コミット済みキーワード（value）が設定されているときは「クリア」ボタンが表示される。
 */
export function NewsSearchBox({
  value,
  onSearch,
  onClear,
}: NewsSearchBoxProps) {
  const [inputValue, setInputValue] = useState(value);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = inputValue.trim();
    if (trimmed) {
      onSearch(trimmed);
    }
  };

  const handleClear = () => {
    setInputValue("");
    onClear();
  };

  return (
    <form
      onSubmit={handleSubmit}
      role="search"
      aria-label="ニュース検索"
      className="search-row"
    >
      {/* label は sr-only で視覚的非表示。htmlFor 関連付けはスクリーンリーダー用に維持 */}
      <label htmlFor="news-search-input" className="sr-only">
        キーワード検索
      </label>
      <input
        id="news-search-input"
        type="search"
        value={inputValue}
        onChange={(e) => setInputValue(e.target.value)}
        placeholder="タイトル・本文で検索"
      />
      <button type="submit" className="btn-primary">
        検索
      </button>
      {value && (
        <button
          type="button"
          className="btn-secondary"
          onClick={handleClear}
          aria-label="検索をクリア"
        >
          クリア
        </button>
      )}
    </form>
  );
}
