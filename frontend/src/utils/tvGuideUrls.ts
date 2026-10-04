/**
 * 番組表サイトの検索 URL を生成するユーティリティ関数。
 * 推しの名前を encodeURIComponent でエンコードしてクエリパラメータに埋め込む。
 * データの取得・保存は一切行わず、URL 文字列の生成のみを行う純粋関数。
 */

/** Gガイドの番組検索 URL を生成する */
export function buildGguideUrl(oshiName: string): string {
  return `https://bangumi.org/search?q=${encodeURIComponent(oshiName)}`;
}

/** J:COM TV ガイドの番組検索 URL を生成する */
export function buildJcomUrl(oshiName: string): string {
  return `https://tvguide.myjcom.jp/search/event/?keyword=${encodeURIComponent(oshiName)}`;
}
