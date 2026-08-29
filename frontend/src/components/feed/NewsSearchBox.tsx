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
 * テキストを入力して「検索」を押すと onSearch が呼ばれる。
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
    <form onSubmit={handleSubmit} role="search" aria-label="ニュース検索">
      <label htmlFor="news-search-input">キーワード検索</label>
      <input
        id="news-search-input"
        type="search"
        value={inputValue}
        onChange={(e) => setInputValue(e.target.value)}
        placeholder="タイトル・本文で検索"
      />
      <button type="submit">検索</button>
      {value && (
        <button type="button" onClick={handleClear} aria-label="検索をクリア">
          クリア
        </button>
      )}
    </form>
  );
}
