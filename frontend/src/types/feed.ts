import { z } from "zod";

/** 転載・同一記事の他媒体情報（FEAT-035） */
export const otherSourceSchema = z.object({
  sourceName: z.string().nullable(),
  sourceUrl: z.string().url(),
});
export type OtherSource = z.infer<typeof otherSourceSchema>;

/** GET /api/v1/news の単一アイテム */
export const newsItemSchema = z.object({
  id: z.number().int(),
  oshiId: z.number().int(),
  oshiName: z.string(),
  title: z.string(),
  summary: z.string().nullable(),
  sourceUrl: z.string().url(),
  publishedAt: z.string().datetime({ offset: true }).nullable(),
  isPublishedAtEstimated: z.boolean(),
  collectedAt: z.string().datetime({ offset: true }),
  /** ログインユーザーがこの記事をブックマーク済みかどうか（未取得の場合は undefined）*/
  isBookmarked: z.boolean().optional(),
  /** 媒体名（FEAT-036）。未取得または取り出せない場合は null/undefined */
  sourceName: z.string().nullable().optional(),
  /** まとめた他媒体の件数（FEAT-035） */
  otherSourceCount: z.number().int().optional(),
  /** まとめた他媒体のリンク一覧（FEAT-035） */
  otherSources: z.array(otherSourceSchema).optional(),
  /**
   * ソースタイプ（FEAT-051）。通常のニュース記事は null。
   * "youtube" | "x_post" | "event" 等。
   * NULL の場合は「ニュース」として扱う。
   */
  sourceType: z.string().nullable().optional(),
});

export type NewsItem = z.infer<typeof newsItemSchema>;

/** GET /api/v1/programs の単一アイテム */
export const programAppearanceSchema = z.object({
  id: z.number().int(),
  oshiId: z.number().int(),
  oshiName: z.string(),
  programName: z.string(),
  broadcaster: z.string().nullable(),
  airedAt: z.string().datetime({ offset: true }).nullable(),
  isAiredAtEstimated: z.boolean(),
  sourceUrl: z.string().url(),
  collectedAt: z.string().datetime({ offset: true }),
  /** 媒体名（FEAT-036） */
  sourceName: z.string().nullable().optional(),
  /** まとめた他媒体の件数（FEAT-035） */
  otherSourceCount: z.number().int().optional(),
  /** まとめた他媒体のリンク一覧（FEAT-035） */
  otherSources: z.array(otherSourceSchema).optional(),
});

export type ProgramAppearance = z.infer<typeof programAppearanceSchema>;

/** ページネーションレスポンス汎用スキーマファクトリ */
const makePaginatedSchema = <T extends z.ZodTypeAny>(itemSchema: T) =>
  z.object({
    items: z.array(itemSchema),
    totalCount: z.number().int(),
    page: z.number().int(),
    pageSize: z.number().int(),
    totalPages: z.number().int(),
  });

export const newsPaginatedSchema = makePaginatedSchema(newsItemSchema);
export type NewsPaginatedResponse = z.infer<typeof newsPaginatedSchema>;

export const programsPaginatedSchema = makePaginatedSchema(
  programAppearanceSchema,
);
export type ProgramsPaginatedResponse = z.infer<typeof programsPaginatedSchema>;

/** GET /api/v1/feed/unread-count のレスポンス */
export const unreadCountSchema = z.object({
  unreadCount: z.number().int().nonnegative(),
});

export type UnreadCountResponse = z.infer<typeof unreadCountSchema>;
