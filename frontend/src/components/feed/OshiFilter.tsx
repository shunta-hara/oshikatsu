import type { OshiResponse } from "../../types/oshi";

interface OshiFilterProps {
  oshis: OshiResponse[];
  selectedOshiId: number | null;
  onChange: (oshiId: number | null) => void;
}

/**
 * ニュース・出演番組を特定の推しで絞り込むセレクトUI。
 * 「全員」を選ぶと selectedOshiId = null になる。
 */
export function OshiFilter({
  oshis,
  selectedOshiId,
  onChange,
}: OshiFilterProps) {
  const handleChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const val = e.target.value;
    onChange(val === "" ? null : Number(val));
  };

  return (
    <div>
      <label htmlFor="oshi-filter">推しで絞り込み:</label>
      <select
        id="oshi-filter"
        value={selectedOshiId ?? ""}
        onChange={handleChange}
        aria-label="推しで絞り込み"
      >
        <option value="">全員</option>
        {oshis.map((oshi) => (
          <option key={oshi.id} value={oshi.id}>
            {oshi.name}
          </option>
        ))}
      </select>
    </div>
  );
}
