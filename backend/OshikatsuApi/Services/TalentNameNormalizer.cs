using System.Text;

namespace OshikatsuApi.Services;

/// <summary>
/// タレント名の正規化ロジック。
/// 同一人物の異なる表記（スペースあり・なし、全角英字など）を同じ normalized_name に変換する。
/// 変更・テストが容易なよう、ロジックはこのクラス1か所に集約する。
/// </summary>
public static class TalentNameNormalizer
{
    /// <summary>
    /// 名前を正規化する。
    /// 手順: Unicode NFKC 正規化 → 英字を小文字に変換 → 空白（半角・全角すべて）を完全除去
    /// </summary>
    /// <param name="name">正規化する名前</param>
    /// <returns>正規化済みの名前。入力が null または空文字の場合はそのまま返す。</returns>
    public static string Normalize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // 1. NFKC 正規化: 全角英数字 → 半角、互換文字を基底形式に変換
        //    例: 'Ａ'(U+FF21) → 'A'(U+0041), '１'(U+FF11) → '1'(U+0031)
        var normalized = name.Normalize(NormalizationForm.FormKC);

        // 2. 英字を小文字に変換
        normalized = normalized.ToLowerInvariant();

        // 3. 空白文字をすべて除去（半角スペース U+0020、全角スペース U+3000、タブなど）
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (!char.IsWhiteSpace(c))
                sb.Append(c);
        }
        return sb.ToString();
    }
}
